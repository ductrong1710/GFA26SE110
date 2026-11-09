#pragma once
#include <stddef.h>
enum class ReadResult { Ok, Missing, Error };
// Only the adapter knows LittleFS. Tests substitute the physical I/O boundary.
class RecordStore {
public:
    using Visitor = bool (*)(const char* path, void* context);
    virtual ~RecordStore() = default;
    virtual ReadResult read(const char* path, char* bytes, size_t capacity, size_t& length) = 0;
    virtual bool writeAtomic(const char* path, const char* bytes, size_t length) = 0;
    virtual bool rename(const char* from, const char* to) = 0;
    virtual bool remove(const char* path) = 0;
    virtual bool visit(const char* directory, Visitor visitor, void* context) = 0;
    virtual size_t freeBytes() = 0;
    virtual void idle() = 0;
};
