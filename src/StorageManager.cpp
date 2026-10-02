#include "StorageManager.h"
#include <LittleFS.h>
#include <mbedtls/sha256.h>
#include "Logger.h"

String StorageManager::key(const String& device, uint32_t sequence) const {
    const String identity = device + ":" + String(sequence);
    unsigned char digest[32];
    mbedtls_sha256_ret(reinterpret_cast<const unsigned char*>(identity.c_str()), identity.length(), digest, 0);
    char hex[65];
    for (size_t i = 0; i < 32; ++i) snprintf(hex + i * 2, 3, "%02x", digest[i]);
    return String(hex);
}
bool StorageManager::begin() {
    ready_ = false; pending_ = stored_ = pendingCursor_ = 0;
    if (!LittleFS.begin(Config::FORMAT_LITTLEFS_ON_FAILURE, "/littlefs", 10, "spiffs")) {
        Logger::error("LittleFS mount failed; do not format existing data"); return false;
    }
    Logger::info("LittleFS mounted");
    for (const char* directory : {"/pending", "/synced", "/receipts"}) {
        if (!LittleFS.exists(directory) && !LittleFS.mkdir(directory)) {
            Logger::error("Storage directory creation failed"); return false;
        }
        File root = LittleFS.open(directory);
        if (!root || !root.isDirectory()) return false;
        for (File file = root.openNextFile(); file; file = root.openNextFile()) {
            if (!file.isDirectory()) {
                // A receipt may coexist with its synced payload after interrupted cleanup.
                if (strcmp(directory, "/synced") != 0 || !LittleFS.exists(String("/receipts/") + file.name())) ++stored_;
                if (strcmp(directory, "/pending") == 0) ++pending_;
            }
            file.close();
        }
        root.close();
    }
    if (LittleFS.exists("/staging.tmp") && !LittleFS.remove("/staging.tmp")) return false;
    ready_ = true;
    Serial.printf("[STORAGE] Stored identities: %u, pending: %u\n", unsigned(stored_), unsigned(pending_));
    return true;
}
bool StorageManager::readRecord(const String& path, GatewayMeasurement& out) {
    File file = LittleFS.open(path, "r");
    if (!file || !file.size() || file.size() > Config::MAX_RECORD_FILE_BYTES) return false;
    StaticJsonDocument<2048> doc;
    const auto error = deserializeJson(doc, file, DeserializationOption::NestingLimit(3));
    bool trailingData = false;
    while (file.available()) if (!isspace(static_cast<unsigned char>(file.read()))) trailingData = true;
    file.close();
    if (error || trailingData || !doc["deviceCode"].is<const char*>()) return false;
    return MeasurementCodec::fromJson(doc.as<JsonObjectConst>(), doc["deviceCode"].as<String>(), out, true);
}
bool StorageManager::receiptExists(const String& path, const String& device, uint32_t sequence) {
    File file = LittleFS.open(path, "r");
    if (!file || file.size() > 384) return false;
    StaticJsonDocument<384> doc;
    const auto error = deserializeJson(doc, file);
    bool trailingData = false;
    while (file.available()) if (!isspace(static_cast<unsigned char>(file.read()))) trailingData = true;
    file.close();
    return !error && !trailingData && doc["deviceCode"].as<String>() == device && doc["sequence"].is<uint32_t>() &&
        doc["sequence"].as<uint32_t>() == sequence && doc["synced"].is<bool>() && doc["synced"].as<bool>();
}
bool StorageManager::measurementExists(const String& device, uint32_t sequence) {
    if (!ready_) return false;
    const String hash = key(device, sequence);
    GatewayMeasurement found;
    for (const char* directory : {"/pending/", "/synced/"}) {
        const String path = String(directory) + hash;
        if (LittleFS.exists(path)) return readRecord(path, found) && found.deviceCode == device && found.sequence == sequence;
    }
    return receiptExists(String("/receipts/") + hash, device, sequence);
}
bool StorageManager::writeVerified(const GatewayMeasurement& record, const String& destination) {
    StaticJsonDocument<2048> doc;
    MeasurementCodec::toJson(record, doc.to<JsonObject>());
    const size_t length = measureJson(doc);
    if (doc.overflowed() || length > Config::MAX_RECORD_FILE_BYTES) return false;
    File file = LittleFS.open("/staging.tmp", "w");
    if (!file) return false;
    const size_t written = serializeJson(doc, file); file.flush(); file.close();
    // Stream comparison keeps stack use small and checks every persisted byte.
    String expected;
    if (!expected.reserve(length + 1)) return false;
    serializeJson(doc, expected);
    File verify = LittleFS.open("/staging.tmp", "r");
    bool same = verify && written == length && verify.size() == length;
    for (size_t i = 0; same && i < length; ++i) same = verify.read() == static_cast<unsigned char>(expected[i]);
    verify.close();
    if (!same || !LittleFS.rename("/staging.tmp", destination)) return false;
    // Rename commit is verified without holding a second large JSON document.
    File committed = LittleFS.open(destination, "r");
    same = committed && committed.size() == length;
    for (size_t i = 0; same && i < length; ++i) same = committed.read() == static_cast<unsigned char>(expected[i]);
    committed.close();
    return same;
}
SaveResult StorageManager::saveMeasurement(const GatewayMeasurement& record) {
    if (!ready_ || !MeasurementCodec::valid(record)) return SaveResult::Failed;
    if (measurementExists(record.deviceCode, record.sequence)) return SaveResult::Duplicate;
    const String hash = key(record.deviceCode, record.sequence);
    if (LittleFS.exists(String("/pending/") + hash) || LittleFS.exists(String("/synced/") + hash) ||
        LittleFS.exists(String("/receipts/") + hash) || stored_ >= Config::MAX_LOCAL_RECORDS) {
        Logger::error("Storage full or identity unreadable; no ACK allowed"); return SaveResult::Failed;
    }
    GatewayMeasurement pending = record; pending.syncStatus = SyncStatus::Pending;
    if (!writeVerified(pending, String("/pending/") + hash)) {
        Logger::error("LittleFS write verification failed; no ACK allowed");
        if (LittleFS.exists(String("/pending/") + hash)) ready_ = false;
        return SaveResult::Failed;
    }
    ++stored_; ++pending_; return SaveResult::Saved;
}
size_t StorageManager::loadPendingBatch(GatewayMeasurement* records, size_t capacity) {
    if (!ready_ || !records) return 0;
    capacity = std::min(capacity, Config::STORAGE_BATCH_SIZE);
    File directory = LittleFS.open("/pending");
    if (!directory) return 0;
    size_t count = 0, visited = 0;
    for (File file = directory.openNextFile(); file && count < capacity; file = directory.openNextFile()) {
        const String path = file.path(); file.close();
        if (visited++ < pendingCursor_) continue;
        if (readRecord(path, records[count]) && path == String("/pending/") + key(records[count].deviceCode, records[count].sequence)) {
            records[count].syncStatus = SyncStatus::Pending; ++count;
        } else Logger::error("Unreadable pending record retained");
    }
    pendingCursor_ = count == capacity ? visited : 0;
    directory.close(); return count;
}
bool StorageManager::markSynced(const GatewayMeasurement& record) {
    if (!ready_) return false;
    const String hash = key(record.deviceCode, record.sequence);
    const String pending = String("/pending/") + hash;
    GatewayMeasurement saved;
    if (!readRecord(pending, saved) || saved.recordId != record.recordId) return false;
    // Directory location is authoritative; rename is one LittleFS transaction.
    if (!LittleFS.rename(pending, String("/synced/") + hash)) return false;
    if (pending_) --pending_;
    return true;
}
bool StorageManager::cleanup() {
    if (!ready_) return false;
    File directory = LittleFS.open("/synced");
    if (!directory) return false;
    File file = directory.openNextFile();
    if (!file) { directory.close(); return true; }
    const String path = file.path(); file.close(); directory.close();
    GatewayMeasurement record;
    if (!readRecord(path, record) || path != String("/synced/") + key(record.deviceCode, record.sequence)) return false;
    const String receipt = String("/receipts/") + key(record.deviceCode, record.sequence);
    if (receiptExists(receipt, record.deviceCode, record.sequence)) return LittleFS.remove(path);
    StaticJsonDocument<384> doc;
    doc["deviceCode"] = record.deviceCode; doc["sequence"] = record.sequence; doc["synced"] = true;
    if (doc.overflowed()) return false;
    File output = LittleFS.open("/staging.tmp", "w");
    if (!output) return false;
    const size_t written = serializeJson(doc, output); output.flush(); output.close();
    if (written != measureJson(doc) || !receiptExists("/staging.tmp", record.deviceCode, record.sequence)) return false;
    if (!LittleFS.rename("/staging.tmp", receipt) || !receiptExists(receipt, record.deviceCode, record.sequence)) return false;
    return LittleFS.remove(path);
}
