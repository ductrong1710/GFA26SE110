#pragma once
#include <stdint.h>
class RetryTimer {
public:
    bool due(uint32_t now, uint32_t interval) const { return !started_ || uint32_t(now - last_) >= interval; }
    void mark(uint32_t now) { last_ = now; started_ = true; }
    void reset() { started_ = false; }
private:
    uint32_t last_ = 0;
    bool started_ = false;
};
