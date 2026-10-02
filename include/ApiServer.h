#pragma once

#include <ArduinoJson.h>
#include <WebServer.h>
#include "Config.h"
#include "NodeRegistry.h"
#include "NodeAuthenticator.h"
#include "StorageManager.h"
#include "GatewayWiFiManager.h"
#include "TimeManager.h"

// WebServer::begin() returns void; expose the listener state for startup checks.
class GatewayWebServer : public WebServer {
public:
    GatewayWebServer() : WebServer(Config::HTTP_PORT) {}
    bool listening() { return static_cast<bool>(_server); }
};

class ApiServer {
public:
    ApiServer(NodeRegistry& registry, const NodeAuthenticator& authenticator, StorageManager& storage, GatewayWiFiManager& wifi, TimeManager& time)
        : wifi_(wifi), time_(time), storage_(storage), registry_(registry), authenticator_(authenticator) {}
    bool begin();
    void handleClient();

private:
    GatewayWiFiManager& wifi_;
    TimeManager& time_;
    StorageManager& storage_;
    NodeRegistry& registry_;
    const NodeAuthenticator& authenticator_;
    GatewayWebServer server_;
    bool started_ = false;
    void setupRoutes();
    void handleGatewayStatus();
    void handleGatewayNodes();
    void handleGatewayTime();
    void handleNodeRegistration();
    void handleNotFound();
    void sendJson(int status, const JsonDocument& document);
    void sendError(int status, const char* code, const char* message);
};
