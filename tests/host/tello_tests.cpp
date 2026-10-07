#include "TelloController.h"
#include "FlightStateManager.h"
#include <cassert>
#include <iostream>
uint32_t testMillis = 1000;
TestSerial Serial;
std::map<uint16_t,std::deque<Packet>> incoming;
std::vector<Packet> outgoing;
bool connected = true;
bool GatewayWiFiManager::targetConfigured(StaTarget) const { return true; }
void GatewayWiFiManager::requestStaTarget(StaTarget target) { target_=target; }
bool GatewayWiFiManager::isTelloConnected() const { return connected; }
bool GatewayWiFiManager::isInternetNetworkReady() const { return connected && target_==StaTarget::Internet; }
IPAddress GatewayWiFiManager::getStaIp() const { static const uint8_t ip[]={192,168,10,2}; return IPAddress(ip); }
void reply(const char* text) { incoming[Config::TELLO_COMMAND_PORT].push_back({IPAddress(Config::TELLO_IP),8889,text}); }
void state(const char* text) { incoming[Config::TELLO_STATE_PORT].push_back({IPAddress(Config::TELLO_IP),8890,text}); }
void prepareReady(FlightStateManager& flight,TelloController& controller) {
    connected=true; assert(flight.prepare()); flight.update(); flight.update();
    reply("ok"); controller.update(); flight.update(); testMillis+=500; flight.update();
    reply("80"); controller.update(); flight.update(); testMillis+=500;
    state("h:0;tof:10;vgz:0;bat:80;"); controller.update(); flight.update();
    assert(flight.state()==FlightState::LandedReady);
}
void fly(FlightStateManager& flight,TelloController& controller) {
    prepareReady(flight,controller); assert(flight.takeoff()); reply("ok"); controller.update(); flight.update();
    testMillis+=500; assert(flight.state()==FlightState::Flying);
}
int main() {
    GatewayWiFiManager wifi;
    TelloController controller(wifi);
    assert(controller.begin());
    assert(!controller.isSdkReady());
    assert(!controller.queryBattery());
    assert(controller.requestSdkMode());
    assert(!controller.requestSdkMode());
    const uint8_t wrongIp[]={192,168,10,99};
    incoming[Config::TELLO_COMMAND_PORT].push_back({IPAddress(wrongIp),8889,"ok"});
    controller.update(); assert(!controller.isSdkReady());
    reply("ok"); controller.update();
    assert(controller.isSdkReady());
    testMillis += 500;
    assert(controller.queryBattery());
    reply("ok"); controller.update(); // unrelated ACK cannot satisfy a numeric query
    assert(controller.commandPending());
    reply("75"); controller.update();
    assert(controller.batteryPercent()==75);
    state("h:0;tof:10;vgz:0;bat:74;pitch:1;roll:2;yaw:3;time:0;");
    controller.update();
    assert(controller.hasFreshTelemetry());
    assert(controller.telemetry().landingFieldsValid);
    assert(controller.telemetry().heightCm==0);
    state("h:no;vgz:nan;bat:999;garbage"); controller.update();
    assert(!controller.telemetry().landingFieldsValid);
    testMillis += Config::TELLO_TELEMETRY_TIMEOUT_MS+1;
    assert(!controller.hasFreshTelemetry());
    testMillis += 500;
    assert(controller.queryBattery());
    for (unsigned i=0;i<=Config::TELLO_COMMAND_RETRY_COUNT;++i) {
        testMillis += Config::TELLO_COMMAND_TIMEOUT_MS+1; controller.update();
    }
    assert(!controller.commandPending());
    assert(controller.lastResult()==TelloResult::Timeout);
    connected=false; controller.update();
    assert(!controller.isSdkReady());
    assert(!controller.requestSdkMode());
    FlightStateManager flight(wifi,controller);
    assert(flight.prepare());
    assert(wifi.staTarget()==StaTarget::Tello);
    flight.update();
    testMillis+=Config::TELLO_CONNECT_TIMEOUT_MS+1; flight.update();
    assert(flight.state()==FlightState::Error);
    connected=true;
    assert(flight.prepare()); flight.update(); flight.update();
    reply("ok"); controller.update(); flight.update();
    testMillis+=500; flight.update();
    assert(outgoing.back().body=="battery?");
    reply("80"); controller.update(); flight.update();
    assert(flight.state()==FlightState::LandedReady);
    assert(outgoing.back().body=="battery?"); // Preparation never sends takeoff.
    assert(!flight.move("forward",50));
    assert(!flight.takeoff()); // No fresh evidence that the prepared aircraft is on the ground.
    state("h:0;tof:10;vgz:0;bat:24;"); controller.update();
    assert(!flight.takeoff());
    state("h:0;tof:10;vgz:0;bat:80;"); controller.update();
    testMillis+=500;
    assert(flight.takeoff());
    assert(!flight.prepare());
    assert(!flight.takeoff());
    reply("ok"); controller.update(); flight.update();
    assert(flight.state()==FlightState::Flying);
    assert(!flight.move("forward;land",50));
    assert(!flight.move("forward",19));
    assert(!flight.rotate("cw",3601));
    assert(!flight.rc(101,0,0,0));
    for(const char* direction : {"up","down","left","right","forward","back"}) {
        testMillis+=500; assert(flight.move(direction,20));
        assert(outgoing.back().body==std::string(direction)+" 20");
        reply("ok"); controller.update(); flight.update();
    }
    for(const char* direction : {"cw","ccw"}) {
        testMillis+=500; assert(flight.rotate(direction,90));
        assert(outgoing.back().body==std::string(direction)+" 90");
        reply("ok"); controller.update(); flight.update();
    }
    testMillis+=500;
    const size_t beforeRc=outgoing.size();
    assert(flight.rc(0,-25,0,0));
    assert(flight.rc(0,25,0,0)); controller.update();
    assert(outgoing.size()==beforeRc+1); // Latest unsent value replaces the earlier one.
    assert(outgoing.back().body=="rc 0 25 0 0");
    assert(controller.responseUncertain()); // RC ACKs cannot authorize ACK-only landing later.
    testMillis+=Config::TELLO_RC_MAX_AGE_MS+1; controller.update();
    assert(outgoing.back().body=="rc 0 0 0 0");
    testMillis+=Config::TELLO_TELEMETRY_TIMEOUT_MS+1; controller.update(); flight.update();
    assert(flight.state()==FlightState::Flying); // Loss is not landing.
    assert(wifi.staTarget()==StaTarget::Tello);
    assert(flight.land()); flight.update();
    assert(outgoing.back().body=="land");
    reply("ok"); controller.update(); flight.update();
    assert(flight.state()==FlightState::Landing);
    for(unsigned i=0;i<11;++i) {
        testMillis+=500; state("h:0;tof:10;vgz:0;bat:80;"); controller.update(); flight.update();
    }
    assert(flight.state()==FlightState::LandedConfirmed);
    flight.update(); flight.update();
    assert(wifi.staTarget()==StaTarget::Internet);
    assert(!controller.isSdkReady());
    assert(flight.prepare()); flight.update(); flight.update();
    reply("ok"); controller.update(); flight.update();
    testMillis+=500; flight.update(); reply("80"); controller.update(); flight.update();
    assert(flight.state()==FlightState::LandedReady);
    assert(!flight.disconnect()); // Fresh stable ground evidence required.
    for(unsigned i=0;i<12;++i) {
        testMillis+=500; state("h:0;tof:10;vgz:0;bat:80;"); controller.update(); flight.update();
    }
    assert(flight.disconnect()); flight.update(); flight.update();
    assert(wifi.staTarget()==StaTarget::Internet);
    fly(flight,controller);
    assert(flight.land()); flight.update();
    const size_t sends=outgoing.size();
    testMillis+=Config::TELLO_FLIGHT_COMMAND_TIMEOUT_MS+1; controller.update(); flight.update();
    assert(outgoing.size()==sends); // Never retry flight commands automatically.
    assert(flight.state()==FlightState::Error);
    assert(wifi.staTarget()==StaTarget::Tello);
    testMillis+=Config::TELLO_RESPONSE_QUARANTINE_MS+1;
    assert(flight.land()); flight.update(); reply("ok"); controller.update(); flight.update();
    testMillis+=100; state("h:90;tof:90;vgz:0;bat:80;"); controller.update(); flight.update();
    testMillis+=Config::TELLO_LAND_ACK_ONLY_SETTLE_MS+1; controller.update(); flight.update();
    assert(flight.state()==FlightState::Landing); // Contradictory evidence cannot disappear into a successful landing.
    assert(wifi.staTarget()==StaTarget::Tello);
    for(unsigned i=0;i<11;++i) {
        testMillis+=500; state("h:0;tof:10;vgz:0;bat:80;"); controller.update(); flight.update();
    }
    assert(flight.state()==FlightState::LandedConfirmed); flight.update(); flight.update();
    fly(flight,controller);
    assert(flight.land()); flight.update(); reply("ok"); controller.update(); flight.update();
    testMillis+=Config::TELLO_LAND_ACK_ONLY_SETTLE_MS+1; controller.update(); flight.update();
    assert(flight.state()==FlightState::LandedConfirmed);
    assert(!strcmp(flight.landingEvidence(),"LAND_ACK_AND_CONSERVATIVE_TIMEOUT"));
    flight.update(); flight.update();
    connected=false; flight.update(10,true,true);
    assert(flight.state()==FlightState::InternetConnecting);
    connected=true; flight.update(10,true,false);
    assert(flight.state()==FlightState::SyncBlocked);
    flight.update(10,true,true); assert(flight.state()==FlightState::Syncing);
    flight.update(10,true,true); assert(flight.state()==FlightState::Syncing);
    flight.update(0,false,true); assert(flight.state()==FlightState::SyncBlocked);
    flight.update(0,true,true); assert(flight.state()==FlightState::SyncComplete);
    assert(flight.prepare()); assert(wifi.staTarget()==StaTarget::Tello);
    controller.stop();
    FlightStateManager recovered(wifi,controller);
    fly(recovered,controller);
    connected=false; controller.update(); recovered.update();
    assert(recovered.state()==FlightState::Error);
    connected=true; recovered.update();
    for(unsigned i=0;i<=Config::TELLO_COMMAND_RETRY_COUNT;++i) {
        testMillis+=Config::TELLO_COMMAND_TIMEOUT_MS+1; controller.update(); recovered.update();
    }
    testMillis+=Config::INTERNET_RECONNECT_INTERVAL_MS; recovered.update();
    assert(controller.commandPending()); // Failed recovery must rearm after bounded backoff.
    reply("ok"); controller.update(); recovered.update();
    testMillis+=500;
    assert(recovered.land()); recovered.update(); reply("ok"); controller.update(); recovered.update();
    testMillis+=Config::TELLO_LAND_ACK_ONLY_SETTLE_MS+1; controller.update(); recovered.update();
    assert(recovered.state()==FlightState::Landing); // Reconnect must not erase flight uncertainty.
    controller.stop();
    testMillis=0xfffffff0u;
    assert(controller.begin()); assert(controller.requestSdkMode());
    testMillis+=Config::TELLO_COMMAND_TIMEOUT_MS+1; controller.update();
    assert(controller.commandPending());
    testMillis+=Config::TELLO_COMMAND_TIMEOUT_MS+1; controller.update();
    assert(controller.lastResult()==TelloResult::Timeout);
    std::cout << "Tello protocol, flight guards and landing tests passed\n";
}
