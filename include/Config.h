#pragma once
#include <stdint.h>
#include <stddef.h>

#ifndef DEMO_MODE
#define DEMO_MODE true
#endif
namespace Config {
constexpr char DEVICE_CODE[] = "SENSOR-001";
constexpr int NODE_ID = 1;
constexpr int FARM_ID = 1;
constexpr int ZONE_ID = 1;
constexpr char FIRMWARE_VERSION[] = "1.0.0";
constexpr char DEVICE_TYPE[] = "ESP8266_SENSOR";
constexpr uint32_t SERIAL_BAUD_RATE = 115200;
constexpr uint32_t SENSOR_READ_INTERVAL_MS = 60000;
constexpr char GATEWAY_SSID[] = "UAV_GATEWAY";
constexpr char GATEWAY_BASE_URL[] = "http://192.168.4.1";
constexpr char GATEWAY_REGISTER_ENDPOINT[] = "/api/gateway/nodes/register";
constexpr uint32_t WIFI_RECONNECT_INTERVAL_MS = 15000;
constexpr uint32_t WIFI_CONNECT_TIMEOUT_MS = 10000;
constexpr uint32_t REGISTRATION_RETRY_INTERVAL_MS = 15000;
constexpr uint32_t REGISTRATION_HEARTBEAT_MS = 20000;
constexpr uint16_t HTTP_CONNECT_TIMEOUT_MS = 500;
constexpr uint16_t HTTP_REQUEST_TIMEOUT_MS = 750;
constexpr uint16_t HTTP_BODY_TIMEOUT_MS = 1000;
constexpr uint16_t HTTP_TOTAL_TIMEOUT_MS = 2500;
constexpr uint16_t HTTP_PORT = 80;
constexpr size_t MAX_LOCAL_RECORDS = 256;
constexpr size_t MAX_API_BATCH_SIZE = 10;
constexpr size_t MAX_API_BODY_BYTES = 1024;
constexpr size_t MAX_RECORD_BYTES = 768;
constexpr size_t RECORD_JSON_CAPACITY = 1024;
constexpr uint32_t SEQUENCE_RESERVATION_SIZE = 64;
constexpr size_t MIN_FREE_STORAGE_BYTES = 16384;
constexpr bool REQUIRE_DEVICE_AUTH = true;
constexpr bool REQUIRE_GATEWAY_AUTH = false;
constexpr char GATEWAY_CODE[] = "GATEWAY-001";
constexpr bool DHT_ENABLED = true;
constexpr uint8_t DHT_PIN = 4; // GPIO4, NodeMCU D2 (not GPIO2).
constexpr bool SOIL_ENABLED = true;
constexpr int SOIL_DRY_ADC = 800;
constexpr int SOIL_WET_ADC = 350;
constexpr int SOIL_DISCONNECTED_LOW = 2;
constexpr int SOIL_DISCONNECTED_HIGH = 1021;
static_assert(MAX_LOCAL_RECORDS > 0 && MAX_LOCAL_RECORDS <= 1024, "Bound RAM index");
static_assert(MAX_API_BATCH_SIZE > 0 && MAX_API_BATCH_SIZE <= 10, "ESP32 batch limit");
static_assert(sizeof(DEVICE_CODE) > 1 && sizeof(DEVICE_CODE) <= 65, "Device code must fit gateway identity");
static_assert(sizeof(FIRMWARE_VERSION) <= 33 && sizeof(DEVICE_TYPE) <= 33, "Gateway metadata limit");
static_assert(FARM_ID > 0 && ZONE_ID > 0, "Gateway requires positive farm/zone IDs");
static_assert(SENSOR_READ_INTERVAL_MS >= 2000, "DHT22 needs at least 2 seconds between reads");
}
