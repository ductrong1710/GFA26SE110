#include "RuntimeConfig.h"
#include "BackendSyncManager.h"
#include "BoundedHttp.h"
#include "Secrets.h"
#include "Logger.h"
static_assert(Config::BACKEND_BATCH_SIZE <= Config::STORAGE_BATCH_SIZE, "Backend batch exceeds storage batch");
bool BackendSyncManager::isConfigured() const {
    return RuntimeConfig::instance().values().backendBaseUrl.c_str()[0] && RuntimeConfig::instance().values().apiKey.c_str()[0] && RuntimeConfig::instance().values().gatewayId>0;
}

void BackendSyncManager::retry(size_t count) {
    const uint32_t scheduled = retryMs_;
    Serial.printf("[SYNC] Keeping %u records pending; retry in %u ms\n", unsigned(count), unsigned(scheduled));
}
void BackendSyncManager::logState(const char* state, size_t pending) {
    const uint32_t now = millis();
    if (!loggedState_ || strcmp(loggedState_, state) ||
        (pending && uint32_t(now - lastStateLog_) >= 30000)) {
        Serial.printf("[SYNC] state=%s pending=%u wifiTarget=%s\n", state,
            unsigned(pending), GatewayWiFiManager::targetName(wifi_.staTarget()));
        loggedState_ = state; lastStateLog_ = now;
    }
}
void BackendSyncManager::update() {
    if (!storage_.isReady()) { logState("STORAGE_NOT_READY", 0); return; }
    const uint32_t now = millis();
    if (now - lastCleanup_ >= Config::STORAGE_CLEANUP_INTERVAL_MS) {
        lastCleanup_ = now;
        if (!storage_.cleanup()) Logger::warn("Synced cleanup failed; data retained");
    }
    const size_t pending = storage_.getPendingCount();
    if (!isConfigured()) { logState("BACKEND_NOT_CONFIGURED", pending); return; }
    // A private LAN backend can be reachable without the public Internet probe.
    if (!wifi_.isBackendNetworkReady()) { logState("WAITING_FOR_WIFI", pending); return; }
    if (!pending) { logState("IDLE_NO_PENDING_RECORDS", 0); return; }
    if (now - lastAttempt_ < retryMs_) { logState("WAITING_FOR_NEXT_ATTEMPT", pending); return; }
    lastAttempt_ = now;
    const size_t count = storage_.loadPendingBatch(batch_, Config::BACKEND_BATCH_SIZE);
    if (!count) { logState("NO_READABLE_PENDING_BATCH", pending); return; }
    String batchKey;
    if (!adapter_.build(batch_, count, document_, batchKey)) {
        logState("BATCH_BUILD_FAILED_CHECK_CHANNELS_AND_TIMESTAMPS", pending);
        retryMs_ = Config::BACKEND_RETRY_MAX_MS; retry(count); return;
    }
    String payload; serializeJson(document_, payload); document_.clear();
    char endpoint[96]; snprintf(endpoint, sizeof(endpoint), Config::BACKEND_SENSOR_BATCH_ENDPOINT, RuntimeConfig::instance().values().gatewayId);
    String base = RuntimeConfig::instance().values().backendBaseUrl.c_str();
    while (base.endsWith("/")) base.remove(base.length() - 1);
    logState("UPLOADING", pending);
    Serial.printf("[SYNC] POST %s gatewayId=%d records=%u payloadBytes=%u\n",
        endpoint, RuntimeConfig::instance().values().gatewayId, unsigned(count), unsigned(payload.length()));
    int status = 0;
    const uint32_t started = millis();
    const bool received = BoundedHttp::request(base + endpoint, "POST", payload,
        "X-Gateway-Code", RuntimeConfig::instance().values().gatewayCode.c_str(), "X-Api-Key", RuntimeConfig::instance().values().apiKey.c_str(), document_, status);
    Serial.printf("[SYNC] Response http=%d parsed=%d elapsed=%lu ms\n", status, received ? 1 : 0,
        static_cast<unsigned long>(millis() - started));
    bool accepted[Config::BACKEND_BATCH_SIZE] = {};
    if (!received || !adapter_.accepted(document_, batchKey, batch_, count, accepted)) {
        Serial.printf("[SYNC] Batch not acknowledged: %s; data retained\n",
            !received ? "HTTP_OR_RESPONSE_PARSE_FAILED" : "ACK_INVALID_OR_BATCH_MISMATCH");
        retryMs_ = std::min(std::max(Config::BACKEND_RETRY_BASE_MS, retryMs_ * 2), Config::BACKEND_RETRY_MAX_MS);
        retry(count); return;
    }
    size_t marked = 0, acknowledged = 0;
    for (size_t i = 0; i < count; ++i) if (accepted[i]) {
        ++acknowledged;
        if (storage_.markSynced(batch_[i])) ++marked;
    }
    Serial.printf("[SYNC] Batch result: acknowledged=%u markedSynced=%u unacceptedOrUnexported=%u flashMarkFailures=%u pendingNow=%u\n",
        unsigned(acknowledged), unsigned(marked), unsigned(count - acknowledged),
        unsigned(acknowledged - marked), unsigned(storage_.getPendingCount()));
    retryMs_ = marked == count ? Config::BACKEND_SYNC_INTERVAL_MS : std::min(retryMs_ * 2, Config::BACKEND_RETRY_MAX_MS);
    if (marked != count) retry(count - marked);
}
