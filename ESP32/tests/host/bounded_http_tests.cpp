#include <Arduino.h>
#include "BoundedHttp.h"
#include "Config.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=0;TestSerial Serial;
namespace HttpFixture {
std::string body;
size_t position=0;
int status=200,length=0,closes=0;
bool chunked=false;
}
int main() {
    StaticJsonDocument<256> response;int status=0;
    auto request=[&]() {return BoundedHttp::request("http://backend/api/test","POST","",nullptr,"",nullptr,"",response,status,true);};
    HttpFixture::body=R"({"success":false,"message":"PLAN_CHANGED_DOWNLOAD_AGAIN"})";
    HttpFixture::length=int(HttpFixture::body.size());HttpFixture::status=409;
    assert(!request());assert(status==409);
    assert(response["message"]=="PLAN_CHANGED_DOWNLOAD_AGAIN");
    assert(HttpFixture::closes==1);
    HttpFixture::status=200;HttpFixture::body=R"({"success":true})";HttpFixture::length=int(HttpFixture::body.size());
    assert(request());assert(response["success"]==true);
    HttpFixture::status=401;HttpFixture::body="";HttpFixture::length=0;
    assert(!request()); // Empty errors must never count as sensor ACKs.
    HttpFixture::status=204;assert(request());
    HttpFixture::status=409;HttpFixture::length=Config::MAX_HTTP_RESPONSE_BYTES+1;
    assert(!request());assert(response.isNull());
    HttpFixture::length=1;HttpFixture::body="{";assert(!request());
    HttpFixture::body="{} garbage";HttpFixture::length=int(HttpFixture::body.size());assert(!request());
    HttpFixture::chunked=true;HttpFixture::body="{}";HttpFixture::length=2;assert(!request());
    std::cout<<"Bounded HTTP preserves conflict details without accepting error ACKs\n";
}
