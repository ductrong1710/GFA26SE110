#pragma once

namespace Logger {
void begin();
void info(const char* message);
void error(const char* message);
void api(const char* message);
void warn(const char* message);
void node(const char* message);
void auth(const char* message);
}  // namespace Logger
