#include "MissionStorage.h"
#include <memory>
#include <cstring>
#include <cctype>
namespace {
const char* activePath="/mission/active.json";
const char* statePath="/mission/state.json";
const char* resultPath="/mission/result.json";
const char* tempPath="/mission/write.tmp";
struct Reader {
    const char* data;size_t size,index=0;
    Reader(const char* d,size_t s):data(d),size(s) {}
    int read(){return index<size ? static_cast<unsigned char>(data[index++]) : -1;}
    size_t readBytes(char* out,size_t n){size_t i=0;for(;i<n && index<size;++i) out[i]=data[index++];return i;}
};
}
bool MissionStorage::begin(){ready_=files_.begin();return ready_;}
bool MissionStorage::readJson(const char* path,JsonDocument& doc) {
    std::unique_ptr<char[]> data(new(std::nothrow) char[Config::MISSION_MAX_FILE_BYTES+1]);
    size_t size=0;if(!ready_ || !data || !files_.read(path,data.get(),Config::MISSION_MAX_FILE_BYTES+1,size) || !size) return false;
    Reader reader(data.get(),size);
    if(deserializeJson(doc,reader,DeserializationOption::NestingLimit(6)) || doc.overflowed() || !doc.is<JsonObject>()) return false;
    for(int c=reader.read();c>=0;c=reader.read()) if(!isspace(static_cast<unsigned char>(c))) return false;
    return true;
}
bool MissionStorage::writeJson(const char* path,const JsonDocument& doc) {
    size_t length=measureJson(doc);
    if(!ready_ || doc.overflowed() || !length || length>Config::MISSION_MAX_FILE_BYTES) return false;
    std::unique_ptr<char[]> expected(new(std::nothrow) char[length+1]),actual(new(std::nothrow) char[length+1]);
    if(!expected || !actual || serializeJson(doc,expected.get(),length+1)!=length) return false;
    if(!files_.write(tempPath,expected.get(),length)) return false;
    size_t size=0;
    if(!files_.read(tempPath,actual.get(),length+1,size) || size!=length || memcmp(expected.get(),actual.get(),length)) return false;
    // LittleFS rename atomically replaces the destination. Never remove it first.
    if(!files_.replace(tempPath,path)) return false;
    if(!files_.read(path,actual.get(),length+1,size) || size!=length || memcmp(expected.get(),actual.get(),length)) {ready_=false;return false;}
    return true;
}
bool MissionStorage::saveActive(const Mission& mission) {
    DynamicJsonDocument doc(Config::MISSION_JSON_CAPACITY);MissionCodec::encode(mission,doc.to<JsonObject>());
    Mission checked;const char* error="";
    if(!MissionCodec::decode(doc.as<JsonObjectConst>(),checked,error)) return false;
    return writeJson(activePath,doc);
}
bool MissionStorage::saveState(const MissionStateRecord& s) {
    StaticJsonDocument<512> doc;
    doc["missionId"]=s.missionId;doc["fingerprint"]=s.fingerprint;doc["state"]=MissionCodec::stateName(s.state);doc["waypointIndex"]=s.waypointIndex;
    return writeJson(statePath,doc);
}
MissionRestore MissionStorage::restore(Mission& m,MissionStateRecord& s) {
    s=MissionStateRecord{};
    if(!ready_) return MissionRestore::RecoveryRequired;
    if(!files_.exists(activePath)) return files_.exists(statePath) || files_.exists(resultPath) ? MissionRestore::RecoveryRequired : MissionRestore::Empty;
    DynamicJsonDocument doc(Config::MISSION_JSON_CAPACITY);const char* error="";
    if(!readJson(activePath,doc) || !MissionCodec::decode(doc.as<JsonObjectConst>(),m,error)) return MissionRestore::RecoveryRequired;
    s.missionId=m.id;s.fingerprint=MissionCodec::fingerprint(m);s.state=MissionState::RecoveryRequired;
    doc.clear();if(!readJson(statePath,doc)) return MissionRestore::RecoveryRequired;
    MissionState parsed;
    if(!doc["missionId"].is<int>() || doc["missionId"].as<int>()!=m.id || !doc["fingerprint"].is<uint32_t>() ||
       doc["fingerprint"].as<uint32_t>()!=s.fingerprint || !doc["waypointIndex"].is<size_t>() ||
       doc["waypointIndex"].as<size_t>()>m.waypointCount || !MissionCodec::parseState(doc["state"],parsed)) return MissionRestore::RecoveryRequired;
    s.waypointIndex=doc["waypointIndex"];
    if(MissionCodec::mayBeAirborne(parsed) || parsed==MissionState::PreparingTello || parsed==MissionState::GroundReady ||
       parsed==MissionState::Downloading || parsed==MissionState::Empty) return MissionRestore::RecoveryRequired;
    s.state=parsed;return parsed==MissionState::Ready ? MissionRestore::Ready : MissionRestore::Retained;
}
bool MissionStorage::saveResult(const MissionResult& r) {
    StaticJsonDocument<2048> doc;
    doc["missionId"]=r.missionId;doc["fingerprint"]=r.fingerprint;doc["finalState"]=MissionCodec::stateName(r.finalState);
    doc["completedWaypoints"]=r.completedWaypoints;doc["failedWaypointSequence"]=r.failedWaypointSequence;
    doc["failureReason"]=r.failureReason;doc["uploaded"]=r.uploaded;doc["mockUpload"]=r.mockUpload;
    JsonArray targets=doc.createNestedArray("targetOutcomes");for(auto t:r.targets) targets.add(static_cast<unsigned>(t));
    return writeJson(resultPath,doc);
}
bool MissionStorage::loadResult(MissionResult& out) {
    StaticJsonDocument<2048> doc;
    if(!readJson(resultPath,doc)) return false;
    MissionResult r;
    if(!doc["missionId"].is<int>() || doc["missionId"].as<int>()<=0 || !doc["fingerprint"].is<uint32_t>() ||
       !MissionCodec::parseState(doc["finalState"],r.finalState) ||
       (r.finalState!=MissionState::Completed && r.finalState!=MissionState::Failed && r.finalState!=MissionState::Aborted) ||
       !doc["completedWaypoints"].is<size_t>() || doc["completedWaypoints"].as<size_t>()>Config::MAX_MISSION_WAYPOINTS ||
       !doc["failedWaypointSequence"].is<int>() || !doc["uploaded"].is<bool>() || !doc["mockUpload"].is<bool>() ||
       !doc["failureReason"].is<const char*>()) return false;
    JsonString reason=doc["failureReason"].as<JsonString>();
    if(reason.size()>=sizeof(r.failureReason) || strlen(reason.c_str())!=reason.size()) return false;
    strcpy(r.failureReason,reason.c_str());
    JsonArrayConst targets=doc["targetOutcomes"];
    if(targets.size()!=Config::MAX_MISSION_TARGETS) return false;
    for(size_t i=0;i<targets.size();++i) {
        if(!targets[i].is<unsigned>() || targets[i].as<unsigned>()>static_cast<unsigned>(TargetOutcome::Skipped)) return false;
        r.targets[i]=static_cast<TargetOutcome>(targets[i].as<unsigned>());
    }
    r.missionId=doc["missionId"];r.fingerprint=doc["fingerprint"];r.completedWaypoints=doc["completedWaypoints"];
    r.failedWaypointSequence=doc["failedWaypointSequence"];r.uploaded=doc["uploaded"];r.mockUpload=doc["mockUpload"];
    out=r;return true;
}
