#include "configured_gateway.h"
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
bool StorageManager::markNodeAcknowledged(const String&,const String&){return true;}
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
    bool rejectStart=true;
    bool remoteDue=false,rejectConsume=false;
    unsigned consumes=0;
    bool configured()const override{return true;}
    bool request(const char* path,const char* method,const JsonDocument* body,JsonDocument& response,int& status)override {
        status=200;
        if(strstr(path,"/start") && rejectStart){status=409;return false;}
        if(!strcmp(method,"GET")) {
            if(deserializeJson(response,R"({"success":true,"data":{"id":15,"name":"local-sensor-mission","gatewayId":1,"uavId":2,"status":"SCHEDULED","waypoints":[{"id":100,"sequenceNo":1,"localX":0.2,"localY":0,"altitudeM":0.35,"actionType":"COLLECT","plannedHoldSeconds":1}],"targets":[{"sensorNodeId":5,"deviceCode":"SENSOR-001","waypointId":100}]}})")) return false;
            response["data"]["waypoints"][0]["altitudeM"]=Config::TELLO_TARGET_ALTITUDE_CM/100.0;
            return true;
        }
        response["success"]=true;
        if(strstr(path,"/launch/ready")) {response["data"]["state"]=remoteDue?"READY_TO_START":"AWAITING_CONFIRMATION";return true;}
        if(strstr(path,"/launch/consume")) {
            ++consumes;assert(outgoing.empty());assert((*body)["session"].as<const char*>());
            if(rejectConsume){status=-1;return false;}
            response["data"]["state"]="START_ACCEPTED";return true;
        }
        if(strstr(path,"/sync")) {
            assert(pendingRecords==0);
            assert((*body)["missionId"].as<int>()==15);
            assert(!strcmp((*body)["collectionResults"][0]["status"],"SUCCESS"));
            assert((*body)["collectionResults"][0]["recordsReceived"].as<int>()==1);
            response["data"]["batchKey"]=(*body)["batchKey"];
            auto item=response["data"].createNestedArray("collectionResults").createNestedObject();
            item["index"]=0;item["sensorNodeId"]=5;item["attemptNo"]=1;item["status"]="ACCEPTED";
        } else {
            response["data"]["id"]=15;
            if(strstr(path,"/downloaded"))response["data"]["status"]="SCHEDULED";
            else if(strstr(path,"/start")){assert(outgoing.empty());response["data"]["status"]="RUNNING";}
            else {assert(strstr(path,"/complete"));assert(pendingRecords==0);response["data"]["status"]="COMPLETED";++results;events.push_back("mission-upload");}
        }
        return true;
    }
};
void reply(const char* data){incoming[8889].push_back({IPAddress(Config::TELLO_IP),8889,data});}
class PartialTransport:public ScenarioTransport {
public:
    bool allFailed=false;
    bool request(const char* path,const char* method,const JsonDocument* body,JsonDocument& response,int& status) override {
        if(!strcmp(method,"GET")) {
            if(!ScenarioTransport::request(path,method,body,response,status)) return false;
            auto w=response["data"]["waypoints"].as<JsonArray>().createNestedObject();
            w["id"]=101;w["sequenceNo"]=2;w["localX"]=0.4;w["localY"]=0;
            w["altitudeM"]=Config::TELLO_TARGET_ALTITUDE_CM/100.0;w["actionType"]="COLLECT";w["plannedHoldSeconds"]=1;
            auto t=response["data"]["targets"].as<JsonArray>().createNestedObject();
            t["sensorNodeId"]=6;t["deviceCode"]="SENSOR-002";t["waypointId"]=101;return true;
        }
        if(strstr(path,"/fail")) {
            response["success"]=true;response["data"]["id"]=15;response["data"]["status"]="FAILED";status=200;return true;
        }
        if(strstr(path,"/sync")) {
            assert(!strcmp((*body)["collectionResults"][0]["status"],"FAILED"));
            assert(!strcmp((*body)["collectionResults"][1]["status"],allFailed?"FAILED":"SUCCESS"));
            response["success"]=true;response["data"]["batchKey"]=(*body)["batchKey"];
            auto a=response["data"].createNestedArray("collectionResults");
            for(int i=0;i<2;++i){auto r=a.createNestedObject();r["index"]=i;r["sensorNodeId"]=5+i;r["attemptNo"]=1;r["status"]="ACCEPTED";}
            status=200;++results;return true;
        }
        return ScenarioTransport::request(path,method,body,response,status);
    }
};
class PartialCollection:public MissionCollection {
public:
    int mode=0;bool second=false;bool active=false;unsigned cancels=0;
    bool requestTarget(const char* code)override {
        assert(!active);second=!strcmp(code,"SENSOR-002");
        if(!second && mode==0)return false; // Offline target times out before request.
        active=true;return true;
    }
    MissionCollectionState targetState()const override{return second && mode!=2 ? MissionCollectionState::Collected:MissionCollectionState::Failed;}
    size_t targetRecordCount()const override{return active ? (second?2:1):0;}
    void cancelTarget()override{active=false;++cancels;}
};
void partialCollectionScenario(int mode) {
    GatewayWiFiManager w;w.requestStaTarget(StaTarget::Internet);TelloController t(w);FlightStateManager f(w,t);
    MissionMemoryFiles fs;MissionStorage s(fs);PartialTransport transport;transport.rejectStart=false;transport.allFailed=mode==2;
    MissionBackendClient b(w,transport,1,false);MissionManager m(s,b,w,t,f,1);PartialCollection c;c.mode=mode;
    m.setCollection(c);assert(m.begin());m.update(true,0,true);assert(m.start());
    int height=0,x=0;size_t processed=0;bool landed=false;
    for(unsigned tick=0;tick<1200 && m.state()!=MissionState::Failed;++tick) {
        testMillis+=100;
        while(processed<outgoing.size()) {
            auto command=outgoing[processed++].body;
            if(command=="command")reply("ok");
            else if(command=="battery?")reply("80");
            else if(command=="takeoff"){height=Config::TELLO_TARGET_ALTITUDE_CM;reply("ok");}
            else if(command=="forward 20"){x+=20;reply("ok");}
            else if(command=="back 40"){assert(c.second);x-=40;reply("ok");}
            else if(command=="land"){assert(c.second && x==0);landed=true;height=0;reply("ok");}
            else assert(command=="rc 0 0 0 0");
        }
        if(w.staTarget()==StaTarget::Tello) {
            char data[80];snprintf(data,sizeof(data),"h:%d;tof:%d;vgz:0;bat:80;",height,height?height:10);
            incoming[8890].push_back({IPAddress(Config::TELLO_IP),8890,data});
        }
        t.update();f.update();m.update(true,0,true);
    }
    assert(landed && c.second && x==0 && transport.results==1);
    assert(m.result().uploaded && m.result().finalState==MissionState::Failed);
    assert(m.result().targets[0]==TargetOutcome::Failed);
    assert(m.result().targetRecords[0]==(mode==0?0u:1u));
    assert(m.result().targets[1]==(mode==2?TargetOutcome::Failed:TargetOutcome::Collected));
    MissionResult persisted;assert(s.loadResult(persisted));assert(persisted.targets[0]==TargetOutcome::Failed);
    t.stop();outgoing.clear();incoming.clear();
}
int main(){
    provisionTestGateway();
    {
        // A lost consume response must not trigger any command or an automatic retry,
        // including after restoring the durable preflight checkpoint.
        GatewayWiFiManager w;w.requestStaTarget(StaTarget::Internet);TelloController t(w);FlightStateManager f(w,t);
        MissionMemoryFiles fs;MissionStorage s(fs);ScenarioTransport transport;
        transport.remoteDue=true;transport.rejectConsume=true;
        MissionBackendClient b(w,transport,1,false);MissionManager m(s,b,w,t,f,1);
        assert(m.begin());m.update(true,0,true);assert(m.state()==MissionState::Ready);
        m.update(true,0,true);assert(m.state()==MissionState::RecoveryRequired);
        assert(transport.consumes==1 && outgoing.empty());
        testMillis+=10000;m.update(true,0,true);assert(transport.consumes==1 && outgoing.empty());
        MissionStorage restoredStorage(fs);MissionBackendClient restoredBackend(w,transport,1,false);
        MissionManager restored(restoredStorage,restoredBackend,w,t,f,1);
        assert(restored.begin());assert(restored.state()==MissionState::RecoveryRequired);
        restored.update(true,0,true);assert(transport.consumes==1 && outgoing.empty());
        // main.cpp selects Tello on boot when the restored mission owns flight.
        w.requestStaTarget(restored.initialStaTarget());
        assert(restored.abort());
        size_t processedRecovery=0;
        for(unsigned tick=0;tick<100 && restored.state()!=MissionState::Landed;++tick) {
            testMillis+=100;
            while(processedRecovery<outgoing.size()) {
                const auto command=outgoing[processedRecovery++].body;
                if(command=="command") reply("ok");
                else {assert(command=="land");reply("error");}
            }
            incoming[8890].push_back({IPAddress(Config::TELLO_IP),8889,"h:0;tof:10;vgz:0;"});
            t.update();f.update();restored.update(true,0,true);
        }
        assert(restored.state()==MissionState::Landed && !f.mayBeAirborne());
        assert(restored.result().finalState==MissionState::Aborted);
        f.update();f.update();assert(w.staTarget()==StaTarget::Internet);
        t.stop();outgoing.clear();incoming.clear();
    }
    static_assert(!Config::GROUND_TEST_MODE,"This host-only scenario models a live-mode build");
    {
        GatewayWiFiManager w;w.requestStaTarget(StaTarget::Internet);TelloController t(w);FlightStateManager f(w,t);
        MissionMemoryFiles fs;MissionStorage s(fs);ScenarioTransport transport;transport.rejectStart=false;
        MissionBackendClient b(w,transport,1,false);MissionManager m(s,b,w,t,f,1);
        assert(m.begin());m.update(true,0,true);assert(m.start());
        f.update();reply("OK");t.update();f.update();
        testMillis+=500;f.update();reply("90\r\n");t.update();f.update();
        assert(f.state()==FlightState::LandedReady && !f.mayBeAirborne());
        testMillis+=Config::TELLO_CONNECT_TIMEOUT_MS;m.update(true,0,true);
        assert(m.state()==MissionState::Landing);
        m.update(true,0,true);
        assert(m.state()==MissionState::Landed && w.staTarget()==StaTarget::Internet);
        assert(!strcmp(m.result().failureReason,"GROUND_TELEMETRY_TIMEOUT"));
        for(const auto& packet:outgoing) assert(packet.body=="command" || packet.body=="battery?");
        m.update(true,0,true);assert(m.state()==MissionState::ConnectingInternet);
        outgoing.clear();incoming.clear();
    }
#if defined(GATEWAY_MISSION_FLIGHT_PROFILE) && GATEWAY_MISSION_FLIGHT_PROFILE
    for(int mode : {0,1,2}) partialCollectionScenario(mode);
    for(int acceptedHeight : {90,110}) {
        GatewayWiFiManager w;w.requestStaTarget(StaTarget::Internet);TelloController t(w);FlightStateManager f(w,t);
        MissionMemoryFiles fs;MissionStorage s(fs);ScenarioTransport transport;transport.rejectStart=false;
        MissionBackendClient b(w,transport,1,false);MissionManager m(s,b,w,t,f,1);
        assert(m.begin());m.update(true,0,true);assert(m.start());
        auto step=[&](int height,int velocity=0) {
            testMillis+=100;
            char data[80];snprintf(data,sizeof(data),"h:%d;tof:%d;vgz:%d;bat:80;",height,height,velocity);
            incoming[8890].push_back({IPAddress(Config::TELLO_IP),8890,data});
            t.update();f.update();m.update(true,0,true);
        };
        f.update();reply("ok");t.update();f.update();testMillis+=500;
        f.update();reply("80");t.update();f.update();testMillis+=500;step(0);
        assert(m.state()==MissionState::TakingOff);reply("ok");step(acceptedHeight);
        assert(m.state()==MissionState::Stabilizing);
        for(int i=0;i<17;++i) step(acceptedHeight);
        assert(m.state()==MissionState::Stabilizing); // Less than two seconds of accepted samples.
        step(acceptedHeight,10); // Vertical motion resets the stable window.
        for(int i=0;i<17;++i) step(acceptedHeight);
        assert(m.state()==MissionState::Stabilizing);
        testMillis+=700;t.update();f.update();m.update(true,0,true);
        assert(m.state()==MissionState::Stabilizing); // Re-reading old data cannot finish the window.
        for(int i=0;i<15 && m.state()==MissionState::Stabilizing;++i) step(acceptedHeight);
        assert(m.state()==MissionState::Executing); // Both inclusive boundaries are accepted.
        for(const auto& p:outgoing) assert(p.body!="rc 0 0 3 0" && p.body!="rc 0 0 10 0");
        t.stop();incoming.clear();outgoing.clear();
    }
#endif
    GatewayWiFiManager wifi;wifi.requestStaTarget(StaTarget::Internet);
    TelloController tello(wifi);FlightStateManager flight(wifi,tello);
    MissionMemoryFiles files;MissionStorage missionStorage(files);ScenarioTransport transport;
    MissionBackendClient backend(wifi,transport,1,false);MissionManager mission(missionStorage,backend,wifi,tello,flight,1);
    NodeRegistry registry;NodeAuthenticator auth(nullptr,0);SensorNodeClient sensor(auth);
    StorageManager storage;storage.begin();TimeManager time;CollectionManager collection(registry,sensor,storage,time);
    SensorNode node;node.deviceCode="SENSOR-001";node.authenticated=true;registry.addOrUpdate(node);
    mission.setCollection(collection);assert(mission.begin());mission.update(true,0,true);
    assert(mission.state()==MissionState::Ready && outgoing.empty());
    assert(!mission.start());
    assert(mission.state()==MissionState::Ready && wifi.staTarget()==StaTarget::Internet && outgoing.empty());
    transport.rejectStart=false;
#if defined(GATEWAY_MISSION_FLIGHT_PROFILE) && GATEWAY_MISSION_FLIGHT_PROFILE
    transport.remoteDue=true;testMillis+=5000;mission.update(true,0,true);
    assert(transport.consumes==1 && mission.state()==MissionState::PreparingTello);
#else
    assert(mission.start());
#endif
    size_t processed=0;int height=0,vertical=0,x=0;bool tookOff=false,landed=false;
    for(unsigned tick=0;tick<1000 && mission.state()!=MissionState::Completed;++tick){
        testMillis+=100;
        while(processed<outgoing.size()) {
            const std::string command=outgoing[processed++].body;
            if(command=="command")reply("ok");
            else if(command=="battery?")reply("80");
            else if(command=="takeoff"){assert(!tookOff);tookOff=true;height=Config::TELLO_TARGET_ALTITUDE_CM+25;reply("ok");}
            else if(command=="land"){height=0;vertical=0;landed=true;events.push_back("land");reply("ok");}
            else if(command.find("down ")==0){height-=atoi(command.c_str()+5);reply("ok");}
            else if(command.find("forward ")==0){assert(height>=Config::TELLO_MIN_OPERATION_ALTITUDE_CM && height<=Config::TELLO_MAX_ALTITUDE_CM);x+=atoi(command.c_str()+8);reply("ok");}
            else if(command.find("back ")==0){assert(height>=Config::TELLO_MIN_OPERATION_ALTITUDE_CM && height<=Config::TELLO_MAX_ALTITUDE_CM);x-=atoi(command.c_str()+5);reply("ok");}
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
    assert(mission.state()==MissionState::Completed);assert(tookOff && landed && transport.results==1);
#if defined(GATEWAY_MISSION_FLIGHT_PROFILE) && GATEWAY_MISSION_FLIGHT_PROFILE
    assert(x==0);
#else
    assert(x==20);
#endif
    assert(mission.result().uploaded && !mission.result().mockUpload);
    const char* ordered[]={"sensor-save","sensor-ack","land","sensor-sync","mission-upload"};
    auto previous=events.begin();for(const char* event:ordered){auto at=std::find(previous,events.end(),event);assert(at!=events.end());previous=at+1;}
    std::cout<<"Full production mission + targeted collection simulation completed in required order\n";
}
