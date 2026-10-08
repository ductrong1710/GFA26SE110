#include "MissionModel.h"
#include <cmath>
#include <cstring>
#include <limits>
namespace {
const char* const states[]={"EMPTY","DOWNLOADING","READY","PREPARING_TELLO","GROUND_READY",
    "TAKING_OFF","STABILIZING","EXECUTING","COLLECTING","LANDING","LANDED",
    "CONNECTING_INTERNET","SYNCING","COMPLETED","FAILED","ABORTED","RECOVERY_REQUIRED"};
bool text(JsonVariantConst value,char* out,size_t capacity) {
    if(!value.is<const char*>()) return false;
    JsonString s=value.as<JsonString>();
    if(!s.size() || s.size()>=capacity || strlen(s.c_str())!=s.size()) return false;
    for(size_t i=0;i<s.size();++i) if(static_cast<unsigned char>(s.c_str()[i])<32) return false;
    memcpy(out,s.c_str(),s.size()+1); return true;
}
bool positive(JsonVariantConst v,int& out) {
    if(!v.is<int>() || v.as<int>()<=0) return false;
    out=v.as<int>(); return true;
}
bool centimeters(JsonVariantConst v,int& out) {
    if(!v.is<double>() || v.is<bool>()) return false;
    double cm=v.as<double>()*100.0;
    if(!std::isfinite(cm) || fabs(cm)>Config::MISSION_MAX_LOCAL_CM) return false;
    // Integer centimetres are the command contract. Never silently round a route.
    double rounded=round(cm);
    if(fabs(cm-rounded)>0.0001) return false;
    out=static_cast<int>(rounded); return true;
}
}
const char* MissionCodec::stateName(MissionState state) {
    size_t i=static_cast<size_t>(state); return i<sizeof(states)/sizeof(states[0]) ? states[i] : "RECOVERY_REQUIRED";
}
bool MissionCodec::parseState(const char* value,MissionState& out) {
    if(!value) return false;
    for(size_t i=0;i<sizeof(states)/sizeof(states[0]);++i) if(!strcmp(value,states[i])) {out=static_cast<MissionState>(i);return true;}
    return false;
}
bool MissionCodec::mayBeAirborne(MissionState s) {
    return s==MissionState::TakingOff || s==MissionState::Stabilizing || s==MissionState::Executing ||
        s==MissionState::Collecting || s==MissionState::Landing || s==MissionState::RecoveryRequired;
}
bool MissionCodec::decode(JsonObjectConst obj,Mission& out,const char*& error) {
    error="INVALID_MISSION";
    // Decode into a temporary object so a rejected payload cannot partially alter active state.
    Mission next;
    if(!positive(obj["id"],next.id) || !positive(obj["gatewayId"],next.gatewayId) ||
       !positive(obj["uavId"],next.uavId) || !text(obj["name"],next.name,sizeof(next.name))) return false;
    if(!obj["status"].is<const char*>() || strcmp(obj["status"],"SCHEDULED")) {error="MISSION_NOT_SCHEDULED";return false;}
    if(!obj["waypoints"].is<JsonArrayConst>() || !obj["targets"].is<JsonArrayConst>()) return false;
    JsonArrayConst waypoints=obj["waypoints"],targets=obj["targets"];
    if(!waypoints.size() || waypoints.size()>Config::MAX_MISSION_WAYPOINTS || targets.size()>Config::MAX_MISSION_TARGETS) {
        error="MISSION_CAPACITY_EXCEEDED";return false;
    }
    for(JsonObjectConst w:waypoints) {
        auto& dst=next.waypoints[next.waypointCount++];
        if(!positive(w["id"],dst.id) || !positive(w["sequenceNo"],dst.sequenceNo)) return false;
        if(!centimeters(w["localX"],dst.xCm) || !centimeters(w["localY"],dst.yCm)) {
            error="MISSION_LOCAL_COORDINATES_REQUIRED";return false;
        }
        if(!centimeters(w["altitudeM"],dst.altitudeCm) || dst.altitudeCm<=0) {error="INVALID_MISSION_ALTITUDE";return false;}
        if(!w["plannedHoldSeconds"].is<uint32_t>() || w["plannedHoldSeconds"].as<uint32_t>()>Config::MISSION_MAX_HOLD_SECONDS) return false;
        dst.holdSeconds=w["plannedHoldSeconds"];
        char action[16]; if(!text(w["actionType"],action,sizeof(action))) return false;
        if(!strcmp(action,"MOVE")) dst.action=MissionAction::Move;
        else if(!strcmp(action,"HOLD")) dst.action=MissionAction::Hold;
        else if(!strcmp(action,"COLLECT")) dst.action=MissionAction::Collect;
        else {error="MISSION_ACTION_UNSUPPORTED";return false;}
    }
    for(JsonObjectConst t:targets) {
        auto& dst=next.targets[next.targetCount++];
        if(!positive(t["sensorNodeId"],dst.sensorNodeId) || !positive(t["waypointId"],dst.waypointId) ||
           !text(t["deviceCode"],dst.deviceCode,sizeof(dst.deviceCode))) {error="MISSION_TARGET_IDENTITY_REQUIRED";return false;}
    }
    out=next; error="";return true;
}
void MissionCodec::encode(const Mission& m,JsonObject obj) {
    obj["id"]=m.id;obj["name"]=m.name;obj["gatewayId"]=m.gatewayId;obj["uavId"]=m.uavId;obj["status"]="SCHEDULED";
    JsonArray waypoints=obj.createNestedArray("waypoints");
    for(size_t i=0;i<m.waypointCount;++i) {
        const auto& w=m.waypoints[i];JsonObject o=waypoints.createNestedObject();
        o["id"]=w.id;o["sequenceNo"]=w.sequenceNo;o["localX"]=w.xCm/100.0;o["localY"]=w.yCm/100.0;
        o["altitudeM"]=w.altitudeCm/100.0;o["plannedHoldSeconds"]=w.holdSeconds;
        o["actionType"]=w.action==MissionAction::Collect ? "COLLECT" : w.action==MissionAction::Hold ? "HOLD" : "MOVE";
    }
    JsonArray targets=obj.createNestedArray("targets");
    for(size_t i=0;i<m.targetCount;++i) {
        const auto& t=m.targets[i];JsonObject o=targets.createNestedObject();
        o["sensorNodeId"]=t.sensorNodeId;o["deviceCode"]=t.deviceCode;o["waypointId"]=t.waypointId;
    }
}
uint32_t MissionCodec::fingerprint(const Mission& mission) {
    // Deterministic content tag for cross-file consistency, not authentication.
    struct HashWriter {
        uint32_t hash=2166136261u;
        size_t write(uint8_t c) { hash=(hash^c)*16777619u;return 1; }
        size_t write(const uint8_t* p,size_t n) {for(size_t i=0;i<n;++i) write(p[i]);return n;}
    } writer;
    DynamicJsonDocument doc(Config::MISSION_JSON_CAPACITY);encode(mission,doc.to<JsonObject>());
    if(doc.overflowed()) return 0;
    serializeJson(doc,writer);return writer.hash;
}
