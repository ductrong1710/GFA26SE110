#include "TimeManager.h"
#include <assert.h>
#include <stdio.h>
int main() {
    TimeManager time;
    assert(!time.synced() && time.unixTime() == 0);
    assert(!time.synchronize(1,0));
    assert(time.synchronize(1720000100,100));
    assert(time.synced() && time.unixTime() == 1720000100);
    time.update(1099); assert(time.unixTime() == 1720000100);
    time.update(1100); assert(time.unixTime() == 1720000101);
    assert(time.synchronize(1720000100,UINT32_MAX - 499));
    time.update(500); assert(time.unixTime() == 1720000101);
    for (int i = 0; i < 10; ++i) time.update(600 + i * 100);
    assert(time.unixTime() == 1720000102); // Fractional milliseconds are retained.
    assert(!time.synchronize(0,1500) && time.synced());
    assert(time.synchronize(4102444800ULL,0));
    time.update(1000); assert(!time.synced() && time.unixTime() == 0);
    TimeManager reboot; assert(!reboot.synced());
    puts("time_test: passed");
}
