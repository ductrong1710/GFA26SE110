#pragma once
#include <Arduino.h>
namespace Logger {
inline void log(const char* tag, const char* message) { Serial.printf("[%s] %s\n", tag, message); }
}
