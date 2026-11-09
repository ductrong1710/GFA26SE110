#include "RuntimeConfig.h"
#include "Preferences.h"
#include "ConfigurationSafety.h"
#include <cassert>
#include <iostream>
TestSerial Serial;
namespace FakeNvs { std::map<std::string,std::string> entries; bool openFails=false,writeFails=false; }
int main() {
    assert(configurationSafe(FlightState::IdleInternet,false,false,MissionState::Empty));
    assert(configurationSafe(FlightState::SyncBlocked,false,false,MissionState::Syncing));
    assert(!configurationSafe(FlightState::IdleInternet,true,false,MissionState::Empty));
    assert(!configurationSafe(FlightState::IdleInternet,false,true,MissionState::Empty));
    for(auto state:{MissionState::Ready,MissionState::PreparingTello,MissionState::TakingOff,MissionState::Executing,MissionState::Landing,MissionState::RecoveryRequired})
        assert(!configurationSafe(FlightState::IdleInternet,false,false,state));
    for(auto state:{FlightState::ConnectingTello,FlightState::LandedReady,FlightState::TakingOff,FlightState::Flying,FlightState::Landing,FlightState::Error})
        assert(!configurationSafe(state,false,false,MissionState::Empty));
    auto& config=RuntimeConfig::instance();
    assert(config.begin());
    GatewaySettings s;
    s.internetSsid="field-wifi";s.internetPassword="wifi-password";
    s.backendBaseUrl="http://192.168.1.10:5043/api";s.gatewayCode="GW-ESP32-01";
    s.gatewayId=11;s.apiKey="abcdefghijklmnopqrstuvwxyz0123456789abcdefg";
    assert(!RuntimeConfig::validate(s));
    config.setIdentityChangeAllowed(false);
    assert(!config.save(s));
    assert(!config.save(s,true)); // Explicit confirmation cannot bypass storage/mission guards.
    config.setPendingAdoptionAllowed(true);
    FakeNvs::writeFails=true;
    assert(!config.save(s,true));
    assert(config.values().gatewayId!=11);
    FakeNvs::writeFails=false;
    assert(config.save(s,true)); // First provisioning can retain legacy pending test records.
    assert(config.values().gatewayId!=11);
    assert(config.begin());
    GatewaySettings different=s;different.gatewayId=12;
    assert(!config.save(different,true)); // A provisioned gateway cannot be rebound this way.
    config.setIdentityChangeAllowed(true);
    assert(config.save(s));
    assert(config.begin());assert(config.values().gatewayId==11);
    assert(config.values().apiKey==s.apiKey);
    GatewaySettings changed=s;changed.gatewayId=12;
    config.setIdentityChangeAllowed(false);
    assert(!config.save(changed));
    changed=s;changed.apiKey="a-new-key-abcdefghijklmnopqrstuvwxyz12345678";
    assert(config.save(changed)); // Rotated keys can recover pending uploads.
    assert(config.begin());assert(config.values().apiKey==changed.apiKey);
    FakeNvs::writeFails=true;assert(!config.save(s));FakeNvs::writeFails=false;
    assert(config.begin());assert(config.values().apiKey==changed.apiKey);
    const char* invalidUrls[]={"https://example.com/api","http://localhost:5000/api","http://127.0.0.1/api","http://user:pass@host/api","http://host/api?key=x","http://host/api\r\nX: y","http:///api","http://host:bad/api"};
    for(auto url:invalidUrls){changed=s;changed.backendBaseUrl=url;assert(RuntimeConfig::validate(changed));}
    changed=s;changed.gatewayCode=std::string(64,'G').c_str();assert(!RuntimeConfig::validate(changed));
    changed.gatewayCode=std::string(65,'G').c_str();assert(RuntimeConfig::validate(changed));
    changed=s;changed.gatewayCode="bad\r\nheader";assert(RuntimeConfig::validate(changed));
    changed=s;changed.gatewayId=0;assert(RuntimeConfig::validate(changed));
    changed=s;changed.apiKey="short";assert(RuntimeConfig::validate(changed));
    changed=s;changed.internetPassword="";assert(!RuntimeConfig::validate(changed));
    FakeNvs::entries["settings"]="corrupted";
    assert(!config.begin());assert(config.values().apiKey.isEmpty());assert(config.values().gatewayId==0);
    assert(!config.canAdoptPending()); // Corrupt identity is not first-time provisioning.
    FakeNvs::openFails=true;assert(!config.begin());assert(!config.save(s));
    std::cout<<"Runtime configuration persistence, validation and failure tests passed\n";
}
