#include "RetryTimer.h"
#include <assert.h>
#include <stdio.h>
int main() {
    RetryTimer timer;
    assert(timer.due(0,15000));
    timer.mark(100);
    assert(!timer.due(15099,15000));
    assert(timer.due(15100,15000));
    timer.mark(UINT32_MAX - 100);
    assert(!timer.due(100,500));
    assert(timer.due(400,500));
    timer.reset(); assert(timer.due(401,15000));
    puts("retry_test: passed");
}
