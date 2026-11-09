#pragma once
#include <Arduino.h>
#include "IPAddress.h"
#include <functional>
#include <map>
constexpr int HTTP_GET=0,HTTP_POST=1,DIGEST_AUTH=1;
class WebServer {
public:
    struct Client {IPAddress address;IPAddress localIP(){return address;}} connection;
    std::map<std::string,String> requestHeaders;
    std::map<std::string,std::function<void()>> routes;
    bool digestValid=false;
    int status=0;
    String output;
    void on(const char* path,int method,std::function<void()> handler) {routes[std::to_string(method)+path]=handler;}
    Client& client() {return connection;}
    String header(const char* name) {return requestHeaders[name];}
    bool authenticate(const char* user,const char* password) {return digestValid && strcmp(user,"admin")==0 && strcmp(password,"test-ap-password")==0;}
    void requestAuthentication(int,const char*,const char*) {status=401;}
    void sendHeader(const char*,const char*) {}
    void send_P(int code,const char*,const char* body) {send(code,"",body);}
    void send(int code,const char*,const String& body) {status=code;output=body;}
    String arg(const char*) {return "";}
    int clientContentLength() {return 0;}
};
