#pragma once
#include <ArduinoJson.h>
namespace BoundedHttp {
bool request(const String& url, const char* method, const String& body,
    const char* header1, const String& value1, const char* header2, const String& value2,
    JsonDocument& response, int& status, bool allowEmpty = false);
}
