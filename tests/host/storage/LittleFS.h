#pragma once
#include "Arduino.h"
#include <map>
#include <set>
#include <memory>
#include <vector>
#include <algorithm>
namespace MemoryFlash {
extern std::map<std::string,std::string> files;
extern std::set<std::string> directories;
extern bool failRename, failRemove;
}
class File {
    std::string path_; size_t position_=0, next_=0; bool valid_=false, directory_=false;
    std::vector<std::string> children_;
public:
    File() = default;
    explicit File(const std::string& path):path_(path),valid_(MemoryFlash::files.count(path)||MemoryFlash::directories.count(path)),directory_(MemoryFlash::directories.count(path)) {
        if(directory_) for(const auto& item:MemoryFlash::files) if(item.first.compare(0,path.size()+1,path+"/")==0) children_.push_back(item.first);
    }
    explicit operator bool() const {return valid_;}
    bool isDirectory() const {return directory_;}
    const char* path() const {return path_.c_str();}
    const char* name() const {return path_.c_str()+path_.find_last_of('/')+1;}
    size_t size() const {return MemoryFlash::files[path_].size();}
    int available() const {return valid_ && !directory_ && position_<size();}
    int read() {return available()?static_cast<unsigned char>(MemoryFlash::files[path_][position_++]):-1;}
    size_t readBytes(char* out,size_t count) {size_t n=0;while(n<count && available())out[n++]=char(read());return n;}
    size_t write(uint8_t byte) {MemoryFlash::files[path_]+=char(byte);return 1;}
    size_t write(const uint8_t* bytes,size_t count) {MemoryFlash::files[path_].append(reinterpret_cast<const char*>(bytes),count);return count;}
    void flush() {}
    void close() {valid_=false;}
    File openNextFile() {return next_<children_.size()?File(children_[next_++]):File();}
};
struct MemoryLittleFS {
    bool begin(bool,const char*,int,const char*) {return true;}
    bool exists(const String& p) {return MemoryFlash::files.count(p.c_str())||MemoryFlash::directories.count(p.c_str());}
    bool mkdir(const String& p) {MemoryFlash::directories.insert(p.c_str());return true;}
    File open(const String& p,const char* mode="r") {if(!strcmp(mode,"w"))MemoryFlash::files[p.c_str()]="";return File(p.c_str());}
    bool rename(const String& from,const String& to) {if(MemoryFlash::failRename||!MemoryFlash::files.count(from.c_str()))return false;MemoryFlash::files[to.c_str()]=MemoryFlash::files[from.c_str()];MemoryFlash::files.erase(from.c_str());return true;}
    bool remove(const String& p) {return !MemoryFlash::failRemove && MemoryFlash::files.erase(p.c_str());}
    size_t usedBytes() const {return MemoryFlash::files.size()*512;}
    size_t totalBytes() const {return 1024*1024;}
};
extern MemoryLittleFS LittleFS;
struct TestEsp {size_t getFreeHeap() const {return 100000;}};
extern TestEsp ESP;
