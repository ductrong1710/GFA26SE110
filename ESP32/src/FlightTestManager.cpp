#include "FlightTestManager.h"
#include <cstring>
#include <cstdio>

namespace { constexpr const char* journal="/flight-test.json";constexpr const char* temporary="/flight-test.tmp"; }
bool FlightTestManager::save() {
    StaticJsonDocument<768> d;d["version"]=1;d["gatewayId"]=gatewayId_;d["id"]=id_;d["phase"]=phase_;
    d["uavConnected"]=uavConnected_;d["landed"]=landed_;d["observedAltitudeCm"]=altitude_;d["heldMs"]=heldMs_;d["errorCode"]=error_;
    d["takeoffAttempted"]=takeoffAttempted_;
    char buffer[768];size_t n=serializeJson(d,buffer,sizeof(buffer));
    char checked[768];size_t actual=0;
    return ready_ && !d.overflowed() && n==measureJson(d) && n<sizeof(buffer) && files_.write(temporary,buffer,n) &&
        files_.read(temporary,checked,sizeof(checked),actual) && actual==n && !memcmp(buffer,checked,n) &&
        files_.replace(temporary,journal) && files_.read(journal,checked,sizeof(checked),actual) && actual==n && !memcmp(buffer,checked,n);
}
void FlightTestManager::recover() {
    recovery_=true;phase_=2;landing_=true;takeoffAttempted_=true;
    if(!error_[0])snprintf(error_,sizeof(error_),"%s","REBOOT_OR_CLAIM_UNCERTAIN");
    flight_.restoreRecovery(FlightOwner::FlightTest);wifi_.requestStaTarget(StaTarget::Tello);
}
bool FlightTestManager::begin(int gatewayId) {
    gatewayId_=gatewayId;ready_=files_.begin();
    if(!ready_){recover();return false;}
    if(!files_.exists(journal) && !files_.exists(temporary)) return true;
    char buffer[768];size_t n=0;StaticJsonDocument<768> d;
    if(!files_.read(journal,buffer,sizeof(buffer),n) || deserializeJson(d,buffer,n) ||
       d["version"]!=1 || !d["gatewayId"].is<int>() || !d["phase"].is<int>() ||
       !d["id"].is<int>() || d["id"].as<int>()<=0 || d["phase"].as<int>()<1 || d["phase"].as<int>()>4 ||
       !d["uavConnected"].is<bool>() || !d["landed"].is<bool>() || !d["takeoffAttempted"].is<bool>() ||
       !d["observedAltitudeCm"].is<int>() || !d["heldMs"].is<uint32_t>() || d["heldMs"].as<uint32_t>()>60000 ||
       !d["errorCode"].is<const char*>() || strlen(d["errorCode"].as<const char*>())>=sizeof(error_)){recover();return false;}
    if(d["gatewayId"]!=gatewayId_) {
        if(d["phase"]==4)return true; // Previous identity's result was already acknowledged.
        recover();return false;
    }
    id_=d["id"];phase_=d["phase"];uavConnected_=d["uavConnected"]|false;landed_=d["landed"]|false;
    altitude_=d["observedAltitudeCm"]|0;heldMs_=d["heldMs"]|0;
    takeoffAttempted_=d["takeoffAttempted"];
    snprintf(error_,sizeof(error_),"%s",d["errorCode"]|"");
    if(takeoffAttempted_ && !landed_) {recover();return true;}
    if(phase_!=3 && phase_!=4) {
        if(takeoffAttempted_)recover();
        else {snprintf(error_,sizeof(error_),"INTERRUPTED_BEFORE_TAKEOFF");finish();}
    }
    return true;
}
bool FlightTestManager::request(const char* suffix,const char* method,const JsonDocument* body,JsonDocument& response) {
    char path[128];if(!strcmp(suffix,"next"))snprintf(path,sizeof(path),"/device/gateways/%d/flight-tests/next",gatewayId_);
    else snprintf(path,sizeof(path),"/device/gateways/%d/flight-tests/%d/%s",gatewayId_,id_,suffix);
    lastHttpStatus_=0;
    return transport_.request(path,method,body,response,lastHttpStatus_) && lastHttpStatus_>=200 && lastHttpStatus_<300 &&
        response["success"].is<bool>() && response["success"].as<bool>() && !response.overflowed();
}
bool FlightTestManager::command(JsonVariantConst d,const char* status) const {
    return d.is<JsonObjectConst>() && d["id"].is<int>() && d["id"].as<int>()>0 &&
        d["gatewayId"].is<int>() && d["gatewayId"].as<int>()==gatewayId_ && d["uavId"].is<int>() && d["uavId"].as<int>()>0 &&
        d["altitudeCm"].is<int>() && d["altitudeCm"].as<int>()==100 &&
        d["holdMs"].is<int>() && d["holdMs"].as<int>()==3000 && !strcmp(d["status"]|"",status);
}
void FlightTestManager::fail(const char* reason) {
    if(!error_[0])snprintf(error_,sizeof(error_),"%s",reason);
    holding_=false;landing_=true;flight_.stopMotion();
    if(flight_.mayBeAirborne()) {flight_.requireTelemetryLanding(true);flight_.land();}
    else if(flight_.state()!=FlightState::LandedReady) {flight_.cancelPreparation(FlightOwner::FlightTest);finish();}
}
void FlightTestManager::finish() {
    phase_=3;tello_.setFlightTestLimits(false);
    if(!save()){phase_=2;recovery_=true;snprintf(error_,sizeof(error_),"RESULT_STORAGE_FAILED");}
}
void FlightTestManager::update(bool idle) {
    if(phase_==0 || phase_==4) {
#if !defined(GATEWAY_FLIGHT_TEST_PROFILE) || !GATEWAY_FLIGHT_TEST_PROFILE
        return;
#endif
        if(Config::GROUND_TEST_MODE)return;
        if(!ready_ || !idle || gatewayId_<=0 || !transport_.configured() || !wifi_.isInternetNetworkReady())return;
        if(attempted_ && uint32_t(millis()-attemptAt_)<5000)return;
        attempted_=true;attemptAt_=millis();StaticJsonDocument<1536> response;
        if(!request("next","GET",nullptr,response) || !command(response["data"],"QUEUED"))return;
        const int next=response["data"]["id"];if(next==id_)return;
        id_=next;phase_=1;takeoffAttempted_=false;uavConnected_=landed_=holding_=landing_=recovery_=false;heldMs_=0;altitude_=0;error_[0]=0;
        // A crash from this point never replays a claim or takeoff.
        if(!save()){recover();return;}
        StaticJsonDocument<16> body;body.to<JsonObject>();response.clear();
        const uint32_t claimAt=millis();
        if(!request("claim","POST",&body,response) || !command(response["data"],"CLAIMED") || response["data"]["id"]!=id_) {
            if(lastHttpStatus_>=400 && lastHttpStatus_<500) {phase_=4;if(!save())recover();}
            else fail("CLAIM_RESPONSE_UNCERTAIN"); // No takeoff has been attempted; report the interrupted claim.
            return;
        }
        phase_=2;startedAt_=millis();
        if(!save()){recover();return;}
        // The server checks TTL atomically; a slow response is never a flight permission.
        if(uint32_t(millis()-claimAt)>5000){fail("CLAIM_RESPONSE_TOO_OLD");return;}
        if(Config::GROUND_TEST_MODE || !tello_.setFlightTestLimits(true)){fail("FLIGHT_TEST_PROFILE_REQUIRED");return;}
        flight_.requireTelemetryLanding(true);
        if(!flight_.prepare(FlightOwner::FlightTest))fail(flight_.error());
        return;
    }
    if(phase_==3 && takeoffAttempted_ && !landed_) {recover();return;}
    if(phase_==3) {
        if(!wifi_.isInternetNetworkReady() || (attempted_ && uint32_t(millis()-attemptAt_)<5000))return;
        attempted_=true;attemptAt_=millis();StaticJsonDocument<768> body;StaticJsonDocument<1536> response;
        const char* status=error_[0] ? (takeoffAttempted_ && !landed_ ? "RECOVERY_REQUIRED" : "FAILED") : "COMPLETED";
        body["status"]=status;body["gatewayConnected"]=true;body["uavConnected"]=uavConnected_;body["landed"]=landed_;
        body["observedAltitudeCm"]=altitude_;body["heldMs"]=heldMs_;
        body["takeoffAttempted"]=takeoffAttempted_;
        if(error_[0])body["errorCode"]=error_;else body["errorCode"]=nullptr;
        if(request("result","POST",&body,response) && response["data"]["id"]==id_ &&
           !strcmp(response["data"]["status"]|"",status)) {phase_=4;if(!save())phase_=3;}
        return;
    }
    if(flight_.state()==FlightState::LandedConfirmed) {landed_=true;uavConnected_=true;finish();return;}
    if(recovery_) {
        // Retain Tello radio and the exclusive lock until telemetry confirms ground.
        if(!flight_.mayBeAirborne()) {finish();return;}
        if(flight_.state()!=FlightState::Landing && tello_.isSdkReady() && !tello_.commandPending())flight_.land();
        return;
    }
    if(landing_) {
        if(!flight_.mayBeAirborne() && flight_.state()==FlightState::LandedReady) {flight_.disconnect(FlightOwner::FlightTest);return;}
        if(flight_.state()==FlightState::Error){recovery_=true;if(!error_[0])snprintf(error_,sizeof(error_),"LANDING_UNCONFIRMED");save();}
        return;
    }
    if(uint32_t(millis()-startedAt_)>60000){fail("FLIGHT_TEST_DEADLINE");return;}
    if(flight_.state()==FlightState::Error){fail(flight_.error());return;}
    if(flight_.mayBeAirborne() && tello_.testOvershoot_!=overshoot_){fail("ALTITUDE_OVERSHOOT");return;}
    if(flight_.mayBeAirborne() && tello_.testLowBattery_!=lowBattery_){fail("BATTERY_TOO_LOW");return;}
    if(flight_.state()==FlightState::LandedReady) {
        uavConnected_=true;
        if(uint32_t(millis()-startedAt_)>45000){fail("PREPARATION_DEADLINE");return;}
        if(!tello_.hasFreshTelemetry() || !tello_.canSend())return;
        if(tello_.batteryPercent()<Config::TELLO_MIN_TAKEOFF_BATTERY_PERCENT){fail("BATTERY_TOO_LOW");return;}
        outOfBand_=tello_.testOutOfBand_;overshoot_=tello_.testOvershoot_;lowBattery_=tello_.testLowBattery_;
        takeoffAttempted_=true;
        if(!save()){takeoffAttempted_=false;fail("TAKEOFF_JOURNAL_FAILED");return;}
        if(!flight_.takeoff(FlightOwner::FlightTest))fail(flight_.error());
        return;
    }
    if(flight_.state()!=FlightState::Flying)return;
    if(!tello_.hasFreshTelemetry() || !tello_.telemetry().landingFieldsValid){fail("ALTITUDE_TELEMETRY_STALE");return;}
    if(tello_.batteryPercent()<Config::TELLO_MIN_TAKEOFF_BATTERY_PERCENT){fail("BATTERY_TOO_LOW");return;}
    const auto& t=tello_.telemetry();
    if(tello_.testOutOfBand_!=outOfBand_){holding_=false;heldMs_=0;outOfBand_=tello_.testOutOfBand_;}
    if(t.heightCm<0 || t.tofCm<0){fail("ALTITUDE_EVIDENCE_MISSING");return;}
    if(t.heightCm>110 || t.tofCm>110){fail("ALTITUDE_OVERSHOOT");return;}
    const bool atTarget=abs(t.heightCm-100)<=3 && abs(t.tofCm-100)<=3 && abs(t.verticalVelocity)<=5;
    if(!atTarget) {holding_=false;heldMs_=0;flight_.rc(0,0,t.heightCm<97 && t.tofCm<97 ? 10 : -10,0,FlightOwner::FlightTest);return;}
    flight_.stopMotion();
    if(t.sample==sample_)return;
    if(!holding_ || uint32_t(t.lastReceivedMs-lastSampleAt_)>Config::TELLO_TELEMETRY_TIMEOUT_MS){holding_=true;holdAt_=t.lastReceivedMs;}
    sample_=t.sample;lastSampleAt_=t.lastReceivedMs;altitude_=t.heightCm;
    heldMs_=t.lastReceivedMs-holdAt_;
    if(heldMs_>=3000){heldMs_=3000;landing_=true;if(!flight_.land())fail(flight_.error());}
}
