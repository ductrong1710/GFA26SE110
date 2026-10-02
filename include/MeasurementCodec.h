#pragma once
#include <ArduinoJson.h>
#include <stdio.h>
#include <string.h>
#include "Config.h"
#include "Models.h"
namespace MeasurementCodec {
inline void recordId(uint32_t sequence, char* out, size_t size) {
    snprintf(out, size, "%s-%lu", Config::DEVICE_CODE, static_cast<unsigned long>(sequence));
}
inline void optional(JsonObject o, const char* key, float v) {
    if (isfinite(v)) o[key] = v; else o[key] = nullptr;
}
inline void toJson(const Measurement& r, JsonObject o) {
    char id[80]; recordId(r.sequence,id,sizeof(id));
    o["recordId"] = id; o["deviceCode"] = Config::DEVICE_CODE;
    o["sequence"] = r.sequence; o["measuredAt"] = r.measuredAt;
    o["timeSynced"] = r.timeSynced; o["uptimeMs"] = r.uptimeMs;
    o["state"] = r.state == MeasurementState::Pending ? "PENDING" : "ACKED";
    optional(o,"temperature",r.reading.temperature); optional(o,"humidity",r.reading.humidity);
    optional(o,"soilMoisture",r.reading.soilMoisture); optional(o,"lightIntensity",r.reading.lightIntensity);
    optional(o,"ph",r.reading.ph); optional(o,"waterLevel",r.reading.waterLevel);
    optional(o,"batteryVoltage",r.reading.batteryVoltage);
}
inline bool readOptional(JsonObjectConst o, const char* key, float& value) {
    if (!o.containsKey(key)) return false;
    if (o[key].isNull()) { value = NAN; return true; }
    if (!o[key].is<float>()) return false;
    value = o[key].as<float>(); return isfinite(value) && fabsf(value) <= 1e12f;
}
inline bool textEquals(JsonVariantConst v, const char* expected) {
    if (!v.is<const char*>()) return false;
    const JsonString s = v.as<JsonString>();
    return s.size() == strlen(expected) && memcmp(s.c_str(),expected,s.size()) == 0;
}
inline bool fromJson(JsonObjectConst o, Measurement& r) {
    if (!o["sequence"].is<uint32_t>() || o["sequence"].as<uint32_t>() == 0 ||
        !o["measuredAt"].is<uint64_t>() || !o["uptimeMs"].is<uint32_t>() || !o["timeSynced"].is<bool>()) return false;
    r.sequence = o["sequence"]; r.measuredAt = o["measuredAt"];
    r.uptimeMs = o["uptimeMs"]; r.timeSynced = o["timeSynced"];
    char id[80]; recordId(r.sequence,id,sizeof(id));
    if (!textEquals(o["recordId"],id) || !textEquals(o["deviceCode"],Config::DEVICE_CODE) ||
        !textEquals(o["state"],"PENDING")) return false;
    if (r.timeSynced ? (r.measuredAt < 1577836800ULL || r.measuredAt > 4102444800ULL) : r.measuredAt != 0) return false;
    return readOptional(o,"temperature",r.reading.temperature) && readOptional(o,"humidity",r.reading.humidity) &&
        readOptional(o,"soilMoisture",r.reading.soilMoisture) && readOptional(o,"lightIntensity",r.reading.lightIntensity) &&
        readOptional(o,"ph",r.reading.ph) && readOptional(o,"waterLevel",r.reading.waterLevel) &&
        readOptional(o,"batteryVoltage",r.reading.batteryVoltage);
}
}
