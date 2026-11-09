#include "CollectionManager.h"
#include "configured_gateway.h"
#include <cassert>
#include <iostream>
#include <vector>
#include <algorithm>
uint32_t testMillis=10000;TestSerial Serial;
std::vector<std::string> events;bool saveOk=true,ackOk=true,duplicate=false;
uint32_t savedMission=0;
bool StorageManager::markNodeAcknowledged(const String&,const String&){return true;}
bool StorageManager::begin(){ready_=true;return true;}
SaveResult StorageManager::saveMeasurement(const GatewayMeasurement& r){events.push_back("save");savedMission=r.missionId;return !saveOk?SaveResult::Failed:duplicate?SaveResult::Duplicate:SaveResult::Saved;}
bool TimeManager::isTimeSynced()const{return true;}
uint64_t TimeManager::unixTime()const{return 1700000000;}
uint64_t TimeManager::collectionTime()const{return unixTime();}
bool SensorNodeClient::getInfo(const SensorNode&,uint32_t& n){events.push_back("info");n=1;return true;}
bool SensorNodeClient::sendTime(const SensorNode&,uint64_t){return true;}
bool SensorNodeClient::getData(const SensorNode&,JsonDocument& doc){events.push_back("fetch");doc.clear();doc.createNestedArray("records").createNestedObject()["recordId"]="record-1";return true;}
bool SensorNodeClient::acknowledge(const SensorNode&,const String*,size_t n){assert(n==1);events.push_back("ack");return ackOk;}
bool MeasurementCodec::fromJson(JsonObjectConst,const String&,GatewayMeasurement& out,bool){out.recordId="record-1";return true;}
namespace Logger {void warn(const char*){} void error(const char*){} void node(const char*){}}
int main(){
    NodeRegistry registry;NodeAuthenticator auth(nullptr,0);SensorNodeClient client(auth);
    StorageManager storage;storage.begin();TimeManager time;
    CollectionManager collection(registry,client,storage,time);
    collection.setMissionMode(true);assert(!collection.requestTarget("SENSOR-001"));
    SensorNode node;node.deviceCode="SENSOR-001";node.authenticated=true;
    registry.addOrUpdate(node);
    assert(!collection.requestTarget("SENSOR-001"));
    collection.setMissionMode(false);
    for(unsigned i=0;i<12;++i)collection.update();
    assert(events.empty()); // First setup cannot create pending data that locks provisioning.
    provisionTestGateway();collection.setMissionMode(true);collection.setMissionId(15);
    assert(collection.requestTarget("SENSOR-001"));
    for(unsigned i=0;i<12;++i)collection.update();
    assert(collection.targetState()==MissionCollectionState::Collected);
    assert(savedMission==15 && collection.targetRecordCount()==1);
    auto saved=std::find(events.begin(),events.end(),"save"),ack=std::find(events.begin(),events.end(),"ack");
    assert(saved!=events.end() && ack!=events.end() && saved<ack);
    events.clear();saveOk=false;assert(collection.requestTarget("SENSOR-001"));
    for(unsigned i=0;i<12;++i)collection.update();
    assert(collection.targetState()==MissionCollectionState::Failed);
    assert(collection.targetRecordCount()==0);
    assert(std::find(events.begin(),events.end(),"ack")==events.end());
    events.clear();saveOk=true;ackOk=false;assert(collection.requestTarget("SENSOR-001"));
    for(unsigned i=0;i<12;++i)collection.update();
    assert(collection.targetState()==MissionCollectionState::Failed);
    assert(collection.targetRecordCount()==1); // Persisted data survives a lost sensor ACK.
    events.clear();ackOk=true;duplicate=true;collection.setMissionId(16);
    assert(collection.requestTarget("SENSOR-001"));
    for(unsigned i=0;i<12;++i)collection.update();
    assert(collection.targetState()==MissionCollectionState::Failed);
    assert(collection.targetRecordCount()==0); // Older persisted identities are never reattributed.
    assert(std::find(events.begin(),events.end(),"ack")!=events.end());
    duplicate=false;
    events.clear();ackOk=true;assert(collection.requestTarget("SENSOR-001"));collection.cancelTarget();
    for(unsigned i=0;i<12;++i)collection.update();
    assert(events.empty());
    testMillis+=31000;registry.update();assert(!collection.requestTarget("SENSOR-001"));
    std::cout<<"Targeted collection requires online identity and verified save-before-ACK\n";
}
