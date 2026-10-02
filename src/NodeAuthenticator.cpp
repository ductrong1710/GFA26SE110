#include "NodeAuthenticator.h"

const char* NodeAuthenticator::tokenFor(const String& deviceCode) const {
    for (size_t i = 0; i < count_; ++i) if (deviceCode == credentials_[i].deviceCode) return credentials_[i].secret;
    return nullptr;
}

bool NodeAuthenticator::isRegisteredDevice(const String& deviceCode) const {
    for (size_t i = 0; i < count_; ++i) {
        if (deviceCode == credentials_[i].deviceCode) return true;
    }
    return false;
}

bool NodeAuthenticator::authenticate(const String& deviceCode, const String& token) const {
    if (deviceCode.isEmpty() || token.isEmpty()) return false;
    for (size_t i = 0; i < count_; ++i) {
        if (deviceCode == credentials_[i].deviceCode) {
            return token == credentials_[i].secret;
        }
    }
    return false;
}
