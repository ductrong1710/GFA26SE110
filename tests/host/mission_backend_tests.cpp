#include "MissionBackendClient.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=30000;
TestSerial Serial;
bool networkReady=false;
bool GatewayWiFiManager::isInternetNetworkReady() const { return networkReady && target_==StaTarget::Internet; }
void GatewayWiFiManager::requestStaTarget(StaTarget t) {target_=t;}
class FakeTransport:public MissionTransport {
public:
    int requests=0;bool ok=true;int status=200;std::string path,method;
    const char* payload=R"({"id":15,"name":"demo","gatewayId":1,"uavId":2,"status":"SCHEDULED","waypoints":[{"id":1,"sequenceNo":1,"localX":0.2,"localY":0,"altitudeM":0.35,"actionType":"MOVE","plannedHoldSeconds":0}],"targets":[]})";
    bool configured() const override {return true;}
    bool request(const char* p,const char* m,const JsonDocument*,JsonDocument& response,int& code) override {
        ++requests;path=p;method=m;code=status;
        if(!ok) return false;
        return !deserializeJson(response,payload);
    }
};
int main() {
    GatewayWiFiManager wifi;FakeTransport transport;
    MissionBackendClient client(wifi,transport,1,false);Mission mission;
    assert(client.update(true,mission)==MissionPull::Idle);assert(transport.requests==0);
    wifi.requestStaTarget(StaTarget::Internet);networkReady=true;
    assert(client.update(false,mission)==MissionPull::Idle);assert(transport.requests==0);
    assert(client.update(true,mission)==MissionPull::Downloaded);assert(mission.id==15);
    assert(transport.path=="/device/gateways/1/missions/next");
    assert(client.update(true,mission)==MissionPull::Idle);assert(transport.requests==1);
    client.requestPull();transport.payload="{broken";
    assert(client.update(true,mission)==MissionPull::Error);assert(mission.id==15);
    wifi.requestStaTarget(StaTarget::Tello);client.requestPull();
    assert(client.update(true,mission)==MissionPull::Idle);assert(transport.requests==2);
    MissionResult result;result.missionId=15;result.fingerprint=7;
    assert(!client.uploadResult(mission,result));assert(transport.requests==2);
    wifi.requestStaTarget(StaTarget::Internet);
    transport.payload=R"({"success":true,"missionId":15,"fingerprint":7})";
    assert(client.uploadResult(mission,result));assert(transport.method=="POST");
    assert(transport.path=="/device/gateways/1/missions/15/result");
    transport.payload=R"({"success":true,"missionId":99,"fingerprint":7})";
    assert(!client.uploadResult(mission,result));
    MissionBackendClient mock(wifi,transport,1,true);int previous=transport.requests;
    assert(mock.update(true,mission)==MissionPull::Downloaded);assert(mock.isMock());assert(transport.requests==previous);
    assert(mission.gatewayId==1);assert(mock.uploadResult(mission,result));assert(transport.requests==previous);
    std::cout<<"Mission network gating, polling, payload and acknowledgement tests passed\n";
}
