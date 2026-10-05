#include "StorageManager.h"
#include <LittleFS.h>
#include <mbedtls/sha256.h>
#include <mbedtls/base64.h>
#include <errno.h>
#include <string.h>
#include "Logger.h"

String StorageManager::key(const String& device, uint32_t sequence) const {
    const String identity = device + ":" + String(sequence);
    unsigned char digest[32];
    mbedtls_sha256_ret(reinterpret_cast<const unsigned char*>(identity.c_str()), identity.length(), digest, 0);
    // SHA-256 is 44 Base64 characters including one '='; reserve the terminator.
    // Base64URL without padding keeps all 256 bits in a filesystem-safe 43-byte name.
    unsigned char encoded[45] = {};
    size_t encodedLength = 0;
    mbedtls_base64_encode(encoded, sizeof(encoded), &encodedLength, digest, sizeof(digest));
    for (size_t i = 0; i < encodedLength; ++i) {
        if (encoded[i] == '+') encoded[i] = '-';
        else if (encoded[i] == '/') encoded[i] = '_';
    }
    while (encodedLength && encoded[encodedLength - 1] == '=') --encodedLength;
    encoded[encodedLength] = '\0';
    return String(reinterpret_cast<const char*>(encoded));
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
    const auto fail = [&](const char* stage, size_t expected, size_t actual) {
        Serial.printf("[STORAGE] writeVerified failed: stage=%s destination=%s expected=%u actual=%u heapFree=%u fsUsed=%u fsTotal=%u\n",
            stage, destination.c_str(), unsigned(expected), unsigned(actual),
            unsigned(ESP.getFreeHeap()), unsigned(LittleFS.usedBytes()), unsigned(LittleFS.totalBytes()));
        return false;
    };
    StaticJsonDocument<2048> doc;
    MeasurementCodec::toJson(record, doc.to<JsonObject>());
    const size_t length = measureJson(doc);
    if (doc.overflowed()) return fail("json-capacity", doc.capacity(), doc.memoryUsage());
    if (length > Config::MAX_RECORD_FILE_BYTES) return fail("record-too-large", Config::MAX_RECORD_FILE_BYTES, length);
    File file = LittleFS.open("/staging.tmp", "w");
    if (!file) return fail("staging-open-write", length, 0);
    const size_t written = serializeJson(doc, file); file.flush(); file.close();
    if (written != length) return fail("staging-short-write", length, written);
    // Stream comparison keeps stack use small and checks every persisted byte.
    String expected;
    if (!expected.reserve(length + 1)) return fail("heap-reserve", length + 1, 0);
    const size_t serialized = serializeJson(doc, expected);
    if (serialized != length || expected.length() != length) return fail("expected-serialization", length, expected.length());
    const auto verifyFile = [&](const char* path, const char* stage) {
        File verify = LittleFS.open(path, "r");
        if (!verify) {
            Serial.printf("[STORAGE] %s: open-read failed\n", stage);
            return fail(stage, length, 0);
        }
        const size_t actualSize = verify.size();
        if (actualSize != length) {
            verify.close();
            Serial.printf("[STORAGE] %s: size mismatch\n", stage);
            return fail(stage, length, actualSize);
        }
        for (size_t i = 0; i < length; ++i) {
            const int actual = verify.read();
            const unsigned char wanted = static_cast<unsigned char>(expected[i]);
            if (actual != wanted) {
                verify.close();
                Serial.printf("[STORAGE] %s: byte mismatch offset=%u expectedByte=%u actualByte=%d (-1 means read failure/EOF)\n",
                    stage, unsigned(i), unsigned(wanted), actual);
                return fail(stage, length, i);
            }
        }
        verify.close();
        return true;
    };
    if (!verifyFile("/staging.tmp", "staging-verify")) return false;
    errno = 0;
    if (!LittleFS.rename("/staging.tmp", destination)) {
        // Capture before logging or other filesystem calls can overwrite errno.
        const int renameError = errno;
        Serial.printf("[STORAGE] rename error: errno=%d message=%s destinationLength=%u filenameLength=%u\n",
            renameError, renameError ? strerror(renameError) : "not set by filesystem wrapper",
            unsigned(destination.length()), unsigned(destination.length() - destination.lastIndexOf('/') - 1));
        Serial.printf("[STORAGE] rename failed: stagingExists=%u destinationExists=%u\n",
            unsigned(LittleFS.exists("/staging.tmp")), unsigned(LittleFS.exists(destination)));
        return fail("rename", length, 0);
    }
    // Rename commit is verified without holding a second large JSON document.
    return verifyFile(destination.c_str(), "committed-verify");
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
