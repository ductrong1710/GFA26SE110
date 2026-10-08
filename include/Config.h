#pragma once

#include <stdint.h>
#include <stddef.h>

namespace Config {
constexpr int TELLO_MAX_ALTITUDE_CM = 40;
constexpr int TELLO_TARGET_ALTITUDE_CM = 35;
constexpr int TELLO_MIN_OPERATION_ALTITUDE_CM = 20;
#ifndef GATEWAY_GROUND_TEST_MODE
#define GATEWAY_GROUND_TEST_MODE 1
#endif
constexpr bool GROUND_TEST_MODE = GATEWAY_GROUND_TEST_MODE != 0;
constexpr size_t MAX_MISSION_WAYPOINTS = 16;
constexpr size_t MAX_MISSION_TARGETS = 8;
constexpr size_t MISSION_MAX_FILE_BYTES = 8192;
constexpr size_t MISSION_JSON_CAPACITY = 16384;
constexpr int MISSION_MAX_LOCAL_CM = 10000;
constexpr int MISSION_MAX_MOVE_SEGMENT_CM = 100;
constexpr size_t MAX_MISSION_ROUTE_STEPS = 128;
constexpr int TELLO_ALTITUDE_TOLERANCE_CM = 3;
constexpr uint32_t TELLO_ALTITUDE_STABILIZE_TIMEOUT_MS = 20000;
constexpr uint32_t TELLO_ALTITUDE_STABLE_MS = 1000;
constexpr uint32_t TELLO_VERTICAL_PULSE_MS = 200;
constexpr uint32_t TELLO_VERTICAL_REASSESS_MS = 500;
constexpr uint32_t MISSION_SENSOR_WAIT_TIMEOUT_MS = 30000;
constexpr uint32_t MISSION_PULL_INTERVAL_MS = 20000;
constexpr uint32_t MISSION_MAX_HOLD_SECONDS = 30;
constexpr bool MOCK_MISSION_BACKEND = false; // Explicit development opt-in; never impersonate the backend.
constexpr char MISSION_NEXT_ENDPOINT[] = "/device/gateways/%d/missions/next";
constexpr char MISSION_RESULT_ENDPOINT[] = "/device/gateways/%d/missions/%d/result";
constexpr bool TELLO_ENABLED = true;
constexpr uint8_t TELLO_IP[] = {192, 168, 10, 1};
constexpr uint16_t TELLO_COMMAND_PORT = 8889;
constexpr uint16_t TELLO_STATE_PORT = 8890;
constexpr uint32_t TELLO_COMMAND_TIMEOUT_MS = 7000;
constexpr uint32_t TELLO_FLIGHT_COMMAND_TIMEOUT_MS = 20000;
constexpr uint32_t TELLO_CONNECT_TIMEOUT_MS = 30000;
constexpr uint32_t TELLO_RECOVERY_RETRY_MS = 15000;
constexpr unsigned TELLO_COMMAND_RETRY_COUNT = 1; // Read/SDK commands only.
constexpr uint32_t TELLO_COMMAND_GAP_MS = 300;
constexpr uint32_t TELLO_RESPONSE_QUARANTINE_MS = 3000;
constexpr int TELLO_MIN_TAKEOFF_BATTERY_PERCENT = 25;
constexpr uint32_t TELLO_BATTERY_MAX_AGE_MS = 30000;
constexpr uint32_t TELLO_LAND_SETTLE_MS = 5000;
constexpr uint32_t TELLO_LAND_ACK_ONLY_SETTLE_MS = 20000;
constexpr uint32_t TELLO_LAND_CONFIRM_TIMEOUT_MS = 60000;
constexpr uint32_t TELLO_TELEMETRY_TIMEOUT_MS = 2000;
constexpr unsigned TELLO_LAND_STABLE_SAMPLES = 10;
constexpr int TELLO_LAND_MAX_HEIGHT_CM = 15;
constexpr int TELLO_LAND_MAX_VERTICAL_SPEED = 5;
constexpr uint32_t TELLO_RC_INTERVAL_MS = 100;
constexpr uint32_t TELLO_RC_MAX_AGE_MS = 500;
constexpr char GATEWAY_CODE[] = "GATEWAY-001";
constexpr uint16_t HTTP_PORT = 80;
constexpr size_t MAX_LOCAL_RECORDS = 1000;
constexpr size_t MAX_RECORD_FILE_BYTES = 1536;
constexpr size_t STORAGE_BATCH_SIZE = 10;
constexpr uint32_t STORAGE_CLEANUP_INTERVAL_MS = 60000;
constexpr size_t COLLECTION_BATCH_SIZE = 10;
constexpr unsigned int MAX_COLLECTION_BATCHES_PER_CYCLE = 3;
constexpr uint32_t NODE_COLLECTION_INTERVAL_MS = 10000;
constexpr uint16_t SENSOR_NODE_HTTP_PORT = 80;
constexpr uint16_t HTTP_CONNECT_TIMEOUT_MS = 500;
constexpr uint16_t HTTP_REQUEST_TIMEOUT_MS = 750;
constexpr uint32_t HTTP_BODY_TIMEOUT_MS = 1500;
constexpr size_t MAX_HTTP_RESPONSE_BYTES = 12288;
constexpr size_t COLLECTION_JSON_CAPACITY = 16384;
constexpr uint32_t INTERNET_RECONNECT_INTERVAL_MS = 15000;
constexpr uint32_t INTERNET_PROBE_INTERVAL_MS = 30000;
constexpr uint8_t INTERNET_PROBE_IP[] = {1, 1, 1, 1};
constexpr uint16_t INTERNET_PROBE_PORT = 53;
constexpr uint16_t INTERNET_PROBE_TIMEOUT_MS = 250;
constexpr char NTP_SERVER[] = "pool.ntp.org";
constexpr int BACKEND_GATEWAY_ID = 0; // Set to the provisioned numeric backend ID.
constexpr char BACKEND_SENSOR_BATCH_ENDPOINT[] = "/device/gateways/%d/sync";
constexpr size_t BACKEND_BATCH_SIZE = 2; // Up to fourteen channel readings.
constexpr uint32_t BACKEND_SYNC_INTERVAL_MS = 5000;
constexpr uint32_t BACKEND_RETRY_BASE_MS = 5000;
constexpr uint32_t BACKEND_RETRY_MAX_MS = 60000;
constexpr size_t BACKEND_JSON_CAPACITY = 8192;
constexpr const char* BACKEND_CHANNEL_CODES[] = {
    "temperature", "humidity", "soilMoisture", "lightIntensity", "ph", "waterLevel", "batteryVoltage"
};
constexpr unsigned int MAX_API_BODY_BYTES = 1024;
constexpr unsigned int MAX_DEVICE_CODE_BYTES = 64;
constexpr unsigned int MAX_SENSOR_NODES = 20;
constexpr uint32_t NODE_TIMEOUT_MS = 30000;
constexpr unsigned int MAX_NODE_METADATA_BYTES = 32;
constexpr int UNKNOWN_NODE_RSSI = 0;
constexpr uint32_t SERIAL_BAUD_RATE = 115200;
// Provision a blank board with `pio run -t uploadfs` before first boot.
// A mount failure must not silently erase previously stored data.
constexpr bool FORMAT_LITTLEFS_ON_FAILURE = false;
constexpr char GATEWAY_AP_SSID[] = "UAV_GATEWAY";
constexpr uint8_t GATEWAY_AP_IP[] = {192, 168, 4, 1};
constexpr uint8_t GATEWAY_AP_SUBNET[] = {255, 255, 255, 0};
}  // namespace Config
