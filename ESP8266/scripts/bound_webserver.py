"""Build-local hardening of the pinned ESP8266WebServer 3.1.2 templates.

The upstream handler gets the body AFTER allocation. Bound request parsing first.
No shared PlatformIO package or ESP32 source is modified. Preserve upstream LGPL
notices in the generated library. Fail the build if expected upstream text changes.
"""
from pathlib import Path
import json
import shutil

Import('env')

def replace_once(text, old, new):
    if text.count(old) != 1:
        raise RuntimeError('ESP8266WebServer changed; review bounds patch: ' + old[:80])
    return text.replace(old, new, 1)

framework = Path(env.PioPlatform().get_package_dir('framework-arduinoespressif8266'))
source = framework / 'libraries/ESP8266WebServer/src'
destination = Path(env.subst('$PROJECT_DIR')) / 'lib/BoundedESP8266WebServer'
destination.mkdir(parents=True, exist_ok=True)
shutil.copytree(source, destination / 'src', dirs_exist_ok=True)
(destination / 'library.json').write_text(json.dumps({
    'name': 'BoundedESP8266WebServer', 'version': '3.1.2-node.1',
    'frameworks': ['arduino'], 'platforms': ['espressif8266'],
    'license': 'LGPL-2.1-or-later'
}), encoding='utf-8')
parser = (source / 'Parsing-impl.h').read_text(encoding='utf-8')
parser = replace_once(parser, '#include <Arduino.h>', '#include <Arduino.h>\n#include "HttpBounds.h"\n#include "Config.h"')
parser = replace_once(parser,
    '  S2Stream dataStream(data);\n  return client.sendSize(dataStream, maxLength, timeout_ms) == maxLength;',
    '''  if (maxLength > Config::MAX_API_BODY_BYTES) return false;
  data = "";
  if (!data.reserve(maxLength)) return false;
  const uint32_t started = millis();
  while (data.length() < maxLength && uint32_t(millis() - started) < uint32_t(timeout_ms)) {
    if (!client.available()) { if (!client.connected()) return false; yield(); continue; }
    const int ch = client.read();
    if (ch < 0 || !data.concat(char(ch))) return false;
  }
  return data.length() == maxLength;''')
parser = replace_once(parser,
    '''  String req = client.readStringUntil('\\r');
  DBGWS("request: %s\\n", req.c_str());
  client.readStringUntil('\\n');''',
    '''  const uint32_t nodeStarted = millis();
  size_t nodeHeaderBytes = 0, nodeLength = 0;
  unsigned int nodeHeaderCount = 0;
  bool nodeLengthSeen = false;
  auto nodeReadLine = [&](String& line) {
    if (!HttpBounds::line(client,line,256,nodeStarted,750,[]() { return millis(); },[]() { yield(); })) return false;
    nodeHeaderBytes += line.length() + 2;
    return nodeHeaderBytes <= 2048;
  };
  auto nodeReject = [&](int status) {
    const char* body = "{\\"success\\":false,\\"errorCode\\":\\"INVALID_REQUEST\\",\\"message\\":\\"HTTP bounds exceeded or framing invalid\\"}";
    client.setTimeout(750);
    client.printf("HTTP/1.0 %d Error\\r\\nContent-Type: application/json\\r\\nConnection: close\\r\\nContent-Length: %u\\r\\n\\r\\n",status,unsigned(strlen(body)));
    client.print(body);
    return CLIENT_MUST_STOP;
  };
  String req;
  if (!nodeReadLine(req)) return nodeReject(400);''')
# Only the two header loops in _parseRequest. Multipart parser is rejected below.
old = "      req = client.readStringUntil('\\r');\n      client.readStringUntil('\\n');"
if parser.count(old) != 2:
    raise RuntimeError('Expected exactly two HTTP header loops')
parser = parser.replace(old, '      if (!nodeReadLine(req)) return nodeReject(400);')
old = "      headerValue = req.substring(headerDiv + 2);"
parser = replace_once(parser,old,"      headerValue = req.substring(headerDiv + 1);\n      headerValue.trim();")
guard = '''      if (++nodeHeaderCount > 20 || headerName.equalsIgnoreCase("Transfer-Encoding")) return nodeReject(400);
      if (headerName.equalsIgnoreCase("Content-Length")) {
        if (nodeLengthSeen) return nodeReject(400);
        nodeLengthSeen = true;
        if (!HttpBounds::contentLength(headerValue.c_str(),Config::MAX_API_BODY_BYTES,nodeLength)) return nodeReject(413);
      }
'''
marker = '      headerValue.trim();\n'
if parser.count(marker) != 2:
    raise RuntimeError('Expected exactly two bounded header-value sites')
parser = parser.replace(marker,marker + guard)
parser = replace_once(parser,'        contentLength = headerValue.toInt();','        contentLength = nodeLength;')
parser = replace_once(parser,'    String plainBuf;',
    '    if (!nodeLengthSeen || isForm || isEncoded) return nodeReject(400);\n    String plainBuf;')
parser = replace_once(parser, '  client.flush();',
    '  if (method != HTTP_POST && method != HTTP_PUT && method != HTTP_PATCH && method != HTTP_DELETE && nodeLength) return nodeReject(400);\n  _keepAlive = false;\n  client.flush();')
(destination / 'src/Parsing-impl.h').write_text(parser,encoding='utf-8')
header = (destination / 'src/ESP8266WebServer.h').read_text(encoding='utf-8')
for macro, old_value, value in [('HTTP_MAX_DATA_WAIT',5000,750),('HTTP_MAX_POST_WAIT',5000,750),
                              ('HTTP_MAX_SEND_WAIT',5000,750),('HTTP_MAX_CLOSE_WAIT',2000,50)]:
    header = replace_once(header,f'#define {macro} {old_value}',f'#define {macro} {value}')
(destination / 'src/ESP8266WebServer.h').write_text(header,encoding='utf-8')
