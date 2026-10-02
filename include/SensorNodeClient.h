#pragma once
#include "NodeAuthenticator.h"
#include "SensorNode.h"
#include <ArduinoJson.h>
class SensorNodeClient {
public:
    explicit SensorNodeClient(const NodeAuthenticator& auth) : auth_(auth) {}
    bool getInfo(const SensorNode& node, uint32_t& pending);
    bool getData(const SensorNode& node, JsonDocument& response);
    bool acknowledge(const SensorNode& node, const String* ids, size_t count);
    bool getHealth(const SensorNode& node, JsonDocument& response);
    bool sendTime(const SensorNode& node, uint64_t unixTime);
private:
    const NodeAuthenticator& auth_;
    bool call(const SensorNode& node, const String& path, const char* method,
              const String& body, JsonDocument& response, bool allowEmpty = false);
};
