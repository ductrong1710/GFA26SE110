#pragma once
#include <Arduino.h>

struct GatewaySettings {
    String internetSsid, internetPassword, backendBaseUrl, gatewayCode, apiKey;
    int gatewayId=0;
};

class RuntimeConfig {
public:
    static RuntimeConfig& instance();
    bool begin();
    bool ready() const { return ready_; }
    const GatewaySettings& values() const { return values_; }
    // Persists one NVS value; settings take effect only after reboot/begin.
    bool save(const GatewaySettings& settings, bool adoptPending = false);
    void setPendingAdoptionAllowed(bool allowed) { pendingAdoptionAllowed_=allowed; }
    bool canAdoptPending() const { return ready_ && pendingAdoptionAllowed_ && values_.apiKey.isEmpty() && !values_.gatewayCode.isEmpty(); }
    static const char* validate(const GatewaySettings& settings);
    void setIdentityChangeAllowed(bool allowed) { identityChangeAllowed_=allowed; }
private:
    RuntimeConfig();
    GatewaySettings values_;
    bool ready_=false, identityChangeAllowed_=true;
    bool pendingAdoptionAllowed_=false;
};
