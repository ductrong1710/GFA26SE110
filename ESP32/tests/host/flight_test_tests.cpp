#include "FlightTestManager.h"
#include "MissionMemoryFiles.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=1000;TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;std::vector<Packet> outgoing;
bool connected=true;
bool GatewayWiFiManager::targetConfigured(StaTarget) const{return true;}
void GatewayWiFiManager::requestStaTarget(StaTarget target){target_=target;}
bool GatewayWiFiManager::isTelloConnected() const{return connected && target_==StaTarget::Tello;}
bool GatewayWiFiManager::isInternetNetworkReady() const{return connected && target_==StaTarget::Internet;}
IPAddress GatewayWiFiManager::getStaIp() const{return IPAddress(Config::TELLO_IP);}
struct Transport:MissionTransport {
    MissionMemoryFiles& files; bool claimOk=true,ack=true; int claims=0,results=0,claimStatus=200;std::string result;
    explicit Transport(MissionMemoryFiles& f):files(f){}
    bool configured() const override{return true;}
    bool request(const char* path,const char* method,const JsonDocument* body,JsonDocument& out,int& status) override {
        status=200;out["success"]=true;auto d=out.createNestedObject("data");
        d["id"]=7;d["gatewayId"]=1;d["uavId"]=2;d["altitudeCm"]=100;d["holdMs"]=3000;
        d["expiresAt"]="2099-01-01T00:00:00Z";d["status"]="QUEUED";
        if(strstr(path,"/claim")) {assert(!strcmp(method,"POST"));assert(files.exists("/flight-test.json"));++claims;d["status"]="CLAIMED";status=claimStatus;return claimOk;}
        if(strstr(path,"/result")) {++results;assert(body);serializeJson(*body,result);d["status"]=(*body)["status"];if(!ack)d["id"]=99;}
        return true;
    }
};
void packet(int port,const char* value){incoming[port].push_back({IPAddress(Config::TELLO_IP),uint16_t(port),value});}
struct Rig {
    MissionMemoryFiles files;Transport transport;GatewayWiFiManager wifi;TelloController tello;FlightStateManager flight;FlightTestManager test;
    Rig():transport(files),tello(wifi),flight(wifi,tello),test(files,transport,wifi,tello,flight){incoming.clear();outgoing.clear();connected=true;wifi.requestStaTarget(StaTarget::Internet);assert(test.begin(1));}
    void tick(unsigned ms=100,const char* telemetry=nullptr){testMillis+=ms;if(telemetry)packet(8890,telemetry);tello.update();flight.update();test.update(true);}
    void ready(){test.update(true);assert(test.locked());assert(transport.claims==1);tick();packet(8889,"ok");tick(500);tick(500);packet(8889,"80");tick(500,"h:0;tof:10;vgz:0;bat:80;");}
    void flying(){ready();tick(500,"h:0;tof:10;vgz:0;bat:80;");assert(flight.mayBeAirborne());packet(8889,"ok");tick(500,"h:90;tof:90;vgz:0;bat:80;");assert(flight.state()==FlightState::Flying);}
    int sends(const char* command){int n=0;for(auto& p:outgoing)if(p.body==command)++n;return n;}
    void landed(){for(int i=0;i<20 && !sends("land");++i)tick(500,"h:100;tof:100;vgz:0;bat:80;");assert(sends("land")==1);packet(8889,"ok");tick(100);for(int i=0;i<13;++i)tick(500,"h:0;tof:10;vgz:0;bat:80;");for(int i=0;i<5;++i)tick(100);}
};
int main(){
    if(Config::GROUND_TEST_MODE){Rig r;for(int i=0;i<50;++i)r.tick(500,"h:0;tof:10;vgz:0;bat:80;");assert(r.sends("takeoff")==0);assert(r.transport.claims==0);std::cout<<"Ground profile never claims or takes off\n";return 0;}
    {Rig r;r.flying();r.tick(100,"h:100;tof:100;vgz:0;bat:80;");for(int i=0;i<29;++i)r.tick(100,"h:100;tof:100;vgz:0;bat:80;");assert(r.sends("land")==0);r.tick(100,"h:100;tof:100;vgz:0;bat:80;");r.landed();assert(r.transport.result.find("COMPLETED")!=std::string::npos);assert(r.transport.result.find("\"heldMs\":3000")!=std::string::npos);assert(!r.test.locked());assert(!r.tello.safety().allowMove("up",20));r.test.update(true);assert(r.sends("takeoff")==1);}
    {Rig r;r.flying();r.tick(100,"h:100;tof:100;vgz:0;bat:80;");r.tick(1500,"h:100;tof:100;vgz:0;bat:80;");r.tick(100,"h:90;tof:90;vgz:0;bat:80;");r.tick(100,"h:100;tof:100;vgz:0;bat:80;");r.tick(1500,"h:100;tof:100;vgz:0;bat:80;");assert(r.sends("land")==0);r.tick(2001);r.landed();assert(r.transport.result.find("FAILED")!=std::string::npos);}
    {Rig r;r.transport.claimOk=false;r.test.update(true);assert(r.sends("takeoff")==0);assert(r.test.locked());}
    {Rig r;r.flying();r.tick(100,"h:100;tof:100;vgz:0;bat:80;");for(int i=0;i<20;++i)r.tick(100,"h:100;tof:100;vgz:0;bat:80;");packet(8890,"h:90;tof:90;vgz:0;bat:80;");r.tick(100,"h:100;tof:100;vgz:0;bat:80;");for(int i=0;i<11;++i)r.tick(100,"h:100;tof:100;vgz:0;bat:80;");assert(r.sends("land")==0);assert(r.flight.state()==FlightState::Flying);}
    {Rig r;r.flying();packet(8890,"h:120;tof:120;vgz:0;bat:80;");r.tick(100,"h:100;tof:100;vgz:0;bat:80;");assert(r.flight.state()==FlightState::Landing);}
    {Rig r;r.transport.claimStatus=409;r.test.update(true);assert(r.sends("takeoff")==0);assert(!r.test.locked());r.test.update(true);assert(r.transport.claims==1);}
    {Rig r;r.test.update(false);assert(r.transport.claims==0);assert(r.sends("takeoff")==0);}
    {Rig r;r.test.update(true);for(int i=0;i<180;++i)r.tick(500);assert(r.sends("takeoff")==0);assert(r.transport.result.find("FAILED")!=std::string::npos);assert(r.transport.result.find("\"takeoffAttempted\":false")!=std::string::npos);assert(!r.test.locked());}
    {Rig r;r.files.fail=true;r.test.update(true);assert(r.transport.claims==0);assert(r.sends("takeoff")==0);}
    {Rig r;r.flying();r.tello.stop();FlightStateManager f(r.wifi,r.tello);FlightTestManager reboot(r.files,r.transport,r.wifi,r.tello,f);assert(reboot.begin(1));assert(reboot.locked());assert(reboot.initialStaTarget()==StaTarget::Tello);for(int i=0;i<5;++i){testMillis+=500;r.tello.update();f.update();reboot.update(true);}assert(r.sends("takeoff")==1);assert(r.transport.claims==1);}
    {Rig r;r.transport.ack=false;r.flying();for(int i=0;i<32;++i)r.tick(100,"h:100;tof:100;vgz:0;bat:80;");r.landed();assert(r.test.locked());assert(r.transport.results>0);}
    for (int phase : {3,4}) {
        Rig r;
        r.files.files["/flight-test.json"] = std::string("{\"version\":1,\"gatewayId\":1,\"id\":7,\"phase\":") + std::to_string(phase) +
            ",\"takeoffAttempted\":true,\"uavConnected\":true,\"landed\":false,\"observedAltitudeCm\":100,\"heldMs\":1500,\"errorCode\":\"LANDING_UNCONFIRMED\"}";
        FlightStateManager f(r.wifi,r.tello);FlightTestManager reboot(r.files,r.transport,r.wifi,r.tello,f);
        assert(reboot.begin(1));assert(reboot.locked());assert(reboot.initialStaTarget()==StaTarget::Tello);
        assert(!strcmp(reboot.error(),"LANDING_UNCONFIRMED"));
        reboot.update(true);assert(r.transport.claims==0 && r.transport.results==0);
    }
    std::cout<<"Flight test sequence, continuous hold, stale telemetry, journal and reboot guards passed\n";
}
