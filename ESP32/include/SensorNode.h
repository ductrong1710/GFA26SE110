#pragma once
#include <Arduino.h>
#include <IPAddress.h>
#include "Config.h"

struct SensorNode {
    String deviceCode;
    int farmId = 0;
    int zoneId = 0;
    String firmwareVersion;
    String deviceType;
    IPAddress ipAddress;
    bool authenticated = false;
    bool online = false;
    uint32_t lastSeenMs = 0;
    int rssi = Config::UNKNOWN_NODE_RSSI;
};
