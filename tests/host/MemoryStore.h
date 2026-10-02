#pragma once
#include "RecordStore.h"
#include <map>
#include <string>
#include <cstring>
struct MemoryStore : RecordStore {
    std::map<std::string,std::string> files;
    bool failWrite = false, failRename = false, failRead = false;
    size_t available = 1024 * 1024;
    ReadResult read(const char* path, char* out, size_t capacity, size_t& length) override {
        length = 0;
        if (failRead) return ReadResult::Error;
        auto it = files.find(path);
        if (it == files.end()) return ReadResult::Missing;
        if (it->second.size() > capacity) return ReadResult::Error;
        length = it->second.size(); memcpy(out,it->second.data(),length); return ReadResult::Ok;
    }
    bool writeAtomic(const char* path, const char* bytes, size_t length) override {
        if (failWrite) return false;
        files[path] = std::string(bytes,length); return true;
    }
    bool rename(const char* from, const char* to) override {
        if (failRename || !files.count(from) || files.count(to)) return false;
        files[to] = files[from]; files.erase(from); return true;
    }
    bool remove(const char* path) override { files.erase(path); return true; }
    bool visit(const char* directory, Visitor visitor, void* context) override {
        const std::string prefix = std::string(directory) + "/";
        for (const auto& pair : files)
            if (pair.first.find(prefix) == 0 && !visitor(pair.first.c_str(),context)) return false;
        return true;
    }
    size_t freeBytes() override { return available; }
    void idle() override {}
};
