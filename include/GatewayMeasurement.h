#pragma once
#include <ArduinoJson.h>
#include "Config.h"

struct OptionalReading { bool present = false; double value = 0; };
enum class SyncStatus { Pending, Synced };
struct GatewayMeasurement {
    String recordId, deviceCode;
    uint32_t sequence = 0;
    uint32_t missionId = 0; // Zero denotes collection outside a mission.
    uint64_t measuredAt = 0, collectedAt = 0;
    bool nodeAcknowledged = false;
    bool timeSynced = false, collectedTimeSynced = false;
    OptionalReading temperature, humidity, soilMoisture, lightIntensity, ph, waterLevel, batteryVoltage;
    String gatewayCode = Config::GATEWAY_CODE;
    int gatewayRssi = 0;
    SyncStatus syncStatus = SyncStatus::Pending;
};
namespace MeasurementCodec {
bool validDeviceCode(const String& code);
bool fromJson(JsonObjectConst object, const String& expectedDevice, GatewayMeasurement& out, bool local = false);
void toJson(const GatewayMeasurement& record, JsonObject object);
bool valid(const GatewayMeasurement& record);
}
