#pragma once
#include <Arduino.h>
#include <WiFiUdp.h>
#include "Config.h"
#include "GatewayWiFiManager.h"
#include "TelloTelemetry.h"
#include "FlightSafetyController.h"

enum class TelloCommand { None, Sdk, Battery, Takeoff, Land, Move, Rotate };
enum class TelloResult { None, Pending, Ok, Rejected, Timeout, Disconnected };
class TelloController {
public:
    explicit TelloController(GatewayWiFiManager& wifi) : wifi_(wifi) {}
    bool begin();
    void stop();
    void update();
    bool requestSdkMode();
    bool queryBattery();
    bool takeoff();
    bool land();
    bool move(const char* direction, int cm);
    bool rotate(const char* direction, int degrees);
    bool sendRc(int leftRight, int forwardBack, int upDown, int yaw);
    bool sendRcPulse(int vertical, uint32_t durationMs);
    void cancelRc();
    bool rcBusy() const { return rcQueued_ || rcMoving_ || rcStopPending_; }
    bool rcFailed() const { return rcFailure_; }
    void clearRcFailure() { rcFailure_=false; }
    static bool validMove(const char* direction, int cm);
    static bool validRotation(const char* direction, int degrees);
    static bool validRc(int a, int b, int c, int d);
    bool isSdkReady() const { return sdkReady_ && wifi_.isTelloConnected(); }
    bool hasFreshTelemetry() const;
    int batteryPercent() const;
    const TelloTelemetry& telemetry() const { return telemetry_; }
    const char* lastResponse() const { return response_; }
    TelloResult lastResult() const { return result_; }
    TelloCommand lastCommand() const { return kind_; }
    bool commandPending() const { return result_ == TelloResult::Pending; }
    bool canSend() const;
    bool responseUncertain() const { return uncertain_; }
    const FlightSafetyController& safety() const { return safety_; }
    void refreshSafety() { safety_.update(telemetry_,hasFreshTelemetry()); }
private:
    friend class FlightTestManager;
    uint32_t testOutOfBand_=0,testOvershoot_=0,testLowBattery_=0;
    bool setFlightTestLimits(bool enabled) {
#if defined(GATEWAY_FLIGHT_TEST_PROFILE) && GATEWAY_FLIGHT_TEST_PROFILE
        safety_=FlightSafetyController(enabled ? AltitudeLimits(105,100,20) : AltitudeLimits());
        refreshSafety();return true;
#else
        return !enabled;
#endif
    }
    GatewayWiFiManager& wifi_;
    WiFiUDP commands_, states_;
    TelloTelemetry telemetry_;
    FlightSafetyController safety_;
    bool started_ = false, sdkReady_ = false, uncertain_ = false;
    bool cooling_ = false;
    uint32_t sentAt_ = 0, finishedAt_ = 0, cooldownMs_ = 0, batteryAt_ = 0;
    unsigned retries_ = 0;
    uint32_t commandStartedAt_=0, replyPackets_=0, droppedReplies_=0, statePackets_=0;
    uint32_t telemetryPackets_=0, telemetryParsed_=0, telemetryRejected_=0, telemetryLogAt_=0;
    int queriedBattery_ = -1;
    TelloCommand kind_ = TelloCommand::None;
    TelloResult result_ = TelloResult::None;
    char command_[48] = {}, response_[96] = {};
    int rc_[4] = {};
    bool rcQueued_ = false, rcMoving_ = false, rcSent_ = false;
    uint32_t rcAt_ = 0, lastRcSentAt_ = 0;
    bool rcPulse_=false, rcStopPending_=false, rcFailure_=false, rcAttempted_=false;
    uint32_t rcStartedAt_=0, rcDurationMs_=0, rcAttemptAt_=0;
    void readReplies();
    void readTelemetry();
    void updateCommand();
    void updateRc();
    bool send(TelloCommand kind, const char* text);
    bool transmit(const char* text);
    void finish(TelloResult result, const char* response);
};
