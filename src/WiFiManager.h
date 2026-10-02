#pragma once
#include <ESP8266WiFi.h>
#include "RetryTimer.h"
class WiFiManager {
public:
    void begin();
    void update();
    bool connected() const { return WiFi.status() == WL_CONNECTED; }
    uint32_t generation() const { return generation_; }
private:
    RetryTimer retry_;
    uint32_t attemptStarted_ = 0, generation_ = 0;
    bool enabled_ = false, connecting_ = false, wasConnected_ = false;
    IPAddress lastIp_;
};
