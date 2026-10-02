#pragma once
#include "Models.h"
#include "Config.h"
#if !DEMO_MODE
#include <DHT.h>
#endif
class SensorManager {
public:
    bool begin();
    SensorReading readSensors();
private:
#if !DEMO_MODE
    DHT dht_{Config::DHT_PIN, DHT22};
#endif
};
