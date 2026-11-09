#include "MissionStorage.h"
#include <LittleFS.h>
bool LittleFSMissionFiles::begin() {
    if(!LittleFS.exists("/mission") && !LittleFS.mkdir("/mission")) return false;
    File directory=LittleFS.open("/mission");return directory && directory.isDirectory();
}
bool LittleFSMissionFiles::exists(const char* path) {return LittleFS.exists(path);}
bool LittleFSMissionFiles::read(const char* path,char* out,size_t capacity,size_t& size) {
    File f=LittleFS.open(path,"r");if(!f || f.isDirectory() || f.size()>=capacity) return false;
    size=f.size();if(f.readBytes(out,size)!=size) return false;
    out[size]=0;return true;
}
bool LittleFSMissionFiles::write(const char* path,const char* data,size_t size) {
    File f=LittleFS.open(path,"w");if(!f) return false;
    size_t written=f.write(reinterpret_cast<const uint8_t*>(data),size);f.flush();f.close();return written==size;
}
bool LittleFSMissionFiles::replace(const char* source,const char* destination) {return LittleFS.rename(source,destination);}
