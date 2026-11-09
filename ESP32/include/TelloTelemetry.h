#pragma once
#include <stdint.h>
#include <stddef.h>
struct TelloTelemetry {
    int battery = -1, heightCm = -1, tofCm = -1, flightTimeSec = -1;
    int verticalVelocity = 0, pitch = 0, roll = 0, yaw = 0;
    bool valid = false, landingFieldsValid = false;
    uint32_t lastReceivedMs = 0, sample = 0;
    bool parse(const char* packet, size_t length, uint32_t now);
};
