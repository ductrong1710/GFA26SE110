#pragma once
#include <stdint.h>

class HeartbeatSchedule {
public:
    bool due(uint32_t now, bool safeAndConnected) const {
        return safeAndConnected && (!sent_ || uint32_t(now - last_) >= 30000U);
    }
    void attempted(uint32_t now) { sent_ = true; last_ = now; }
private:
    bool sent_ = false;
    uint32_t last_ = 0;
};
