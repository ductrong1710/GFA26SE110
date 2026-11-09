#pragma once
#include "MissionModel.h"
// Narrow filesystem interface permits power-loss/short-write tests with the
// production codec and commit algorithm. Only the ESP32 adapter uses LittleFS.
class MissionFiles {
public:
    virtual ~MissionFiles()=default;
    virtual bool begin()=0;
    virtual bool exists(const char* path)=0;
    virtual bool read(const char* path,char* out,size_t capacity,size_t& size)=0;
    virtual bool write(const char* path,const char* data,size_t size)=0;
    virtual bool replace(const char* source,const char* destination)=0;
};
class LittleFSMissionFiles : public MissionFiles {
public:
    bool begin() override;
    bool exists(const char*) override;
    bool read(const char*,char*,size_t,size_t&) override;
    bool write(const char*,const char*,size_t) override;
    bool replace(const char*,const char*) override;
};
enum class MissionRestore { Empty, Ready, Retained, RecoveryRequired };
class MissionStorage {
public:
    explicit MissionStorage(MissionFiles& files):files_(files) {}
    bool begin();
    bool isReady() const {return ready_;}
    bool saveActive(const Mission& mission);
    bool saveState(const MissionStateRecord& state);
    bool saveResult(const MissionResult& result);
    bool loadResult(MissionResult& result);
    MissionRestore restore(Mission& mission,MissionStateRecord& state);
private:
    MissionFiles& files_;
    bool ready_=false;
    bool readJson(const char* path,JsonDocument& doc);
    bool writeJson(const char* path,const JsonDocument& doc);
};
