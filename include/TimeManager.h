#pragma once
#include <stdint.h>
class TimeManager {
public:
    void update(bool staConnected);
    bool isTimeSynced() const;
    uint64_t unixTime() const;
    uint64_t collectionTime() const;
private:
    bool ntpStarted_ = false, wasSynced_ = false;
};
