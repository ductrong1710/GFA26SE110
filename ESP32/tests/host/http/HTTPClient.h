#pragma once
#include "WiFiClient.h"
namespace HttpFixture {
extern int status, length, closes;
extern bool chunked;
}
class HTTPClient {
    WiFiClient* socket_=nullptr;
public:
    void setConnectTimeout(int) {}
    void setTimeout(int) {}
    void setReuse(bool) {}
    void useHTTP10(bool) {}
    bool begin(WiFiClient& socket,const String&) {socket_=&socket;HttpFixture::position=0;return true;}
    void collectHeaders(const char**,size_t) {}
    void addHeader(const char*,const String&) {}
    int sendRequest(const char*,const String&) {return HttpFixture::status;}
    int getSize() {return HttpFixture::length;}
    bool hasHeader(const char*) {return HttpFixture::chunked;}
    bool connected() {return false;}
    WiFiClient* getStreamPtr() {return socket_;}
    void end() {++HttpFixture::closes;}
};
