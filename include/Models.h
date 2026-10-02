#pragma once
#include <stdint.h>
#include <math.h>
struct SensorReading {
    float temperature = NAN;
    float humidity = NAN;
    float soilMoisture = NAN;
    float lightIntensity = NAN;
    float ph = NAN;
    float waterLevel = NAN;
    float batteryVoltage = NAN;
};
enum class MeasurementState { Pending, Acked };
struct Measurement {
    uint32_t sequence = 0;
    uint64_t measuredAt = 0;
    bool timeSynced = false;
    uint32_t uptimeMs = 0;
    SensorReading reading;
    MeasurementState state = MeasurementState::Pending;
};
