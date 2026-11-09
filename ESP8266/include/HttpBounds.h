#pragma once
#include <stddef.h>
#include <stdint.h>
namespace HttpBounds {
inline bool contentLength(const char* input, size_t maximum, size_t& length) {
    length = 0;
    if (!input || !*input) return false;
    for (const char* p = input; *p; ++p) {
        if (*p < '0' || *p > '9') return false;
        const size_t digit = *p - '0';
        if (digit > maximum || length > (maximum - digit) / 10) return false;
        length = length * 10 + digit;
    }
    return true;
}
template<class Client, class Text, class Clock, class Idle>
bool line(Client& client, Text& out, size_t maximum, uint32_t start, uint32_t timeout, Clock now, Idle idle) {
    out = ""; bool cr = false;
    while (uint32_t(now() - start) < timeout) {
        if (!client.available()) {
            if (!client.connected()) return false;
            idle(); continue;
        }
        const int ch = client.read();
        if (cr) return ch == '\n';
        if (ch == '\r') { cr = true; continue; }
        if (ch < 0 || ch == '\n' || ch == 0 || out.length() >= maximum) return false;
        out += static_cast<char>(ch);
    }
    return false;
}
}
