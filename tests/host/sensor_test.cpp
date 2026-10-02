#include "SensorMath.h"
#include <assert.h>
#include <stdio.h>
int main() {
    // Catch reversed calibration, missing clamping, rail readings and division by zero.
    assert(SensorMath::soilPercent(800,800,350,2,1021) == 0);
    assert(SensorMath::soilPercent(350,800,350,2,1021) == 100);
    assert(SensorMath::soilPercent(575,800,350,2,1021) == 50);
    assert(SensorMath::soilPercent(900,800,350,2,1021) == 0);
    assert(SensorMath::soilPercent(200,800,350,2,1021) == 100);
    assert(SensorMath::soilPercent(575,350,800,2,1021) == 50);
    assert(isnan(SensorMath::soilPercent(0,800,350,2,1021)));
    assert(isnan(SensorMath::soilPercent(1023,800,350,2,1021)));
    assert(isnan(SensorMath::soilPercent(575,350,350,2,1021)));
    puts("sensor_test: passed");
}
