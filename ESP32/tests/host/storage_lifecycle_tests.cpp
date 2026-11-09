#include "StorageManager.h"
#include "LittleFS.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=0;TestSerial Serial;MemoryLittleFS LittleFS;TestEsp ESP;
namespace MemoryFlash {std::map<std::string,std::string> files;std::set<std::string> directories;bool failRename=false,failRemove=false;}
namespace Logger {void info(const char*){} void error(const char*){} }
GatewayMeasurement sample(unsigned sequence) {
    GatewayMeasurement r;r.deviceCode="node";r.sequence=sequence;r.recordId=r.deviceCode+"-"+String(sequence);
    r.missionId=sequence%2?15:16;r.temperature.present=true;r.temperature.value=25;
    return r;
}
int main() {
    StorageManager storage;assert(storage.begin());
    for(unsigned i=1;i<=1200;++i) {
        auto r=sample(i);assert(storage.saveMeasurement(r)==SaveResult::Saved);
        assert(storage.markNodeAcknowledged(r.deviceCode,r.recordId));
        assert(storage.markSynced(r));assert(storage.cleanup());
        assert(storage.getStoredCount()==0);
    }
    auto r=sample(1201);assert(storage.saveMeasurement(r)==SaveResult::Saved);
    assert(storage.markSynced(r));assert(storage.cleanup()); // Lost sensor ACK retains identity.
    StorageManager reboot;assert(reboot.begin());
    assert(reboot.saveMeasurement(r)==SaveResult::Duplicate);
    assert(reboot.markNodeAcknowledged(r.deviceCode,r.recordId));
    assert(reboot.getStoredCount()==0);
    auto a=sample(1203),b=sample(1204);
    assert(reboot.saveMeasurement(a)==SaveResult::Saved);assert(reboot.saveMeasurement(b)==SaveResult::Saved);
    assert(reboot.getPendingCount(15)==1 && reboot.getPendingCount(16)==1);
    MemoryFlash::failRename=true;
    assert(!reboot.markNodeAcknowledged(a.deviceCode,a.recordId));
    MemoryFlash::failRename=false;
    StorageManager again;assert(again.begin());assert(again.saveMeasurement(a)==SaveResult::Duplicate);
    assert(again.getPendingCount()==2);
    std::cout<<"Storage reclaims confirmed deliveries beyond lifetime cap; lost ACK and failed writes retain data\n";
}
