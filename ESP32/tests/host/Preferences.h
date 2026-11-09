#pragma once
#include "Arduino.h"
#include <map>
namespace FakeNvs {
extern std::map<std::string,std::string> entries;
extern bool openFails, writeFails;
}
class Preferences {
public:
    bool begin(const char*, bool = false) { return !FakeNvs::openFails; }
    bool isKey(const char* key) { return FakeNvs::entries.count(key)>0; }
    String getString(const char* key, const char* fallback="") { return isKey(key)?String(FakeNvs::entries[key]):String(fallback); }
    size_t putString(const char* key,const char* value) { if(FakeNvs::writeFails) return 0; FakeNvs::entries[key]=value; return strlen(value); }
    void end() {}
};
