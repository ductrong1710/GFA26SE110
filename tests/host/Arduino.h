#pragma once
#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <string>
class String {
    std::string text_;
public:
    String(const char* text="") : text_(text) {}
    String(const std::string& text) : text_(text) {}
    const char* c_str() const { return text_.c_str(); }
    String operator+(const char* text) const { return text_+text; }
    String operator+(const String& text) const { return text_+text.text_; }
    bool operator==(const char* text) const { return text_==text; }
};
extern unsigned ntpStarts;
inline void configTime(long,int,const char*) { ++ntpStarts; }
extern uint32_t testMillis;
inline uint32_t millis() { return testMillis; }
struct TestSerial { template<class... Args> void printf(const char*, Args...) {} };
extern TestSerial Serial;
