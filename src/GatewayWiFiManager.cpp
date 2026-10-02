#include "GatewayWiFiManager.h"

#include <WiFi.h>
#include "Config.h"
#include "Logger.h"
#include "Secrets.h"

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
    lastReconnect_ = millis() - Config::INTERNET_RECONNECT_INTERVAL_MS;
    return true;
}

bool GatewayWiFiManager::isStaConnected() const { return WiFi.status() == WL_CONNECTED; }
bool GatewayWiFiManager::isInternetConnected() const { return isStaConnected() && internetReachable_; }
IPAddress GatewayWiFiManager::getStaIp() const { return isStaConnected() ? WiFi.localIP() : IPAddress(); }

void GatewayWiFiManager::update() {
    if (!started_ || !Secrets::INTERNET_SSID[0]) return;
    const uint32_t now = millis();
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
        WiFi.begin(Secrets::INTERNET_SSID, Secrets::INTERNET_PASSWORD);
        Logger::info("STA connection attempt; AP remains active");
    }
    if (connected && now - lastProbe_ >= Config::INTERNET_PROBE_INTERVAL_MS) {
        lastProbe_ = now;
        WiFiClient probe;
        const bool reachable = probe.connect(IPAddress(Config::INTERNET_PROBE_IP),
            Config::INTERNET_PROBE_PORT, Config::INTERNET_PROBE_TIMEOUT_MS);
        probe.stop();
        if (reachable != internetReachable_) Logger::info(reachable ? "Internet probe reachable" : "Internet probe unavailable");
        internetReachable_ = reachable;
    }
}
