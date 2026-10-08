#include "ApiServer.h"

#include <ctype.h>
#include <WiFi.h>
#include "Logger.h"

namespace {
bool validIpv4(const char* text) {
    unsigned int octet = 0, digits = 0, dots = 0;
    for (; *text; ++text) {
        if (*text == '.') {
            if (!digits || dots == 3) return false;
            ++dots;
            octet = digits = 0;
        } else {
            if (*text < '0' || *text > '9' || ++digits > 3) return false;
            octet = octet * 10 + (*text - '0');
            if (octet > 255) return false;
        }
    }
    return dots == 3 && digits != 0;
}

// A bounded reader also lets us reject trailing garbage after a JSON object.
struct JsonBodyReader {
    explicit JsonBodyReader(const String& input) : body(input) {}
    const String& body;
    size_t position = 0;
    int read() {
        return position < body.length() ? static_cast<unsigned char>(body[position++]) : -1;
    }
    size_t readBytes(char* output, size_t length) {
        size_t count = 0;
        while (count < length && position < body.length()) output[count++] = body[position++];
        return count;
    }
};
}

bool ApiServer::begin() {
    if (started_) return true;
    Logger::api("Starting HTTP server");
    setupRoutes();
    const char* headers[] = {"Content-Length", "Content-Type", "Transfer-Encoding",
                             "X-Device-Code", "X-Device-Token"};
    server_.collectHeaders(headers, 5);
    server_.begin();
    started_ = server_.listening();
    if (!started_) {
        Logger::error("HTTP server failed to start");
        return false;
    }
    Logger::api((String("HTTP server started on port ") + Config::HTTP_PORT).c_str());
    return true;
}

void ApiServer::handleClient() {
    if (started_) server_.handleClient();
}

void ApiServer::setupRoutes() {
    server_.on("/api/gateway/status", HTTP_GET, [this]() { handleGatewayStatus(); });
    server_.on("/api/gateway/nodes", HTTP_GET, [this]() { handleGatewayNodes(); });
    server_.on("/api/gateway/time", HTTP_GET, [this]() { handleGatewayTime(); });
    server_.on("/api/gateway/nodes/register", HTTP_POST,
               [this]() { handleNodeRegistration(); });
    server_.onNotFound([this]() { handleNotFound(); });
    server_.on("/api/tello/status", HTTP_GET, [this]() { handleTelloStatus(); });
    server_.on("/api/tello/prepare", HTTP_POST, [this]() { handleTelloPrepare(); });
    server_.on("/api/tello/takeoff", HTTP_POST, [this]() { if(manualFlightAllowed()) sendFlightResult(flight_.takeoff()); });
    server_.on("/api/tello/land", HTTP_POST, [this]() { if(mission_.ownsFlight()) sendMissionResult(mission_.landNow()); else sendFlightResult(flight_.land()); });
    server_.on("/api/tello/move", HTTP_POST, [this]() { handleTelloControl("move"); });
    server_.on("/api/tello/rotate", HTTP_POST, [this]() { handleTelloControl("rotate"); });
    server_.on("/api/tello/rc", HTTP_POST, [this]() { handleTelloControl("rc"); });
    server_.on("/api/tello/disconnect", HTTP_POST, [this]() { if(manualFlightAllowed()) sendFlightResult(flight_.disconnect()); });
    server_.on("/api/mission/status",HTTP_GET,[this](){handleMissionStatus();});
    server_.on("/api/mission/active",HTTP_GET,[this](){handleMissionActive();});
    server_.on("/api/mission/pull",HTTP_POST,[this](){sendMissionResult(mission_.requestPull());});
    server_.on("/api/mission/start",HTTP_POST,[this](){sendMissionResult(mission_.start());});
    server_.on("/api/mission/abort",HTTP_POST,[this](){sendMissionResult(mission_.abort());});
    server_.on("/api/mission/land",HTTP_POST,[this](){sendMissionResult(mission_.landNow());});
}
bool ApiServer::manualFlightAllowed() {
    if(!mission_.ownsFlight()) return true;
    sendError(409,"MISSION_OWNS_FLIGHT","Use mission abort or land while a mission owns flight control");return false;
}
void ApiServer::sendMissionResult(bool accepted) {
    if(!accepted){sendError(409,mission_.error(),"Mission request rejected; inspect mission status");return;}
    StaticJsonDocument<256> doc;doc["success"]=true;doc["state"]=mission_.stateName();sendJson(202,doc);
}
void ApiServer::addAltitudeStatus(JsonDocument& doc) {
    tello_.refreshSafety();
    doc["configuredMaxAltitudeCm"]=Config::TELLO_MAX_ALTITUDE_CM;
    doc["configuredTargetAltitudeCm"]=Config::TELLO_TARGET_ALTITUDE_CM;
    doc["currentAltitudeCm"]=tello_.safety().altitudeCm();
    doc["altitudeTelemetryFresh"]=tello_.safety().altitudeFresh();
    doc["altitudeLimitActive"]=tello_.safety().limitActive();
    doc["altitudeConfigurationValid"]=tello_.safety().configurationValid();
    doc["groundTestMode"]=Config::GROUND_TEST_MODE;
}
void ApiServer::handleMissionStatus() {
    StaticJsonDocument<1536> doc;doc["success"]=true;
    doc["missionId"]=mission_.hasActive() ? mission_.active().id : 0;
    doc["missionName"]=mission_.hasActive() ? mission_.active().name : "";
    doc["state"]=mission_.stateName();doc["currentWaypointIndex"]=mission_.waypointIndex();
    doc["waypointCount"]=mission_.hasActive() ? mission_.active().waypointCount : 0;
    doc["flightState"]=flight_.stateName();doc["staTarget"]=GatewayWiFiManager::targetName(wifi_.staTarget());
    doc["pendingUploadRecords"]=storage_.getPendingCount();doc["lastError"]=mission_.error();
    doc["mockMissionBackend"]=Config::MOCK_MISSION_BACKEND;doc["resultUploaded"]=mission_.result().uploaded;
    doc["mockResultUpload"]=mission_.result().mockUpload;addAltitudeStatus(doc);sendJson(200,doc);
}
void ApiServer::handleMissionActive() {
    size_t offset=0;
    if(server_.hasArg("routeOffset")) {
        const String input=server_.arg("routeOffset");
        if(input.isEmpty() || input.length()>3){sendError(400,"INVALID_ROUTE_OFFSET","Use a nonnegative route step index");return;}
        for(size_t i=0;i<input.length();++i) if(input[i]<'0' || input[i]>'9') {sendError(400,"INVALID_ROUTE_OFFSET","Use a nonnegative route step index");return;}
        offset=static_cast<size_t>(strtoul(input.c_str(),nullptr,10));
        if(offset>mission_.plan().count){sendError(400,"INVALID_ROUTE_OFFSET","Route offset exceeds plan");return;}
    }
    DynamicJsonDocument doc(Config::MISSION_JSON_CAPACITY);
    doc["success"]=true;
    if(mission_.hasActive()) MissionCodec::encode(mission_.active(),doc.createNestedObject("mission"));
    else doc["mission"]=nullptr;
    // The planned count is inspectable without sending any flight command.
    doc["plannedSteps"]=mission_.plan().count;
    doc["routeOffset"]=offset;
    JsonArray steps=doc.createNestedArray("routeSteps");
    size_t i=offset;
    for(;i<mission_.plan().count && i<offset+8;++i) {
        const auto& step=mission_.plan().steps[i];JsonObject object=steps.createNestedObject();
        object["index"]=i;object["waypointIndex"]=step.waypointIndex;
        object["kind"]=step.kind==RouteStepKind::Move ? "MOVE" : step.kind==RouteStepKind::Altitude ? "ALTITUDE" : "WAYPOINT";
        object["cm"]=step.cm;
        if(step.kind==RouteStepKind::Move) object["direction"]=MissionRoutePlanner::directionName(step.direction);
    }
    if(i<mission_.plan().count) doc["nextRouteOffset"]=i;else doc["nextRouteOffset"]=nullptr;
    sendJson(200,doc);
}

bool ApiServer::readControlBody(JsonDocument& doc) {
    const String body=server_.arg("plain");
    if(body.length()>Config::MAX_API_BODY_BYTES) { sendError(413,"BODY_TOO_LARGE","Control body too large"); return false; }
    JsonBodyReader reader(body);
    const auto error=deserializeJson(doc,reader,DeserializationOption::NestingLimit(2));
    bool trailing=false;
    for(int ch=reader.read();ch>=0;ch=reader.read()) if(!isspace(static_cast<unsigned char>(ch))) trailing=true;
    if(error || trailing || !doc.is<JsonObject>() || body.length()!=static_cast<size_t>(server_.clientContentLength())) {
        sendError(400,"INVALID_JSON","Expected one JSON object"); return false;
    }
    return true;
}
void ApiServer::handleTelloControl(const char* action) {
    if(!manualFlightAllowed()) return;
    StaticJsonDocument<512> doc;
    if(!readControlBody(doc)) return;
    if(!strcmp(action,"rc")) {
        if(!doc["leftRight"].is<int>() || !doc["forwardBack"].is<int>() || !doc["upDown"].is<int>() || !doc["yaw"].is<int>() ||
            !TelloController::validRc(doc["leftRight"],doc["forwardBack"],doc["upDown"],doc["yaw"])) {
            sendError(400,"INVALID_RC","Four integer channels in -100..100 required"); return;
        }
        sendFlightResult(flight_.rc(doc["leftRight"],doc["forwardBack"],doc["upDown"],doc["yaw"])); return;
    }
    const bool move=!strcmp(action,"move");
    const char* field=move ? "distanceCm" : "degrees";
    const char* direction=doc["direction"].as<const char*>();
    if(!direction || doc["direction"].as<JsonString>().size()!=strlen(direction) || !doc[field].is<int>() ||
        !(move ? TelloController::validMove(direction,doc[field]) : TelloController::validRotation(direction,doc[field]))) {
        sendError(400,"INVALID_COMMAND_ARGUMENTS",move ? "Direction and integer distanceCm 20..500 required" : "cw/ccw and integer degrees 1..3600 required"); return;
    }
    sendFlightResult(move ? flight_.move(direction,doc[field]) : flight_.rotate(direction,doc[field]));
}

void ApiServer::sendFlightResult(bool accepted) {
    if (!accepted) { sendError(409, flight_.error(), "Flight request rejected; inspect Tello status"); return; }
    StaticJsonDocument<256> doc;
    doc["success"]=true; doc["state"]=flight_.stateName();
    sendJson(202,doc);
}
void ApiServer::handleTelloPrepare() { if(manualFlightAllowed()) sendFlightResult(flight_.prepare()); }
void ApiServer::handleTelloStatus() {
    StaticJsonDocument<1536> doc;
    doc["success"]=true;
    doc["staTarget"]=GatewayWiFiManager::targetName(wifi_.staTarget());
    doc["telloWifiConnected"]=wifi_.isTelloConnected();
    doc["sdkReady"]=tello_.isSdkReady(); doc["flightState"]=flight_.stateName();
    const auto& t=tello_.telemetry();
    doc["battery"]=tello_.batteryPercent(); doc["heightCm"]=t.heightCm;
    doc["tofCm"]=t.tofCm; doc["flightTimeSec"]=t.flightTimeSec;
    doc["verticalVelocity"]=t.verticalVelocity;
    doc["pitch"]=t.pitch; doc["roll"]=t.roll; doc["yaw"]=t.yaw;
    doc["telemetryFresh"]=tello_.hasFreshTelemetry();
    doc["commandPending"]=tello_.commandPending();
    doc["lastResponse"]=tello_.lastResponse(); doc["errorCode"]=flight_.error();
    doc["landingEvidence"]=flight_.landingEvidence();
    doc["pendingUploadRecords"]=storage_.getPendingCount();
    addAltitudeStatus(doc);
    sendJson(200,doc);
}

void ApiServer::handleGatewayStatus() {
    Logger::api("GET /api/gateway/status");
    StaticJsonDocument<768> doc;
    doc["success"] = true;
    doc["gatewayCode"] = Config::GATEWAY_CODE;
    doc["status"] = "ONLINE";
    doc["uptimeMs"] = millis();
    doc["freeHeap"] = ESP.getFreeHeap();
    doc["apIp"] = WiFi.softAPIP().toString();
    doc["connectedStations"] = WiFi.softAPgetStationNum();
    doc["internetConnected"] = wifi_.isInternetConnected();
    doc["staConnected"] = wifi_.isStaConnected();
    doc["staTarget"] = GatewayWiFiManager::targetName(wifi_.staTarget());
    doc["backendNetworkReady"] = wifi_.isBackendNetworkReady();
    doc["flightState"] = flight_.stateName();
    doc["staIp"] = wifi_.getStaIp().toString();
    doc["connectedNodes"] = registry_.onlineCount();
    doc["timeSynced"] = time_.isTimeSynced();
    doc["pendingUploadRecords"] = storage_.getPendingCount();
    doc["storedRecords"] = storage_.getStoredCount();
    doc["storageReady"] = storage_.isReady();
    sendJson(200, doc);
}

void ApiServer::handleGatewayNodes() {
    Logger::api("GET /api/gateway/nodes");
    registry_.update();
    auto fillNode = [](JsonDocument& doc, const SensorNode& node) {
        doc["deviceCode"] = node.deviceCode;
        doc["farmId"] = node.farmId;
        doc["zoneId"] = node.zoneId;
        doc["firmwareVersion"] = node.firmwareVersion;
        doc["deviceType"] = node.deviceType;
        doc["ipAddress"] = node.ipAddress.toString();
        doc["authenticated"] = node.authenticated;
        doc["online"] = node.online;
        doc["rssi"] = node.rssi;
        doc["lastSeenMs"] = node.lastSeenMs;
    };
    // Check capacities before sending HTTP headers, then stream one node at a time.
    for (size_t i = 0; i < registry_.count(); ++i) {
        StaticJsonDocument<768> doc;
        fillNode(doc, *registry_.at(i));
        if (doc.overflowed() || measureJson(doc) >= 1024) {
            sendError(500, "JSON_OVERFLOW", "Node exceeds JSON buffer");
            return;
        }
    }
    server_.setContentLength(CONTENT_LENGTH_UNKNOWN);
    server_.send(200, "application/json", "");
    server_.sendContent(String("{\"success\":true,\"count\":") + registry_.count() +
                        ",\"onlineCount\":" + registry_.onlineCount() + ",\"nodes\":[");
    for (size_t i = 0; i < registry_.count(); ++i) {
        StaticJsonDocument<768> doc;
        fillNode(doc, *registry_.at(i));
        char output[1024];
        serializeJson(doc, output, sizeof(output));
        if (i) server_.sendContent(",");
        server_.sendContent(output);
    }
    server_.sendContent("]}");
    server_.sendContent("");
}

void ApiServer::handleGatewayTime() {
    Logger::api("GET /api/gateway/time");
    StaticJsonDocument<192> doc;
    doc["success"] = true;
    doc["unixTime"] = time_.unixTime();
    doc["timeSynced"] = time_.isTimeSynced();
    doc["uptimeMs"] = millis();
    sendJson(200, doc);
}

void ApiServer::handleNodeRegistration() {
    Logger::api("POST /api/gateway/nodes/register");
    const String body = server_.arg("plain");
    if (body.length() > Config::MAX_API_BODY_BYTES) {
        sendError(413, "BODY_TOO_LARGE", "Request body exceeds 1024 bytes");
        return;
    }
    StaticJsonDocument<1536> request;
    JsonBodyReader reader{body};
    const DeserializationError error = deserializeJson(
        request, reader, DeserializationOption::NestingLimit(4));
    bool trailingData = false;
    for (int ch = reader.read(); ch >= 0; ch = reader.read()) {
        trailingData |= !isspace(static_cast<unsigned char>(ch));
    }
    if (error || !request.is<JsonObject>() ||
        trailingData ||
        body.length() != static_cast<size_t>(server_.clientContentLength())) {
        Logger::warn("Invalid registration JSON");
        sendError(400, "INVALID_JSON", "Request body contains invalid JSON");
        return;
    }

    if (!request["deviceCode"].is<const char*>()) {
        Logger::warn("Missing deviceCode");
        sendError(400, "MISSING_DEVICE_CODE", "deviceCode is required");
        return;
    }
    const JsonString code = request["deviceCode"].as<JsonString>();
    bool hasVisibleCharacter = false;
    for (size_t i = 0; i < code.size(); ++i) {
        if (code.c_str()[i] == '\0') {
            sendError(400, "INVALID_DEVICE_CODE", "deviceCode contains a null character");
            return;
        }
        hasVisibleCharacter |= !isspace(static_cast<unsigned char>(code.c_str()[i]));
    }
    if (!hasVisibleCharacter) {
        Logger::warn("Missing deviceCode");
        sendError(400, "MISSING_DEVICE_CODE", "deviceCode is required");
        return;
    }
    if (code.size() > Config::MAX_DEVICE_CODE_BYTES) {
        sendError(400, "INVALID_DEVICE_CODE", "deviceCode exceeds 64 bytes");
        return;
    }

    // Restrict identity to printable non-space ASCII to keep logs unambiguous.
    for (size_t i = 0; i < code.size(); ++i) {
        const unsigned char ch = code.c_str()[i];
        if (ch <= 32 || ch > 126) {
            sendError(400, "INVALID_DEVICE_CODE", "deviceCode contains invalid characters");
            return;
        }
    }
    auto validText = [](JsonVariantConst value) {
        if (!value.is<const char*>()) return false;
        const JsonString text = value.as<JsonString>();
        if (!text.size() || text.size() > Config::MAX_NODE_METADATA_BYTES) return false;
        bool visible = false;
        for (size_t i = 0; i < text.size(); ++i) {
            const unsigned char ch = text.c_str()[i];
            if (ch < 32 || ch > 126) return false;
            visible |= ch != ' ';
        }
        return visible;
    };
    if (!request["farmId"].is<int>() || request["farmId"].as<int>() <= 0 ||
        !request["zoneId"].is<int>() || request["zoneId"].as<int>() <= 0 ||
        !validText(request["firmwareVersion"]) || !validText(request["deviceType"])) {
        sendError(400, "INVALID_NODE_METADATA", "Positive farmId/zoneId and valid firmwareVersion/deviceType are required");
        return;
    }
    if (request.containsKey("ipAddress")) {
        IPAddress suppliedIp;
        if (!request["ipAddress"].is<const char*>() ||
            request["ipAddress"].as<JsonString>().size() != strlen(request["ipAddress"].as<const char*>()) ||
            !validIpv4(request["ipAddress"].as<const char*>()) ||
            !suppliedIp.fromString(request["ipAddress"].as<const char*>())) {
            sendError(400, "INVALID_IP_ADDRESS", "ipAddress must be a valid IPv4 address");
            return;
        }
    }
    const String deviceCode = code.c_str();
    Logger::node((String("Registration request: ") + deviceCode).c_str());
    const String headerCode = server_.header("X-Device-Code");
    const String token = server_.header("X-Device-Token");
    if (headerCode.isEmpty() || token.isEmpty()) {
        sendError(401, "MISSING_CREDENTIALS", "Device credentials are required");
        return;
    }
    if (headerCode != deviceCode) {
        sendError(400, "DEVICE_CODE_MISMATCH", "Header and body deviceCode must match");
        return;
    }
    if (!authenticator_.isRegisteredDevice(deviceCode)) {
        Logger::warn((String("Unknown device attempted registration: ") + deviceCode).c_str());
        sendError(401, "UNKNOWN_DEVICE", "Device is not registered");
        return;
    }
    if (!authenticator_.authenticate(deviceCode, token)) {
        Logger::warn((String("Authentication failed for ") + deviceCode).c_str());
        sendError(401, "AUTHENTICATION_FAILED", "Device authentication failed");
        return;
    }
    Logger::auth((deviceCode + " authenticated").c_str());
    SensorNode node;
    node.deviceCode = deviceCode;
    node.farmId = request["farmId"].as<int>();
    node.zoneId = request["zoneId"].as<int>();
    node.firmwareVersion = request["firmwareVersion"].as<const char*>();
    node.deviceType = request["deviceType"].as<const char*>();
    node.ipAddress = server_.client().remoteIP();
    node.authenticated = true;
    const RegistrationResult result = registry_.addOrUpdate(node);
    if (result == RegistrationResult::Full) {
        sendError(503, "NODE_REGISTRY_FULL", "Gateway node registry is full");
        return;
    }
    if (result == RegistrationResult::Unauthenticated) {
        sendError(401, "AUTHENTICATION_FAILED", "Device authentication failed");
        return;
    }
    StaticJsonDocument<256> response;
    response["success"] = true;
    response["message"] = "Node registered";
    response["deviceCode"] = code;
    response["authenticated"] = true;
    sendJson(200, response);
}

void ApiServer::handleNotFound() {
    sendError(404, "NOT_FOUND", "Endpoint not found");
}

void ApiServer::sendJson(int status, const JsonDocument& document) {
    const size_t length=measureJson(document);
    if (document.overflowed() || length>Config::MISSION_MAX_FILE_BYTES) {
        server_.send(500, "application/json",
                     "{\"success\":false,\"errorCode\":\"JSON_OVERFLOW\","
                     "\"message\":\"Response exceeds JSON buffer\"}");
        return;
    }
    String output;
    if(!output.reserve(length+1) || serializeJson(document,output)!=length) {
        server_.send(503,"application/json","{\"success\":false,\"errorCode\":\"MEMORY_UNAVAILABLE\"}");return;
    }
    server_.send(status, "application/json", output);
}

void ApiServer::sendError(int status, const char* code, const char* message) {
    StaticJsonDocument<256> doc;
    doc["success"] = false;
    doc["errorCode"] = code;
    doc["message"] = message;
    sendJson(status, doc);
}
