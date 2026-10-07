#include <Arduino.h>

#include "Logger.h"
#include "StorageManager.h"
#include "GatewayWiFiManager.h"
#include "ApiServer.h"
#include "Secrets.h"
#include "StorageDiagnostics.h"
#include "CollectionManager.h"
#include "BackendSyncManager.h"

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
ApiServer apiServer(nodeRegistry, nodeAuthenticator, storage, wifiManager, timeManager, telloController, flightStateManager);
}

void setup() {
    Logger::begin();
    Logger::info("Boot");

    if (!storage.begin()) {
        Logger::error("Phase 1 initialization failed");
        // Keep local API available for diagnosis; collection/sync refuse storage writes.
    }

    if (!wifiManager.begin()) {
        Logger::error("Phase 2 initialization failed");
        return;
    }
    if (!apiServer.begin()) {
        Logger::error("Phase 3 initialization failed");
        return;
    }
}

void loop() {
    StorageDiagnostics::update(storage);
    wifiManager.update();
    apiServer.handleClient();
    telloController.update();
    flightStateManager.update(storage.getPendingCount(), storage.isReady(), backendSyncManager.isConfigured());
    timeManager.update(wifiManager.isInternetNetworkReady());
    nodeRegistry.update();
#ifndef STORAGE_DIAGNOSTICS
    collectionManager.update();
    backendSyncManager.update();
#endif
    // Yield to Wi-Fi and system background tasks.
    yield();
}
