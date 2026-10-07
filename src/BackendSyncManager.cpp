#include "BackendSyncManager.h"
#include "BoundedHttp.h"
#include "Secrets.h"
#include "Logger.h"
static_assert(Config::BACKEND_BATCH_SIZE <= Config::STORAGE_BATCH_SIZE, "Backend batch exceeds storage batch");
bool BackendSyncManager::isConfigured() const {
    return Secrets::BACKEND_BASE_URL[0] && Secrets::BACKEND_API_KEY[0] && Config::BACKEND_GATEWAY_ID>0;
}

void BackendSyncManager::retry(size_t count) {
    const uint32_t scheduled = retryMs_;
    Serial.printf("[SYNC] Keeping %u records pending; retry in %u ms\n", unsigned(count), unsigned(scheduled));
}
void BackendSyncManager::update() {
    if (!storage_.isReady()) return;
    const uint32_t now = millis();
    if (now - lastCleanup_ >= Config::STORAGE_CLEANUP_INTERVAL_MS) {
        lastCleanup_ = now;
        if (!storage_.cleanup()) Logger::warn("Synced cleanup failed; data retained");
    }
    if (!isConfigured()) return;
    // A private LAN backend can be reachable without the public Internet probe.
    if (!wifi_.isBackendNetworkReady() || now - lastAttempt_ < retryMs_ || !storage_.getPendingCount()) return;
    lastAttempt_ = now;
    const size_t count = storage_.loadPendingBatch(batch_, Config::BACKEND_BATCH_SIZE);
    if (!count) return;
    String batchKey;
    if (!adapter_.build(batch_, count, document_, batchKey)) {
        Logger::warn("Pending batch lacks exportable channels or valid timestamps; retained");
        retryMs_ = Config::BACKEND_RETRY_MAX_MS; return;
    }
    String payload; serializeJson(document_, payload); document_.clear();
    char endpoint[96]; snprintf(endpoint, sizeof(endpoint), Config::BACKEND_SENSOR_BATCH_ENDPOINT, Config::BACKEND_GATEWAY_ID);
    String base = Secrets::BACKEND_BASE_URL;
    while (base.endsWith("/")) base.remove(base.length() - 1);
    Serial.printf("[SYNC] Pending records: %u\n[SYNC] Uploading batch: %u\n",
                  unsigned(storage_.getPendingCount()), unsigned(count));
    int status;
    const bool received = BoundedHttp::request(base + endpoint, "POST", payload,
        "X-Gateway-Code", Config::GATEWAY_CODE, "X-Api-Key", Secrets::BACKEND_API_KEY, document_, status);
    Serial.printf("[SYNC] Backend response: %d\n", status);
    bool accepted[Config::BACKEND_BATCH_SIZE] = {};
    if (!received || !adapter_.accepted(document_, batchKey, batch_, count, accepted)) {
        retryMs_ = std::min(std::max(Config::BACKEND_RETRY_BASE_MS, retryMs_ * 2), Config::BACKEND_RETRY_MAX_MS);
        retry(count); return;
    }
    size_t marked = 0;
    for (size_t i = 0; i < count; ++i) if (accepted[i] && storage_.markSynced(batch_[i])) ++marked;
    Serial.printf("[SYNC] Marked %u records synced\n", unsigned(marked));
    retryMs_ = marked == count ? Config::BACKEND_SYNC_INTERVAL_MS : std::min(retryMs_ * 2, Config::BACKEND_RETRY_MAX_MS);
    if (marked != count) retry(count - marked);
}
