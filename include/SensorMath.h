#pragma once
#include <math.h>
namespace SensorMath {
inline float soilPercent(int raw, int dry, int wet, int low, int high) {
    if (raw <= low || raw >= high || dry == wet) return NAN;
    const float percent = 100.0f * (raw - dry) / (wet - dry);
    return fmaxf(0, fminf(100, percent));
}
}
