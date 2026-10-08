#include "MissionBackendClient.h"
#include <cstdio>
#include <cstring>
namespace {
const char* outcome(TargetOutcome t) {
    switch(t){case TargetOutcome::Collected:return "COLLECTED";case TargetOutcome::Failed:return "FAILED";
    case TargetOutcome::Skipped:return "SKIPPED";default:return "PENDING";}
}
}
MissionPull MissionBackendClient::update(bool mayPull,Mission& candidate) {
    // Both callers and this boundary gate traffic; stale caller state cannot use Tello.
    if(!mayPull || !wifi_.isInternetNetworkReady()) return MissionPull::Idle;
    uint32_t now=millis();
    if(!requested_ && attempted_ && uint32_t(now-lastAttempt_)<Config::MISSION_PULL_INTERVAL_MS) return MissionPull::Idle;
    requested_=false;attempted_=true;lastAttempt_=now;error_="";
    if(!isConfigured()){error_="MISSION_BACKEND_NOT_CONFIGURED";return MissionPull::Error;}
    DynamicJsonDocument response(Config::MISSION_JSON_CAPACITY);
    if(mock_) {
        // Same decoder/planner/storage/executor as real payloads. No simulated flight.
        Mission example;example.id=1;example.gatewayId=gatewayId_;example.uavId=1;
        strcpy(example.name,"MOCK-GROUND-DEMO");example.waypointCount=1;
        example.waypoints[0].id=1;example.waypoints[0].sequenceNo=1;
        example.waypoints[0].xCm=20;example.waypoints[0].altitudeCm=Config::TELLO_TARGET_ALTITUDE_CM;
        example.waypoints[0].holdSeconds=2;example.waypoints[0].action=MissionAction::Hold;
        MissionCodec::encode(example,response.to<JsonObject>());
        Serial.printf("[MISSION] MOCK source; this is not a backend mission pull\n");
    } else {
        char path[128];int n=snprintf(path,sizeof(path),Config::MISSION_NEXT_ENDPOINT,gatewayId_);
        if(n<0 || size_t(n)>=sizeof(path)){error_="MISSION_ENDPOINT_TOO_LONG";return MissionPull::Error;}
        int status=0;
        if(!transport_.request(path,"GET",nullptr,response,status)) {error_="MISSION_DOWNLOAD_FAILED";return MissionPull::Error;}
        if(status==204) return MissionPull::NoMission;
        if(status!=200){error_="MISSION_HTTP_STATUS_INVALID";return MissionPull::Error;}
    }
    Mission parsed;
    if(response.overflowed() || !MissionCodec::decode(response.as<JsonObjectConst>(),parsed,error_)) return MissionPull::Error;
    if(parsed.gatewayId!=gatewayId_){error_="MISSION_GATEWAY_MISMATCH";return MissionPull::Error;}
    candidate=parsed;return MissionPull::Downloaded;
}
bool MissionBackendClient::uploadResult(const Mission& mission,const MissionResult& result) {
    if(!wifi_.isInternetNetworkReady() || !isConfigured()) {error_="MISSION_BACKEND_UNAVAILABLE";return false;}
    if(mock_) {Serial.printf("[MISSION] MOCK result accepted locally; no backend upload\n");return true;}
    DynamicJsonDocument body(Config::MISSION_JSON_CAPACITY);
    body["missionId"]=mission.id;body["fingerprint"]=result.fingerprint;body["gatewayId"]=gatewayId_;
    body["finalState"]=result.finalState==MissionState::Aborted ? "CANCELLED" : MissionCodec::stateName(result.finalState);
    body["completedWaypoints"]=result.completedWaypoints;
    if(result.failedWaypointSequence) body["failedWaypointSequence"]=result.failedWaypointSequence;
    else body["failedWaypointSequence"]=nullptr;
    if(result.failureReason[0]) body["failureReason"]=result.failureReason;else body["failureReason"]=nullptr;
    JsonArray targets=body.createNestedArray("collectedTargets");
    for(size_t i=0;i<mission.targetCount;++i) {
        JsonObject t=targets.createNestedObject();t["sensorNodeId"]=mission.targets[i].sensorNodeId;
        t["deviceCode"]=mission.targets[i].deviceCode;t["status"]=outcome(result.targets[i]);
    }
    if(body.overflowed()) {error_="MISSION_RESULT_TOO_LARGE";return false;}
    char path[128];int n=snprintf(path,sizeof(path),Config::MISSION_RESULT_ENDPOINT,gatewayId_,mission.id);
    if(n<0 || size_t(n)>=sizeof(path)){error_="MISSION_ENDPOINT_TOO_LONG";return false;}
    StaticJsonDocument<1024> response;int status=0;
    if(!transport_.request(path,"POST",&body,response,status) || status!=200 ||
       !response["success"].is<bool>() || !response["success"].as<bool>() ||
       !response["missionId"].is<int>() || response["missionId"].as<int>()!=mission.id ||
       !response["fingerprint"].is<uint32_t>() || response["fingerprint"].as<uint32_t>()!=result.fingerprint) {
        error_="MISSION_RESULT_NOT_ACKNOWLEDGED";return false;
    }
    error_="";return true;
}
