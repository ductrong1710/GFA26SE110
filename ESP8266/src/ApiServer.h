#pragma once
#include <ESP8266WebServer.h>
#include <ArduinoJson.h>
#include "MeasurementQueue.h"
#include "TimeManager.h"
class ApiServer {
public:
    ApiServer(MeasurementQueue& queue, TimeManager& time) : queue_(queue), time_(time) {}
    void begin();
    void handleClient() { server_.handleClient(); }
private:
    ESP8266WebServer server_{Config::HTTP_PORT};
    MeasurementQueue& queue_;
    TimeManager& time_;
    DynamicJsonDocument doc_{1536};
    char output_[Config::MAX_RECORD_BYTES + 32]{};
    bool authenticate();
    bool body();
    void info();
    void data();
    void ack();
    void health();
    void setTime();
    void send(int status = 200);
    void error(int status, const char* code, const char* message);
};
