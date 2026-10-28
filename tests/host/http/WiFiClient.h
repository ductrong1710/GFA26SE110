#pragma once
#include <Arduino.h>
#include <algorithm>
namespace HttpFixture {
extern std::string body;
extern size_t position;
}
class WiFiClient {
public:
    int available() {return int(HttpFixture::body.size()-HttpFixture::position);}
    int read(uint8_t* out,size_t size) {
        size=std::min(size,size_t(available()));
        memcpy(out,HttpFixture::body.data()+HttpFixture::position,size);
        HttpFixture::position+=size;return int(size);
    }
};
