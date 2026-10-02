#include "NodeApp.h"
#include "Logger.h"
void NodeApp::begin() {
    Serial.begin(Config::SERIAL_BAUD_RATE);
    Logger::log("NODE", "Boot");
    if (storage_.begin() && !queue_.begin()) Logger::log("ERROR", "Queue recovery failed; stored files preserved");
    if (queue_.healthy()) Serial.printf("[STORAGE] Recovered %u pending records\n", static_cast<unsigned int>(queue_.pendingCount()));
    sensors_.begin();
    wifi_.begin();
    api_.begin();
    lastSample_ = millis();
}
void NodeApp::update() {
    time_.update(millis());
    api_.handleClient();
    wifi_.update();
    const uint32_t now = millis();
    if (maintenance_.due(now,1000)) { queue_.update(); maintenance_.mark(now); }
    if (now - lastSample_ >= Config::SENSOR_READ_INTERVAL_MS) {
        lastSample_ = now;
        Measurement record;
        record.reading = sensors_.readSensors();
        record.uptimeMs = now;
        time_.update(now);
        record.timeSynced = time_.synced();
        record.measuredAt = time_.unixTime();
        const SaveResult result = queue_.append(record);
        if (result == SaveResult::Saved) {
            Serial.printf("[SENSOR] Measurement created: %s-%lu\n", Config::DEVICE_CODE, static_cast<unsigned long>(record.sequence));
            Serial.printf("[STORAGE] Saved %s-%lu\n", Config::DEVICE_CODE, static_cast<unsigned long>(record.sequence));
        } else Logger::log("ERROR", result == SaveResult::Full ? "Storage full; pending records preserved, sample not stored" : "Measurement not stored; storage/sequence error");
    }
    gateway_.update();
    yield();
}
