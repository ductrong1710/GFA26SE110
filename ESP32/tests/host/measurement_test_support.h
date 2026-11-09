#pragma once
#include "Arduino.h"
#include <ArduinoJson.h>
inline void convertToJson(const String& value, JsonVariant out) {out.set(value.c_str());}
inline void convertFromJson(JsonVariantConst value, String& out) {out=String(value.as<const char*>()?value.as<const char*>():"");}
