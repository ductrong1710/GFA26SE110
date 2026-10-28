#include "TelloController.h"
#include <ctype.h>
#include <stdlib.h>

bool TelloController::begin() {
    if (!Config::TELLO_ENABLED || !wifi_.isTelloConnected()) return false;
    stop();
    if (!commands_.begin(wifi_.getStaIp(), Config::TELLO_COMMAND_PORT) ||
        !states_.begin(IPAddress(), Config::TELLO_STATE_PORT)) {
        Serial.printf("[TELLO] UDP start failed: commandPort=%u statePort=%u\n",
            unsigned(Config::TELLO_COMMAND_PORT), unsigned(Config::TELLO_STATE_PORT));
        stop(); return false;
    }
    started_ = true;
    telemetryPackets_=telemetryParsed_=telemetryRejected_=0;telemetryLogAt_=millis();
    safety_.begin();
    uncertain_ = false; cooling_ = false;
    kind_ = TelloCommand::None; result_ = TelloResult::None;
    response_[0] = 0;
    Serial.printf("[TELLO] UDP started: localIp=%s commandPort=%u statePort=%u\n",
        wifi_.getStaIp().toString().c_str(), unsigned(Config::TELLO_COMMAND_PORT),
        unsigned(Config::TELLO_STATE_PORT));
    // SDK 1.3 specifies a wildcard state listener; retain the source-IP check below.
    Serial.printf("[TELLO] Telemetry listening: 0.0.0.0:%u expectedSource=%s\n",
        unsigned(Config::TELLO_STATE_PORT),IPAddress(Config::TELLO_IP).toString().c_str());
    return true;
}
void TelloController::stop() {
    commands_.stop(); states_.stop();
    started_ = sdkReady_ = false;
    rcQueued_ = rcMoving_ = rcSent_ = false;
    rcPulse_=rcStopPending_=rcFailure_=rcAttempted_=false;
    telemetry_ = TelloTelemetry(); queriedBattery_ = -1;
    safety_.update(telemetry_,false);
    if (commandPending()) finish(TelloResult::Disconnected, "disconnected");
}
bool TelloController::canSend() const {
    return started_ && wifi_.isTelloConnected() && !commandPending() &&
        !rcBusy() && (!rcSent_ || uint32_t(millis()-lastRcSentAt_)>=Config::TELLO_COMMAND_GAP_MS) &&
        (!cooling_ || uint32_t(millis()-finishedAt_) >= cooldownMs_);
}
bool TelloController::transmit(const char* text) {
    if (!started_ || !wifi_.isTelloConnected()) return false;
    const size_t length = strlen(text);
    return commands_.beginPacket(IPAddress(Config::TELLO_IP), Config::TELLO_COMMAND_PORT) &&
        commands_.write(reinterpret_cast<const uint8_t*>(text), length) == length && commands_.endPacket();
}
bool TelloController::send(TelloCommand kind, const char* text) {
    if (!canSend() || strlen(text) >= sizeof(command_)) return false;
    if (kind != TelloCommand::Sdk && !isSdkReady()) return false;
    // Drain queued replies while idle in update(), never reuse one as a new ACK.
    if (commands_.parsePacket()) { commands_.flush(); return false; }
    if (!transmit(text)) {
        Serial.printf("[TELLO] UDP send failed: command=%s localIp=%s target=%s:%u\n",
            text,wifi_.getStaIp().toString().c_str(),IPAddress(Config::TELLO_IP).toString().c_str(),unsigned(Config::TELLO_COMMAND_PORT));
        return false;
    }
    strcpy(command_,text); kind_=kind; result_=TelloResult::Pending;
    sentAt_=millis(); retries_=0;
    commandStartedAt_=sentAt_;replyPackets_=droppedReplies_=statePackets_=0;
    Serial.printf("[TELLO] Command: %s\n", command_);
    Serial.printf("[TELLO] UDP sent: localIp=%s target=%s:%u bytes=%u\n",
        wifi_.getStaIp().toString().c_str(),IPAddress(Config::TELLO_IP).toString().c_str(),
        unsigned(Config::TELLO_COMMAND_PORT),unsigned(strlen(text)));
    return true;
}
bool TelloController::requestSdkMode() { return send(TelloCommand::Sdk,"command"); }
bool TelloController::queryBattery() { return send(TelloCommand::Battery,"battery?"); }
bool TelloController::takeoff() {
    if(Config::GROUND_TEST_MODE || !safety_.configurationValid()) return false;
    return send(TelloCommand::Takeoff,"takeoff");
}
bool TelloController::land() { return send(TelloCommand::Land,"land"); }
bool TelloController::validMove(const char* direction, int cm) {
    if (!direction || cm<20 || cm>500) return false;
    return !strcmp(direction,"up") || !strcmp(direction,"down") || !strcmp(direction,"left") ||
        !strcmp(direction,"right") || !strcmp(direction,"forward") || !strcmp(direction,"back");
}
bool TelloController::validRotation(const char* direction, int degrees) {
    return direction && degrees>=1 && degrees<=3600 && (!strcmp(direction,"cw") || !strcmp(direction,"ccw"));
}
bool TelloController::validRc(int a,int b,int c,int d) {
    return a>=-100 && a<=100 && b>=-100 && b<=100 && c>=-100 && c<=100 && d>=-100 && d<=100;
}
bool TelloController::move(const char* direction, int cm) {
    refreshSafety();
    if (Config::GROUND_TEST_MODE || !validMove(direction,cm) || !safety_.allowMove(direction,cm)) return false;
    char text[32]; snprintf(text,sizeof(text),"%s %d",direction,cm);
    return send(TelloCommand::Move,text);
}
bool TelloController::rotate(const char* direction, int degrees) {
    if (Config::GROUND_TEST_MODE || !validRotation(direction,degrees)) return false;
    char text[32]; snprintf(text,sizeof(text),"%s %d",direction,degrees);
    return send(TelloCommand::Rotate,text);
}
bool TelloController::sendRc(int a,int b,int c,int d) {
    if (!validRc(a,b,c,d) || !isSdkReady()) return false;
    // Neutral preempts pending motion even during command cooldown.
    if (!(a || b || c || d)) {
        if(rcBusy()) {cancelRc();return true;}
        if(!canSend()) return false;
        rc_[0]=rc_[1]=rc_[2]=rc_[3]=0;rcStopPending_=true;updateRc();return true;
    }
    if(Config::GROUND_TEST_MODE || commandPending() || rcStopPending_ || rcPulse_ ||
       (cooling_ && uint32_t(millis()-finishedAt_)<cooldownMs_)) return false;
    rc_[0]=a;rc_[1]=b;rc_[2]=c;rc_[3]=d;
    rcAt_=millis();rcQueued_=true;
    return true;
}
bool TelloController::sendRcPulse(int vertical,uint32_t durationMs) {
    if(!vertical || !durationMs || durationMs>Config::TELLO_RC_MAX_AGE_MS || rcBusy()) return false;
    if(!sendRc(0,0,vertical,0)) return false;
    rcPulse_=true;rcDurationMs_=durationMs;rcFailure_=false;
    Serial.printf("[TELLO-RC] pulse queued vertical=%d durationMs=%lu\n",vertical,static_cast<unsigned long>(durationMs));
    return true;
}
void TelloController::cancelRc() {
    if(!rcBusy()) return;
    // Keep the neutral obligation until a send succeeds. Never restore motion
    // after cancellation, stale telemetry, a limit clamp, or a failed refresh.
    rcQueued_=false;rcPulse_=false;rcMoving_=false;
    rc_[0]=rc_[1]=rc_[2]=rc_[3]=0;
    rcStopPending_=true;
    updateRc();
}
void TelloController::finish(TelloResult result, const char* response) {
    result_=result; finishedAt_=millis(); cooling_=true;
    cooldownMs_=result==TelloResult::Timeout ? Config::TELLO_RESPONSE_QUARANTINE_MS : Config::TELLO_COMMAND_GAP_MS;
    snprintf(response_,sizeof(response_),"%s",response);
    if (result==TelloResult::Timeout) uncertain_=true;
    Serial.printf("[TELLO] Result: %s\n",response_);
    if(result==TelloResult::Timeout) Serial.printf(
        "[TELLO] Timeout detail: command=%s elapsed=%lu ms retries=%u replyPackets=%lu droppedReplies=%lu statePackets=%lu wifiConnected=%d localIp=%s\n",
        command_,static_cast<unsigned long>(millis()-commandStartedAt_),retries_,
        static_cast<unsigned long>(replyPackets_),static_cast<unsigned long>(droppedReplies_),
        static_cast<unsigned long>(statePackets_),wifi_.isTelloConnected()?1:0,wifi_.getStaIp().toString().c_str());
}
bool TelloController::hasFreshTelemetry() const {
    return started_ && wifi_.isTelloConnected() && telemetry_.valid &&
        uint32_t(millis()-telemetry_.lastReceivedMs)<=Config::TELLO_TELEMETRY_TIMEOUT_MS;
}
int TelloController::batteryPercent() const {
    if (hasFreshTelemetry() && telemetry_.battery>=0) return telemetry_.battery;
    return queriedBattery_>=0 && uint32_t(millis()-batteryAt_)<=Config::TELLO_BATTERY_MAX_AGE_MS ? queriedBattery_ : -1;
}
void TelloController::update() {
    if (!started_) return;
    if (!wifi_.isTelloConnected()) {
        const bool interrupted=rcBusy();stop();rcFailure_=interrupted;return;
    }
    readReplies();
    readTelemetry();
    refreshSafety();
    updateCommand();
    updateRc();
}
void TelloController::readReplies() {
    // Bound work per update even under a datagram flood.
    for (unsigned i=0;i<4;++i) {
        const int length=commands_.parsePacket(); if (!length) break;
        if(commandPending()) ++replyPackets_;
        if (commands_.remoteIP()!=IPAddress(Config::TELLO_IP) || commands_.remotePort()!=Config::TELLO_COMMAND_PORT || length>=96) {
            if(commandPending() && ++droppedReplies_<=3) Serial.printf("[TELLO] UDP reply discarded: source=%s:%u bytes=%d expected=%s:%u maxBytes=95\n",
                commands_.remoteIP().toString().c_str(),unsigned(commands_.remotePort()),length,
                IPAddress(Config::TELLO_IP).toString().c_str(),unsigned(Config::TELLO_COMMAND_PORT));
            commands_.flush(); continue;
        }
        char reply[96]; const int n=commands_.read(reply,sizeof(reply)-1); commands_.flush();
        if (n<=0 || n!=length || memchr(reply,0,n)) {
            if(commandPending()) ++droppedReplies_;
            continue;
        }
        if(commandPending() && replyPackets_<=3) {
            // Log original bytes before whitespace normalization. A two-byte
            // datagram is not necessarily the SDK's exact "ok" acknowledgement.
            char hex[49]={};
            const int shown=n<16?n:16;
            for(int j=0;j<shown;++j) snprintf(hex+j*3,sizeof(hex)-j*3,"%02X ",unsigned(static_cast<unsigned char>(reply[j])));
            Serial.printf("[TELLO] UDP reply received: source=%s:%u bytes=%d command=%s kind=%u hex=[%s]%s\n",
                commands_.remoteIP().toString().c_str(),unsigned(commands_.remotePort()),n,
                command_,unsigned(kind_),hex,n>shown?" (first 16 bytes)":"");
        }
        reply[n]=0;
        int end=n; while(end && isspace(static_cast<unsigned char>(reply[end-1]))) reply[--end]=0;
        if (!commandPending()) continue;
        if (kind_==TelloCommand::Battery) {
            char* tail; const long value=strtol(reply,&tail,10);
            if (tail!=reply && !*tail && value>=0 && value<=100) {
                queriedBattery_=value; batteryAt_=millis(); finish(TelloResult::Ok,reply);
            } else if (!strncmp(reply,"error",5)) finish(TelloResult::Rejected,reply);
        // Physical Tello firmware may acknowledge with uppercase OK (4F 4B).
        // Match the entire token; an ACK still cannot satisfy a battery query.
        } else if (!strcmp(reply,"ok") || !strcmp(reply,"OK")) {
            if (kind_==TelloCommand::Sdk) sdkReady_=true;
            finish(TelloResult::Ok,reply);
        } else if (!strncmp(reply,"error",5)) finish(TelloResult::Rejected,reply);
    }
}
void TelloController::readTelemetry() {
    for (unsigned i=0;i<4;++i) {
        const int length=states_.parsePacket(); if(!length) break;
        ++telemetryPackets_;
        if(commandPending() && states_.remoteIP()==IPAddress(Config::TELLO_IP)) ++statePackets_;
        if (states_.remoteIP()!=IPAddress(Config::TELLO_IP) || length>511) { ++telemetryRejected_;states_.flush(); continue; }
        char packet[512]; const int n=states_.read(packet,sizeof(packet)-1); states_.flush();
        if(n==length && n>0) {
            const bool parsed=telemetry_.parse(packet,n,millis());
            if(parsed) ++telemetryParsed_;else ++telemetryRejected_;
            if(telemetryPackets_<=2) Serial.printf("[TELLO-STATE] sample bytes=%d data=%.*s\n",n,n<160?n:160,packet);
            // Preserve violations even when a later packet in this bounded drain
            // returns to the target. The test executor must see every excursion.
            if(!parsed || !telemetry_.landingFieldsValid || abs(telemetry_.heightCm-100)>3 ||
               abs(telemetry_.tofCm-100)>3 || abs(telemetry_.verticalVelocity)>5) ++testOutOfBand_;
            if(parsed && (telemetry_.heightCm>110 || telemetry_.tofCm>110)) ++testOvershoot_;
            if(parsed && telemetry_.battery>=0 && telemetry_.battery<Config::TELLO_MIN_TAKEOFF_BATTERY_PERCENT) ++testLowBattery_;
        } else ++telemetryRejected_;
    }
    if(sdkReady_ && uint32_t(millis()-telemetryLogAt_)>=5000) {
        telemetryLogAt_=millis();
        Serial.printf("[TELLO-STATE] packets=%lu parsed=%lu rejected=%lu fresh=%d groundFields=%d h=%d tof=%d vgz=%d\n",
            static_cast<unsigned long>(telemetryPackets_),static_cast<unsigned long>(telemetryParsed_),
            static_cast<unsigned long>(telemetryRejected_),hasFreshTelemetry()?1:0,telemetry_.landingFieldsValid?1:0,
            telemetry_.heightCm,telemetry_.tofCm,telemetry_.verticalVelocity);
    }
}
void TelloController::updateCommand() {
    const bool readOnly=kind_==TelloCommand::Sdk || kind_==TelloCommand::Battery;
    const uint32_t timeout=readOnly ? Config::TELLO_COMMAND_TIMEOUT_MS : Config::TELLO_FLIGHT_COMMAND_TIMEOUT_MS;
    if(commandPending() && uint32_t(millis()-sentAt_)>=timeout) {
        if(readOnly && retries_<Config::TELLO_COMMAND_RETRY_COUNT && transmit(command_)) {
            ++retries_; sentAt_=millis(); uncertain_=true;
            Serial.printf("[TELLO] Retry: command=%s attempt=%u/%u\n",command_,retries_+1,Config::TELLO_COMMAND_RETRY_COUNT+1);
        } else finish(TelloResult::Timeout,"timeout");
    }
}
void TelloController::updateRc() {
    if(!rcBusy()) return;
    const uint32_t now=millis();
    if(!rcStopPending_) {
        refreshSafety();
        const bool expired=rcPulse_ ?
            (rcMoving_ ? uint32_t(now-rcStartedAt_)>=rcDurationMs_ : uint32_t(now-rcAt_)>=Config::TELLO_RC_MAX_AGE_MS) :
            uint32_t(now-rcAt_)>=Config::TELLO_RC_MAX_AGE_MS;
        const int vertical=safety_.filterVerticalRc(rc_[2]);
        const bool unsafe=!safety_.configurationValid() || !safety_.altitudeFresh() ||
            safety_.sensorsDisagree() || (rc_[2] && !vertical);
        if(expired || unsafe) {
            if(expired && rcPulse_ && !rcMoving_) rcFailure_=true;
            rcQueued_=rcMoving_=rcPulse_=false;
            rcStopPending_=true;
            rc_[0]=rc_[1]=rc_[2]=rc_[3]=0;
        } else rc_[2]=vertical;
    }
    if(!isSdkReady() || commandPending() ||
       (rcAttempted_ && uint32_t(now-rcAttemptAt_)<Config::TELLO_RC_INTERVAL_MS)) return;
    char text[48];snprintf(text,sizeof(text),"rc %d %d %d %d",rc_[0],rc_[1],rc_[2],rc_[3]);
    rcAttempted_=true;rcAttemptAt_=now;
    if(!transmit(text)) {
        Serial.printf("[TELLO-RC] TX failed: %s; neutralPending=%d\n",text,rcStopPending_?1:0);
        rcFailure_=true;
        // A refresh failure ends the active window. Initial sends may retry
        // until the enqueue deadline; a neutral failure must stay pending.
        if(rcMoving_) {
            rcMoving_=rcQueued_=rcPulse_=false;rcStopPending_=true;
            rc_[0]=rc_[1]=rc_[2]=rc_[3]=0;
        }
        return;
    }
    uncertain_=true;lastRcSentAt_=now;rcSent_=true;
    if(rcStopPending_) {Serial.printf("[TELLO-RC] neutral sent\n");rcStopPending_=false;return;}
    if(!rcMoving_) {
        rcStartedAt_=now;
        Serial.printf("[TELLO-RC] started %s durationMs=%lu\n",text,static_cast<unsigned long>(rcPulse_?rcDurationMs_:Config::TELLO_RC_MAX_AGE_MS));
    }
    rcMoving_=true;rcQueued_=false;
}
