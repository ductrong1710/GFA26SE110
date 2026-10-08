#pragma once
#include "TelloController.h"
enum class FlightState {
    IdleInternet, ConnectingTello, EnteringSdk, QueryingBattery, LandedReady,
    TakingOff, Flying, Landing, LandedConfirmed, SwitchingToInternet,
    InternetConnecting, Syncing, SyncComplete, SyncBlocked, Error
};
class FlightStateManager {
public:
    FlightStateManager(GatewayWiFiManager& wifi, TelloController& tello) : wifi_(wifi), tello_(tello) {}
    bool prepare();
    bool takeoff();
    bool land();
    bool disconnect();
    bool move(const char* direction, int cm);
    bool rotate(const char* direction, int degrees);
    bool rc(int a,int b,int c,int d);
    void update(size_t pendingRecords = 0, bool storageReady = true, bool backendConfigured = true);
    FlightState state() const { return state_; }
    const char* stateName() const;
    const char* error() const { return error_; }
    const char* landingEvidence() const { return landingEvidence_; }
    bool mayBeAirborne() const { return airbornePossible_; }
    void requireTelemetryLanding(bool required) { strictLanding_=required; }
    void restoreRecovery();
    bool cancelPreparation();
private:
    GatewayWiFiManager& wifi_;
    TelloController& tello_;
    FlightState state_ = FlightState::IdleInternet;
    const char* error_ = "";
    uint32_t stateAt_ = 0;
    bool airbornePossible_ = false, batteryRequested_ = false;
    bool landSent_ = false, landAcked_ = false;
    uint32_t landAckAt_ = 0;
    uint32_t groundSince_ = 0, groundLastAt_ = 0, observedSample_ = 0;
    unsigned groundSamples_ = 0;
    int groundHeight_ = -1;
    bool contradictoryLandingTelemetry_ = false, recoveryStarted_ = false;
    bool flightUncertain_ = false;
    bool strictLanding_ = false;
    uint32_t recoveryAt_ = 0;
    const char* landingEvidence_ = "NONE";
    void observeGround();
    bool stableGround() const;
    bool groundedTelemetry() const;
    void transition(FlightState state);
    bool reject(const char* error) { error_=error; return false; }
    void fail(const char* error) { if(airbornePossible_) flightUncertain_=true; error_=error; transition(FlightState::Error); }
};
