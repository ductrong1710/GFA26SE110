#pragma once
#include "WiFiManager.h"
#include "RetryTimer.h"
class GatewayClient {
public:
    explicit GatewayClient(WiFiManager& wifi) : wifi_(wifi) {}
    void update();
    bool registered() const { return registered_ && wifi_.connected(); }
private:
    WiFiManager& wifi_;
    RetryTimer retry_;
    uint32_t generation_ = 0;
    bool registered_ = false;
    bool registerNode();
};
