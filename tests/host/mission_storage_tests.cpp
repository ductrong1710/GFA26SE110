#include "MissionStorage.h"
#include <cassert>
#include <iostream>
#include <map>
#include <string>

// Fault injection operates below production serialization/commit logic.
class MemoryFiles : public MissionFiles {
public:
    std::map<std::string,std::string> files;
    bool failWrite=false, failReplace=false;
    bool exists(const char* path) override { return files.count(path); }
    bool read(const char* path,char* out,size_t capacity,size_t& size) override {
        auto it=files.find(path); if(it==files.end() || it->second.size()>=capacity) return false;
        size=it->second.size(); memcpy(out,it->second.data(),size); out[size]=0; return true;
    }
    bool write(const char* path,const char* data,size_t size) override {
        files[path]=std::string(data,failWrite ? size/2 : size); return !failWrite;
    }
    bool replace(const char* source,const char* destination) override {
        if(failReplace) return false;
        files[destination]=files[source]; files.erase(source); return true;
    }
    bool begin() override { return true; }
};
const char* payload=R"({"id":15,"name":"demo","gatewayId":1,"uavId":2,"status":"SCHEDULED","waypoints":[{"id":100,"sequenceNo":1,"localX":0.2,"localY":0,"altitudeM":0.35,"actionType":"COLLECT","plannedHoldSeconds":1}],"targets":[{"sensorNodeId":5,"deviceCode":"SENSOR-001","waypointId":100}]})";
int main() {
    DynamicJsonDocument doc(16000); assert(!deserializeJson(doc,payload));
    Mission mission; const char* error="";
    assert(MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(mission.id==15 && mission.waypoints[0].altitudeCm==35);
    assert(mission.waypoints[0].xCm==20 && mission.targets[0].sensorNodeId==5);
    MemoryFiles files; MissionStorage storage(files); assert(storage.begin());
    assert(storage.saveActive(mission));
    MissionStateRecord state; state.missionId=15; state.fingerprint=MissionCodec::fingerprint(mission);
    state.state=MissionState::Ready; assert(storage.saveState(state));
    Mission restored; MissionStateRecord recovered;
    assert(storage.restore(restored,recovered)==MissionRestore::Ready);
    assert(restored.id==15 && recovered.state==MissionState::Ready);
    const std::string valid=files.files["/mission/active.json"];
    mission.id=16; files.failWrite=true; assert(!storage.saveActive(mission));
    assert(files.files["/mission/active.json"]==valid); files.failWrite=false;
    files.failReplace=true; assert(!storage.saveActive(mission));
    assert(files.files["/mission/active.json"]==valid); files.failReplace=false;
    // A durable airborne state never restores READY.
    state.state=MissionState::Executing; assert(storage.saveState(state));
    assert(storage.restore(restored,recovered)==MissionRestore::RecoveryRequired);
    // Unknown, truncated and missing state are never interpreted as permission to fly.
    files.files["/mission/state.json"]="{";
    assert(storage.restore(restored,recovered)==MissionRestore::RecoveryRequired);
    files.files.erase("/mission/state.json");
    assert(storage.restore(restored,recovered)==MissionRestore::RecoveryRequired);
    state.state=MissionState::Ready; state.fingerprint++;
    assert(storage.saveState(state));
    assert(storage.restore(restored,recovered)==MissionRestore::RecoveryRequired);
    files.files["/mission/active.json"]=valid+"junk";
    assert(storage.restore(restored,recovered)==MissionRestore::RecoveryRequired);
    // Structural validation rejects absent identity, GPS-only, embedded NUL and huge arrays.
    assert(!deserializeJson(doc,payload)); doc.remove("gatewayId");
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(!deserializeJson(doc,payload)); doc["waypoints"][0].remove("localX");
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(std::string(error)=="MISSION_LOCAL_COORDINATES_REQUIRED");
    assert(!deserializeJson(doc,payload)); doc["name"]="";
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(!deserializeJson(doc,payload)); doc["waypoints"][0]["altitudeM"]=0.355;
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(!deserializeJson(doc,payload)); doc["targets"][0].remove("deviceCode");
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(!deserializeJson(doc,payload));
    for(size_t i=1;i<=Config::MAX_MISSION_WAYPOINTS;++i) doc["waypoints"].as<JsonArray>().createNestedObject();
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    assert(!strcmp(error,"MISSION_CAPACITY_EXCEEDED"));
    assert(!deserializeJson(doc,payload));doc["waypoints"][0]["altitudeM"]=0.5;
    assert(MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));assert(mission.waypoints[0].altitudeCm==50);
    assert(!deserializeJson(doc,payload));doc["status"]="RUNNING";
    assert(!MissionCodec::decode(doc.as<JsonObjectConst>(),mission,error));
    MissionResult result; result.missionId=15; result.fingerprint=123;
    result.finalState=MissionState::Completed; result.completedWaypoints=1;
    assert(storage.saveResult(result)); MissionResult loaded;
    assert(storage.loadResult(loaded)); assert(loaded.missionId==15 && !loaded.uploaded);
    loaded.uploaded=true; files.failReplace=true;
    assert(!storage.saveResult(loaded)); assert(storage.loadResult(loaded)); assert(!loaded.uploaded);
    std::cout << "Mission codec, verified storage, fault and reboot tests passed\n";
}
