#pragma once
#include <Arduino.h>
#include "Config.h"
#include "TelloTelemetry.h"
struct AltitudeLimits {
    int maximum,target,minimum;
    AltitudeLimits(int max=Config::TELLO_MAX_ALTITUDE_CM,int desired=Config::TELLO_TARGET_ALTITUDE_CM,
        int min=Config::TELLO_MIN_OPERATION_ALTITUDE_CM):maximum(max),target(desired),minimum(min) {}
    bool valid() const {return maximum>0 && target>0 && minimum>0 && minimum<=target && target<=maximum;}
};
class FlightSafetyController {
public:
    explicit FlightSafetyController(AltitudeLimits limits=AltitudeLimits()):limits_(limits) {}
    bool begin();
    void update(const TelloTelemetry& telemetry,bool packetFresh);
    bool configurationValid() const {return limits_.valid();}
    bool altitudeFresh() const {return fresh_;}
    int altitudeCm() const {return fresh_ ? altitude_ : -1;}
    bool limitActive() const {return fresh_ && upper_>=limits_.maximum;}
    bool inOperatingRange() const {return configurationValid() && fresh_ && altitude_>=limits_.minimum && upper_<=limits_.maximum;}
    bool allowMove(const char* direction,int cm) const;
    int filterVerticalRc(int value) const;
    const char* error() const;
private:
    AltitudeLimits limits_;
    bool fresh_=false;
    int altitude_=-1,upper_=-1,lastLog_=-1;
};
