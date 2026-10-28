#include "MissionBackendClient.h"
#include <cstdio>
#include <cstring>
#ifdef ARDUINO_ARCH_ESP32
#include <esp_system.h>
#endif
void MissionBackendClient::beginLaunchSession() {
#ifdef ARDUINO_ARCH_ESP32
    for(int i=0;i<4;++i) snprintf(launchSession_+i*8,9,"%08lx",static_cast<unsigned long>(esp_random()));
#else
    strcpy(launchSession_,"00000000000000000000000000000001");
#endif
    launchPolled_=false;
}
bool MissionBackendClient::launchRequest(const Mission& m,bool consume,JsonDocument& response,int& status) {
    if(mock_ || !launchSession_[0] || !isConfigured() || !wifi_.isInternetNetworkReady()) return false;
    char path[128];snprintf(path,sizeof(path),"/device/gateways/%d/missions/%d/launch/%s",gatewayId_,m.id,consume?"consume":"ready");
    StaticJsonDocument<256> body;body["session"]=launchSession_;body["planRevision"]=m.planRevision;
    return transport_.request(path,"POST",&body,response,status);
}
RemoteLaunch MissionBackendClient::pollLaunch(const Mission& m) {
    if(mock_ || (launchPolled_ && uint32_t(millis()-lastLaunchPoll_)<5000)) return RemoteLaunch::Waiting;
    launchPolled_=true;lastLaunchPoll_=millis();
    StaticJsonDocument<2048> response;int status=0;
    const bool received=launchRequest(m,false,response,status);
    if(status==409 && !strcmp(response["message"] | "","PLAN_CHANGED_DOWNLOAD_AGAIN")) return RemoteLaunch::Reload;
    if(!received || status!=200 || response.overflowed() || response["success"]!=true) return RemoteLaunch::Waiting;
    const char* state=response["data"]["state"] | "";
    Serial.printf("[MISSION-LAUNCH] missionId=%d state=%s\n",m.id,state);
    if(!strcmp(state,"READY_TO_START")) return RemoteLaunch::Due;
    if(!strcmp(state,"MISSED") || !strcmp(state,"TERMINAL")) return RemoteLaunch::Retired;
    if(!strcmp(state,"CONSUMED")) return RemoteLaunch::Consumed;
    return RemoteLaunch::Waiting;
}
bool MissionBackendClient::consumeLaunch(const Mission& m) {
    StaticJsonDocument<2048> response;int status=0;
    const bool received=launchRequest(m,true,response,status);
    const bool accepted=received && status==200 && !response.overflowed() && response["success"]==true &&
        !strcmp(response["data"]["state"] | "","START_ACCEPTED");
    Serial.printf("[MISSION-LAUNCH] consume missionId=%d http=%d accepted=%d\n",m.id,status,accepted);
    if(!accepted) error_="REMOTE_START_NOT_ACKNOWLEDGED_CHECK_BACKEND";
    return accepted;
}
MissionPull MissionBackendClient::update(bool mayPull,Mission& candidate) {
    if(!mayPull || !wifi_.isInternetNetworkReady()) return MissionPull::Idle;
    uint32_t now=millis();
    if(!requested_ && attempted_ && uint32_t(now-lastAttempt_)<Config::MISSION_PULL_INTERVAL_MS) return MissionPull::Idle;
    requested_=false;attempted_=true;lastAttempt_=now;error_="";
    if(!isConfigured()){error_="MISSION_BACKEND_NOT_CONFIGURED";return MissionPull::Error;}
    DynamicJsonDocument response(Config::MISSION_JSON_CAPACITY);
    JsonObjectConst payload;
    if(mock_) {
        Mission example;example.id=1;example.gatewayId=gatewayId_;example.uavId=1;
        strcpy(example.name,"MOCK-GROUND-DEMO");example.waypointCount=1;
        example.waypoints[0].id=1;example.waypoints[0].sequenceNo=1;
        example.waypoints[0].xCm=20;example.waypoints[0].altitudeCm=Config::TELLO_TARGET_ALTITUDE_CM;
        example.waypoints[0].holdSeconds=2;example.waypoints[0].action=MissionAction::Hold;
        MissionCodec::encode(example,response.to<JsonObject>());
        payload=response.as<JsonObjectConst>();
    } else {
        char path[128];snprintf(path,sizeof(path),Config::MISSION_NEXT_ENDPOINT,gatewayId_);
        int status=0;
        if(!transport_.request(path,"GET",nullptr,response,status)){error_="MISSION_DOWNLOAD_FAILED";return MissionPull::Error;}
        if(status!=200 || !response["success"].is<bool>() || !response["success"].as<bool>() || !response.containsKey("data")) {
            error_="MISSION_RESPONSE_INVALID";return MissionPull::Error;
        }
        if(response["data"].isNull()) return MissionPull::NoMission;
        if(!response["data"].is<JsonObjectConst>()){error_="INVALID_MISSION";return MissionPull::Error;}
        payload=response["data"].as<JsonObjectConst>();
    }
    Mission parsed;
    if(response.overflowed() || !MissionCodec::decode(payload,parsed,error_)) return MissionPull::Error;
    if(parsed.gatewayId!=gatewayId_){error_="MISSION_GATEWAY_MISMATCH";return MissionPull::Error;}
    candidate=parsed;Serial.printf("[MISSION] Downloaded id=%d waypoints=%u targets=%u; awaiting local start\n",parsed.id,unsigned(parsed.waypointCount),unsigned(parsed.targetCount));
    return MissionPull::Downloaded;
}
bool MissionBackendClient::stateRequest(const Mission& mission,const char* action,const char* expected,const JsonDocument* body) {
    if(!wifi_.isInternetNetworkReady() || !isConfigured() || mission.gatewayId!=gatewayId_) {error_="MISSION_BACKEND_UNAVAILABLE";return false;}
    if(mock_) return true;
    char path[128];snprintf(path,sizeof(path),"/device/gateways/%d/missions/%d/%s",gatewayId_,mission.id,action);
    StaticJsonDocument<2048> response;int status=0;
    const bool received=transport_.request(path,"POST",body,response,status);
    const char* returned=response["data"]["status"] | "";
    const bool stateMatches=!strcmp(returned,expected) || (!strcmp(action,"downloaded") && !strcmp(returned,"RUNNING"));
    if(!received || status!=200 || response.overflowed() || !response["success"].is<bool>() || !response["success"].as<bool>() ||
       !response["data"]["id"].is<int>() || response["data"]["id"].as<int>()!=mission.id || !stateMatches) {
        Serial.printf("[MISSION] %s rejected id=%d http=%d; retained for retry\n",action,mission.id,status);
        error_="MISSION_BACKEND_STATE_NOT_ACKNOWLEDGED";return false;
    }
    Serial.printf("[MISSION] %s acknowledged id=%d state=%s\n",action,mission.id,returned);error_="";return true;
}
bool MissionBackendClient::acknowledgeDownloaded(const Mission& mission) {return stateRequest(mission,"downloaded","SCHEDULED");}
bool MissionBackendClient::startMission(const Mission& mission) {return stateRequest(mission,"start","RUNNING");}
bool MissionBackendClient::uploadCollectionResults(const Mission& mission,const MissionResult& result) {
    if(!mission.targetCount || mock_) return true;
    DynamicJsonDocument body(Config::MISSION_JSON_CAPACITY),response(Config::MISSION_JSON_CAPACITY);
    char key[96];snprintf(key,sizeof(key),"mission-%d-%lu-outcomes",mission.id,static_cast<unsigned long>(result.fingerprint));
    body["batchKey"]=key;body["missionId"]=mission.id;body.createNestedArray("records");
    JsonArray items=body.createNestedArray("collectionResults");
    for(size_t i=0;i<mission.targetCount;++i) {
        if(result.targets[i]==TargetOutcome::Pending){error_="MISSION_TARGET_RESULT_PENDING";return false;}
        JsonObject item=items.createNestedObject();item["sensorNodeId"]=mission.targets[i].sensorNodeId;
        item["attemptNo"]=1;item["recordsReceived"]=result.targetRecords[i];
        item["status"]=result.targets[i]==TargetOutcome::Collected ? "SUCCESS" : result.targets[i]==TargetOutcome::Skipped ? "SKIPPED" : "FAILED";
    }
    char path[96];snprintf(path,sizeof(path),Config::BACKEND_SENSOR_BATCH_ENDPOINT,gatewayId_);int status=0;
    if(body.overflowed() || !transport_.request(path,"POST",&body,response,status) || status!=200 || response.overflowed() || response["success"]!=true ||
       strcmp(response["data"]["batchKey"] | "",key)) {error_="MISSION_OUTCOMES_NOT_ACKNOWLEDGED";return false;}
    JsonArrayConst outcomes=response["data"]["collectionResults"].as<JsonArrayConst>();
    if(outcomes.size()!=mission.targetCount){error_="MISSION_OUTCOMES_NOT_ACKNOWLEDGED";return false;}
    bool accepted[Config::MAX_MISSION_TARGETS]={};
    for(JsonObjectConst item:outcomes) {
        if(!item["index"].is<size_t>()){error_="MISSION_OUTCOMES_NOT_ACKNOWLEDGED";return false;}
        size_t i=item["index"].as<size_t>();const char* s=item["status"] | "";
        if(i>=mission.targetCount || accepted[i] || item["sensorNodeId"].as<int>()!=mission.targets[i].sensorNodeId ||
           item["attemptNo"].as<int>()!=1 || (strcmp(s,"ACCEPTED") && strcmp(s,"DUPLICATE"))) {
            error_="MISSION_OUTCOMES_NOT_ACKNOWLEDGED";return false;
        }
        accepted[i]=true;
    }
    return true;
}
bool MissionBackendClient::uploadResult(const Mission& mission,const MissionResult& result) {
    if(!wifi_.isInternetNetworkReady() || !isConfigured()) {error_="MISSION_BACKEND_UNAVAILABLE";return false;}
    if(mock_) return true;
    if(result.missionId!=mission.id){error_="MISSION_RESULT_IDENTITY_MISMATCH";return false;}
    StaticJsonDocument<512> body;
    if(result.finalState==MissionState::Completed) {
        if(!uploadCollectionResults(mission,result)) return false;
        return stateRequest(mission,"complete","COMPLETED",&body);
    }
    if(result.finalState==MissionState::Aborted) {
        body["reason"]=result.failureReason;
        return stateRequest(mission,"cancel","CANCELLED",&body) && uploadCollectionResults(mission,result);
    }
    body["failureReason"]=result.failureReason[0] ? result.failureReason : "MISSION_FAILED";
    return stateRequest(mission,"fail","FAILED",&body) && uploadCollectionResults(mission,result);
}
