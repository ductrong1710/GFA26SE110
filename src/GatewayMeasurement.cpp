#include "GatewayMeasurement.h"
#include <math.h>
#include <string.h>
namespace {
bool readOptional(JsonVariantConst v, OptionalReading& out) {
    out.present = !v.isNull();
    if (!out.present) return true;
    if (!v.is<double>()) return false;
    out.value = v.as<double>();
    return isfinite(out.value) && fabs(out.value) <= 1e12;
}
void writeOptional(JsonObject o, const char* name, const OptionalReading& v) {
    if (v.present) o[name] = v.value; else o[name] = nullptr;
}
bool safeString(JsonVariantConst v, size_t max) {
    if (!v.is<const char*>()) return false;
    const JsonString s = v.as<JsonString>();
    return s.size() > 0 && s.size() <= max && s.size() == strlen(s.c_str());
}
}
bool MeasurementCodec::validDeviceCode(const String& code) {
    if (code.isEmpty() || code.length() > Config::MAX_DEVICE_CODE_BYTES) return false;
    for (size_t i = 0; i < code.length(); ++i) if ((unsigned char)code[i] < 33 || (unsigned char)code[i] > 126) return false;
    return true;
}
bool MeasurementCodec::valid(const GatewayMeasurement& r) {
    if (!validDeviceCode(r.deviceCode) || r.recordId != r.deviceCode + "-" + String(r.sequence)) return false;
    if (r.gatewayCode.isEmpty() || r.gatewayCode.length() > 64) return false;
    const OptionalReading* values[] = {&r.temperature, &r.humidity, &r.soilMoisture, &r.lightIntensity, &r.ph, &r.waterLevel, &r.batteryVoltage};
    for (const auto v : values) if (v->present && (!isfinite(v->value) || fabs(v->value) > 1e12)) return false;
    return (!r.timeSynced || (r.measuredAt >= 1577836800ULL && r.measuredAt <= 4102444800ULL)) &&
           (!r.collectedTimeSynced || (r.collectedAt >= 1577836800ULL && r.collectedAt <= 4102444800ULL));
}
bool MeasurementCodec::fromJson(JsonObjectConst o, const String& expected, GatewayMeasurement& out, bool local) {
    if (o.isNull() || !safeString(o["deviceCode"], 64) || !safeString(o["recordId"], 80) ||
        !o["sequence"].is<uint32_t>() || !o["measuredAt"].is<uint64_t>() || !o["timeSynced"].is<bool>()) return false;
    GatewayMeasurement r;
    r.deviceCode = o["deviceCode"].as<const char*>(); r.recordId = o["recordId"].as<const char*>();
    r.sequence = o["sequence"].as<uint32_t>(); r.measuredAt = o["measuredAt"].as<uint64_t>(); r.timeSynced = o["timeSynced"].as<bool>();
    if (r.deviceCode != expected || !readOptional(o["temperature"], r.temperature) || !readOptional(o["humidity"], r.humidity) ||
        !readOptional(o["soilMoisture"], r.soilMoisture) || !readOptional(o["lightIntensity"], r.lightIntensity) ||
        !readOptional(o["ph"], r.ph) || !readOptional(o["waterLevel"], r.waterLevel) || !readOptional(o["batteryVoltage"], r.batteryVoltage)) return false;
    if (local) {
        if (!safeString(o["gatewayCode"], 64) || !o["collectedAt"].is<uint64_t>() || !o["collectedTimeSynced"].is<bool>() ||
            !o["gatewayRssi"].is<int>() || !o["syncStatus"].is<const char*>()) return false;
        const String status = o["syncStatus"].as<const char*>();
        if (status != "PENDING" && status != "SYNCED") return false;
        r.collectedAt = o["collectedAt"].as<uint64_t>(); r.collectedTimeSynced = o["collectedTimeSynced"].as<bool>();
        r.gatewayCode = o["gatewayCode"].as<const char*>(); r.gatewayRssi = o["gatewayRssi"].as<int>();
        r.syncStatus = status == "SYNCED" ? SyncStatus::Synced : SyncStatus::Pending;
    }
    if (!valid(r)) return false;
    out = r; return true;
}
void MeasurementCodec::toJson(const GatewayMeasurement& r, JsonObject o) {
    o["recordId"] = r.recordId; o["deviceCode"] = r.deviceCode; o["sequence"] = r.sequence;
    o["measuredAt"] = r.measuredAt; o["timeSynced"] = r.timeSynced;
    o["collectedAt"] = r.collectedAt; o["collectedTimeSynced"] = r.collectedTimeSynced;
    o["gatewayCode"] = r.gatewayCode; o["gatewayRssi"] = r.gatewayRssi;
    o["syncStatus"] = r.syncStatus == SyncStatus::Synced ? "SYNCED" : "PENDING";
    writeOptional(o,"temperature",r.temperature); writeOptional(o,"humidity",r.humidity);
    writeOptional(o,"soilMoisture",r.soilMoisture); writeOptional(o,"lightIntensity",r.lightIntensity);
    writeOptional(o,"ph",r.ph); writeOptional(o,"waterLevel",r.waterLevel); writeOptional(o,"batteryVoltage",r.batteryVoltage);
}
