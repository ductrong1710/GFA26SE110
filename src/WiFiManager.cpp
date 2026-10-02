#include "WiFiManager.h"
#include "Config.h"
#include "Credentials.h"
#include "Logger.h"
void WiFiManager::begin() {
    WiFi.persistent(false); // Do not rewrite SDK credentials on each attempt.
    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(false);
    WiFi.setSleepMode(WIFI_NONE_SLEEP);
    WiFi.disconnect();
    const size_t length = strlen(Secrets::GATEWAY_WIFI_PASSWORD);
    enabled_ = length >= 8 && length <= 63;
    if (!enabled_) Logger::log("WARN","Configure Secrets.h gateway Wi-Fi password; sampling remains active");
}
void WiFiManager::update() {
    if (!enabled_) return;
    const uint32_t now = millis();
    if (connected()) {
        if (!wasConnected_ || WiFi.localIP() != lastIp_) {
            ++generation_; lastIp_ = WiFi.localIP();
            Logger::log("WIFI","Connected to UAV_GATEWAY");
            Serial.printf("[WIFI] IP: %s\n",lastIp_.toString().c_str());
        }
        wasConnected_ = true; connecting_ = false; return;
    }
    if (wasConnected_) { wasConnected_ = false; retry_.reset(); Logger::log("WARN","Gateway disconnected; sampling continues offline"); }
    if (connecting_) {
        if (uint32_t(now - attemptStarted_) < Config::WIFI_CONNECT_TIMEOUT_MS) return;
        WiFi.disconnect(); connecting_ = false;
        Logger::log("WARN","Gateway unavailable");
    }
    if (retry_.due(now,Config::WIFI_RECONNECT_INTERVAL_MS)) {
        retry_.mark(now); attemptStarted_ = now; connecting_ = true;
        Logger::log("WIFI","Searching for UAV_GATEWAY");
        WiFi.begin(Config::GATEWAY_SSID,Secrets::GATEWAY_WIFI_PASSWORD);
    }
}
