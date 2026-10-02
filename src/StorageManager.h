#pragma once
#include "RecordStore.h"
class StorageManager : public RecordStore {
public:
    bool begin();
    bool healthy() const { return healthy_; }
    ReadResult read(const char* path, char* bytes, size_t capacity, size_t& length) override;
    bool writeAtomic(const char* path, const char* bytes, size_t length) override;
    bool rename(const char* from, const char* to) override;
    bool remove(const char* path) override;
    bool visit(const char* directory, Visitor visitor, void* context) override;
    size_t freeBytes() override;
    void idle() override;
private:
    bool healthy_ = false;
};
