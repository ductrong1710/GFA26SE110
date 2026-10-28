#include "BackendAdapter.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=0;TestSerial Serial;
GatewayMeasurement reading(uint32_t sequence,uint32_t mission) {
    GatewayMeasurement r;r.deviceCode="SENSOR-001";r.sequence=sequence;
    r.recordId=r.deviceCode+"-"+String(sequence);r.missionId=mission;
    r.timeSynced=r.collectedTimeSynced=true;r.measuredAt=1700000000;r.collectedAt=1700000001;
    r.temperature.present=true;r.temperature.value=23;
    return r;
}
int main() {
    DynamicJsonDocument doc(16384),response(16384);
    GatewayMeasurement original=reading(1,15),decoded;
    MeasurementCodec::toJson(original,doc.to<JsonObject>());
    assert(MeasurementCodec::fromJson(doc.as<JsonObjectConst>(),original.deviceCode,decoded,true));
    assert(decoded.missionId==15);
    doc.remove("missionId");
    assert(MeasurementCodec::fromJson(doc.as<JsonObjectConst>(),original.deviceCode,decoded,true) && decoded.missionId==0);
    doc["missionId"]=nullptr;
    assert(MeasurementCodec::fromJson(doc.as<JsonObjectConst>(),original.deviceCode,decoded,true) && decoded.missionId==0);
    doc["missionId"]=-1;
    assert(!MeasurementCodec::fromJson(doc.as<JsonObjectConst>(),original.deviceCode,decoded,true));
    doc["missionId"]=99;
    assert(MeasurementCodec::fromJson(doc.as<JsonObjectConst>(),original.deviceCode,decoded,false) && decoded.missionId==0);
    GatewayMeasurement records[]={reading(1,15),reading(2,0)};
    records[0].humidity.present=true;records[0].humidity.value=70;
    BackendAdapter adapter;String key;
    assert(adapter.build(records,2,doc,key));
    assert(doc["missionId"]==15 && doc["records"].size()==2);
    response["success"]=true;response["data"]["batchKey"]=key;
    JsonArray results=response["data"].createNestedArray("records");
    for(size_t i=0;i<2;++i) {
        JsonObject result=results.createNestedObject();result["index"]=i;
        result["sourceRecordKey"]=records[0].recordId;
        result["status"]="ACCEPTED";
    }
    bool accepted[2]={};
    assert(adapter.accepted(response,key,records,2,accepted));
    assert(accepted[0] && !accepted[1]);
    results[1]["status"]="REJECTED";
    assert(adapter.accepted(response,key,records,2,accepted));
    assert(!accepted[0] && !accepted[1]);
    records[1].missionId=15;
    assert(adapter.build(records,2,doc,key));
    assert(doc["missionId"]==15 && doc["records"].size()==3);
    records[0]=reading(3,0);records[1]=reading(4,16);
    assert(adapter.build(records,2,doc,key));
    assert(doc["missionId"].isNull() && doc["records"].size()==1);
    // Unknown measurement times must be durably archived without inventing a timestamp.
    records[0].timeSynced=false;
    assert(adapter.build(records,2,doc,key));
    assert(doc["missionId"].isNull() && doc["records"].size()==0);
    assert(doc["unresolvedRecords"].size()==1);
    assert(doc["unresolvedRecords"][0]["payload"]["timeSynced"]==false);
    assert(doc["unresolvedRecords"][0]["sourceRecordKey"].as<String>()==records[0].recordId);
    response.clear();response["success"]=true;response["data"]["batchKey"]=key;
    response["data"].createNestedArray("records");
    JsonObject archived=response["data"].createNestedArray("unresolvedRecords").createNestedObject();
    archived["index"]=0;archived["sourceRecordKey"]=records[0].recordId;archived["status"]="ACCEPTED";
    assert(adapter.accepted(response,key,records,2,accepted) && accepted[0] && !accepted[1]);
    archived["status"]="REJECTED";
    assert(adapter.accepted(response,key,records,2,accepted) && !accepted[0]);
    response["data"].remove("unresolvedRecords");
    assert(!adapter.accepted(response,key,records,2,accepted));
    records[0]=reading(5,0);records[0].temperature.present=false;
    assert(adapter.build(records,1,doc,key) && doc["unresolvedRecords"].size()==1);
    records[0]=reading(6,0);records[0].collectedTimeSynced=false;
    assert(adapter.build(records,1,doc,key) && doc["unresolvedRecords"].size()==1);
    std::cout<<"Persisted mission IDs, legacy records, mixed batches and row ACK ownership verified\n";
}
