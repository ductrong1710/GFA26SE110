#include "GatewayClient.h"
#include <ESP8266HTTPClient.h>
#include "Config.h"
#include "Credentials.h"
#include "GatewayProtocol.h"
#include "JsonSafety.h"
#include "Logger.h"

namespace {
// HTTPClient in ESP8266 3.1.2 has no setConnectTimeout(). Apply a separate
// deadline to the transport and use WiFiClient::setTimeout for connect.
class BoundedClient : public WiFiClient {
    uint32_t started_ = millis();
    size_t received_ = 0;
    bool expired() {
        // Includes HTTP status/headers, which HTTPClient otherwise reads into Strings.
        if (received_ < 2048 && uint32_t(millis() - started_) < Config::HTTP_TOTAL_TIMEOUT_MS) return false;
        WiFiClient::stop(0); return true;
    }
public:
    int connect(const char* host, uint16_t port) override {
        setTimeout(Config::HTTP_CONNECT_TIMEOUT_MS);
        const int result = WiFiClient::connect(host,port);
        setTimeout(Config::HTTP_REQUEST_TIMEOUT_MS); return result;
    }
    int connect(IPAddress ip, uint16_t port) override {
        setTimeout(Config::HTTP_CONNECT_TIMEOUT_MS);
        const int result = WiFiClient::connect(ip,port);
        setTimeout(Config::HTTP_REQUEST_TIMEOUT_MS); return result;
    }
    int available() override { return expired() ? 0 : WiFiClient::available(); }
    uint8_t connected() override { return expired() ? 0 : WiFiClient::connected(); }
    int read() override {
        if (expired()) return -1;
        const int result = WiFiClient::read();
        if (result >= 0) ++received_;
        return result;
    }
    int read(uint8_t* buffer, size_t size) override {
        if (expired()) return -1;
        const int result = WiFiClient::read(buffer,std::min(size,size_t(2048) - received_));
        if (result > 0) received_ += result;
        return result;
    }
};
}
void GatewayClient::update() {
    if (!wifi_.connected()) { registered_ = false; retry_.reset(); return; }
    if (generation_ != wifi_.generation()) {
        generation_ = wifi_.generation(); registered_ = false; retry_.reset();
    }
    const uint32_t interval = registered_ ? Config::REGISTRATION_HEARTBEAT_MS : Config::REGISTRATION_RETRY_INTERVAL_MS;
    if (!retry_.due(millis(),interval)) return;
    if (!strlen(Secrets::DEVICE_SECRET)) {
        retry_.mark(millis()); Logger::log("WARN","Configure Secrets.h device secret; registration disabled"); return;
    }
    registered_ = registerNode(); retry_.mark(millis());
    Logger::log(registered_ ? "GATEWAY" : "WARN",registered_ ? "Registration successful" : "Registration failed; will retry");
}
bool GatewayClient::registerNode() {
    Serial.printf("[GATEWAY] Registering %s\n",Config::DEVICE_CODE);
    StaticJsonDocument<768> doc;
    const String ip = WiFi.localIP().toString();
    GatewayProtocol::registration(doc,ip.c_str());
    char body[512];
    if (doc.overflowed() || measureJson(doc) >= sizeof(body)) return false;
    const size_t bodySize = serializeJson(doc,body,sizeof(body));
    BoundedClient socket;
    HTTPClient http;
    http.setTimeout(Config::HTTP_REQUEST_TIMEOUT_MS);
    http.setReuse(false); http.useHTTP10(true);
    const String url = String(Config::GATEWAY_BASE_URL) + Config::GATEWAY_REGISTER_ENDPOINT;
    if (!url.startsWith("http://") || !http.begin(socket,url)) return false;
    const char* headers[] = {"Transfer-Encoding"}; http.collectHeaders(headers,1);
    http.addHeader("Content-Type","application/json");
    http.addHeader("X-Device-Code",Config::DEVICE_CODE);
    http.addHeader("X-Device-Token",Secrets::DEVICE_SECRET);
    const int status = http.POST(reinterpret_cast<uint8_t*>(body),bodySize);
    const int length = http.getSize();
    bool valid = status >= 200 && status < 300 && length > 0 && length < 512 && !http.hasHeader("Transfer-Encoding");
    char response[512]; size_t received = 0;
    const uint32_t started = millis();
    WiFiClient* stream = http.getStreamPtr();
    while (valid && received < static_cast<size_t>(length)) {
        if (!stream || uint32_t(millis() - started) >= Config::HTTP_BODY_TIMEOUT_MS || (!http.connected() && !stream->available())) { valid = false; break; }
        const int available = stream->available();
        if (!available) { yield(); continue; }
        const size_t amount = std::min(static_cast<size_t>(available),static_cast<size_t>(length) - received);
        const int n = stream->read(reinterpret_cast<uint8_t*>(response + received),amount);
        if (n <= 0) { valid = false; break; }
        received += n;
    }
    http.end(); // Every path after begin reaches cleanup; never log response bodies/tokens.
    return valid && received == static_cast<size_t>(length) && JsonSafety::parse(response,received,doc) && GatewayProtocol::registered(doc);
}
