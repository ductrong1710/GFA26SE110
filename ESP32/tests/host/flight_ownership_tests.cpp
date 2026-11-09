#include "FlightStateManager.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=1000;TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;std::vector<Packet> outgoing;
bool GatewayWiFiManager::targetConfigured(StaTarget)const{return true;}
void GatewayWiFiManager::requestStaTarget(StaTarget t){target_=t;}
bool GatewayWiFiManager::isTelloConnected()const{return target_==StaTarget::Tello;}
bool GatewayWiFiManager::isInternetNetworkReady()const{return target_==StaTarget::Internet;}
IPAddress GatewayWiFiManager::getStaIp()const{return IPAddress(Config::TELLO_IP);}
int main(){
    GatewayWiFiManager w;TelloController t(w);FlightStateManager f(w,t);
    assert(f.prepare(FlightOwner::Mission));
    assert(!f.prepare(FlightOwner::Manual));assert(!f.prepare(FlightOwner::FlightTest));
    assert(!f.cancelPreparation(FlightOwner::Manual));
    assert(f.owner()==FlightOwner::Mission && w.staTarget()==StaTarget::Tello);
    assert(f.cancelPreparation(FlightOwner::Mission));assert(f.owner()==FlightOwner::None);
    assert(f.prepare(FlightOwner::FlightTest));
    assert(!f.rc(0,0,10,0,FlightOwner::Mission));assert(!f.takeoff());
    assert(f.cancelPreparation(FlightOwner::FlightTest));
    f.restoreRecovery(FlightOwner::Mission);
    assert(f.owner()==FlightOwner::Mission && f.mayBeAirborne());
    assert(!f.cancelPreparation(FlightOwner::Mission));assert(!f.prepare(FlightOwner::Manual));
    assert(outgoing.empty());
    std::cout<<"Flight owner conflict, release and recovery tests passed\n";
}
