#include <Arduino.h>

#include "Logger.h"
#include "StorageManager.h"
#include "GatewayWiFiManager.h"
#include "ApiServer.h"
#include "Secrets.h"
#include "StorageDiagnostics.h"
#include "CollectionManager.h"
#include "BackendSyncManager.h"
#include "RuntimeConfig.h"
#include "ConfigurationSafety.h"
#include "PendingDeletion.h"
#include "HeartbeatSchedule.h"

namespace {
StorageManager storage;
GatewayWiFiManager wifiManager;
NodeRegistry nodeRegistry;
NodeAuthenticator nodeAuthenticator(Secrets::TRUSTED_DEVICES,
    sizeof(Secrets::TRUSTED_DEVICES) / sizeof(Secrets::TRUSTED_DEVICES[0]));
TimeManager timeManager;
SensorNodeClient sensorClient(nodeAuthenticator);
CollectionManager collectionManager(nodeRegistry, sensorClient, storage, timeManager);
BackendSyncManager backendSyncManager(storage, wifiManager);
TelloController telloController(wifiManager);
FlightStateManager flightStateManager(wifiManager, telloController);
LittleFSMissionFiles missionFiles;
MissionStorage missionStorage(missionFiles);
MissionHttpTransport missionTransport;
MissionBackendClient missionBackend(wifiManager,missionTransport);
MissionManager missionManager(missionStorage,missionBackend,wifiManager,telloController,flightStateManager);
FlightTestManager flightTest(missionFiles,missionTransport,wifiManager,telloController,flightStateManager);
ApiServer apiServer(nodeRegistry, nodeAuthenticator, storage, wifiManager, timeManager, telloController, flightStateManager,missionManager);
HeartbeatSchedule heartbeatSchedule;

void updateHeartbeat() {
    // A downloaded mission waiting for local START is still safely on Internet Wi-Fi.
    const auto missionState = missionManager.state();
    const auto heartbeatMissionState = missionState == MissionState::Ready || missionState == MissionState::Downloading
        ? MissionState::Empty : missionState;
    const bool safe = !flightTest.locked() && !missionManager.ownsFlight() &&
        configurationSafe(flightStateManager.state(), flightStateManager.mayBeAirborne(),
            telloController.commandPending(), heartbeatMissionState) &&
        collectionManager.isIdle() && wifiManager.isInternetNetworkReady();
    const auto& config = RuntimeConfig::instance().values();
    if (!heartbeatSchedule.due(millis(), safe) || config.gatewayId <= 0 || !missionTransport.configured()) return;
    heartbeatSchedule.attempted(millis());
    char path[96];
    snprintf(path, sizeof(path), "/device/gateways/%d/heartbeat", config.gatewayId);
    StaticJsonDocument<512> response;
    int status = 0;
    const bool parsed = missionTransport.request(path, "POST", nullptr, response, status);
    const bool acknowledged = parsed && status == 200 && response["success"] == true &&
        response["data"]["gatewayId"] == config.gatewayId;
    Serial.printf("[HEARTBEAT] gatewayId=%d http=%d acknowledged=%d; next attempt in 30000 ms\n",
        config.gatewayId, status, acknowledged);
}

void handleMaintenanceCommand() {
    static char line[48] = {};
    static size_t length = 0;
    static bool overflow = false;
    // Bounded work per loop; never wait for input while servicing Wi-Fi/flight.
    for (unsigned n = 0; n < 64 && Serial.available(); ++n) {
        const char ch = char(Serial.read());
        if (ch == '\r') continue;
        if (ch != '\n') {
            if (length + 1 < sizeof(line)) line[length++] = ch;
            else overflow = true;
            continue;
        }
        line[length] = '\0';
        const bool confirmed = pendingDeleteConfirmed(line, length, overflow);
        const bool hadInput = length || overflow;
        length = 0; overflow = false;
        if (!hadInput) return;
        if (!confirmed) {
            Serial.printf("[STORAGE] Command: DELETE_PENDING CONFIRM (permanently deletes unsynced measurements only)\n");
            return;
        }
        const bool safe = !flightTest.locked() &&
            configurationSafe(flightStateManager.state(), flightStateManager.mayBeAirborne(),
                telloController.commandPending(), missionManager.state()) &&
            (!missionManager.hasActive() || missionManager.result().uploaded) &&
            collectionManager.isIdle();
        if (!safe) {
            Serial.printf("[STORAGE] DELETE_PENDING REFUSED: flight/mission/recovery or collection busy; retry when idle\n");
            return;
        }
        if (!storage.isReady()) {
            Serial.printf("[STORAGE] DELETE_PENDING REFUSED: storage not ready\n"); return;
        }
        size_t removed = 0;
        storage.deleteAllPending(removed);
        return;
    }
}
}

void setup() {
    Logger::begin();
    Logger::info("Boot");
    auto& configuration=RuntimeConfig::instance();
    if(!configuration.begin()) Logger::error("Saved configuration unavailable; use local configuration page");
    missionBackend.setStartupGatewayId(configuration.values().gatewayId);
    missionManager.setStartupGatewayId(configuration.values().gatewayId);

    if (!storage.begin()) {
        Logger::error("Phase 1 initialization failed");
        // Keep local API available for diagnosis; collection/sync refuse storage writes.
    }

    missionManager.begin(); // Recover possible flight BEFORE selecting a STA network.
    missionManager.setCollection(collectionManager);
    flightTest.begin(configuration.values().gatewayId);
    apiServer.setFlightTest(flightTest);
    if (!wifiManager.begin(flightTest.locked() ? flightTest.initialStaTarget() : missionManager.initialStaTarget())) {
        Logger::error("Phase 2 initialization failed");
        return;
    }
    if (!apiServer.begin()) {
        Logger::error("Phase 3 initialization failed");
        return;
    }
}

void loop() {
    if(apiServer.restartPending()) {
        if(apiServer.restartReady()) ESP.restart();
        yield();return;
    }
    StorageDiagnostics::update(storage);
    wifiManager.update();
    apiServer.handleClient();
    if(apiServer.restartPending()) {yield();return;}
    telloController.update();
    flightStateManager.update(storage.getPendingCount(), storage.isReady(), backendSyncManager.isConfigured());
    handleMaintenanceCommand();
    const bool testIdle=storage.isReady() && configurationSafe(flightStateManager.state(),flightStateManager.mayBeAirborne(),telloController.commandPending(),missionManager.state()) && (!missionManager.hasActive() || missionManager.result().uploaded);
    flightTest.update(testIdle);
    if(!flightTest.locked()) {
        const size_t missionPending = missionManager.state() == MissionState::Syncing && missionManager.hasActive()
            ? storage.getPendingCount(missionManager.active().id) : storage.getPendingCount();
        missionManager.update(storage.isReady(),missionPending,timeManager.isTimeSynced());
    }
    timeManager.update(wifiManager.isInternetNetworkReady());
    nodeRegistry.update();
#ifndef STORAGE_DIAGNOSTICS
    collectionManager.setMissionMode(missionManager.ownsFlight());
    static bool syncPausedForTest = false;
    if (syncPausedForTest != flightTest.locked()) {
        syncPausedForTest = flightTest.locked();
        Serial.printf("[SYNC] %s\n", syncPausedForTest ?
            "PAUSED_FLIGHT_TEST: sensor upload waits for flight-test completion/recovery" :
            "RESUMED_AFTER_FLIGHT_TEST");
    }
    if(!flightTest.locked()) {collectionManager.update();backendSyncManager.update();}
    updateHeartbeat();
#endif
    // Yield to Wi-Fi and system background tasks.
    yield();
}
