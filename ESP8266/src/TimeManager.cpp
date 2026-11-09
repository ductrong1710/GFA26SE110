#include "TimeManager.h"
bool TimeManager::synchronize(uint64_t unixTime, uint32_t now) {
    if (unixTime < 1577836800ULL || unixTime > 4102444800ULL) return false;
    reference_ = unixTime; lastMillis_ = now; remainderMs_ = 0; synced_ = true; return true;
}
void TimeManager::update(uint32_t now) {
    const uint32_t delta = uint32_t(now - lastMillis_);
    lastMillis_ = now;
    if (!synced_) return;
    const uint64_t elapsed = uint64_t(delta) + remainderMs_;
    reference_ += elapsed / 1000;
    remainderMs_ = elapsed % 1000;
    if (reference_ > 4102444800ULL) synced_ = false;
}
