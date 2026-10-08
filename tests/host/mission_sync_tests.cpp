#include "MissionManager.h"
#include "MissionMemoryFiles.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=30000;TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;std::vector<Packet> outgoing;
bool GatewayWiFiManager::targetConfigured(StaTarget)const{return true;}
bool GatewayWiFiManager::isTelloConnected()const{return target_==StaTarget::Tello;}
bool GatewayWiFiManager::isInternetNetworkReady()const{return target_==StaTarget::Internet;}
void GatewayWiFiManager::requestStaTarget(StaTarget t){target_=t;}
IPAddress GatewayWiFiManager::getStaIp()const{return IPAddress(Config::TELLO_IP);}
class ResultTransport:public MissionTransport {
public:
    unsigned uploads=0;bool fail=true;
    bool configured()const override{return true;}
    bool request(const char*,const char* method,const JsonDocument* body,JsonDocument& response,int& code)override {
        if(strcmp(method,"POST")){code=204;return true;}
        ++uploads;code=fail?503:200;if(fail)return false;
        response["success"]=true;response["missionId"]=(*body)["missionId"];
        response["fingerprint"]=(*body)["fingerprint"];return true;
    }
};
int main(){
    GatewayWiFiManager wifi;wifi.requestStaTarget(StaTarget::Internet);
    TelloController tello(wifi);FlightStateManager flight(wifi,tello);
    MissionMemoryFiles files;MissionStorage storage(files);assert(storage.begin());
    Mission m;m.id=15;m.gatewayId=1;m.uavId=2;strcpy(m.name,"finished");m.waypointCount=1;
    m.waypoints[0].id=1;m.waypoints[0].sequenceNo=1;m.waypoints[0].altitudeCm=35;
    assert(storage.saveActive(m));MissionStateRecord state;state.missionId=m.id;
    state.fingerprint=MissionCodec::fingerprint(m);state.state=MissionState::Syncing;assert(storage.saveState(state));
    MissionResult result;result.missionId=m.id;result.fingerprint=state.fingerprint;
    result.finalState=MissionState::Completed;result.completedWaypoints=1;assert(storage.saveResult(result));
    ResultTransport transport;MissionBackendClient backend(wifi,transport,1,false);
    MissionManager manager(storage,backend,wifi,tello,flight,1);assert(manager.begin());
    manager.update(true,2,true);assert(transport.uploads==0);assert(!manager.requestPull());
    manager.update(true,0,false);assert(transport.uploads==0);
    wifi.requestStaTarget(StaTarget::Tello);manager.update(true,0,true);assert(transport.uploads==0);
    wifi.requestStaTarget(StaTarget::Internet);manager.update(true,0,true);
    assert(transport.uploads==1 && manager.state()==MissionState::Syncing);
    assert(storage.loadResult(result) && !result.uploaded);
    manager.update(true,0,true);assert(transport.uploads==1); // Bounded retry, no busy loop.
    transport.fail=false;testMillis+=Config::BACKEND_RETRY_MAX_MS+1;files.fail=true;
    manager.update(true,0,true);assert(transport.uploads==2);assert(manager.state()==MissionState::Syncing);
    assert(storage.loadResult(result) && !result.uploaded);
    files.fail=false;testMillis+=Config::BACKEND_RETRY_MAX_MS+1;manager.update(true,0,true);
    assert(transport.uploads==3 && manager.state()==MissionState::Completed);
    assert(storage.loadResult(result) && result.uploaded && !result.mockUpload);
    MissionStorage rebootStorage(files);MissionManager reboot(rebootStorage,backend,wifi,tello,flight,1);
    assert(reboot.begin());assert(reboot.state()==MissionState::Completed);
    reboot.update(true,0,true);assert(transport.uploads==3);
    std::cout<<"Mission result sync orders sensor backlog/time/network and persists acknowledgements with retry\n";
}
