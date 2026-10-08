#include "MissionManager.h"
#include "MissionMemoryFiles.h"
#include "CollectionManager.h"
#include <cassert>
#include <iostream>
#include <vector>
#include <algorithm>
uint32_t testMillis=30000;TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;std::vector<Packet> outgoing;
std::vector<std::string> events;
size_t pendingRecords=0;bool haveSensorData=true;
bool GatewayWiFiManager::targetConfigured(StaTarget)const{return true;}
bool GatewayWiFiManager::isTelloConnected()const{return target_==StaTarget::Tello;}
bool GatewayWiFiManager::isInternetNetworkReady()const{return target_==StaTarget::Internet;}
void GatewayWiFiManager::requestStaTarget(StaTarget t){target_=t;}
IPAddress GatewayWiFiManager::getStaIp()const{return IPAddress(Config::TELLO_IP);}
bool StorageManager::begin(){ready_=true;return true;}
SaveResult StorageManager::saveMeasurement(const GatewayMeasurement&){events.push_back("sensor-save");++pendingRecords;return SaveResult::Saved;}
bool TimeManager::isTimeSynced()const{return true;}
uint64_t TimeManager::unixTime()const{return 1700000000;}
uint64_t TimeManager::collectionTime()const{return unixTime();}
bool SensorNodeClient::getInfo(const SensorNode&,uint32_t& n){n=haveSensorData?1:0;return true;}
bool SensorNodeClient::sendTime(const SensorNode&,uint64_t){return true;}
bool SensorNodeClient::getData(const SensorNode&,JsonDocument& doc){doc.clear();doc.createNestedArray("records").createNestedObject()["recordId"]="sample-1";return true;}
bool SensorNodeClient::acknowledge(const SensorNode&,const String*,size_t){events.push_back("sensor-ack");haveSensorData=false;return true;}
bool MeasurementCodec::fromJson(JsonObjectConst,const String&,GatewayMeasurement& out,bool){out.recordId="sample-1";return true;}
namespace Logger {void warn(const char*){}void error(const char*){}void node(const char*){}}
class ScenarioTransport:public MissionTransport {
public:
    unsigned results=0;
    bool configured()const override{return true;}
    bool request(const char*,const char* method,const JsonDocument* body,JsonDocument& response,int& status)override {
        status=200;
        if(!strcmp(method,"GET")) return !deserializeJson(response,R"({"id":15,"name":"local-sensor-mission","gatewayId":1,"uavId":2,"status":"SCHEDULED","waypoints":[{"id":100,"sequenceNo":1,"localX":0.2,"localY":0,"altitudeM":0.35,"actionType":"COLLECT","plannedHoldSeconds":1}],"targets":[{"sensorNodeId":5,"deviceCode":"SENSOR-001","waypointId":100}]})");
        assert(pendingRecords==0);assert(!strcmp((*body)["finalState"],"COMPLETED"));
        assert((*body)["completedWaypoints"].as<int>()==1);
        assert(!strcmp((*body)["collectedTargets"][0]["status"],"COLLECTED"));
        ++results;events.push_back("mission-upload");
        response["success"]=true;response["missionId"]=(*body)["missionId"];response["fingerprint"]=(*body)["fingerprint"];return true;
    }
};
void reply(const char* data){incoming[8889].push_back({IPAddress(Config::TELLO_IP),8889,data});}
int main(){
    static_assert(!Config::GROUND_TEST_MODE,"This host-only scenario models a live-mode build");
    GatewayWiFiManager wifi;wifi.requestStaTarget(StaTarget::Internet);
    TelloController tello(wifi);FlightStateManager flight(wifi,tello);
    MissionMemoryFiles files;MissionStorage missionStorage(files);ScenarioTransport transport;
    MissionBackendClient backend(wifi,transport,1,false);MissionManager mission(missionStorage,backend,wifi,tello,flight,1);
    NodeRegistry registry;NodeAuthenticator auth(nullptr,0);SensorNodeClient sensor(auth);
    StorageManager storage;storage.begin();TimeManager time;CollectionManager collection(registry,sensor,storage,time);
    SensorNode node;node.deviceCode="SENSOR-001";node.authenticated=true;registry.addOrUpdate(node);
    mission.setCollection(collection);assert(mission.begin());mission.update(true,0,true);
    assert(mission.state()==MissionState::Ready && outgoing.empty());assert(mission.start());
    size_t processed=0;int height=0,vertical=0,x=0;bool tookOff=false,landed=false;
    for(unsigned tick=0;tick<1000 && mission.state()!=MissionState::Completed;++tick){
        testMillis+=100;
        while(processed<outgoing.size()) {
            const std::string command=outgoing[processed++].body;
            if(command=="command")reply("ok");
            else if(command=="battery?")reply("80");
            else if(command=="takeoff"){assert(!tookOff);tookOff=true;height=60;reply("ok");}
            else if(command=="land"){height=0;vertical=0;landed=true;events.push_back("land");reply("ok");}
            else if(command.find("down ")==0){height-=atoi(command.c_str()+5);reply("ok");}
            else if(command.find("forward ")==0){assert(height>=20 && height<=40);x+=atoi(command.c_str()+8);reply("ok");}
            else if(command.find("rc ")==0){int a,b,d;assert(sscanf(command.c_str(),"rc %d %d %d %d",&a,&b,&vertical,&d)==4);assert(a==0 && b==0 && d==0);}
            else assert(false);
        }
        if(vertical)height+=vertical>0?1:-1;
        if(wifi.staTarget()==StaTarget::Tello){
            char telemetry[96];snprintf(telemetry,sizeof(telemetry),"h:%d;tof:%d;vgz:%d;bat:80;",height,height,vertical);
            incoming[8890].push_back({IPAddress(Config::TELLO_IP),8890,telemetry});
        }
        tello.update();flight.update(pendingRecords,true,true);mission.update(true,pendingRecords,true);
        registry.update();collection.setMissionMode(mission.ownsFlight());collection.update();
        if(wifi.staTarget()==StaTarget::Internet && pendingRecords){assert(landed);events.push_back("sensor-sync");pendingRecords=0;}
    }
    if(mission.state()!=MissionState::Completed)std::cerr<<mission.stateName()<<" "<<mission.error()<<" height="<<height<<"\n";
    assert(mission.state()==MissionState::Completed);assert(tookOff && landed && x==20 && transport.results==1);
    assert(mission.result().uploaded && !mission.result().mockUpload);
    const char* ordered[]={"sensor-save","sensor-ack","land","sensor-sync","mission-upload"};
    auto previous=events.begin();for(const char* event:ordered){auto at=std::find(previous,events.end(),event);assert(at!=events.end());previous=at+1;}
    std::cout<<"Full production mission + targeted collection simulation completed in required order\n";
}
