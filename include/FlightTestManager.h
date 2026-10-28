#pragma once
#include "MissionStorage.h"
#include "MissionBackendClient.h"
#include "FlightStateManager.h"

// Only a claimed fixed backend command can enter this executor. No local start API.
class FlightTestManager {
public:
    FlightTestManager(MissionFiles& files,MissionTransport& transport,GatewayWiFiManager& wifi,
        TelloController& tello,FlightStateManager& flight)
        :files_(files),transport_(transport),wifi_(wifi),tello_(tello),flight_(flight) {}
    bool begin(int gatewayId);
    void update(bool idle);
    bool locked() const {return phase_!=0 && phase_!=4;}
    int id() const {return id_;}
    const char* error() const {return error_;}
    void abort() { if(phase_==2) fail("OPERATOR_LAND_REQUEST"); }
    StaTarget initialStaTarget() const {return recovery_ ? StaTarget::Tello : StaTarget::Internet;}
private:
    MissionFiles& files_;MissionTransport& transport_;GatewayWiFiManager& wifi_;
    TelloController& tello_;FlightStateManager& flight_;
    int gatewayId_=0,id_=0,phase_=0,altitude_=0,lastHttpStatus_=0;
    bool takeoffAttempted_=false;
    bool ready_=false,recovery_=false,uavConnected_=false,landed_=false,holding_=false,landing_=false;
    bool attempted_=false;
    uint32_t attemptAt_=0,startedAt_=0,holdAt_=0,lastSampleAt_=0,sample_=0,heldMs_=0;
    uint32_t outOfBand_=0,overshoot_=0,lowBattery_=0;
    char error_[65]={};
    bool save();
    bool command(JsonVariantConst data,const char* status) const;
    bool request(const char* suffix,const char* method,const JsonDocument* body,JsonDocument& response);
    void fail(const char* error);
    void finish();
    void recover();
};
