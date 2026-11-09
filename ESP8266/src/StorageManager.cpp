#include "StorageManager.h"
#include <LittleFS.h>
#include <flash_hal.h>
#include "FlashPartitionCheck.h"
#include "Logger.h"
bool StorageManager::begin() {
    LittleFSConfig options;
    options.setAutoFormat(false);
    LittleFS.setConfig(options);
    healthy_ = LittleFS.begin();
    if (!healthy_) {
        const auto state = checkFlashPartition(FS_PHYS_ADDR, FS_PHYS_SIZE,
            ESP.getFlashChipRealSize(),
            [](uint32_t address, uint32_t* data, size_t size) {
                return ESP.flashRead(address, data, size);
            }, []() { yield(); });
        if (state != FlashPartitionState::Blank) {
            Logger::log("ERROR", state == FlashPartitionState::NonBlank
                ? "LittleFS mount failed; nonblank partition preserved (no format)"
                : "LittleFS mount failed; flash range/read check failed (no format)");
            return false;
        }
        Logger::log("NODE", "LittleFS partition is entirely blank; initializing first-use filesystem");
        healthy_ = LittleFS.format() && LittleFS.begin();
        if (!healthy_) {
            Logger::log("ERROR", "LittleFS first-use initialization failed");
            return false;
        }
    }
    healthy_ = (LittleFS.exists("/pending") || LittleFS.mkdir("/pending")) &&
        (LittleFS.exists("/acked") || LittleFS.mkdir("/acked"));
    Logger::log(healthy_ ? "NODE" : "ERROR", healthy_ ? "LittleFS mounted" : "LittleFS mounted but queue directories unavailable");
    return healthy_;
}
ReadResult StorageManager::read(const char* path, char* bytes, size_t capacity, size_t& length) {
    length = 0;
    if (!healthy_) return ReadResult::Error;
    if (!LittleFS.exists(path)) return ReadResult::Missing;
    File file = LittleFS.open(path,"r");
    if (!file || file.isDirectory() || file.size() > capacity) { healthy_ = false; return ReadResult::Error; }
    const size_t expected = file.size();
    length = file.readBytes(bytes,expected); file.close();
    if (length != expected) { healthy_ = false; return ReadResult::Error; }
    return ReadResult::Ok;
}
bool StorageManager::writeAtomic(const char* path, const char* bytes, size_t length) {
    if (!healthy_) return false;
    const String temp = String(path) + ".tmp";
    File file = LittleFS.open(temp,"w");
    if (!file) { healthy_ = false; return false; }
    const size_t written = file.write(reinterpret_cast<const uint8_t*>(bytes),length);
    file.flush(); file.close();
    Serial.printf("[STORAGE] Free stack watermark: %u bytes\n", ESP.getFreeContStack());
    // ESP8266 File::flush/close return void. Re-open and verify before publishing.
    file = LittleFS.open(temp,"r");
    bool valid = written == length && file && file.size() == length;
    char block[64]; size_t offset = 0;
    while (valid && offset < length) {
        const size_t n = std::min(sizeof(block),length - offset);
        valid = file.readBytes(block,n) == n && memcmp(block,bytes + offset,n) == 0;
        offset += n; yield();
    }
    file.close();
    if (!valid || !LittleFS.rename(temp,path)) { healthy_ = false; return false; }
    return true;
}
bool StorageManager::rename(const char* from, const char* to) {
    if (!healthy_ || LittleFS.exists(to) || !LittleFS.rename(from,to)) { healthy_ = false; return false; }
    return true;
}
bool StorageManager::remove(const char* path) {
    if (!healthy_ || !LittleFS.remove(path)) { healthy_ = false; return false; }
    return true;
}
bool StorageManager::visit(const char* directory, Visitor visitor, void* context) {
    if (!healthy_) return false;
    Dir dir = LittleFS.openDir(directory);
    while (dir.next()) {
        const String path = String(directory) + "/" + dir.fileName();
        if (dir.isDirectory() || !visitor(path.c_str(),context)) return false;
        yield();
    }
    return true;
}
size_t StorageManager::freeBytes() {
    FSInfo info;
    if (!healthy_ || !LittleFS.info(info)) return 0;
    return info.totalBytes - info.usedBytes;
}
void StorageManager::idle() { yield(); }
