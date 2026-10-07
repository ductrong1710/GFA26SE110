#include "TimeManager.h"
#include "Config.h"
#include "Logger.h"
#include <Arduino.h>
#include <esp_timer.h>
#include <time.h>
#include <esp_sntp.h>

bool TimeManager::isTimeSynced() const {
    const int64_t now = static_cast<int64_t>(time(nullptr));
    return now >= 1577836800LL && now <= 4102444800LL;
}
uint64_t TimeManager::unixTime() const { return isTimeSynced() ? uint64_t(time(nullptr)) : 0; }
uint64_t TimeManager::collectionTime() const {
    return isTimeSynced() ? unixTime() : uint64_t(esp_timer_get_time()) / 1000000;
}
void TimeManager::update(bool staConnected) {
    if (!staConnected && ntpStarted_) {
        sntp_stop();
        ntpStarted_ = false;
    }
    if (staConnected && !ntpStarted_) {
        configTime(0, 0, Config::NTP_SERVER);
        ntpStarted_ = true;
        Logger::info("NTP started asynchronously");
    }
    const bool synced = isTimeSynced();
    if (synced && !wasSynced_) Logger::info("Time synchronized");
    wasSynced_ = synced;
}
