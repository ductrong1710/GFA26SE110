#include "GatewayProtocol.h"
#include "JsonSafety.h"
#include <assert.h>
#include <stdio.h>
int main() {
    StaticJsonDocument<768> doc;
    const char* success = "{\"success\":true,\"message\":\"Node registered\",\"deviceCode\":\"SENSOR-001\",\"authenticated\":true}";
    assert(JsonSafety::parse(success,strlen(success),doc));
    assert(GatewayProtocol::registered(doc));
    doc["success"] = false; assert(!GatewayProtocol::registered(doc));
    doc["success"] = true; doc["authenticated"] = false; assert(!GatewayProtocol::registered(doc));
    doc["authenticated"] = true; doc["deviceCode"] = "OTHER"; assert(!GatewayProtocol::registered(doc));
    GatewayProtocol::registration(doc,"192.168.4.2");
    assert(MeasurementCodec::textEquals(doc["deviceCode"],"SENSOR-001"));
    assert(MeasurementCodec::textEquals(doc["ipAddress"],"192.168.4.2"));
    assert(doc["farmId"].as<int>() == 1 && doc["zoneId"].as<int>() == 1);
    assert(!doc.containsKey("deviceSecret"));
    puts("gateway_test: passed");
}
