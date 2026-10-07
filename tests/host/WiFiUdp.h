#pragma once
#include "IPAddress.h"
#include <deque>
#include <map>
#include <string>
#include <vector>
#include <algorithm>
struct Packet { IPAddress ip; uint16_t port; std::string body; };
extern std::map<uint16_t, std::deque<Packet>> incoming;
extern std::vector<Packet> outgoing;
class WiFiUDP {
    uint16_t local_ = 0, remote_ = 0;
    IPAddress destination_;
    std::string body_;
    Packet current_;
public:
    bool begin(IPAddress, uint16_t port) { local_ = port; return true; }
    void stop() { incoming[local_].clear(); local_ = 0; }
    int beginPacket(IPAddress ip, uint16_t port) { destination_=ip; remote_=port; body_.clear(); return 1; }
    size_t write(const uint8_t* bytes, size_t count) { body_.append(reinterpret_cast<const char*>(bytes),count); return count; }
    int endPacket() { outgoing.push_back({destination_,remote_,body_}); return 1; }
    int parsePacket() { if (incoming[local_].empty()) return 0; current_=incoming[local_].front(); incoming[local_].pop_front(); return int(current_.body.size()); }
    IPAddress remoteIP() { return current_.ip; }
    uint16_t remotePort() { return current_.port; }
    int read(char* buffer, size_t count) { count=std::min(count,current_.body.size()); memcpy(buffer,current_.body.data(),count); return int(count); }
    void flush() {}
};
