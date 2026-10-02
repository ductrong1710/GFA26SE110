#include "ApiServer.h"
#include "Credentials.h"
#include "MeasurementCodec.h"
#include "JsonSafety.h"
#include "ErrorCodes.h"
#include "Logger.h"
#include <ESP8266WiFi.h>

void ApiServer::begin() {
    server_.collectHeaders("X-Device-Code","X-Device-Token","X-Gateway-Code","X-Gateway-Token","Content-Type","Content-Length","Transfer-Encoding");
    server_.on("/api/node/info",HTTP_GET,[this]() { if (authenticate()) info(); });
    server_.on("/api/node/data",HTTP_GET,[this]() { if (authenticate()) data(); });
    server_.on("/api/node/ack",HTTP_POST,[this]() { if (authenticate() && body()) ack(); });
    server_.on("/api/node/health",HTTP_GET,[this]() { if (authenticate()) health(); });
    server_.on("/api/node/time",HTTP_POST,[this]() { if (authenticate() && body()) setTime(); });
    server_.onNotFound([this]() { error(404,"NOT_FOUND","Endpoint not found"); });
    server_.begin(); Logger::log("API","HTTP server started on port 80");
}
bool ApiServer::authenticate() {
    bool valid = true;
    if (Config::REQUIRE_DEVICE_AUTH) valid = strlen(Secrets::DEVICE_SECRET) &&
        server_.header("X-Device-Code") == Config::DEVICE_CODE && server_.header("X-Device-Token") == Secrets::DEVICE_SECRET;
    if (Config::REQUIRE_GATEWAY_AUTH) valid = valid && strlen(Secrets::GATEWAY_TOKEN) &&
        server_.header("X-Gateway-Code") == Config::GATEWAY_CODE && server_.header("X-Gateway-Token") == Secrets::GATEWAY_TOKEN;
    if (!valid) error(401,ErrorCodes::UNAUTHORIZED,"Valid node credentials required");
    return valid;
}
bool ApiServer::body() {
    const String body = server_.arg("plain");
    if (body.length() > Config::MAX_API_BODY_BYTES) { error(413,ErrorCodes::BODY_TOO_LARGE,"Body exceeds configured limit"); return false; }
    String contentType = server_.header("Content-Type"); contentType.toLowerCase();
    const int separator = contentType.indexOf(';'); if (separator >= 0) contentType = contentType.substring(0,separator);
    contentType.trim();
    if (contentType != "application/json" || server_.hasHeader("Transfer-Encoding")) { error(415,ErrorCodes::INVALID_REQUEST,"Use application/json and Content-Length"); return false; }
    if (!JsonSafety::parse(body.c_str(),body.length(),doc_)) { error(400,ErrorCodes::INVALID_JSON,"Invalid JSON object"); return false; }
    return true;
}
void ApiServer::send(int status) {
    if (doc_.overflowed() || measureJson(doc_) >= sizeof(output_)) {
        server_.send(500,"application/json","{\"success\":false,\"errorCode\":\"INTERNAL_ERROR\",\"message\":\"JSON capacity exceeded\"}"); return;
    }
    serializeJson(doc_,output_,sizeof(output_));
    server_.sendHeader("Cache-Control","no-store"); server_.send(status,"application/json",output_);
}
void ApiServer::error(int status, const char* code, const char* message) {
    doc_.clear(); doc_["success"] = false; doc_["errorCode"] = code; doc_["message"] = message; send(status);
}
void ApiServer::info() {
    Logger::log("API","GET /api/node/info");
    if (!queue_.healthy()) { error(503,ErrorCodes::STORAGE_ERROR,"Queue requires recovery"); return; }
    doc_.clear(); doc_["success"] = true; doc_["deviceCode"] = Config::DEVICE_CODE;
    doc_["nodeId"] = Config::NODE_ID; doc_["farmId"] = Config::FARM_ID; doc_["zoneId"] = Config::ZONE_ID;
    doc_["firmwareVersion"] = Config::FIRMWARE_VERSION; doc_["deviceType"] = Config::DEVICE_TYPE;
    doc_["pendingRecords"] = queue_.pendingCount(); send();
}
void ApiServer::data() {
    Logger::log("API","GET /api/node/data");
    size_t limit = Config::MAX_API_BATCH_SIZE;
    if (server_.hasArg("limit") && !JsonSafety::limit(server_.arg("limit").c_str(),limit)) { error(400,ErrorCodes::INVALID_LIMIT,"limit must be a positive integer"); return; }
    if (!queue_.healthy()) { error(503,ErrorCodes::STORAGE_ERROR,"Queue requires recovery"); return; }
    const size_t count = std::min(limit,queue_.pendingCount());
    doc_.clear(); doc_["success"] = true; doc_["deviceCode"] = Config::DEVICE_CODE; doc_["count"] = count;
    serializeJson(doc_,output_,sizeof(output_));
    String prefix(output_); prefix.remove(prefix.length() - 1); prefix += ",\"records\":[";
    size_t length = prefix.length() + 2 + (count ? count - 1 : 0);
    for (size_t i = 0; i < count; ++i) {
        Measurement record;
        if (!queue_.readAt(i,record)) { error(503,ErrorCodes::STORAGE_ERROR,"Record read failed"); return; }
        doc_.clear(); MeasurementCodec::toJson(record,doc_.to<JsonObject>());
        if (doc_.overflowed() || measureJson(doc_) >= sizeof(output_)) { error(500,ErrorCodes::INTERNAL_ERROR,"Record exceeds buffer"); return; }
        length += measureJson(doc_);
    }
    // Known Content-Length works with ESP32's bounded HTTP/1.0 client; no giant document.
    server_.setContentLength(length); server_.sendHeader("Cache-Control","no-store");
    server_.send(200,"application/json",""); server_.sendContent(prefix);
    for (size_t i = 0; i < count; ++i) {
        Measurement record;
        if (!queue_.readAt(i,record)) { server_.client().stop(); return; }
        doc_.clear(); MeasurementCodec::toJson(record,doc_.to<JsonObject>());
        serializeJson(doc_,output_,sizeof(output_));
        if (i) server_.sendContent(",");
        server_.sendContent(output_);
        yield();
    }
    server_.sendContent("]}");
}
void ApiServer::ack() {
    Logger::log("API","POST /api/node/ack");
    if (!JsonSafety::ackIds(doc_["recordIds"])) { error(400,ErrorCodes::INVALID_REQUEST,"recordIds must contain 1..10 bounded strings"); return; }
    // Validate the entire array before changing any state.
    size_t acked = 0;
    for (JsonVariantConst id : doc_["recordIds"].as<JsonArrayConst>()) {
        const AckResult result = queue_.acknowledge(id.as<const char*>());
        if (result == AckResult::Error) { error(503,ErrorCodes::STORAGE_ERROR,"ACK not fully committed; safe to retry exact IDs"); return; }
        if (result == AckResult::Acked) { ++acked; Serial.printf("[ACK] Marked %s ACKED\n",id.as<const char*>()); }
    }
    doc_.clear(); doc_["success"] = true; doc_["ackedCount"] = acked; send();
}
void ApiServer::health() {
    Logger::log("API","GET /api/node/health");
    doc_.clear(); doc_["success"] = true; doc_["deviceCode"] = Config::DEVICE_CODE;
    doc_["status"] = !queue_.healthy() ? "STORAGE_ERROR" : queue_.full() ? "STORAGE_FULL" : "OK";
    doc_["uptimeMs"] = millis(); doc_["freeHeap"] = ESP.getFreeHeap();
    doc_["gatewayConnected"] = WiFi.status() == WL_CONNECTED;
    if (WiFi.status() == WL_CONNECTED) doc_["wifiRSSI"] = WiFi.RSSI(); else doc_["wifiRSSI"] = nullptr;
    if (queue_.healthy()) doc_["pendingRecords"] = queue_.pendingCount(); else doc_["pendingRecords"] = nullptr;
    doc_["storageHealthy"] = queue_.healthy(); doc_["storageFull"] = queue_.full();
    doc_["timeSynced"] = time_.synced(); doc_["demoMode"] = bool(DEMO_MODE); send();
}
void ApiServer::setTime() {
    Logger::log("API","POST /api/node/time");
    if (!JsonSafety::unixTime(doc_["unixTime"]) || !time_.synchronize(doc_["unixTime"].as<uint64_t>(),millis())) { error(400,ErrorCodes::TIME_INVALID,"Unix seconds must be between 2020 and 2100"); return; }
    Logger::log("TIME","Gateway time synchronized");
    doc_.clear(); doc_["success"] = true; doc_["timeSynced"] = true; send();
}
