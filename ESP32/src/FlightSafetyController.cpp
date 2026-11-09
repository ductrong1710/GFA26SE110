#include "FlightSafetyController.h"
#include <algorithm>
#include <cstring>
bool FlightSafetyController::begin() {
    if(!configurationValid()) Serial.printf("[SAFETY] Invalid altitude configuration; autonomous start disabled\n");
    return configurationValid();
}
void FlightSafetyController::update(const TelloTelemetry& t,bool packetFresh) {
    // Keep H as the control source. A missing H must not silently switch to ToF.
    fresh_=packetFresh && t.heightCm>=0;
    altitude_=t.heightCm;
    // Preserve the conservative ceiling using both reported height and ToF range.
    upper_=std::max(t.heightCm,t.tofCm);
    const int difference=t.heightCm>=0 && t.tofCm>=0 ? abs(t.heightCm-t.tofCm) : -1;
    disagree_=fresh_ && difference>Config::TELLO_ALTITUDE_DISAGREEMENT_CM;
    if(!disagree_) {
        disagreementTracking_=disagreementConfirmed_=false;
    } else if(!disagreementTracking_ || observedSample_!=t.sample) {
        if(!disagreementTracking_ || uint32_t(t.lastReceivedMs-disagreementLast_)>Config::TELLO_TELEMETRY_TIMEOUT_MS) {
            disagreementSince_=t.lastReceivedMs;disagreementConfirmed_=false;
        }
        disagreementTracking_=true;observedSample_=t.sample;disagreementLast_=t.lastReceivedMs;
        disagreementConfirmed_=uint32_t(t.lastReceivedMs-disagreementSince_)>=Config::TELLO_ALTITUDE_DISAGREEMENT_MS;
    }
    int reason=!configurationValid() ? 3 : !fresh_ ? 1 : disagree_ ? 4 : limitActive() ? 2 : 0;
    if(reason!=lastLog_ || uint32_t(millis()-logAt_)>=1000) {
        Serial.printf("[ALTITUDE] h=%d tof=%d source=H selected=%d configuredTarget=%d ceiling=%d difference=%d decision=%s reason=%s\n",
            t.heightCm,t.tofCm,altitudeCm(),limits_.target,limits_.maximum,difference,
            reason ? "BLOCK_ASCENT" : "ALLOW_CONTROL",reason ? error() : "OK");
        logAt_=millis();
    }
    if(reason!=lastLog_) {
        if(reason==1) Serial.printf("[SAFETY] Altitude telemetry stale; ascent blocked\n");
        if(reason==2) Serial.printf("[SAFETY] Maximum altitude reached: %d cm\n[SAFETY] Ascent blocked\n",limits_.maximum);
        lastLog_=reason;
    }
}
bool FlightSafetyController::allowMove(const char* direction,int cm) const {
    if(!strcmp(direction,"up")) return configurationValid() && fresh_ && !disagree_ && cm>0 && cm<=limits_.maximum-upper_;
    if(!strcmp(direction,"down")) return configurationValid() && fresh_ && !disagree_ && cm>0 && cm<=altitude_-limits_.minimum;
    return true;
}
int FlightSafetyController::filterVerticalRc(int value) const {
    if(!value) return 0;
    if(!configurationValid() || !fresh_ || disagree_) return 0;
    if(value<0) return altitude_<=limits_.minimum ? 0 : std::max(value,-10);
    if(upper_>=limits_.maximum) return 0;
    const int slow=std::max(0,limits_.maximum-10),verySlow=std::max(0,limits_.maximum-5);
    if(upper_>=verySlow) return std::min(value,3);
    if(upper_>=slow) return std::min(value,10);
    return value;
}
const char* FlightSafetyController::error() const {
    return !configurationValid() ? "INVALID_ALTITUDE_CONFIGURATION" : !fresh_ ? "ALTITUDE_TELEMETRY_STALE_OR_H_MISSING" :
        disagree_ ? "ALTITUDE_SENSOR_DISAGREEMENT" : "ALTITUDE_LIMIT";
}
