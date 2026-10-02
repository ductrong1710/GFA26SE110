#include "Logger.h"

#include <Arduino.h>
#include "Config.h"

void Logger::begin() {
    Serial.begin(Config::SERIAL_BAUD_RATE);
}

void Logger::info(const char* message) {
    Serial.printf("[GATEWAY] %s\n", message);
}

void Logger::error(const char* message) {
    Serial.printf("[ERROR] %s\n", message);
}

void Logger::api(const char* message) {
    Serial.printf("[API] %s\n", message);
}

void Logger::warn(const char* message) {
    Serial.printf("[WARN] %s\n", message);
}

void Logger::node(const char* message) {
    Serial.printf("[NODE] %s\n", message);
}

void Logger::auth(const char* message) {
    Serial.printf("[AUTH] %s\n", message);
}
