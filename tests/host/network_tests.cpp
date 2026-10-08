#include "GatewayWiFiManager.h"
#include "TimeManager.h"
#include "Config.h"
#include "WiFi.h"
#include "Secrets.h"
#include "Logger.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=1000;
TestSerial Serial;
FakeWiFi WiFi;
unsigned ntpStarts=0,ntpStops=0,probes=0;
namespace Logger { void info(const char*) {} void error(const char*) {} }
int main() {
    GatewayWiFiManager wifi; TimeManager time;
    assert(wifi.begin()); assert(WiFi.modeValue==WIFI_AP_STA && WiFi.apStarts==1);
    assert(!wifi.isBackendNetworkReady());
    testMillis+=100; wifi.update(); assert(WiFi.attempts.back()==Secrets::INTERNET_SSID);
    WiFi.link=WL_CONNECTED; WiFi.ssid=Secrets::INTERNET_SSID; wifi.update();
    assert(wifi.isBackendNetworkReady()); time.update(wifi.isInternetNetworkReady()); assert(ntpStarts==1);
    const unsigned previousProbes=probes;
    wifi.requestStaTarget(StaTarget::Tello);
    assert(ntpStops>=1); assert(!wifi.isBackendNetworkReady());
    // Old connection remains visible until the asynchronous disconnect completes.
    wifi.update(); assert(WiFi.attempts.size()==1);
    WiFi.link=0; testMillis+=100; wifi.update(); assert(WiFi.attempts.back()==Secrets::TELLO_SSID);
    WiFi.link=WL_CONNECTED; WiFi.ssid=Secrets::TELLO_SSID; wifi.update();
    assert(wifi.isTelloConnected()); assert(!wifi.isInternetConnected()); assert(!wifi.isBackendNetworkReady());
    time.update(wifi.isInternetNetworkReady()); assert(ntpStarts==1 && ntpStops>=2);
    testMillis+=60000; wifi.update(); time.update(wifi.isInternetNetworkReady());
    assert(probes==previousProbes && ntpStarts==1);
    WiFi.link=0; wifi.update(); testMillis+=Config::INTERNET_RECONNECT_INTERVAL_MS; wifi.update();
    assert(WiFi.attempts.back()==Secrets::TELLO_SSID);
    wifi.requestStaTarget(StaTarget::None); testMillis+=100; wifi.update();
    assert(!wifi.isStaConnected()); assert(WiFi.apStarts==1 && WiFi.modeValue==WIFI_AP_STA);
    wifi.requestStaTarget(StaTarget::Internet); testMillis+=100; wifi.update();
    assert(WiFi.attempts.back()==Secrets::INTERNET_SSID);
    WiFi.link=WL_CONNECTED; WiFi.ssid=Secrets::TELLO_SSID;
    assert(!wifi.isBackendNetworkReady()); // Matching role alone is insufficient: SSID must match.
    WiFi.ssid=Secrets::INTERNET_SSID; wifi.update(); time.update(wifi.isInternetNetworkReady());
    assert(wifi.isBackendNetworkReady() && ntpStarts==2);
    assert(time.isTimeSynced()); // Host clock used; stopping SNTP did not reset time.
    // Boot recovery selects Tello from the outset, never briefly Internet.
    WiFi.link=0;WiFi.attempts.clear();GatewayWiFiManager recovery;
    assert(recovery.begin(StaTarget::Tello));testMillis+=100;recovery.update();
    assert(WiFi.attempts.size()==1 && WiFi.attempts[0]==Secrets::TELLO_SSID);
    assert(!recovery.isBackendNetworkReady());
    std::cout << "Production Wi-Fi role and NTP gating tests passed\n";
}
