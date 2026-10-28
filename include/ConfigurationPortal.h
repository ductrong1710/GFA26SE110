#pragma once
#include <WebServer.h>
#include <ArduinoJson.h>
#include <functional>

class ConfigurationPortal {
public:
    ConfigurationPortal(WebServer& server, std::function<bool()> canChange,
                        std::function<void()> saved)
        : server_(server), canChange_(canChange), saved_(saved) {}
    // ApiServer must collect Authorization, Content-Type and X-Config-Token.
    void begin();
    void onControl(const char* path, std::function<void()> handler);
private:
    WebServer& server_;
    std::function<bool()> canChange_;
    std::function<void()> saved_;
    String token_;
    bool savedOnce_ = false;
    bool authorize();
    void headers();
    void reply(int status, const JsonDocument& doc);
    void error(int status, const char* message);
    void get();
    void save();
};
