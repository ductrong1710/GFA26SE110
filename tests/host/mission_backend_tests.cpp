#include "MissionBackendClient.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=30000;TestSerial Serial;
bool networkReady=false;
bool GatewayWiFiManager::isInternetNetworkReady() const { return networkReady && target_==StaTarget::Internet; }
void GatewayWiFiManager::requestStaTarget(StaTarget t) {target_=t;}
class FakeTransport:public MissionTransport {
public:
 int requests=0;bool ok=true, httpError=false;int status=200;std::string path,method;
 const char* payload=R"({"success":true,"data":{"id":15,"name":"demo","gatewayId":1,"uavId":2,"status":"SCHEDULED","waypoints":[{"id":1,"sequenceNo":1,"localX":0.2,"localY":0,"altitudeM":0.35,"actionType":"MOVE","plannedHoldSeconds":0}],"targets":[]}})";
 bool configured() const override{return true;}
 bool request(const char* p,const char* m,const JsonDocument*,JsonDocument& response,int& code)override{
  ++requests;path=p;method=m;code=status;if(!ok)return false;return !deserializeJson(response,payload) && !httpError;
 }
};
int main(){
 GatewayWiFiManager wifi;FakeTransport transport;MissionBackendClient client(wifi,transport,1,false);Mission mission;
 assert(client.update(true,mission)==MissionPull::Idle);assert(!transport.requests);
 wifi.requestStaTarget(StaTarget::Internet);networkReady=true;
 assert(client.update(true,mission)==MissionPull::Downloaded);assert(mission.id==15);
 assert(client.update(true,mission)==MissionPull::Idle);
 client.requestPull();transport.payload=R"({"success":true,"data":null})";
 assert(client.update(true,mission)==MissionPull::NoMission);assert(mission.id==15);
 client.requestPull();transport.payload=R"({"success":false,"data":null})";
 assert(client.update(true,mission)==MissionPull::Error);
 transport.payload=R"({"success":true,"data":{"id":15,"status":"SCHEDULED"}})";
 assert(client.acknowledgeDownloaded(mission));assert(transport.path=="/device/gateways/1/missions/15/downloaded");
 transport.payload=R"({"success":true,"data":{"id":99,"status":"RUNNING"}})";
 assert(!client.startMission(mission));
 transport.payload=R"({"success":true,"data":{"id":15,"status":"RUNNING"}})";
 assert(client.startMission(mission));assert(transport.path=="/device/gateways/1/missions/15/start");
 wifi.requestStaTarget(StaTarget::Tello);int previous=transport.requests;
 assert(!client.startMission(mission));assert(transport.requests==previous);
 wifi.requestStaTarget(StaTarget::Internet);MissionResult result;result.missionId=15;result.fingerprint=7;result.finalState=MissionState::Completed;
 transport.payload=R"({"success":true,"data":{"id":15,"status":"COMPLETED"}})";
 assert(client.uploadResult(mission,result));assert(transport.path=="/device/gateways/1/missions/15/complete");
 transport.payload=R"({"success":true,"data":{"id":99,"status":"COMPLETED"}})";
 assert(!client.uploadResult(mission,result));
 client.beginLaunchSession();
 transport.payload=R"({"success":true,"data":{"state":"AWAITING_CONFIRMATION"}})";
 assert(client.pollLaunch(mission)==RemoteLaunch::Waiting);
 int beforePoll=transport.requests;
 assert(client.pollLaunch(mission)==RemoteLaunch::Waiting);assert(transport.requests==beforePoll);
 testMillis+=5000;
 transport.payload=R"({"success":true,"data":{"state":"READY_TO_START"}})";
 assert(client.pollLaunch(mission)==RemoteLaunch::Due);
 transport.payload=R"({"success":true,"data":{"state":"CONSUMED"}})";
 assert(!client.consumeLaunch(mission)); // A duplicate acknowledgement never authorizes flight.
 transport.payload=R"({"success":true,"data":{"state":"START_ACCEPTED"}})";
 assert(client.consumeLaunch(mission));
 transport.ok=false;assert(!client.consumeLaunch(mission));transport.ok=true;
 testMillis+=5000;transport.status=409;
 transport.payload=R"({"success":false,"message":"PLAN_CHANGED_DOWNLOAD_AGAIN"})";
 assert(client.pollLaunch(mission)==RemoteLaunch::Reload);transport.status=200;
 testMillis+=5000;transport.status=409;transport.httpError=true;
 assert(client.pollLaunch(mission)==RemoteLaunch::Reload); // Real transport preserves an HTTP error body but returns false.
 transport.status=200;transport.httpError=false;
 MissionBackendClient mock(wifi,transport,1,true);assert(mock.update(true,mission)==MissionPull::Downloaded);
 std::cout<<"Mission envelope, no mission, lifecycle identity and network gating passed\n";
}
