#pragma once
#include <ArduinoJson.h>
#include <ctype.h>
#include <string.h>
#include "Config.h"
namespace JsonSafety {
struct Reader {
    const char* data; size_t length; size_t position = 0;
    int read() { return position < length ? static_cast<unsigned char>(data[position++]) : -1; }
    size_t readBytes(char* out, size_t count) {
        size_t n = 0; while (n < count && position < length) out[n++] = data[position++]; return n;
    }
};
inline bool parse(const char* data, size_t length, JsonDocument& doc) {
    doc.clear();
    if (!length || length > Config::MAX_API_BODY_BYTES) return false;
    Reader reader{data,length,0};
    if (deserializeJson(doc,reader,DeserializationOption::NestingLimit(3)) || !doc.is<JsonObject>()) return false;
    for (int ch = reader.read(); ch >= 0; ch = reader.read()) if (!isspace(static_cast<unsigned char>(ch))) return false;
    return !doc.overflowed();
}
inline bool limit(const char* input, size_t& count) {
    if (!input || !*input) return false;
    count = 0;
    for (const char* p = input; *p; ++p) {
        if (*p < '0' || *p > '9') return false;
        if (count <= Config::MAX_API_BATCH_SIZE) count = count * 10 + (*p - '0');
    }
    if (!count) return false;
    if (count > Config::MAX_API_BATCH_SIZE) count = Config::MAX_API_BATCH_SIZE;
    return true;
}
inline bool ackIds(JsonVariantConst value) {
    if (!value.is<JsonArrayConst>() || value.size() == 0 || value.size() > Config::MAX_API_BATCH_SIZE) return false;
    for (JsonVariantConst id : value.as<JsonArrayConst>()) {
        if (!id.is<const char*>()) return false;
        const JsonString text = id.as<JsonString>();
        if (!text.size() || text.size() >= 80 || text.size() != strlen(text.c_str())) return false;
        for (size_t i = 0; i < text.size(); ++i) if (static_cast<unsigned char>(text.c_str()[i]) < 33 || static_cast<unsigned char>(text.c_str()[i]) > 126) return false;
    }
    return true;
}
inline bool unixTime(JsonVariantConst value) {
    return value.is<uint64_t>() && value.as<uint64_t>() >= 1577836800ULL && value.as<uint64_t>() <= 4102444800ULL;
}
}
