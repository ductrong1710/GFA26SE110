#pragma once
#include <IPAddress.h>
#include <stdint.h>

class GatewayWiFiManager {
public:
    bool begin();
    void update();
    bool isStaConnected() const;
    bool isInternetConnected() const;
    IPAddress getStaIp() const;
private:
    bool started_ = false, wasConnected_ = false, internetReachable_ = false;
    uint32_t lastReconnect_ = 0, lastProbe_ = 0;
};
