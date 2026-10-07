#include "TelloTelemetry.h"
#include <stdlib.h>
#include <string.h>
#include <errno.h>

bool TelloTelemetry::parse(const char* packet, size_t length, uint32_t now) {
    if (!length || length > 511 || memchr(packet, '\0', length)) return false;
    char buffer[512]; memcpy(buffer, packet, length); buffer[length] = 0;
    TelloTelemetry next;
    bool height = false, tof = false, velocity = false, any = false;
    char* context = nullptr;
    for (char* item = strtok_r(buffer, ";\r\n", &context); item; item = strtok_r(nullptr, ";\r\n", &context)) {
        char* colon = strchr(item, ':');
        if (!colon) continue;
        *colon++ = 0;
        char* end; errno = 0;
        const long n = strtol(colon, &end, 10);
        if (errno || colon == end || *end || n < -32768 || n > 32767) continue;
        if (!strcmp(item,"bat") && n>=0 && n<=100) { next.battery=n; any=true; }
        else if (!strcmp(item,"h") && n>=0 && n<=3000) { next.heightCm=n; height=any=true; }
        else if (!strcmp(item,"tof") && n>=0 && n<=10000) { next.tofCm=n; tof=any=true; }
        else if (!strcmp(item,"vgz") && n>=-1000 && n<=1000) { next.verticalVelocity=n; velocity=any=true; }
        else if (!strcmp(item,"time") && n>=0) { next.flightTimeSec=n; any=true; }
        else if (!strcmp(item,"pitch") && n>=-180 && n<=180) { next.pitch=n; any=true; }
        else if (!strcmp(item,"roll") && n>=-180 && n<=180) { next.roll=n; any=true; }
        else if (!strcmp(item,"yaw") && n>=-180 && n<=180) { next.yaw=n; any=true; }
    }
    // Never combine old height/velocity with a new partial packet for landing.
    next.landingFieldsValid = velocity && (height || tof);
    next.valid = any;
    next.lastReceivedMs = now;
    next.sample = sample + 1;
    *this = next;
    return any;
}
