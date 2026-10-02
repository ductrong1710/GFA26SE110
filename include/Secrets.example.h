#pragma once
// Copy to Secrets.h. Empty defaults deliberately disable gateway connections.
namespace Secrets {
constexpr char GATEWAY_WIFI_PASSWORD[] = ""; // Match ESP32 AP password, 8-63 bytes.
constexpr char DEVICE_SECRET[] = ""; // Match ESP32 TRUSTED_DEVICES for DEVICE_CODE.
constexpr char GATEWAY_TOKEN[] = ""; // Only used if REQUIRE_GATEWAY_AUTH is enabled.
}
