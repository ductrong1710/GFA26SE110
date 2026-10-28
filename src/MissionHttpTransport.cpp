#include "RuntimeConfig.h"
#include "MissionBackendClient.h"
#include "BoundedHttp.h"
#include "Secrets.h"
bool MissionHttpTransport::configured() const {return RuntimeConfig::instance().values().backendBaseUrl.c_str()[0] && RuntimeConfig::instance().values().apiKey.c_str()[0];}
bool MissionHttpTransport::request(const char* path,const char* method,const JsonDocument* body,JsonDocument& response,int& status) {
    if(!configured()) return false;
    String base=RuntimeConfig::instance().values().backendBaseUrl.c_str();
    while(base.endsWith("/")) base.remove(base.length()-1);
    String payload;
    if(body && (body->overflowed() || serializeJson(*body,payload)!=measureJson(*body))) return false;
    return BoundedHttp::request(base+path,method,payload,"X-Gateway-Code",RuntimeConfig::instance().values().gatewayCode.c_str(),
        "X-Api-Key",RuntimeConfig::instance().values().apiKey.c_str(),response,status,true);
}
