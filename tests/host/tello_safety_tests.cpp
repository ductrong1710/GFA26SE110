#include "TelloController.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=1000;TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;std::vector<Packet> outgoing;
bool GatewayWiFiManager::isTelloConnected() const {return true;}
IPAddress GatewayWiFiManager::getStaIp() const {return IPAddress(Config::TELLO_IP);}
void packet(uint16_t port,const char* data) {incoming[port].push_back({IPAddress(Config::TELLO_IP),port,data});}
int main() {
    GatewayWiFiManager wifi;TelloController tello(wifi);assert(tello.begin());assert(tello.requestSdkMode());
    packet(8889,"ok");tello.update();testMillis+=500;
    if(Config::GROUND_TEST_MODE) {
        size_t before=outgoing.size();assert(!tello.takeoff());assert(!tello.move("forward",20));
        assert(!tello.rotate("cw",90));assert(!tello.sendRc(0,0,1,0));assert(outgoing.size()==before);
        std::cout<<"Default ground mode blocks flight at UDP boundary\n";return 0;
    }
    packet(8890,"h:35;tof:35;vgz:0;");tello.update();assert(!tello.move("up",20));
    assert(tello.sendRc(0,0,100,0));tello.update();assert(outgoing.back().body=="rc 0 0 3 0");
    testMillis+=100;packet(8890,"h:40;tof:40;vgz:0;");tello.update();assert(outgoing.back().body=="rc 0 0 0 0");
    testMillis+=500;packet(8890,"h:20;tof:20;vgz:0;");tello.update();assert(tello.move("up",20));
    packet(8889,"ok");tello.update();testMillis+=500;
    assert(!tello.move("up",21));
    testMillis+=Config::TELLO_TELEMETRY_TIMEOUT_MS+1;tello.update();
    assert(!tello.move("up",20));assert(tello.sendRc(0,0,100,0));tello.update();assert(outgoing.back().body=="rc 0 0 0 0");
    std::cout<<"Direct Tello commands and active RC obey shared altitude safety\n";
}
