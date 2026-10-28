#include "MissionRoutePlanner.h"
#include "FlightSafetyController.h"
#include <cassert>
#include <iostream>
uint32_t testMillis=1000;TestSerial Serial;
int main() {
    {
        FlightSafetyController assessment(AltitudeLimits(120,100,80));
        TelloTelemetry sample;sample.valid=true;sample.heightCm=80;sample.tofCm=110;
        sample.sample=1;sample.lastReceivedMs=testMillis;
        assessment.update(sample,true);
        assert(assessment.filterVerticalRc(3)==0); // Disagreement even below ceiling.
        assert(!assessment.allowMove("up",5));
        assert(!assessment.inOperatingRange());
        assert(assessment.sensorsDisagree() && !assessment.disagreementConfirmed());
        testMillis+=2100;assessment.update(sample,true);
        assert(!assessment.disagreementConfirmed()); // Re-reading a packet is not sustained evidence.
        for(unsigned i=0;i<5;++i) {
            ++sample.sample;sample.lastReceivedMs+=500;assessment.update(sample,true);
        }
        assert(assessment.disagreementConfirmed());
        sample.heightCm=100;sample.tofCm=125;++sample.sample;assessment.update(sample,true);
        assert(!assessment.sensorsDisagree()); // Exactly 25 cm is within threshold.
        assert(assessment.limitActive() && !assessment.allowMove("up",1));
        sample.heightCm=80;sample.tofCm=124;++sample.sample;assessment.update(sample,true);
        assert(!assessment.disagreementConfirmed());
        sample.lastReceivedMs+=3000;++sample.sample;assessment.update(sample,true);
        assert(!assessment.disagreementConfirmed()); // A packet gap restarts the window.
        assessment.update(sample,false);assert(!assessment.disagreementConfirmed());
        sample.heightCm=-1;sample.tofCm=100;++sample.sample;assessment.update(sample,true);
        assert(!assessment.altitudeFresh() && assessment.altitudeCm()==-1);
        assert(assessment.filterVerticalRc(3)==0); // No silent fallback to ToF.
        sample.heightCm=100;sample.tofCm=-1;++sample.sample;assessment.update(sample,true);
        assert(assessment.altitudeCm()==100 && !assessment.sensorsDisagree());
        assert(assessment.inOperatingRange());
    }
    Mission m;m.id=1;m.gatewayId=1;m.uavId=1;strcpy(m.name,"route");m.waypointCount=1;
    auto& w=m.waypoints[0];w.id=1;w.sequenceNo=1;w.xCm=121;w.altitudeCm=35;
    MissionRoutePlanner planner;MissionRoutePlan plan;const char* error="";
    assert(planner.build(m,1,plan,error));
    int total=0;for(size_t i=0;i<plan.count;++i) if(plan.steps[i].kind==RouteStepKind::Move) {
        assert(plan.steps[i].cm>=20 && plan.steps[i].cm<=100);total+=plan.steps[i].cm;
    }
    assert(total==121);
    w.altitudeCm=40;assert(planner.build(m,1,plan,error));
    w.altitudeCm=50;assert(!planner.build(m,1,plan,error));assert(!strcmp(error,"MISSION_ALTITUDE_EXCEEDS_LIMIT"));
    MissionRoutePlanner larger(AltitudeLimits(60,35,20));assert(larger.build(m,1,plan,error));
    w.altitudeCm=35;w.xCm=10;assert(!planner.build(m,1,plan,error));
    w.xCm=20;m.waypointCount=2;m.waypoints[1]=w;assert(!planner.build(m,1,plan,error));
    m.waypointCount=1;assert(!planner.build(m,2,plan,error));
    w.action=MissionAction::Collect;assert(!planner.build(m,1,plan,error));
    m.targetCount=1;m.targets[0].sensorNodeId=7;m.targets[0].waypointId=1;strcpy(m.targets[0].deviceCode,"SENSOR-001");
    assert(planner.build(m,1,plan,error));
    FlightSafetyController safety;
    assert(safety.configurationValid());
    TelloTelemetry t;t.valid=true;t.heightCm=29;t.tofCm=29;t.landingFieldsValid=true;
    safety.update(t,true);assert(safety.filterVerticalRc(40)==40);
    t.heightCm=t.tofCm=30;safety.update(t,true);assert(safety.filterVerticalRc(40)==10);
    t.heightCm=t.tofCm=35;safety.update(t,true);assert(safety.filterVerticalRc(40)==3);
    assert(!safety.allowMove("up",20));
    t.heightCm=t.tofCm=40;safety.update(t,true);assert(safety.filterVerticalRc(40)==0);assert(safety.limitActive());
    assert(safety.allowMove("down",20));assert(!safety.allowMove("down",21));
    safety.update(t,false);assert(safety.filterVerticalRc(40)==0);assert(!safety.allowMove("up",20));
    assert(!safety.allowMove("down",20));assert(!safety.inOperatingRange());
    t.heightCm=t.tofCm=20;safety.update(t,true);assert(safety.filterVerticalRc(-20)==0);
    t.heightCm=35;t.tofCm=45;safety.update(t,true);assert(safety.filterVerticalRc(40)==0);
    FlightSafetyController invalid(AltitudeLimits(0,35,20));assert(!invalid.configurationValid());
    invalid.update(t,true);assert(invalid.filterVerticalRc(40)==0);
    FlightSafetyController small(AltitudeLimits(8,5,1));t.heightCm=t.tofCm=1;small.update(t,true);
    assert(small.filterVerticalRc(100)<=10);
    std::cout<<"Mission route bounds, splitting, configurable ceiling and fresh telemetry safety tests passed\n";
}
