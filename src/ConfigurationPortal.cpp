#include "ConfigurationPortal.h"
#include "ConfigurationPage.h"
#include "RuntimeConfig.h"
#include "Secrets.h"
#include <WiFi.h>
#include <esp_system.h>
#include <ctype.h>

namespace {
struct BodyReader {
    const String& body;
    size_t position = 0;
    explicit BodyReader(const String& value) : body(value) {}
    int read() { return position < body.length() ? static_cast<unsigned char>(body[position++]) : -1; }
    size_t readBytes(char* output, size_t length) {
        size_t count = 0;
        while(count < length && position < body.length()) output[count++] = body[position++];
        return count;
    }
};
bool validString(JsonVariantConst value) {
    if (!value.is<const char*>()) return false;
    const JsonString text = value.as<JsonString>();
    return text.size() == strlen(text.c_str());
}
}

void ConfigurationPortal::begin() {
    char token[33];
    uint8_t random[16];
    esp_fill_random(random, sizeof(random));
    for (size_t i = 0; i < sizeof(random); ++i) snprintf(token + i * 2, 3, "%02x", random[i]);
    token_ = token;
    server_.on("/config", HTTP_GET, [this]() {
        if (!authorize()) return;
        server_.send_P(200, "text/html; charset=utf-8", CONFIGURATION_PAGE);
    });
    server_.on("/api/config", HTTP_GET, [this]() { get(); });
    server_.on("/api/config", HTTP_POST, [this]() { save(); });
}

void ConfigurationPortal::onControl(const char* path, std::function<void()> handler) {
    server_.on(path, HTTP_POST, [this, handler]() {
        if (!authorize()) return;
        if (token_.isEmpty() || server_.header("X-Config-Token") != token_) {
            error(403, "Reload gateway configuration to obtain a current control token"); return;
        }
        handler();
    });
}

void ConfigurationPortal::headers() {
    server_.sendHeader("Cache-Control", "no-store");
    server_.sendHeader("Pragma", "no-cache");
    server_.sendHeader("X-Content-Type-Options", "nosniff");
    server_.sendHeader("X-Frame-Options", "DENY");
    server_.sendHeader("Referrer-Policy", "no-referrer");
    server_.sendHeader("Content-Security-Policy", "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'");
}

bool ConfigurationPortal::authorize() {
    headers();
    if (server_.client().localIP() != WiFi.softAPIP()) {
        error(403, "Configuration is available only through the gateway Wi-Fi access point");
        return false;
    }
    // authenticate() accepts Basic too; explicitly require Digest on every route.
    if (!server_.header("Authorization").startsWith("Digest ") ||
        !server_.authenticate("admin", Secrets::GATEWAY_AP_PASSWORD)) {
        server_.requestAuthentication(DIGEST_AUTH, "Gateway configuration", "Authentication required");
        return false;
    }
    return true;
}

void ConfigurationPortal::reply(int status, const JsonDocument& doc) {
    String output;
    serializeJson(doc, output);
    server_.send(status, "application/json", output);
}
void ConfigurationPortal::error(int status, const char* message) {
    StaticJsonDocument<256> doc;
    doc["success"] = false;
    doc["message"] = message;
    reply(status, doc);
}

void ConfigurationPortal::get() {
    if (!authorize()) return;
    const auto& settings = RuntimeConfig::instance().values();
    StaticJsonDocument<1536> doc;
    doc["success"] = true;
    doc["internetSsid"] = settings.internetSsid;
    doc["backendBaseUrl"] = settings.backendBaseUrl;
    doc["gatewayId"] = settings.gatewayId;
    doc["gatewayCode"] = settings.gatewayCode;
    doc["hasWifiPassword"] = !settings.internetPassword.isEmpty();
    doc["hasApiKey"] = !settings.apiKey.isEmpty();
    doc["canAdoptPending"] = RuntimeConfig::instance().canAdoptPending();
    doc["csrfToken"] = token_;
    doc["canChange"] = !savedOnce_ && RuntimeConfig::instance().ready() && canChange_();
    reply(200, doc);
}

void ConfigurationPortal::save() {
    if (!authorize()) return;
    if (server_.header("X-Config-Token") != token_) { error(403, "Reload the configuration page before saving"); return; }
    if (savedOnce_ || !canChange_()) { error(409, "Configuration is locked until the gateway is idle and safe; a saved change may be awaiting restart"); return; }
    if (!RuntimeConfig::instance().ready()) { error(503, "Configuration storage is unavailable"); return; }
    String contentType = server_.header("Content-Type");
    int separator = contentType.indexOf(';');
    if (separator >= 0) contentType = contentType.substring(0, separator);
    contentType.trim();
    if (!contentType.equalsIgnoreCase("application/json")) { error(415, "Use application/json"); return; }
    const String body = server_.arg("plain");
    if (body.length() > 1024 || server_.clientContentLength() > 1024) { error(413, "Configuration body exceeds 1024 bytes"); return; }
    StaticJsonDocument<3072> doc;
    BodyReader reader(body);
    const auto result = deserializeJson(doc, reader, DeserializationOption::NestingLimit(2));
    bool trailing = false;
    for (int ch = reader.read(); ch >= 0; ch = reader.read()) trailing |= !isspace(static_cast<unsigned char>(ch));
    if (result || trailing || !doc.is<JsonObject>() || body.length() != static_cast<size_t>(server_.clientContentLength())) {
        error(400, "Expected one complete JSON object"); return;
    }
    const char* fields[] = {"internetSsid", "internetPassword", "backendBaseUrl", "gatewayCode", "apiKey"};
    for (const auto* field : fields) if (!validString(doc[field])) { error(400, "Configuration text fields must be strings without null characters"); return; }
    if (!doc["gatewayId"].is<int>() || !doc["clearWifiPassword"].is<bool>()) { error(400, "gatewayId must be an integer and clearWifiPassword a boolean"); return; }
    if (doc.containsKey("adoptPending") && !doc["adoptPending"].is<bool>()) { error(400, "adoptPending must be a boolean"); return; }
    const bool adoptPending = doc["adoptPending"] | false;
    if (adoptPending && !RuntimeConfig::instance().canAdoptPending()) { error(409, "Pending test-data adoption requires an unprovisioned gateway, ready storage and no saved mission"); return; }
    const auto& previous = RuntimeConfig::instance().values();
    GatewaySettings next = previous;
    next.internetSsid = doc["internetSsid"].as<const char*>();
    next.backendBaseUrl = doc["backendBaseUrl"].as<const char*>();
    next.gatewayCode = doc["gatewayCode"].as<const char*>();
    next.gatewayId = doc["gatewayId"].as<int>();
    const String password = doc["internetPassword"].as<const char*>();
    const String key = doc["apiKey"].as<const char*>();
    if (doc["clearWifiPassword"].as<bool>()) {
        if (!password.isEmpty()) { error(400, "Choose either a Wi-Fi password or an open network"); return; }
        next.internetPassword = "";
    } else if (!password.isEmpty()) next.internetPassword = password;
    if ((next.gatewayId != previous.gatewayId || next.gatewayCode != previous.gatewayCode) && key.isEmpty()) {
        error(400, "Enter the provisioned API key when changing gateway identity"); return;
    }
    if (!key.isEmpty()) next.apiKey = key;
    if (const char* invalid = RuntimeConfig::validate(next)) { error(400, invalid); return; }
    if (!RuntimeConfig::instance().save(next, adoptPending)) { error(503, "Could not save. With pending test data, explicitly confirm adoption on an unprovisioned gateway. Otherwise keep backend identity unchanged and check storage or saved missions"); return; }
    savedOnce_ = true;
    StaticJsonDocument<192> response;
    response["success"] = true;
    response["message"] = "Settings saved. Gateway is restarting. Reconnect to gateway Wi-Fi, then reload this page.";
    reply(200, response);
    saved_();
}
