#pragma once
#include <FS.h>
#include "GatewayMeasurement.h"
enum class SaveResult { Saved, Duplicate, Failed };
class StorageManager {
public:
    bool begin();
    bool isReady() const { return ready_; }
    SaveResult saveMeasurement(const GatewayMeasurement& record);
    bool measurementExists(const String& deviceCode, uint32_t sequence);
    size_t getPendingCount() const { return pending_; }
    size_t getStoredCount() const { return stored_; }
    size_t loadPendingBatch(GatewayMeasurement* records, size_t capacity);
    bool markSynced(const GatewayMeasurement& record);
    bool cleanup();
private:
    bool ready_ = false;
    size_t pending_ = 0, stored_ = 0;
    size_t pendingCursor_ = 0;
    String key(const String& deviceCode, uint32_t sequence) const;
    bool readRecord(const String& path, GatewayMeasurement& out);
    bool writeVerified(const GatewayMeasurement& record, const String& destination);
    bool receiptExists(const String& path, const String& deviceCode, uint32_t sequence);
};
