#pragma once
#include "RuntimeConfig.h"
#include <cassert>
inline void provisionTestGateway() {
    auto& config=RuntimeConfig::instance();
    assert(config.begin());
    GatewaySettings s;
    s.internetSsid="test-wifi";s.internetPassword="test-password";
    s.backendBaseUrl="http://192.168.1.10:5000/api";
    s.gatewayId=1;s.gatewayCode="GW-ESP32-01";
    s.apiKey="abcdefghijklmnopqrstuvwxyz0123456789";
    assert(config.save(s));assert(config.begin());
}
