#include "MissionManager.h"
#include "MissionMemoryFiles.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=30000;TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;std::vector<Packet> outgoing;
bool connected=true;
bool GatewayWiFiManager::targetConfigured(StaTarget) const {return true;}
bool GatewayWiFiManager::isTelloConnected() const {return connected && target_==StaTarget::Tello;}
bool GatewayWiFiManager::isInternetNetworkReady() const {return connected && target_==StaTarget::Internet;}
void GatewayWiFiManager::requestStaTarget(StaTarget t) {target_=t;}
IPAddress GatewayWiFiManager::getStaIp() const {return IPAddress(Config::TELLO_IP);}
struct NoTransport:MissionTransport {
    bool configured()const override{return false;}
    bool request(const char*,const char*,const JsonDocument*,JsonDocument&,int&)override{return false;}
};
void packet(uint16_t port,const char* data){incoming[port].push_back({IPAddress(Config::TELLO_IP),port,data});}
void tick(TelloController& tello,FlightStateManager& flight,MissionManager& mission,const char* telemetry=nullptr,unsigned ms=100) {
    testMillis+=ms;if(telemetry) packet(8890,telemetry);tello.update();flight.update();mission.update(true,0,true);
}
int main() {
    {
        // READY may coexist with manual flight; abort must use actual aircraft
        // state rather than persisting a false LANDED marker.
        GatewayWiFiManager w;w.requestStaTarget(StaTarget::Internet);TelloController t(w);FlightStateManager f(w,t);
        MissionMemoryFiles fs;MissionStorage s(fs);NoTransport transport;
        MissionBackendClient b(w,transport,1,true);MissionManager m(s,b,w,t,f,1);
        assert(m.begin());m.update(true,0,true);assert(m.state()==MissionState::Ready);
        f.restoreRecovery();w.requestStaTarget(StaTarget::Tello);assert(m.abort());
        assert(m.state()==MissionState::Landing);
        Mission restored;MissionStateRecord checkpoint;
        assert(s.restore(restored,checkpoint)==MissionRestore::RecoveryRequired);
    }
    GatewayWiFiManager wifi;wifi.requestStaTarget(StaTarget::Internet);
    TelloController tello(wifi);FlightStateManager flight(wifi,tello);
    MissionMemoryFiles files;MissionStorage storage(files);NoTransport transport;
    MissionBackendClient backend(wifi,transport,1,true);
    MissionManager mission(storage,backend,wifi,tello,flight,1);
    assert(mission.begin());assert(mission.state()==MissionState::Empty);
    mission.update(true,0,true);assert(mission.state()==MissionState::Ready);
    assert(outgoing.empty());assert(!mission.ownsFlight());
    const auto original=files.files["/mission/active.json"];
    assert(!mission.requestPull());mission.update(true,0,true);assert(files.files["/mission/active.json"]==original);
    MissionStorage restoredStorage(files);MissionManager restored(restoredStorage,backend,wifi,tello,flight,1);
    assert(restored.begin());assert(restored.state()==MissionState::Ready);
    files.fail=true;assert(!mission.start());assert(outgoing.empty());files.fail=false;
    assert(mission.start());assert(mission.ownsFlight());assert(!mission.start());
    tick(tello,flight,mission);packet(8889,"ok");tick(tello,flight,mission);
    tick(tello,flight,mission,nullptr,500);packet(8889,"80");
    tick(tello,flight,mission,"h:0;tof:10;vgz:0;bat:80;");
    if(Config::GROUND_TEST_MODE) {
        assert(mission.state()==MissionState::GroundReady);
        for(auto& p:outgoing) assert(p.body!="takeoff");
        for(unsigned i=0;i<12;++i)tick(tello,flight,mission,"h:0;tof:10;vgz:0;bat:80;",500);
        assert(mission.abort());
        for(unsigned i=0;i<8;++i)tick(tello,flight,mission,"h:0;tof:10;vgz:0;bat:80;",500);
        assert(wifi.staTarget()==StaTarget::Internet);
        assert(mission.state()==MissionState::Syncing || mission.state()==MissionState::Aborted);
        std::cout<<"Mission download/restore/explicit start/ground abort tests passed\n";return 0;
    }
    tick(tello,flight,mission,"h:0;tof:10;vgz:0;bat:80;",500);
    assert(mission.state()==MissionState::TakingOff);assert(outgoing.back().body=="takeoff");
    packet(8889,"ok");tick(tello,flight,mission,"h:60;tof:60;vgz:0;bat:80;");
    assert(mission.state()==MissionState::Stabilizing);
    // No movement may use the telemetry that arrived with takeoff completion.
    size_t before=outgoing.size();tick(tello,flight,mission,nullptr,500);assert(outgoing.size()==before);
    tick(tello,flight,mission,"h:60;tof:60;vgz:0;bat:80;");
    assert(outgoing.back().body=="down 20");before=outgoing.size();
    tick(tello,flight,mission,"h:60;tof:60;vgz:0;bat:80;",500);assert(outgoing.size()==before);
    packet(8889,"ok");tick(tello,flight,mission);
    before=outgoing.size();tick(tello,flight,mission,nullptr,500);assert(outgoing.size()==before);
    for(unsigned i=0;i<20 && mission.state()==MissionState::Stabilizing;++i)
        tick(tello,flight,mission,"h:35;tof:35;vgz:0;bat:80;",100);
    assert(mission.state()==MissionState::Executing);
    MissionMemoryFiles flightSnapshot=files;
    assert(mission.abort());tick(tello,flight,mission);tick(tello,flight,mission);
    assert(outgoing.back().body=="land");
    const size_t landSends=outgoing.size();
    tick(tello,flight,mission,nullptr,Config::TELLO_FLIGHT_COMMAND_TIMEOUT_MS+1);
    assert(mission.state()==MissionState::RecoveryRequired);
    tick(tello,flight,mission,nullptr,4000);
    assert(outgoing.size()==landSends);assert(wifi.staTarget()==StaTarget::Tello);
    // Only an explicit operator action starts a new land attempt.
    assert(mission.abort());tick(tello,flight,mission);tick(tello,flight,mission);
    assert(outgoing.back().body=="land");packet(8889,"ok");tick(tello,flight,mission);
    tick(tello,flight,mission,nullptr,Config::TELLO_LAND_ACK_ONLY_SETTLE_MS+1);
    assert(wifi.staTarget()==StaTarget::Tello);assert(mission.state()==MissionState::Landing);
    for(unsigned i=0;i<12;++i) tick(tello,flight,mission,"h:0;tof:90;vgz:0;bat:80;",500);
    assert(wifi.staTarget()==StaTarget::Tello); // Conflicting height/range is not ground evidence.
    for(unsigned i=0;i<14;++i) tick(tello,flight,mission,"h:0;tof:10;vgz:0;bat:80;",500);
    assert(wifi.staTarget()==StaTarget::Internet);
    // A reboot from a flight state cannot restart the route or select Internet.
    MissionStorage rebootStorage(flightSnapshot);MissionManager reboot(rebootStorage,backend,wifi,tello,flight,1);
    assert(reboot.begin());assert(reboot.state()==MissionState::RecoveryRequired);
    assert(reboot.initialStaTarget()==StaTarget::Tello);assert(!reboot.start());
    std::cout<<"Mission takeoff stabilization, ACK/freshness and reboot recovery tests passed\n";
}
