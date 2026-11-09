#pragma once
#include "SensorNode.h"

enum class RegistrationResult { Added, Updated, Full, Unauthenticated };

class NodeRegistry {
public:
    RegistrationResult addOrUpdate(const SensorNode& node, uint32_t now = millis());
    const SensorNode* findNode(const String& deviceCode) const;
    const SensorNode* at(size_t index) const;
    size_t count() const { return count_; }
    size_t onlineCount() const;
    bool touchNode(const String& deviceCode, uint32_t now = millis());
    void update(uint32_t now = millis());
private:
    SensorNode nodes_[Config::MAX_SENSOR_NODES];
    size_t count_ = 0;
};
