#pragma once
#include "MissionStorage.h"
#include "MissionBackendClient.h"
#include "MissionRoutePlanner.h"
#include "FlightStateManager.h"
#include "MissionCollection.h"
class MissionManager {
public:
    MissionManager(MissionStorage& storage,MissionBackendClient& backend,GatewayWiFiManager& wifi,
        TelloController& tello,FlightStateManager& flight,int gatewayId=Config::BACKEND_GATEWAY_ID)
        :storage_(storage),backend_(backend),wifi_(wifi),tello_(tello),flight_(flight),gatewayId_(gatewayId) {}
    bool begin();
    void update(bool sensorStorageHealthy,size_t pendingRecords,bool timeSynced);
    bool requestPull();
    bool start();
    bool abort();
    bool landNow();
    bool ownsFlight() const;
    bool hasActive() const {return hasActive_;}
    const Mission& active() const {return mission_;}
    const MissionRoutePlan& plan() const {return plan_;}
    MissionState state() const {return record_.state;}
    const char* stateName() const {return MissionCodec::stateName(state());}
    size_t waypointIndex() const {return record_.waypointIndex;}
    const char* error() const {return error_;}
    StaTarget initialStaTarget() const {return state()==MissionState::RecoveryRequired ? StaTarget::Tello : StaTarget::Internet;}
    void setCollection(MissionCollection& collection){collection_=&collection;}
    const MissionResult& result() const {return result_;}
private:
    MissionStorage& storage_;MissionBackendClient& backend_;GatewayWiFiManager& wifi_;
    TelloController& tello_;FlightStateManager& flight_;int gatewayId_;
    MissionCollection* collection_=nullptr;
    Mission mission_;MissionRoutePlan plan_;MissionRoutePlanner planner_;
    MissionStateRecord record_;MissionResult result_;
    bool hasActive_=false,sensorHealthy_=false,stopRequested_=false,waitingMove_=false;
    bool altitudeDownPending_=false,altitudePulse_=false,altitudeWaiting_=false,altitudeStarted_=false;
    bool arrived_=false,targetRequested_=false;
    bool landRequested_=false,recoveryLandRequested_=false;
    size_t stepIndex_=0,targetIndex_=0;
    uint32_t stateAt_=0,altitudeAt_=0,altitudeSample_=0,altitudeWaitAt_=0,stableAt_=0;
    uint32_t holdAt_=0,targetAt_=0,keepaliveAt_=0,lastResultAttempt_=0;
    uint32_t resultRetryMs_=Config::BACKEND_RETRY_BASE_MS;
    unsigned stableSamples_=0;
    char error_[97]={};
    bool reject(const char* error);
    bool transition(MissionState state);
    void pull();
    void failAndLand(const char* reason);
    void updateLanding();
    void finishLanded();
    void updateRoute();
    void updateCollection();
    void beginAltitude();
    int adjustAltitude(int targetCm);
    void keepAlive();
    bool saveProgress();
    void updateSync(size_t pendingRecords,bool timeSynced);
};
