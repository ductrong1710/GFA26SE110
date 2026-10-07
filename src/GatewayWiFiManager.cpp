#include "GatewayWiFiManager.h"

#include <WiFi.h>
#include "Config.h"
#include "Logger.h"
#include "Secrets.h"
#include <esp_sntp.h>

static_assert(sizeof(Secrets::GATEWAY_AP_PASSWORD) >= 9 &&
              sizeof(Secrets::GATEWAY_AP_PASSWORD) <= 64,
              "AP password must contain 8-63 characters");

bool GatewayWiFiManager::begin() {
    Logger::info("Starting Wi-Fi");
    WiFi.persistent(false);

    // AP and STA share the radio; retries must never turn off the AP interface.
    if (!WiFi.mode(WIFI_AP_STA)) {
        Logger::error("AP initialization failed: could not enable WIFI_AP_STA");
        return false;
    }

    const IPAddress apIp(Config::GATEWAY_AP_IP);
    const IPAddress subnet(Config::GATEWAY_AP_SUBNET);
    if (!WiFi.softAPConfig(apIp, apIp, subnet)) {
        Logger::error("AP initialization failed: could not configure AP IP");
        return false;
    }

    if (!WiFi.softAP(Config::GATEWAY_AP_SSID, Secrets::GATEWAY_AP_PASSWORD)) {
        Logger::error("AP initialization failed: could not start access point");
        return false;
    }

    Logger::info("AP started");
    Logger::info((String("SSID: ") + Config::GATEWAY_AP_SSID).c_str());
    Logger::info((String("AP IP: ") + WiFi.softAPIP().toString()).c_str());
    started_ = true;
    WiFi.setAutoReconnect(false);
    requestStaTarget(StaTarget::Internet);
    return true;
}

const char* GatewayWiFiManager::targetName(StaTarget target) {
    switch (target) {
    case StaTarget::Tello: return "TELLO";
    case StaTarget::Internet: return "INTERNET";
    default: return "NONE";
    }
}
const char* GatewayWiFiManager::targetSsid() const {
    return target_ == StaTarget::Tello ? Secrets::TELLO_SSID :
        target_ == StaTarget::Internet ? Secrets::INTERNET_SSID : "";
}
bool GatewayWiFiManager::targetConfigured(StaTarget target) const {
    return target == StaTarget::Tello ? Secrets::TELLO_SSID[0] :
        target == StaTarget::Internet ? Secrets::INTERNET_SSID[0] : true;
}
void GatewayWiFiManager::requestStaTarget(StaTarget target) {
    if (target_ == target) return;
    // Stop background SNTP before changing the network; system time is retained.
    if (target_ == StaTarget::Internet) sntp_stop();
    target_ = target;
    internetReachable_ = wasConnected_ = false;
    disconnecting_ = true;
    transitionAt_ = millis();
    WiFi.disconnect(false, false);
    Serial.printf("[WIFI] STA target=%s; SoftAP remains enabled\n", targetName(target));
}
bool GatewayWiFiManager::isStaConnected() const {
    return !disconnecting_ && target_ != StaTarget::None && targetSsid()[0] &&
        WiFi.status() == WL_CONNECTED && WiFi.SSID() == targetSsid() &&
        WiFi.localIP() != IPAddress();
}
bool GatewayWiFiManager::isTelloConnected() const { return target_ == StaTarget::Tello && isStaConnected(); }
bool GatewayWiFiManager::isInternetStaConnected() const { return target_ == StaTarget::Internet && isStaConnected(); }
bool GatewayWiFiManager::isInternetNetworkReady() const { return isInternetStaConnected(); }
bool GatewayWiFiManager::isInternetConnected() const { return isInternetNetworkReady() && internetReachable_; }
IPAddress GatewayWiFiManager::getStaIp() const { return isStaConnected() ? WiFi.localIP() : IPAddress(); }

void GatewayWiFiManager::update() {
    if (!started_) return;
    const uint32_t now = millis();
    if (disconnecting_) {
        // Wait for disconnect to be observable, not merely for disconnect() to return.
        if (WiFi.status() == WL_CONNECTED || now - transitionAt_ < 100) return;
        disconnecting_ = false;
        lastReconnect_ = now - Config::INTERNET_RECONNECT_INTERVAL_MS;
    }
    if (target_ == StaTarget::None || !targetSsid()[0]) return;
    const bool connected = isStaConnected();
    if (connected != wasConnected_) {
        Logger::info(connected ? "STA connected" : "STA disconnected; AP remains active");
        wasConnected_ = connected;
        if (connected) lastProbe_ = now - Config::INTERNET_PROBE_INTERVAL_MS;
        else { internetReachable_ = false; lastReconnect_ = now; }
    }
    if (!connected && now - lastReconnect_ >= Config::INTERNET_RECONNECT_INTERVAL_MS) {
        lastReconnect_ = now;
        WiFi.disconnect(false, false);
        WiFi.begin(targetSsid(), target_ == StaTarget::Tello ? Secrets::TELLO_PASSWORD : Secrets::INTERNET_PASSWORD);
        Logger::info("STA connection attempt; AP remains active");
    }
    if (isInternetNetworkReady() && now - lastProbe_ >= Config::INTERNET_PROBE_INTERVAL_MS) {
        lastProbe_ = now;
        WiFiClient probe;
        const bool reachable = probe.connect(IPAddress(Config::INTERNET_PROBE_IP),
            Config::INTERNET_PROBE_PORT, Config::INTERNET_PROBE_TIMEOUT_MS);
        probe.stop();
        if (reachable != internetReachable_) Logger::info(reachable ? "Internet probe reachable" : "Internet probe unavailable");
        internetReachable_ = reachable;
    }
}
