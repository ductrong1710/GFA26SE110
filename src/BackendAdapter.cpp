#include "BackendAdapter.h"
#include <mbedtls/sha256.h>
#include <time.h>

namespace {
String isoTime(uint64_t seconds) {
    const time_t epoch = static_cast<time_t>(seconds);
    struct tm utc;
    if (!gmtime_r(&epoch, &utc)) return String();
    char text[25];
    if (!strftime(text, sizeof(text), "%Y-%m-%dT%H:%M:%SZ", &utc)) return String();
    return String(text);
}
}

bool BackendAdapter::build(const GatewayMeasurement* records, size_t count, JsonDocument& document, String& batchKey) {
    if (count > Config::BACKEND_BATCH_SIZE) return false;
    rowCount_ = unresolvedCount_ = 0; memset(included_, 0, sizeof(included_)); document.clear();
    document["missionId"] = nullptr;
    JsonArray rows = document.createNestedArray("records");
    document.createNestedArray("collectionResults");
    JsonArray unresolved = document.createNestedArray("unresolvedRecords");
    bool missionSelected=false;uint32_t missionId=0;
    for (size_t i = 0; i < count; ++i) {
        const auto& r = records[i];
        if (!MeasurementCodec::valid(r)) continue;
        if (missionSelected && r.missionId != missionId) continue;
        const OptionalReading* values[] = {&r.temperature, &r.humidity, &r.soilMoisture, &r.lightIntensity, &r.ph, &r.waterLevel, &r.batteryVoltage};
        bool hasValue = false;
        for (const auto value : values) hasValue |= value->present;
        const String measured = r.timeSynced ? isoTime(r.measuredAt) : String();
        const String collected = r.collectedTimeSynced ? isoTime(r.collectedAt) : String();
        if (!missionSelected) {
            missionSelected = true; missionId = r.missionId;
            if (missionId) document["missionId"] = missionId;
        }
        if (!hasValue || measured.isEmpty() || collected.isEmpty() || r.measuredAt > r.collectedAt) {
            JsonObject item = unresolved.createNestedObject();
            item["sensorNodeCode"] = r.deviceCode;
            item["sourceRecordKey"] = r.recordId;
            MeasurementCodec::toJson(r, item.createNestedObject("payload"));
            item["payload"].remove("nodeAcknowledged");
            unresolvedOwners_[unresolvedCount_++] = i;
            continue;
        }
        for (size_t channel = 0; channel < 7; ++channel) {
            if (!values[channel]->present) continue;
            JsonObject row = rows.createNestedObject();
            row["sensorNodeCode"] = r.deviceCode;
            row["channelCode"] = Config::BACKEND_CHANNEL_CODES[channel];
            row["sourceRecordKey"] = r.recordId;
            row["value"] = values[channel]->value;
            row["measuredAt"] = measured; row["collectedAt"] = collected;
            row["qualityStatus"] = "VALID";
            owners_[rowCount_++] = i; included_[i] = true;
        }
    }
    if ((!rowCount_ && !unresolvedCount_) || document.overflowed()) return false;
    String canonical; serializeJson(document, canonical);
    unsigned char digest[32];
    mbedtls_sha256_ret(reinterpret_cast<const unsigned char*>(canonical.c_str()), canonical.length(), digest, 0);
    char hex[65]; for (size_t i = 0; i < 32; ++i) snprintf(hex + i * 2, 3, "%02x", digest[i]);
    batchKey = String("esp32-") + hex;
    document["batchKey"] = batchKey;
    return !document.overflowed() && measureJson(document) <= Config::MAX_HTTP_RESPONSE_BYTES;
}

bool BackendAdapter::accepted(const JsonDocument& response, const String& batchKey,
    const GatewayMeasurement* records, size_t count, bool* acceptedRecords) const {
    if (count > Config::BACKEND_BATCH_SIZE) return false;
    for (size_t i = 0; i < count; ++i) acceptedRecords[i] = false;
    if (count > Config::BACKEND_BATCH_SIZE || !response["success"].is<bool>() || response["success"] != true ||
        response["data"]["batchKey"].as<String>() != batchKey || !response["data"]["records"].is<JsonArrayConst>()) return false;
    JsonArrayConst results = response["data"]["records"].as<JsonArrayConst>();
    if (results.size() != rowCount_) return false;
    bool seen[Config::BACKEND_BATCH_SIZE * 7] = {};
    bool good[Config::BACKEND_BATCH_SIZE];
    for (size_t i = 0; i < count; ++i) good[i] = included_[i];
    for (JsonObjectConst result : results) {
        if (!result["index"].is<size_t>()) return false;
        const size_t index = result["index"].as<size_t>();
        if (index >= rowCount_ || seen[index]) return false;
        seen[index] = true;
        const size_t owner = owners_[index];
        if (owner >= count || result["sourceRecordKey"].as<String>() != records[owner].recordId) return false;
        const String status = result["status"].as<String>();
        if (status != "ACCEPTED" && status != "DUPLICATE") good[owner] = false;
    }
    if (unresolvedCount_) {
        JsonArrayConst archived = response["data"]["unresolvedRecords"].as<JsonArrayConst>();
        if (archived.isNull() || archived.size() != unresolvedCount_) return false;
        bool archivedSeen[Config::BACKEND_BATCH_SIZE] = {};
        for (JsonObjectConst result : archived) {
            if (!result["index"].is<size_t>()) return false;
            const size_t index = result["index"].as<size_t>();
            if (index >= unresolvedCount_ || archivedSeen[index]) return false;
            archivedSeen[index] = true;
            const size_t owner = unresolvedOwners_[index];
            if (result["sourceRecordKey"].as<String>() != records[owner].recordId) return false;
            const String status = result["status"].as<String>();
            good[owner] = status == "ACCEPTED" || status == "DUPLICATE";
        }
    }
    for (size_t i = 0; i < count; ++i) acceptedRecords[i] = good[i];
    return true;
}
