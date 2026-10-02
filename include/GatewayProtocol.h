#pragma once
#include "MeasurementCodec.h"
namespace GatewayProtocol {
inline bool registered(JsonDocument& doc) {
    return doc["success"].is<bool>() && doc["success"].as<bool>() &&
        doc["authenticated"].is<bool>() && doc["authenticated"].as<bool>() &&
        MeasurementCodec::textEquals(doc["deviceCode"],Config::DEVICE_CODE);
}
inline void registration(JsonDocument& doc, const char* ip) {
    doc.clear(); doc["deviceCode"] = Config::DEVICE_CODE; doc["farmId"] = Config::FARM_ID;
    doc["zoneId"] = Config::ZONE_ID; doc["firmwareVersion"] = Config::FIRMWARE_VERSION;
    doc["deviceType"] = Config::DEVICE_TYPE; doc["ipAddress"] = ip;
}
}
