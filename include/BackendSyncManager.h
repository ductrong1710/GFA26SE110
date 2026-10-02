#pragma once
#include "StorageManager.h"
#include "GatewayWiFiManager.h"
#include "BackendAdapter.h"

class BackendSyncManager {
public:
    BackendSyncManager(StorageManager& storage, GatewayWiFiManager& wifi)
        : storage_(storage), wifi_(wifi), document_(Config::BACKEND_JSON_CAPACITY) {}
    void update();
private:
    StorageManager& storage_;
    GatewayWiFiManager& wifi_;
    BackendAdapter adapter_;
    DynamicJsonDocument document_;
    GatewayMeasurement batch_[Config::BACKEND_BATCH_SIZE];
    uint32_t lastAttempt_ = 0, retryMs_ = Config::BACKEND_SYNC_INTERVAL_MS, lastCleanup_ = 0;
    void retry(size_t count);
};
