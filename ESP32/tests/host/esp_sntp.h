#pragma once
extern unsigned ntpStops;
inline void sntp_stop() { ++ntpStops; }
