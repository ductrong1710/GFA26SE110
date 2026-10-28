#pragma once
#include "Config.h"
#include "TelloTelemetry.h"
#include "FlightSafetyController.h"
#include <stdio.h>

enum class AltitudeAction { Wait, Pulse, Up20, Down20, Done, Failed };

// Pure decisions only. The coordinator owns transmission and pulse duration.
class AltitudeController {
public:
    void reset() { *this=AltitudeController(); }
    void begin(int target,const TelloTelemetry& t,uint32_t now) {
        reset();target_=target;active_=true;started_=reassessAt_=now;
        observed_=excluded_=t.sample;initialHeight_=maximumHeight_=t.heightCm;
        if(target<=0 || target<Config::TELLO_MIN_OPERATION_ALTITUDE_CM ||
           target>Config::TELLO_MAX_ALTITUDE_CM || Config::TELLO_MAX_ALTITUDE_CM>120)
            actionFailed("INVALID_ALTITUDE_CONFIGURATION");
    }
    AltitudeAction update(const TelloTelemetry& t,const FlightSafetyController& safety,
                          bool fresh,uint32_t now) {
        if(!active_) return result_;
        if(uint32_t(now-started_)>=Config::TELLO_ALTITUDE_STABILIZE_TIMEOUT_MS) {
            actionFailed(maximumHeight_<=initialHeight_ ? "ALTITUDE_TIMEOUT_NO_PROGRESS" : "ALTITUDE_TIMEOUT_NOT_STABLE");
            return result_;
        }
        if(!safety.configurationValid()) {actionFailed("INVALID_ALTITUDE_CONFIGURATION");return result_;}
        if(safety.disagreementConfirmed()) {actionFailed("ALTITUDE_SENSOR_DISAGREEMENT");return result_;}
        if(!fresh || !t.valid || t.heightCm<0 || !safety.altitudeFresh() ||
           uint32_t(now-t.lastReceivedMs)>Config::TELLO_TELEMETRY_TIMEOUT_MS || safety.sensorsDisagree()) {
            stable_=false;pending_=AltitudeAction::Wait;vertical_=0;
            return AltitudeAction::Wait;
        }
        if(t.heightCm>maximumHeight_) maximumHeight_=t.heightCm;
        if(waiting_) return AltitudeAction::Wait;
        if(uint32_t(now-reassessAt_)<Config::TELLO_VERTICAL_REASSESS_MS || t.sample==excluded_ ||
           int32_t(t.lastReceivedMs-reassessAt_)<int32_t(Config::TELLO_VERTICAL_REASSESS_MS))
            return AltitudeAction::Wait;
        const int delta=target_-t.heightCm;
        const bool inBand=delta>=-Config::TELLO_ALTITUDE_TOLERANCE_CM && delta<=Config::TELLO_ALTITUDE_TOLERANCE_CM;
        const bool newSample=t.sample!=observed_;
        if(inBand) {
            pending_=AltitudeAction::Wait;vertical_=0;
            if(!safety.inOperatingRange() || !t.landingFieldsValid || t.verticalVelocity < -Config::TELLO_LAND_MAX_VERTICAL_SPEED ||
               t.verticalVelocity > Config::TELLO_LAND_MAX_VERTICAL_SPEED) stable_=false;
            else if(newSample) {
                if(!stable_ || uint32_t(t.lastReceivedMs-stableLast_)>Config::TELLO_TELEMETRY_TIMEOUT_MS) {
                    stable_=true;stableSince_=t.lastReceivedMs;
                }
                stableLast_=t.lastReceivedMs;
                if(uint32_t(stableLast_-stableSince_)>=Config::TELLO_ALTITUDE_STABLE_MS) {
                    active_=false;result_=AltitudeAction::Done;
                }
            }
        } else {
            stable_=false;
            if(newSample || pending_!=AltitudeAction::Wait) {
                if(delta>0) {
                    vertical_=0;
                    // SDK up has a 20 cm minimum. Never overshoot the accepted
                    // band or the conservative H/ToF ceiling to force ascent.
                    pending_=(20<=delta+Config::TELLO_ALTITUDE_TOLERANCE_CM && safety.allowMove("up",20))
                        ? AltitudeAction::Up20 : AltitudeAction::Wait;
                } else if(delta<=-20 && safety.allowMove("down",20)) {
                    pending_=AltitudeAction::Down20;vertical_=0;
                } else {
                    vertical_=safety.filterVerticalRc(-5);
                    pending_=vertical_ ? AltitudeAction::Pulse : AltitudeAction::Wait;
                }
            }
        }
        observed_=t.sample;
        return active_ ? pending_ : result_;
    }
    int vertical() const {return vertical_;}
    void invalidateStability() {stable_=false;}
    const char* error() const {return error_;}
    void actionStarted() {
        if(active_ && (pending_==AltitudeAction::Pulse || pending_==AltitudeAction::Up20 || pending_==AltitudeAction::Down20)) {
            waiting_=true;pending_=AltitudeAction::Wait;stable_=false;
        }
    }
    void actionFinished(const TelloTelemetry& t,uint32_t now) {
        if(!active_ || !waiting_) return;
        waiting_=false;vertical_=0;reassessAt_=now;excluded_=observed_=t.sample;
    }
    void actionFailed(const char* error) {
        if(!active_) return;
        snprintf(error_,sizeof(error_),"%s",error ? error : "ALTITUDE_ACTION_FAILED");
        active_=waiting_=stable_=false;vertical_=0;pending_=AltitudeAction::Wait;result_=AltitudeAction::Failed;
    }
    bool active() const {return active_;}
    int target() const {return target_;}
    int initialHeight() const {return initialHeight_;}
    int maximumHeight() const {return maximumHeight_;}
private:
    bool active_=false,waiting_=false,stable_=false;
    int target_=0,vertical_=0,initialHeight_=-1,maximumHeight_=-1;
    uint32_t started_=0,reassessAt_=0,excluded_=0,observed_=0,stableSince_=0,stableLast_=0;
    AltitudeAction pending_=AltitudeAction::Wait,result_=AltitudeAction::Wait;
    char error_[80]={0};
};
