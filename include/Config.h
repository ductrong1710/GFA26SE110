#pragma once

#include <stdint.h>
#include <stddef.h>

namespace Config {
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
