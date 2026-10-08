#include "MissionBackendClient.h"
#include "BoundedHttp.h"
#include "Secrets.h"
bool MissionHttpTransport::configured() const {return Secrets::BACKEND_BASE_URL[0] && Secrets::BACKEND_API_KEY[0];}
bool MissionHttpTransport::request(const char* path,const char* method,const JsonDocument* body,JsonDocument& response,int& status) {
    if(!configured()) return false;
    String base=Secrets::BACKEND_BASE_URL;
    while(base.endsWith("/")) base.remove(base.length()-1);
    String payload;
    if(body && (body->overflowed() || serializeJson(*body,payload)!=measureJson(*body))) return false;
    return BoundedHttp::request(base+path,method,payload,"X-Gateway-Code",Config::GATEWAY_CODE,
        "X-Api-Key",Secrets::BACKEND_API_KEY,response,status,true);
}
