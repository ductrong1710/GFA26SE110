#include "AltitudeController.h"
#include <cassert>
#include <cstring>
#include <iostream>
uint32_t testMillis=0; TestSerial Serial;
struct Rig {
    TelloTelemetry t;
    FlightSafetyController safety;
    AltitudeController control;
    Rig(int height=10,uint32_t now=0) {
        t.heightCm=t.tofCm=height;t.valid=t.landingFieldsValid=true;
        t.sample=1;t.lastReceivedMs=now;
        control.begin(Config::TELLO_TARGET_ALTITUDE_CM,t,now);
    }
    AltitudeAction tick(uint32_t now,int h,bool fresh=true,bool newSample=true) {
        testMillis=now;t.heightCm=h;t.tofCm=h;
        if(newSample) {++t.sample;t.lastReceivedMs=now;}
        safety.update(t,fresh);return control.update(t,safety,fresh,now);
    }
};
int main() {
    const int target=Config::TELLO_TARGET_ALTITUDE_CM;
    const uint32_t stable=Config::TELLO_ALTITUDE_STABLE_MS;
    // The takeoff ACK sample cannot produce a correction; dispatch is explicit.
    Rig r;
    assert(r.tick(499,10)==AltitudeAction::Wait);
    assert(r.tick(500,10,true,false)==AltitudeAction::Wait);
    assert(r.tick(501,10)==AltitudeAction::Up20);
    assert(r.tick(502,10,true,false)==AltitudeAction::Up20);
    r.control.actionStarted();assert(r.tick(1000,10)==AltitudeAction::Wait);
    r.control.actionFinished(r.t,1000);
    assert(r.tick(1500,10,true,false)==AltitudeAction::Wait);
    assert(r.tick(1501,10)==AltitudeAction::Up20);
    r.control.actionStarted();r.control.actionFailed("SEND_FAILED");
    assert(r.tick(1502,10)==AltitudeAction::Failed && !r.control.active());
    assert(!strcmp(r.control.error(),"SEND_FAILED"));
    // No duplicate sample may advance the stability window.
    Rig band(target);
    assert(band.tick(500,target)==AltitudeAction::Wait);
    assert(band.tick(500+stable,target,true,false)==AltitudeAction::Wait);
    assert(band.tick(500+stable,target)==AltitudeAction::Done);
    Rig interrupted(target);interrupted.tick(500,target);
    interrupted.control.invalidateStability();
    assert(interrupted.tick(600,target)==AltitudeAction::Wait);
    assert(interrupted.tick(500+stable,target)==AltitudeAction::Wait);
    assert(interrupted.tick(600+stable,target)==AltitudeAction::Done);
    // Missing velocity, high speed, out-of-band and stale data reset stability.
    for(int cause=0;cause<4;++cause) {
        Rig reset(target);reset.tick(500,target);
        reset.t.landingFieldsValid=cause!=0;reset.t.verticalVelocity=cause==1?6:0;
        assert(reset.tick(700,cause==2?target+11:target,cause!=3)==AltitudeAction::Wait || cause==2);
        reset.t.landingFieldsValid=true;reset.t.verticalVelocity=0;
        assert(reset.tick(800,target)==AltitudeAction::Wait);
        assert(reset.tick(799+stable,target)==AltitudeAction::Wait);
        assert(reset.tick(800+stable,target)==AltitudeAction::Done);
    }
    Rig velocity(target);velocity.t.verticalVelocity=100;
    assert(velocity.tick(500,target)==AltitudeAction::Wait);
    Rig near(target-11);assert(near.tick(500,target-11)==(Config::TELLO_ALTITUDE_TOLERANCE_CM>=9 ? AltitudeAction::Up20 : AltitudeAction::Wait));
    Rig down(target+20);assert(down.tick(500,target+20)==AltitudeAction::Down20);
    Rig smallDown(target+11);assert(smallDown.tick(500,target+11)==AltitudeAction::Pulse);
    assert(smallDown.control.vertical()==-5);
    // ToF constrains ascent even though H remains the primary input.
    Rig cap(Config::TELLO_MAX_ALTITUDE_CM-20);
    cap.control.begin(Config::TELLO_MAX_ALTITUDE_CM,cap.t,0);
    cap.t.tofCm=Config::TELLO_MAX_ALTITUDE_CM;++cap.t.sample;cap.t.lastReceivedMs=500;
    cap.safety.update(cap.t,true);
    assert(cap.control.update(cap.t,cap.safety,true,500)==AltitudeAction::Wait);
    Rig clip(Config::TELLO_MAX_ALTITUDE_CM-23);
    clip.control.begin(Config::TELLO_MAX_ALTITUDE_CM,clip.t,0);
    clip.t.tofCm=Config::TELLO_MAX_ALTITUDE_CM-3;++clip.t.sample;clip.t.lastReceivedMs=500;
    clip.safety.update(clip.t,true);
    assert(clip.control.update(clip.t,clip.safety,true,500)==AltitudeAction::Wait);
    // Mission 17: H=80, ToF=96 permits up 20, but ToF=105 does not.
    if(target==100) {
        for(int tof : {96,105}) {
            Rig recorded(80);recorded.t.tofCm=tof;
            ++recorded.t.sample;recorded.t.lastReceivedMs=500;
            recorded.safety.update(recorded.t,true);
            assert(recorded.control.update(recorded.t,recorded.safety,true,500)==
                (tof==96 ? AltitudeAction::Up20 : AltitudeAction::Wait));
        }
    }
    Rig disagree;disagree.t.tofCm=80;++disagree.t.sample;disagree.t.lastReceivedMs=500;
    disagree.safety.update(disagree.t,true);
    assert(disagree.control.update(disagree.t,disagree.safety,true,500)==AltitudeAction::Wait);
    ++disagree.t.sample;disagree.t.lastReceivedMs=2500;disagree.safety.update(disagree.t,true);
    assert(disagree.control.update(disagree.t,disagree.safety,true,2500)==AltitudeAction::Failed);
    Rig timeout;timeout.tick(500,10);timeout.control.actionStarted();
    assert(timeout.tick(19999,10,false)==AltitudeAction::Wait);
    assert(timeout.tick(20000,10,false)==AltitudeAction::Failed);
    assert(!strcmp(timeout.control.error(),"ALTITUDE_TIMEOUT_NO_PROGRESS"));
    Rig progress;progress.tick(500,15);progress.control.actionStarted();
    progress.control.invalidateStability();
    assert(progress.tick(20000,15,false)==AltitudeAction::Failed);
    assert(!strcmp(progress.control.error(),"ALTITUDE_TIMEOUT_NOT_STABLE"));
    Rig wrap(10,0xffffff00u);
    assert(wrap.tick(uint32_t(0xffffff00u+499u),10)==AltitudeAction::Wait);
    assert(wrap.tick(uint32_t(0xffffff00u+500u),10)==AltitudeAction::Up20);
    assert(wrap.tick(uint32_t(0xffffff00u+20000u),10)==AltitudeAction::Failed);
    Rig ack(target+20);assert(ack.tick(500,target+20)==AltitudeAction::Down20);
    ack.control.actionStarted();assert(ack.tick(1000,target)==AltitudeAction::Wait);
    ack.control.actionFinished(ack.t,1000);
    assert(ack.tick(1499,target)==AltitudeAction::Wait);
    assert(ack.tick(1500,target)==AltitudeAction::Wait);
    assert(ack.tick(1500+stable,target)==AltitudeAction::Done);
    Rig missing;missing.t.valid=false;
    assert(missing.tick(500,10)==AltitudeAction::Wait);
    missing.t.valid=true;assert(missing.tick(600,-1)==AltitudeAction::Wait);
    Rig invalid;invalid.control.begin(Config::TELLO_MAX_ALTITUDE_CM+1,invalid.t,0);
    assert(invalid.tick(500,10)==AltitudeAction::Failed);
    std::cout << "Altitude decision tests passed (target " << target << ")\n";
}
