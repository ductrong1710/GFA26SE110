#include "StorageDiagnostics.h"
#ifdef STORAGE_DIAGNOSTICS
#include <Arduino.h>
namespace {
GatewayMeasurement sample(uint32_t sequence) {
    GatewayMeasurement r;
    r.deviceCode = "SENSOR-001"; r.sequence = sequence;
    r.recordId = r.deviceCode + "-" + String(sequence);
    r.temperature.present = true; r.temperature.value = 30.2;
    return r;
}
}
void StorageDiagnostics::update(StorageManager& storage) {
    if (!Serial.available()) return;
    const char command = Serial.read();
    if (command == 's') {
        const auto a = storage.saveMeasurement(sample(100));
        Serial.printf("[TEST] save100=%d stored=%u\n", int(a), unsigned(storage.getStoredCount()));
        const auto duplicate = storage.saveMeasurement(sample(100));
        Serial.printf("[TEST] duplicate100=%d stored=%u\n", int(duplicate), unsigned(storage.getStoredCount()));
        const auto b = storage.saveMeasurement(sample(101));
        Serial.printf("[TEST] save101=%d stored=%u\n", int(b), unsigned(storage.getStoredCount()));
    } else if (command == 'v') {
        Serial.printf("[TEST] exists100=%d exists101=%d stored=%u pending=%u\n",
            storage.measurementExists("SENSOR-001", 100), storage.measurementExists("SENSOR-001", 101),
            unsigned(storage.getStoredCount()), unsigned(storage.getPendingCount()));
    } else if (command == 'm') {
        Serial.printf("[TEST] mark100=%d\n", storage.markSynced(sample(100)));
    } else if (command == 'c') {
        Serial.printf("[TEST] cleanup=%d\n", storage.cleanup());
    }
}
#else
void StorageDiagnostics::update(StorageManager&) {}
#endif
