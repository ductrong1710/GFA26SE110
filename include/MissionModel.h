#pragma once
#include <ArduinoJson.h>
#include "Config.h"

enum class MissionState {
    Empty, Downloading, Ready, PreparingTello, GroundReady, TakingOff,
    Stabilizing, Executing, Collecting, Landing, Landed, ConnectingInternet,
    Syncing, Completed, Failed, Aborted, RecoveryRequired
};
enum class MissionAction { Move, Hold, Collect };
enum class TargetOutcome { Pending, Collected, Failed, Skipped };
struct MissionWaypoint {
    int id=0, sequenceNo=0, xCm=0, yCm=0, altitudeCm=0;
    uint32_t holdSeconds=0;
    MissionAction action=MissionAction::Move;
};
struct MissionTarget {
    int sensorNodeId=0, waypointId=0;
    char deviceCode[Config::MAX_DEVICE_CODE_BYTES+1]={};
};
struct Mission {
    int id=0, gatewayId=0, uavId=0;
    char name[65]={};
    size_t waypointCount=0, targetCount=0;
    MissionWaypoint waypoints[Config::MAX_MISSION_WAYPOINTS];
    MissionTarget targets[Config::MAX_MISSION_TARGETS];
};
struct MissionStateRecord {
    int missionId=0;
    uint32_t fingerprint=0;
    MissionState state=MissionState::Empty;
    size_t waypointIndex=0;
};
struct MissionResult {
    int missionId=0;
    uint32_t fingerprint=0;
    MissionState finalState=MissionState::Failed;
    size_t completedWaypoints=0;
    int failedWaypointSequence=0;
    bool uploaded=false, mockUpload=false;
    char failureReason[97]={};
    TargetOutcome targets[Config::MAX_MISSION_TARGETS]={};
};
namespace MissionCodec {
const char* stateName(MissionState state);
bool parseState(const char* text, MissionState& state);
bool mayBeAirborne(MissionState state);
bool decode(JsonObjectConst object,Mission& out,const char*& error);
void encode(const Mission& mission,JsonObject object);
uint32_t fingerprint(const Mission& mission);
}
