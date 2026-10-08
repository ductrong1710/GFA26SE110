#pragma once
#include "MissionStorage.h"
#include <map>
#include <string>
class MissionMemoryFiles:public MissionFiles {
public:
    std::map<std::string,std::string> files;
    bool fail=false;
    bool begin() override {return true;}
    bool exists(const char* p) override {return files.count(p);}
    bool read(const char* p,char* out,size_t cap,size_t& size) override {
        if(!files.count(p) || files[p].size()>=cap) return false;
        size=files[p].size();memcpy(out,files[p].data(),size);out[size]=0;return true;
    }
    bool write(const char* p,const char* data,size_t size) override {if(fail)return false;files[p]=std::string(data,size);return true;}
    bool replace(const char* a,const char* b) override {if(fail)return false;files[b]=files[a];files.erase(a);return true;}
};
