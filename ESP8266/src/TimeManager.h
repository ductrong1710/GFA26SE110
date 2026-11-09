#pragma once
#include <stdint.h>
class TimeManager {
public:
    bool synchronize(uint64_t unixTime, uint32_t now);
    bool synced() const { return synced_; }
    void update(uint32_t now);
    uint64_t unixTime() const { return synced_ ? reference_ : 0; }
private:
    uint64_t reference_ = 0;
    uint32_t lastMillis_ = 0;
    uint16_t remainderMs_ = 0;
    bool synced_ = false;
};
