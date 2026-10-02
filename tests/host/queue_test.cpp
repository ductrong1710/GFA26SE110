#include "MeasurementQueue.h"
#include "MemoryStore.h"
#include <assert.h>
#include <stdio.h>
int main() {
    MemoryStore fs;
    MeasurementQueue q(fs);
    assert(q.begin());
    Measurement first;
    first.reading.temperature = 30.2f;
    assert(q.append(first) == SaveResult::Saved && first.sequence == 1);
    assert(q.pendingCount() == 1);
    Measurement read;
    assert(q.readAt(0,read) && read.sequence == 1);
    assert(read.reading.temperature == 30.2f && isnan(read.reading.lightIntensity));
    assert(q.readAt(0,read) && q.pendingCount() == 1); // GET must not ACK.
    MeasurementQueue reboot(fs);
    assert(reboot.begin() && reboot.pendingCount() == 1);
    Measurement next;
    assert(reboot.append(next) == SaveResult::Saved && next.sequence == 65);
    assert(reboot.acknowledge("SENSOR-001-1") == AckResult::Acked);
    assert(reboot.pendingCount() == 1);
    assert(reboot.acknowledge("SENSOR-001-1") == AckResult::NotPending);
    assert(reboot.acknowledge("OTHER-65") == AckResult::NotPending);
    assert(reboot.acknowledge("SENSOR-001-065") == AckResult::NotPending);
    assert(reboot.acknowledge("SENSOR-001-999") == AckResult::NotPending);
    fs.failRename = true;
    assert(reboot.acknowledge("SENSOR-001-65") == AckResult::Error);
    MeasurementQueue lostAck(fs);
    assert(lostAck.begin() && lostAck.pendingCount() == 1);
    fs.failRename = false;
    assert(lostAck.acknowledge("SENSOR-001-65") == AckResult::Acked);
    MeasurementQueue ackReboot(fs);
    assert(ackReboot.begin() && ackReboot.pendingCount() == 0);
    for (size_t i = 0; i < Config::MAX_LOCAL_RECORDS; ++i) assert(ackReboot.append(next) == SaveResult::Saved);
    assert(ackReboot.append(next) == SaveResult::Full);
    assert(ackReboot.pendingCount() == Config::MAX_LOCAL_RECORDS);
    MemoryStore broken;
    MeasurementQueue failed(broken);
    assert(failed.begin());
    broken.failWrite = true;
    assert(failed.append(next) == SaveResult::Error && failed.pendingCount() == 0);
    MemoryStore low;
    MeasurementQueue full(low); assert(full.begin()); low.available = 0;
    assert(full.append(next) == SaveResult::Full && full.pendingCount() == 0);
    MemoryStore interrupted;
    MeasurementQueue beforeCut(interrupted); assert(beforeCut.begin());
    assert(beforeCut.append(next) == SaveResult::Saved);
    interrupted.files["/pending/0000000002.rec.tmp"] = "torn write";
    MeasurementQueue afterCut(interrupted); assert(afterCut.begin());
    afterCut.update();
    assert(!interrupted.files.count("/pending/0000000002.rec.tmp"));
    assert(afterCut.pendingCount() == 1 && afterCut.readAt(0,read) && read.sequence == 1);
    // Repeat interrupted writes with distinct reserved sequence IDs, then cleanup.
    interrupted.files["/pending/0000000066.rec.tmp"] = "interrupted";
    interrupted.files["/pending/0000000129.rec.tmp"] = "interrupted";
    MeasurementQueue repeatedCut(interrupted); assert(repeatedCut.begin());
    repeatedCut.update(); repeatedCut.update();
    assert(!interrupted.files.count("/pending/0000000066.rec.tmp"));
    assert(!interrupted.files.count("/pending/0000000129.rec.tmp"));
    assert(repeatedCut.pendingCount() == 1);
    interrupted.files["/pending/0000000001.rec"][12] ^= 1;
    MeasurementQueue corruptRecord(interrupted); assert(!corruptRecord.begin());
    MemoryStore exhausted;
    exhausted.files["/sequence0"] = "2539b297\n{\"deviceCode\":\"SENSOR-001\",\"high\":4294967294}";
    exhausted.files["/sequence1"] = exhausted.files["/sequence0"];
    MeasurementQueue endOfSequence(exhausted); assert(endOfSequence.begin());
    assert(endOfSequence.append(next) == SaveResult::Saved && next.sequence == UINT32_MAX);
    assert(endOfSequence.append(next) == SaveResult::SequenceExhausted);
    MeasurementQueue exhaustedReboot(exhausted); assert(exhaustedReboot.begin());
    assert(exhaustedReboot.append(next) == SaveResult::SequenceExhausted);
    MemoryStore missingMetadata;
    MeasurementQueue initialized(missingMetadata); assert(initialized.begin());
    assert(initialized.append(next) == SaveResult::Saved);
    assert(initialized.acknowledge("SENSOR-001-1") == AckResult::Acked);
    initialized.update();
    MemoryStore oneMissing = missingMetadata;
    oneMissing.files.erase("/sequence1");
    MeasurementQueue lostNewest(oneMissing); assert(!lostNewest.begin());
    missingMetadata.files.erase("/sequence0"); missingMetadata.files.erase("/sequence1");
    MeasurementQueue lostMetadata(missingMetadata); assert(!lostMetadata.begin());
    // Corrupt committed metadata must never reset sequence and reuse identity.
    fs.files["/sequence0"] = "corrupt";
    MeasurementQueue corrupt(fs); assert(!corrupt.begin());
    puts("queue_test: passed");
}
