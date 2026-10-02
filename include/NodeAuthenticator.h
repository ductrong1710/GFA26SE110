#pragma once
#include <Arduino.h>
#include "TrustedDeviceCredential.h"

class NodeAuthenticator {
public:
    NodeAuthenticator(const TrustedDeviceCredential* credentials, size_t count)
        : credentials_(credentials), count_(count) {}
    bool authenticate(const String& deviceCode, const String& token) const;
    bool isRegisteredDevice(const String& deviceCode) const;
    // Only the node HTTP client consumes this credential; never store it in nodes.
    const char* tokenFor(const String& deviceCode) const;
private:
    const TrustedDeviceCredential* credentials_;
    size_t count_;
};
