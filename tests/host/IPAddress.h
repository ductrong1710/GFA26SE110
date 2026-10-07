#pragma once
#include <stdint.h>
#include "Arduino.h"
struct IPAddress {
    uint32_t value = 0;
    IPAddress() = default;
    explicit IPAddress(const uint8_t* p) : value((uint32_t(p[0])<<24)|(uint32_t(p[1])<<16)|(uint32_t(p[2])<<8)|p[3]) {}
    bool operator==(const IPAddress& other) const { return value == other.value; }
    bool operator!=(const IPAddress& other) const { return !(*this == other); }
    String toString() const { return "192.168.10.2"; }
};
