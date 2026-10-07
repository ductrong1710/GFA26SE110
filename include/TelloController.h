#pragma once
#include <Arduino.h>
#include <WiFiUdp.h>
#include "Config.h"
#include "GatewayWiFiManager.h"
#include "TelloTelemetry.h"

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
    void cancelRc();
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
private:
    GatewayWiFiManager& wifi_;
    WiFiUDP commands_, states_;
    TelloTelemetry telemetry_;
    bool started_ = false, sdkReady_ = false, uncertain_ = false;
    bool cooling_ = false;
    uint32_t sentAt_ = 0, finishedAt_ = 0, cooldownMs_ = 0, batteryAt_ = 0;
    unsigned retries_ = 0;
    int queriedBattery_ = -1;
    TelloCommand kind_ = TelloCommand::None;
    TelloResult result_ = TelloResult::None;
    char command_[48] = {}, response_[96] = {};
    int rc_[4] = {};
    bool rcQueued_ = false, rcMoving_ = false, rcSent_ = false;
    uint32_t rcAt_ = 0, lastRcSentAt_ = 0;
    bool send(TelloCommand kind, const char* text);
    bool transmit(const char* text);
    void finish(TelloResult result, const char* response);
};
