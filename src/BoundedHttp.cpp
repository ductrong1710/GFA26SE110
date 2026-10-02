#include "BoundedHttp.h"
#include <HTTPClient.h>
#include <WiFiClient.h>
#include "Config.h"

namespace {
struct Reader {
    const String& text;
    size_t index = 0;
    explicit Reader(const String& value) : text(value) {}
    int read() { return index < text.length() ? (unsigned char)text[index++] : -1; }
    size_t readBytes(char* out, size_t size) {
        size_t n = 0; while (n < size && index < text.length()) out[n++] = text[index++]; return n;
    }
};
}

bool BoundedHttp::request(const String& url, const char* method, const String& body,
    const char* h1, const String& v1, const char* h2, const String& v2,
    JsonDocument& response, int& status, bool allowEmpty) {
    response.clear(); status = -1;
    if (!url.startsWith("http://") || body.length() > Config::MAX_HTTP_RESPONSE_BYTES || response.capacity() == 0) return false;
    WiFiClient socket;
    HTTPClient http;
    http.setConnectTimeout(Config::HTTP_CONNECT_TIMEOUT_MS);
    http.setTimeout(Config::HTTP_REQUEST_TIMEOUT_MS);
    http.setReuse(false); http.useHTTP10(true);
    if (!http.begin(socket, url)) return false;
    const char* headers[] = {"Transfer-Encoding"}; http.collectHeaders(headers, 1);
    http.addHeader("Accept", "application/json");
    if (h1) http.addHeader(h1, v1);
    if (h2) http.addHeader(h2, v2);
    if (strcmp(method, "POST") == 0) http.addHeader("Content-Type", "application/json");
    status = http.sendRequest(method, body);
    const int length = http.getSize();
    if (status < 200 || status >= 300 || length > int(Config::MAX_HTTP_RESPONSE_BYTES) || http.hasHeader("Transfer-Encoding")) {
        http.end(); return false;
    }
    String received;
    if (!received.reserve(length >= 0 ? length + 1 : Config::MAX_HTTP_RESPONSE_BYTES + 1)) { http.end(); return false; }
    WiFiClient* stream = http.getStreamPtr();
    if (!stream) { http.end(); return allowEmpty && (length == 0 || status == 204); }
    const uint32_t start = millis();
    bool ok = true;
    while ((length < 0 || received.length() < size_t(length)) && (http.connected() || stream->available())) {
        if (millis() - start > Config::HTTP_BODY_TIMEOUT_MS) { ok = false; break; }
        const int available = stream->available();
        if (!available) { delay(1); continue; }
        if (received.length() >= Config::MAX_HTTP_RESPONSE_BYTES) { ok = false; break; }
        char buffer[128];
        size_t amount = std::min(size_t(available), sizeof(buffer));
        amount = std::min(amount, Config::MAX_HTTP_RESPONSE_BYTES - received.length());
        if (length >= 0) amount = std::min(amount, size_t(length) - received.length());
        const int n = stream->read(reinterpret_cast<uint8_t*>(buffer), amount);
        if (n <= 0 || !received.concat(buffer, n)) { ok = false; break; }
    }
    http.end();
    if (!ok || (length >= 0 && received.length() != size_t(length))) return false;
    if (received.isEmpty()) return allowEmpty;
    Reader reader(received);
    if (deserializeJson(response, reader, DeserializationOption::NestingLimit(6))) return false;
    for (int ch = reader.read(); ch >= 0; ch = reader.read()) if (!isspace((unsigned char)ch)) return false;
    return !response.overflowed();
}
