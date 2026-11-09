#include "HeartbeatSchedule.h"
#include <assert.h>
#include <stdio.h>

int main() {
    HeartbeatSchedule schedule;
    assert(!schedule.due(0, false));
    assert(schedule.due(0, true));
    schedule.attempted(0);
    assert(!schedule.due(29999, true));
    assert(schedule.due(30000, true));
    assert(!schedule.due(60000, false));
    assert(schedule.due(60000, true));
    schedule.attempted(60000); // Failure also waits the full interval.
    assert(!schedule.due(60001, true));
    schedule.attempted(UINT32_MAX - 10000);
    assert(!schedule.due(19998, true));
    assert(schedule.due(19999, true));
    puts("Heartbeat schedule tests passed");
}
