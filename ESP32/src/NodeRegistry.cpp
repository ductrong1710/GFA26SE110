#include "NodeRegistry.h"
#include "Logger.h"

RegistrationResult NodeRegistry::addOrUpdate(const SensorNode& node, uint32_t now) {
    if (!node.authenticated || node.deviceCode.isEmpty()) return RegistrationResult::Unauthenticated;
    size_t index = 0;
    while (index < count_ && nodes_[index].deviceCode != node.deviceCode) ++index;
    const bool existing = index < count_;
    if (!existing && count_ == Config::MAX_SENSOR_NODES) return RegistrationResult::Full;
    nodes_[index] = node;
    nodes_[index].online = true;
    nodes_[index].lastSeenMs = now;
    if (!existing) ++count_;
    Logger::node((node.deviceCode + (existing ? " registration updated" : " added to registry")).c_str());
    return existing ? RegistrationResult::Updated : RegistrationResult::Added;
}

const SensorNode* NodeRegistry::findNode(const String& deviceCode) const {
    for (size_t i = 0; i < count_; ++i) {
        if (nodes_[i].deviceCode == deviceCode) return &nodes_[i];
    }
    return nullptr;
}

const SensorNode* NodeRegistry::at(size_t index) const {
    return index < count_ ? &nodes_[index] : nullptr;
}

size_t NodeRegistry::onlineCount() const {
    size_t result = 0;
    for (size_t i = 0; i < count_; ++i) if (nodes_[i].online) ++result;
    return result;
}

bool NodeRegistry::touchNode(const String& deviceCode, uint32_t now) {
    for (size_t i = 0; i < count_; ++i) {
        if (nodes_[i].deviceCode == deviceCode && nodes_[i].authenticated) {
            nodes_[i].lastSeenMs = now;
            nodes_[i].online = true;
            return true;
        }
    }
    return false;
}

void NodeRegistry::update(uint32_t now) {
    for (size_t i = 0; i < count_; ++i) {
        if (nodes_[i].online && static_cast<uint32_t>(now - nodes_[i].lastSeenMs) > Config::NODE_TIMEOUT_MS) {
            nodes_[i].online = false;
            Logger::node((nodes_[i].deviceCode + " marked offline").c_str());
        }
    }
}
