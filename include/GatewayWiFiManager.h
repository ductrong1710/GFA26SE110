#pragma once
#include <IPAddress.h>
#include <stdint.h>

enum class StaTarget { None, Tello, Internet };

class GatewayWiFiManager {
public:
    bool begin();
    void update();
    void requestStaTarget(StaTarget target);
    StaTarget staTarget() const { return target_; }
    static const char* targetName(StaTarget target);
    bool targetConfigured(StaTarget target) const;
    bool isTelloConnected() const;
    bool isInternetStaConnected() const;
    bool isInternetNetworkReady() const;
    bool isBackendNetworkReady() const { return isInternetNetworkReady(); }
    bool isStaConnected() const;
    bool isInternetConnected() const;
    IPAddress getStaIp() const;
private:
    StaTarget target_ = StaTarget::None;
    bool disconnecting_ = false;
    uint32_t transitionAt_ = 0;
    const char* targetSsid() const;
    bool started_ = false, wasConnected_ = false, internetReachable_ = false;
    uint32_t lastReconnect_ = 0, lastProbe_ = 0;
};
