#include "SensorNodeClient.h"
#include "BoundedHttp.h"

bool SensorNodeClient::call(const SensorNode& node, const String& path, const char* method,
    const String& body, JsonDocument& response, bool allowEmpty) {
    const char* token = auth_.tokenFor(node.deviceCode);
    if (!node.authenticated || !node.online || !token) return false;
    const String url = String("http://") + node.ipAddress.toString() + ":" + Config::SENSOR_NODE_HTTP_PORT + path;
    int status;
    return BoundedHttp::request(url, method, body, "X-Device-Code", node.deviceCode,
                               "X-Device-Token", token, response, status, allowEmpty);
}
bool SensorNodeClient::getInfo(const SensorNode& node, uint32_t& pending) {
    StaticJsonDocument<1024> doc;
    if (!call(node, "/api/node/info", "GET", "", doc) || doc["deviceCode"].as<String>() != node.deviceCode ||
        !doc["pendingRecords"].is<uint32_t>()) return false;
    pending = doc["pendingRecords"].as<uint32_t>(); return true;
}
bool SensorNodeClient::getData(const SensorNode& node, JsonDocument& response) {
    if (!call(node, String("/api/node/data?limit=") + Config::COLLECTION_BATCH_SIZE, "GET", "", response)) return false;
    return response["deviceCode"].as<String>() == node.deviceCode && response["count"].is<unsigned int>() &&
        response["records"].is<JsonArray>() && response["records"].size() <= Config::COLLECTION_BATCH_SIZE &&
        response["records"].size() == response["count"].as<size_t>();
}
bool SensorNodeClient::acknowledge(const SensorNode& node, const String* ids, size_t count) {
    if (!count || count > Config::COLLECTION_BATCH_SIZE) return false;
    StaticJsonDocument<1536> doc;
    JsonArray array = doc.createNestedArray("recordIds");
    for (size_t i = 0; i < count; ++i) array.add(ids[i]);
    if (doc.overflowed()) return false;
    String body; serializeJson(doc, body); doc.clear();
    if (!call(node, "/api/node/ack", "POST", body, doc, true)) return false;
    return !doc.containsKey("success") || doc["success"] == true;
}
bool SensorNodeClient::getHealth(const SensorNode& node, JsonDocument& response) {
    return call(node, "/api/node/health", "GET", "", response) && response["deviceCode"].as<String>() == node.deviceCode;
}
bool SensorNodeClient::sendTime(const SensorNode& node, uint64_t unixTime) {
    if (unixTime < 1577836800ULL) return false;
    StaticJsonDocument<256> doc; doc["unixTime"] = unixTime;
    String body; serializeJson(doc, body); doc.clear();
    return call(node, "/api/node/time", "POST", body, doc, true) && (!doc.containsKey("success") || doc["success"] == true);
}
