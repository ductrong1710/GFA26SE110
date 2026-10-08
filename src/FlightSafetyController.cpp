#include "FlightSafetyController.h"
#include <algorithm>
#include <cstring>
bool FlightSafetyController::begin() {
    if(!configurationValid()) Serial.printf("[SAFETY] Invalid altitude configuration; autonomous start disabled\n");
    return configurationValid();
}
void FlightSafetyController::update(const TelloTelemetry& t,bool packetFresh) {
    fresh_=packetFresh && (t.heightCm>=0 || t.tofCm>=0);
    altitude_=t.heightCm>=0 ? t.heightCm : t.tofCm;
    // The higher reading blocks ascent if barometric height and ground range disagree.
    upper_=std::max(t.heightCm,t.tofCm);
    int reason=!configurationValid() ? 3 : !fresh_ ? 1 : limitActive() ? 2 : 0;
    if(reason!=lastLog_) {
        if(reason==1) Serial.printf("[SAFETY] Altitude telemetry stale; ascent blocked\n");
        if(reason==2) Serial.printf("[SAFETY] Maximum altitude reached: %d cm\n[SAFETY] Ascent blocked\n",limits_.maximum);
        lastLog_=reason;
    }
}
bool FlightSafetyController::allowMove(const char* direction,int cm) const {
    if(!strcmp(direction,"up")) return configurationValid() && fresh_ && cm>0 && cm<=limits_.maximum-upper_;
    if(!strcmp(direction,"down")) return configurationValid() && fresh_ && cm>0 && cm<=altitude_-limits_.minimum;
    return true;
}
int FlightSafetyController::filterVerticalRc(int value) const {
    if(!value) return 0;
    if(!configurationValid() || !fresh_) return 0;
    if(value<0) return altitude_<=limits_.minimum ? 0 : std::max(value,-10);
    if(upper_>=limits_.maximum) return 0;
    const int slow=std::max(0,limits_.maximum-10),verySlow=std::max(0,limits_.maximum-5);
    if(upper_>=verySlow) return std::min(value,3);
    if(upper_>=slow) return std::min(value,10);
    return value;
}
const char* FlightSafetyController::error() const {
    return !configurationValid() ? "INVALID_ALTITUDE_CONFIGURATION" : !fresh_ ? "ALTITUDE_TELEMETRY_STALE" : "ALTITUDE_LIMIT";
}
