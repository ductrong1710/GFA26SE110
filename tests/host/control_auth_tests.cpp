#include "ConfigurationPortal.h"
#include <WiFi.h>
#include <cassert>
#include <iostream>
uint32_t testMillis=0;TestSerial Serial;FakeWiFi WiFi;
int main() {
    WebServer server;ConfigurationPortal portal(server,[](){return true;},[](){});
    int commands=0;
    portal.begin();
    portal.onControl("/api/tello/land",[&](){++commands;});
    auto control=[&](){server.routes.at("1/api/tello/land")();};
    server.connection.address=WiFi.softAPIP();
    control();assert(server.status==401 && commands==0);
    server.requestHeaders["Authorization"]="Basic credentials";server.digestValid=true;
    control();assert(server.status==401 && commands==0);
    server.requestHeaders["Authorization"]="Digest credentials";server.digestValid=false;
    control();assert(server.status==401 && commands==0);
    server.digestValid=true;
    server.routes.at("0/api/config")();assert(server.status==200);
    StaticJsonDocument<2048> config;assert(!deserializeJson(config,server.output.c_str()));
    const String token=config["csrfToken"].as<const char*>();assert(!token.isEmpty());
    control();assert(server.status==403 && commands==0);
    server.requestHeaders["X-Config-Token"]="wrong";
    control();assert(server.status==403 && commands==0);
    server.requestHeaders["X-Config-Token"]=token;
    control();assert(commands==1);
    server.connection.address=IPAddress();
    control();assert(server.status==403 && commands==1);
    std::cout<<"Flight controls require AP interface, Digest authentication and current token\n";
}
