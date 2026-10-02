"""Bound POST bodies before Arduino-ESP32 2.0.17 WebServer allocates them.

Only a generated build copy is changed; the installed framework stays intact.
Exact-match anchors deliberately fail the build if the pinned parser changes.
"""
from pathlib import Path

Import("env")


def bounded_parser(build_env, node):
    original = Path(node.srcnode().get_abspath())
    if original.name != "Parsing.cpp" or original.parent.parent.name != "WebServer":
        return node
    source = original.read_text(encoding="utf-8")
    anchor = '    if (!isForm && _currentHandler && _currentHandler->canRaw(_currentUri)){'
    guard = r'''
    // Reject before buffering (including multipart and URL-encoded requests).
    auto reject = [&](int status, const char* body) {
      send(status, "application/json", body);
      client.stop();
      return false;
    };
    if (hasHeader("Transfer-Encoding")) {
      return reject(400, "{\"success\":false,\"errorCode\":\"INVALID_REQUEST\",\"message\":\"Transfer-Encoding is not supported\"}");
    }
    const String lengthHeader = header("Content-Length");
    size_t boundedLength = 0;
    for (size_t i = 0; i < lengthHeader.length(); ++i) {
      const char digit = lengthHeader[i];
      if (digit < '0' || digit > '9') {
        return reject(400, "{\"success\":false,\"errorCode\":\"INVALID_REQUEST\",\"message\":\"Invalid Content-Length\"}");
      }
      boundedLength = boundedLength * 10 + (digit - '0');
      if (boundedLength > Config::MAX_API_BODY_BYTES) {
        return reject(413, "{\"success\":false,\"errorCode\":\"BODY_TOO_LARGE\",\"message\":\"Request body exceeds 1024 bytes\"}");
      }
    }
    _clientContentLength = boundedLength;
    String mediaType = header("Content-Type");
    const int separator = mediaType.indexOf(';');
    if (separator >= 0) mediaType = mediaType.substring(0, separator);
    mediaType.trim();
    if ((boundedLength > 0 || isForm) && !mediaType.equalsIgnoreCase("application/json")) {
      return reject(415, "{\"success\":false,\"errorCode\":\"UNSUPPORTED_MEDIA_TYPE\",\"message\":\"Use application/json\"}");
    }
'''
    replacements = {
        '#include "WebServer.h"': '#include "WebServer.h"\n#include "' +
            (Path(build_env.subst("$PROJECT_INCLUDE_DIR")) / "Config.h").as_posix() + '"',
        anchor: guard + '\n' + anchor,
        '\n    if (!buf) {':
            '\n    newLength = std::min(newLength, maxLength - dataLength);\n    if (!buf) {',
    }
    for old, new in replacements.items():
        if source.count(old) != 1:
            raise RuntimeError("WebServer parser changed; review body-limit patch: " + old)
        source = source.replace(old, new, 1)
    generated = Path(build_env.subst("$BUILD_DIR")) / "bounded_webserver" / "Parsing.cpp"
    generated.parent.mkdir(parents=True, exist_ok=True)
    if not generated.exists() or generated.read_text(encoding="utf-8") != source:
        generated.write_text(source, encoding="utf-8")
    build_env.AppendUnique(CPPPATH=[str(original.parent)])
    return build_env.File(str(generated))


env.AddBuildMiddleware(bounded_parser)
