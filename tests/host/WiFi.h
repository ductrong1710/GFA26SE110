#pragma once
#include "IPAddress.h"
#include <vector>
constexpr int WIFI_AP_STA=3, WL_CONNECTED=3;
struct FakeWiFi {
    int link=0, modeValue=0;
    unsigned apStarts=0, disconnects=0;
    std::string ssid;
    std::vector<std::string> attempts;
    void persistent(bool) {}
    bool mode(int n) { modeValue=n; return true; }
    bool softAPConfig(IPAddress,IPAddress,IPAddress) { return true; }
    bool softAP(const char*,const char*) { ++apStarts; return true; }
    IPAddress softAPIP() { static const uint8_t p[]={192,168,4,1}; return IPAddress(p); }
    void setAutoReconnect(bool) {}
    bool disconnect(bool radioOff,bool erase) { if(radioOff || erase) abort(); ++disconnects; return true; }
    int status() { return link; }
    String SSID() { return String(ssid); }
    IPAddress localIP() { static const uint8_t p[]={192,168,10,2}; return link==WL_CONNECTED ? IPAddress(p) : IPAddress(); }
    void begin(const char* name,const char*) { attempts.push_back(name); }
};
extern FakeWiFi WiFi;
extern unsigned probes;
struct WiFiClient {
    bool connect(IPAddress,uint16_t,int) { ++probes; return true; }
    void stop() {}
};
