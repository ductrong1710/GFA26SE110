#include "JsonSafety.h"
#include <assert.h>
#include <stdio.h>
int main() {
    StaticJsonDocument<1536> doc;
    const char* valid = "{\"recordIds\":[\"SENSOR-001-1\"]}";
    assert(JsonSafety::parse(valid,strlen(valid),doc));
    assert(JsonSafety::ackIds(doc["recordIds"]));
    assert(!JsonSafety::parse("{}x",3,doc));
    assert(!JsonSafety::parse("[]",2,doc));
    assert(!JsonSafety::parse("{",1,doc));
    const char* nul = "{\"recordIds\":[\"SENSOR-001-1\\u0000evil\"]}";
    assert(JsonSafety::parse(nul,strlen(nul),doc));
    assert(!JsonSafety::ackIds(doc["recordIds"]));
    const char* mixed = "{\"recordIds\":[\"SENSOR-001-1\",123]}";
    assert(JsonSafety::parse(mixed,strlen(mixed),doc));
    assert(!JsonSafety::ackIds(doc["recordIds"]));
    size_t count;
    assert(JsonSafety::limit("1",count) && count == 1);
    assert(JsonSafety::limit("99999999999999999",count) && count == 10);
    assert(!JsonSafety::limit("-1",count));
    assert(!JsonSafety::limit("1oops",count));
    assert(!JsonSafety::limit("0",count));
    doc.clear(); doc["unixTime"] = 1720000100ULL;
    assert(JsonSafety::unixTime(doc["unixTime"]));
    doc["unixTime"] = true; assert(!JsonSafety::unixTime(doc["unixTime"]));
    doc["unixTime"] = 1; assert(!JsonSafety::unixTime(doc["unixTime"]));
    doc["unixTime"] = 4102444801ULL; assert(!JsonSafety::unixTime(doc["unixTime"]));
    puts("json_test: passed");
}
