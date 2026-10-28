#include "RuntimeConfig.h"
#include "Config.h"
#include <Secrets.h>
#include <Preferences.h>
#include <ArduinoJson.h>
#include <string>
#include <cstdlib>

namespace {
GatewaySettings defaults() {
    GatewaySettings s;
    s.internetSsid=Secrets::INTERNET_SSID;s.internetPassword=Secrets::INTERNET_PASSWORD;
    s.backendBaseUrl=Secrets::BACKEND_BASE_URL;s.apiKey=Secrets::BACKEND_API_KEY;
    s.gatewayCode=Config::GATEWAY_CODE;s.gatewayId=Config::BACKEND_GATEWAY_ID;
    return s;
}
bool control(const String& s) {
    for(size_t i=0;i<s.length();++i) {
        unsigned char c=s.c_str()[i];if(c<32 || c==127) return true;
    }
    return false;
}
bool validUrl(const String& input) {
    std::string s(input.c_str());
    if(s.size()>256 || s.compare(0,7,"http://")!=0 || s.find_first_of(" \t\r\n?#@\\")!=std::string::npos) return false;
    size_t slash=s.find('/',7);
    if(slash==std::string::npos || s.substr(slash)!="/api") return false;
    std::string host=s.substr(7,slash-7);
    size_t colon=host.find(':');
    if(colon!=std::string::npos) {
        std::string port=host.substr(colon+1);
        if(port.empty() || port.size()>5 || port.find_first_not_of("0123456789")!=std::string::npos) return false;
        int number=std::atoi(port.c_str());if(number<1 || number>65535) return false;
        host=host.substr(0,colon);
    }
    if(host.empty() || host.find_first_not_of("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-")!=std::string::npos) return false;
    for(char& c:host) if(c>='A' && c<='Z') c+=32;
    return host!="localhost" && host!="0.0.0.0" && host.compare(0,4,"127.")!=0;
}
}
RuntimeConfig::RuntimeConfig():values_(defaults()) {}
RuntimeConfig& RuntimeConfig::instance() { static RuntimeConfig config;return config; }
const char* RuntimeConfig::validate(const GatewaySettings& s) {
    if(s.internetSsid.isEmpty() || s.internetSsid.length()>32 || control(s.internetSsid)) return "Wi-Fi SSID must contain 1-32 bytes.";
    if((!s.internetPassword.isEmpty() && (s.internetPassword.length()<8 || s.internetPassword.length()>63)) || control(s.internetPassword)) return "Wi-Fi password must be empty for an open network or contain 8-63 bytes.";
    if(control(s.backendBaseUrl) || !validUrl(s.backendBaseUrl)) return "Use http://LAN-host:port/api; HTTPS, localhost and URL credentials are not supported.";
    if(s.gatewayId<=0) return "Gateway ID must be a positive integer.";
    if(s.gatewayCode.isEmpty() || s.gatewayCode.length()>64 || control(s.gatewayCode)) return "Gateway Code must contain 1-64 bytes without control characters.";
    if(s.apiKey.length()<32 || s.apiKey.length()>256 || control(s.apiKey)) return "API Key must contain 32-256 bytes without control characters.";
    return nullptr;
}
bool RuntimeConfig::begin() {
    Preferences prefs;values_=GatewaySettings{};ready_=false;
    if(!prefs.begin("gateway-config",false)) return false;
    ready_=true;
    if(!prefs.isKey("settings")) {
        values_=defaults();prefs.end();
        Serial.printf("[CONFIG] source=FIRMWARE_DEFAULTS\n");
        if(validUrl(values_.backendBaseUrl))
            Serial.printf("[CONFIG] backend=%s gatewayId=%d\n",values_.backendBaseUrl.c_str(),values_.gatewayId);
        return true;
    }
    String encoded=prefs.getString("settings", "");prefs.end();
    if(encoded.length()>1536) return false;
    DynamicJsonDocument doc(2048);
    if(deserializeJson(doc,encoded.c_str()) || doc["version"]!=1) return false;
    GatewaySettings s;
    s.internetSsid=doc["ssid"]|"";s.internetPassword=doc["wifiPassword"]|"";
    s.backendBaseUrl=doc["backendUrl"]|"";s.gatewayCode=doc["gatewayCode"]|"";
    s.apiKey=doc["apiKey"]|"";s.gatewayId=doc["gatewayId"]|0;
    if(validate(s)) return false; // Never fall back to compiled secrets after a corrupt saved record.
    values_=s;
    Serial.printf("[CONFIG] source=SAVED_NVS backend=%s gatewayId=%d\n",
        values_.backendBaseUrl.c_str(),values_.gatewayId);
    return true;
}
bool RuntimeConfig::save(const GatewaySettings& s, bool adoptPending) {
    if(!ready_ || validate(s)) return false;
    if(adoptPending && !canAdoptPending()) return false;
    if(!identityChangeAllowed_ && !adoptPending && (s.gatewayId!=values_.gatewayId || s.gatewayCode!=values_.gatewayCode || s.backendBaseUrl!=values_.backendBaseUrl)) return false;
    DynamicJsonDocument doc(2048);
    doc["version"]=1;doc["ssid"]=s.internetSsid.c_str();doc["wifiPassword"]=s.internetPassword.c_str();
    doc["backendUrl"]=s.backendBaseUrl.c_str();doc["gatewayId"]=s.gatewayId;
    doc["gatewayCode"]=s.gatewayCode.c_str();doc["apiKey"]=s.apiKey.c_str();
    if(doc.overflowed()) return false;
    std::string encoded;serializeJson(doc,encoded);
    if(encoded.size()>1536) return false;
    Preferences prefs;if(!prefs.begin("gateway-config",false)) return false;
    bool ok=prefs.putString("settings",encoded.c_str())==encoded.size();
    if(ok) ok=prefs.getString("settings", "")==encoded.c_str();
    prefs.end();return ok;
}
