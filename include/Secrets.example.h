#pragma once
#include "TrustedDeviceCredential.h"

// Copy to Secrets.h and set your AP password (8-63 characters).
// Secrets.h is ignored by Git. Never log or commit actual credentials.
namespace Secrets {
constexpr char INTERNET_SSID[] = "";
constexpr char INTERNET_PASSWORD[] = "";
constexpr char BACKEND_BASE_URL[] = ""; // e.g. http://192.168.1.100:5000/api
constexpr char BACKEND_API_KEY[] = "";  // Repository uses X-Api-Key, not JWT.
constexpr char GATEWAY_AP_PASSWORD[] = "replace-with-your-ap-password";
constexpr TrustedDeviceCredential TRUSTED_DEVICES[] = {
    {"SENSOR-001", "replace-with-device-001-secret"},
    {"SENSOR-002", "replace-with-device-002-secret"}
};
}
