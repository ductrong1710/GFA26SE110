#include "SensorManager.h"
#include "SensorMath.h"
#include "Logger.h"
bool SensorManager::begin() {
#if DEMO_MODE
    Logger::log("SENSOR", "DEMO_MODE enabled; simulated sensors use the normal data path");
#else
    if (Config::DHT_ENABLED) dht_.begin();
    Logger::log("SENSOR", "Physical sensor mode");
#endif
    return true;
}
SensorReading SensorManager::readSensors() {
    Logger::log("SENSOR", "Reading sensors");
    SensorReading reading;
#if DEMO_MODE
    reading.temperature = random(250, 351) / 10.0f;
    reading.humidity = random(500, 901) / 10.0f;
    reading.soilMoisture = random(200, 801) / 10.0f;
#else
    if (Config::DHT_ENABLED) {
        reading.temperature = dht_.readTemperature();
        reading.humidity = dht_.readHumidity();
        if (!isfinite(reading.temperature) || reading.temperature < -40 || reading.temperature > 80) reading.temperature = NAN;
        if (!isfinite(reading.humidity) || reading.humidity < 0 || reading.humidity > 100) reading.humidity = NAN;
        if (isnan(reading.temperature) || isnan(reading.humidity)) Logger::log("WARN", "DHT22 read failed; unavailable fields are null");
    }
    if (Config::SOIL_ENABLED) {
        reading.soilMoisture = SensorMath::soilPercent(analogRead(A0), Config::SOIL_DRY_ADC,
            Config::SOIL_WET_ADC, Config::SOIL_DISCONNECTED_LOW, Config::SOIL_DISCONNECTED_HIGH);
        if (isnan(reading.soilMoisture)) Logger::log("WARN", "Soil ADC invalid; field is null");
    }
#endif
    return reading;
}
