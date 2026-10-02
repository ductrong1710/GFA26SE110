#include "HttpBounds.h"
#include <string>
#include <assert.h>
#include <stdio.h>
struct Client {
    std::string bytes; size_t offset = 0;
    int available() { return offset < bytes.size(); }
    bool connected() { return true; }
    int read() { return offset < bytes.size() ? bytes[offset++] : -1; }
};
int main() {
    size_t length = 0;
    assert(HttpBounds::contentLength("1024",1024,length) && length == 1024);
    assert(!HttpBounds::contentLength("1025",1024,length));
    assert(!HttpBounds::contentLength("999999999999999999999",1024,length));
    assert(!HttpBounds::contentLength("-1",1024,length));
    assert(!HttpBounds::contentLength("2x",1024,length));
    assert(!HttpBounds::contentLength("",1024,length));
    uint32_t now = 0; auto clock = [&]() { return now; }; auto idle = [&]() { ++now; };
    Client client{"GET /api/node/info HTTP/1.0\r\n"}; std::string out;
    assert(HttpBounds::line(client,out,256,0,750,clock,idle));
    assert(out == "GET /api/node/info HTTP/1.0");
    Client longLine{std::string(257,'X') + "\r\n"};
    assert(!HttpBounds::line(longLine,out,256,0,750,clock,idle));
    Client slow{"X"}; assert(!HttpBounds::line(slow,out,256,0,750,clock,idle));
    assert(now == 750);
    Client invalid{"X\n"}; now = 0; assert(!HttpBounds::line(invalid,out,256,0,750,clock,idle));
    puts("http_bounds_test: passed");
}
