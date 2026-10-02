#include "MeasurementQueue.h"
#include "MeasurementCodec.h"
#include <algorithm>
#include <stdlib.h>

namespace {
// A checksum covers the exact JSON bytes, independent of JSON float reformatting.
uint32_t crc32(const char* data, size_t size) {
    uint32_t crc = 0xffffffffU;
    for (size_t i = 0; i < size; ++i) {
        crc ^= static_cast<unsigned char>(data[i]);
        for (int bit = 0; bit < 8; ++bit) crc = (crc >> 1) ^ (0xedb88320U & (0U - (crc & 1U)));
    }
    return ~crc;
}
void pathFor(uint32_t sequence, bool acked, char* path, size_t capacity) {
    snprintf(path,capacity,"/%s/%010lu.rec",acked ? "acked" : "pending",static_cast<unsigned long>(sequence));
}
bool temporaryRecord(const char* path) {
    if (strncmp(path,"/pending/",9) != 0 || strlen(path) != 27 || strcmp(path + 19,".rec.tmp") != 0) return false;
    for (size_t i = 9; i < 19; ++i) if (path[i] < '0' || path[i] > '9') return false;
    const unsigned long long sequence = strtoull(path + 9,nullptr,10);
    return sequence > 0 && sequence <= UINT32_MAX;
}
ReadResult readFrame(RecordStore& store, const char* path, JsonDocument& doc) {
    char buffer[Config::MAX_RECORD_BYTES]; size_t size = 0;
    const ReadResult result = store.read(path,buffer,sizeof(buffer),size);
    if (result != ReadResult::Ok) return result;
    if (size < 10 || buffer[8] != '\n') return ReadResult::Error;
    char hashText[9]; memcpy(hashText,buffer,8); hashText[8] = 0;
    for (size_t i = 0; i < 8; ++i) if (!((hashText[i] >= '0' && hashText[i] <= '9') || (hashText[i] >= 'a' && hashText[i] <= 'f'))) return ReadResult::Error;
    if (crc32(buffer + 9,size - 9) != strtoul(hashText,nullptr,16)) return ReadResult::Error;
    // const buffer forces ArduinoJson to own strings after this function returns.
    if (deserializeJson(doc,static_cast<const char*>(buffer + 9),size - 9,DeserializationOption::NestingLimit(3))) return ReadResult::Error;
    return doc.is<JsonObject>() ? ReadResult::Ok : ReadResult::Error;
}
bool writeFrame(RecordStore& store, const char* path, const JsonDocument& doc) {
    char buffer[Config::MAX_RECORD_BYTES];
    const size_t length = measureJson(doc);
    if (doc.overflowed() || length + 9 >= sizeof(buffer)) return false;
    serializeJson(doc,buffer + 9,sizeof(buffer) - 9);
    const uint32_t crc = crc32(buffer + 9,length);
    snprintf(buffer,10,"%08lx",static_cast<unsigned long>(crc)); buffer[8] = '\n';
    // snprintf terminates at byte 8; byte 9 still starts the JSON.
    return store.writeAtomic(path,buffer,length + 9);
}
}
bool MeasurementQueue::load(const char* path, Measurement& record) {
    StaticJsonDocument<Config::RECORD_JSON_CAPACITY> doc;
    return readFrame(store_,path,doc) == ReadResult::Ok && MeasurementCodec::fromJson(doc.as<JsonObjectConst>(),record);
}
bool MeasurementQueue::scan(const char* path, void* context) {
    auto& q = *static_cast<MeasurementQueue*>(context);
    if (temporaryRecord(path)) return true; // Uncommitted data is never offered to gateway.
    Measurement record;
    if (q.count_ >= Config::MAX_LOCAL_RECORDS || !q.load(path,record)) return false;
    char expected[32]; pathFor(record.sequence,false,expected,sizeof(expected));
    if (strcmp(path,expected) != 0 || record.sequence > q.reserved_) return false;
    q.pending_[q.count_++] = record.sequence;
    q.store_.idle(); return true;
}
bool MeasurementQueue::begin() {
    healthy_ = false; count_ = 0; reserved_ = 0; next_ = 0; full_ = false;
    for (const char* p = Config::DEVICE_CODE; *p; ++p)
        if (static_cast<unsigned char>(*p) < 33 || static_cast<unsigned char>(*p) > 126) return false;
    unsigned int found = 0;
    for (unsigned int i = 0; i < 2; ++i) {
        const char* path = i ? "/sequence1" : "/sequence0";
        StaticJsonDocument<256> doc;
        const ReadResult status = readFrame(store_,path,doc);
        if (status == ReadResult::Error) return false; // Never roll back past corrupt metadata.
        if (status == ReadResult::Missing) continue;
        if (!doc["high"].is<uint32_t>() || !MeasurementCodec::textEquals(doc["deviceCode"],Config::DEVICE_CODE)) return false;
        const uint32_t high = doc["high"];
        if (!found || high >= reserved_) { reserved_ = high; slot_ = 1 - i; }
        ++found;
    }
    StaticJsonDocument<256> identity;
    const ReadResult identityStatus = readFrame(store_,"/identity",identity);
    if (identityStatus == ReadResult::Error || (identityStatus == ReadResult::Ok &&
        (found != 2 || !MeasurementCodec::textEquals(identity["deviceCode"],Config::DEVICE_CODE)))) return false;
    if (identityStatus == ReadResult::Missing) {
        // Commit sequence metadata BEFORE the marker: interrupted first provisioning
        // remains recoverable, but losing both metadata slots later never resets IDs.
        if (found < 2) {
            if (reserved_ != 0) return false;
            identity["deviceCode"] = Config::DEVICE_CODE; identity["high"] = uint32_t(0);
            if (!writeFrame(store_,"/sequence0",identity) || !writeFrame(store_,"/sequence1",identity)) return false;
            slot_ = 1;
        }
        identity.clear(); identity["deviceCode"] = Config::DEVICE_CODE;
        if (!writeFrame(store_,"/identity",identity)) return false;
    }
    if (!store_.visit("/pending",scan,this)) return false;
    std::sort(pending_,pending_ + count_);
    for (size_t i = 1; i < count_; ++i) if (pending_[i] == pending_[i - 1]) return false;
    next_ = reserved_; // Skip the unused reservation after every boot.
    healthy_ = true;
    return true;
}
bool MeasurementQueue::reserve() {
    const uint32_t remaining = UINT32_MAX - reserved_;
    const uint32_t amount = std::min(remaining,Config::SEQUENCE_RESERVATION_SIZE);
    if (!amount) return false;
    const uint32_t high = reserved_ + amount;
    StaticJsonDocument<256> doc;
    doc["deviceCode"] = Config::DEVICE_CODE; doc["high"] = high;
    if (!writeFrame(store_,slot_ ? "/sequence1" : "/sequence0",doc)) { healthy_ = false; return false; }
    reserved_ = high; slot_ = 1 - slot_; return true;
}
SaveResult MeasurementQueue::append(Measurement& record) {
    if (!healthy_) return SaveResult::Error;
    if (count_ == Config::MAX_LOCAL_RECORDS || store_.freeBytes() < Config::MIN_FREE_STORAGE_BYTES) {
        full_ = true; return SaveResult::Full;
    }
    if (next_ == UINT32_MAX) return SaveResult::SequenceExhausted;
    if (next_ == reserved_ && !reserve()) return SaveResult::Error;
    record.sequence = ++next_; record.state = MeasurementState::Pending;
    StaticJsonDocument<Config::RECORD_JSON_CAPACITY> doc;
    MeasurementCodec::toJson(record,doc.to<JsonObject>());
    char path[32]; pathFor(record.sequence,false,path,sizeof(path));
    if (!writeFrame(store_,path,doc)) { healthy_ = false; return SaveResult::Error; }
    pending_[count_++] = record.sequence; full_ = false; return SaveResult::Saved;
}
bool MeasurementQueue::readAt(size_t index, Measurement& record) {
    if (!healthy_ || index >= count_) return false;
    char path[32]; pathFor(pending_[index],false,path,sizeof(path));
    if (!load(path,record) || record.sequence != pending_[index]) { healthy_ = false; return false; }
    return true;
}
AckResult MeasurementQueue::acknowledge(const char* id) {
    if (!healthy_) return AckResult::Error;
    // Comparing canonical IDs forbids ranges, prefixes and alternate number spellings.
    for (size_t i = 0; i < count_; ++i) {
        char expected[80]; MeasurementCodec::recordId(pending_[i],expected,sizeof(expected));
        if (strcmp(id,expected) != 0) continue;
        char from[32],to[32]; pathFor(pending_[i],false,from,sizeof(from)); pathFor(pending_[i],true,to,sizeof(to));
        if (!store_.rename(from,to)) { healthy_ = false; return AckResult::Error; }
        for (size_t j = i + 1; j < count_; ++j) pending_[j - 1] = pending_[j];
        --count_; full_ = false; return AckResult::Acked;
    }
    return AckResult::NotPending;
}
void MeasurementQueue::update() {
    if (!healthy_) return;
    // One ACKED file per loop; state is represented by directory, payload stays immutable.
    char candidate[32]{};
    store_.visit("/acked",[](const char* path,void* context) {
        auto out = static_cast<char*>(context);
        if (strlen(path) >= 32) return false;
        strcpy(out,path); return false;
    },candidate);
    if (!candidate[0]) {
        store_.visit("/pending",[](const char* path,void* context) {
            if (!temporaryRecord(path)) return true;
            strcpy(static_cast<char*>(context),path); return false;
        },candidate);
    }
    if (candidate[0] && !store_.remove(candidate)) healthy_ = false;
}
