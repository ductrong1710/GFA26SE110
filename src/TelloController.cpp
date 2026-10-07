#include "TelloController.h"
#include <ctype.h>
#include <stdlib.h>

bool TelloController::begin() {
    if (!Config::TELLO_ENABLED || !wifi_.isTelloConnected()) return false;
    stop();
    if (!commands_.begin(wifi_.getStaIp(), Config::TELLO_COMMAND_PORT) ||
        !states_.begin(wifi_.getStaIp(), Config::TELLO_STATE_PORT)) { stop(); return false; }
    started_ = true;
    uncertain_ = false; cooling_ = false;
    kind_ = TelloCommand::None; result_ = TelloResult::None;
    response_[0] = 0;
    Serial.printf("[TELLO] UDP started\n");
    return true;
}
void TelloController::stop() {
    commands_.stop(); states_.stop();
    started_ = sdkReady_ = false;
    rcQueued_ = rcMoving_ = rcSent_ = false;
    telemetry_ = TelloTelemetry(); queriedBattery_ = -1;
    if (commandPending()) finish(TelloResult::Disconnected, "disconnected");
}
bool TelloController::canSend() const {
    return started_ && wifi_.isTelloConnected() && !commandPending() &&
        !rcQueued_ && !rcMoving_ && (!rcSent_ || uint32_t(millis()-lastRcSentAt_)>=Config::TELLO_COMMAND_GAP_MS) &&
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
    if (!transmit(text)) return false;
    strcpy(command_,text); kind_=kind; result_=TelloResult::Pending;
    sentAt_=millis(); retries_=0;
    Serial.printf("[TELLO] Command: %s\n", command_);
    return true;
}
bool TelloController::requestSdkMode() { return send(TelloCommand::Sdk,"command"); }
bool TelloController::queryBattery() { return send(TelloCommand::Battery,"battery?"); }
bool TelloController::takeoff() { return send(TelloCommand::Takeoff,"takeoff"); }
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
    if (!validMove(direction,cm)) return false;
    char text[32]; snprintf(text,sizeof(text),"%s %d",direction,cm);
    return send(TelloCommand::Move,text);
}
bool TelloController::rotate(const char* direction, int degrees) {
    if (!validRotation(direction,degrees)) return false;
    char text[32]; snprintf(text,sizeof(text),"%s %d",direction,degrees);
    return send(TelloCommand::Rotate,text);
}
bool TelloController::sendRc(int a,int b,int c,int d) {
    if (!isSdkReady() || commandPending() || !validRc(a,b,c,d) ||
        (cooling_ && uint32_t(millis()-finishedAt_)<cooldownMs_)) return false;
    rc_[0]=a; rc_[1]=b; rc_[2]=c; rc_[3]=d;
    rcAt_=millis(); rcQueued_=true;
    return true;
}
void TelloController::cancelRc() {
    rcQueued_=false;
    if (rcMoving_ && isSdkReady() && !commandPending()) {
        transmit("rc 0 0 0 0"); lastRcSentAt_=millis(); rcSent_=true;
    }
    rcMoving_=false;
}
void TelloController::finish(TelloResult result, const char* response) {
    result_=result; finishedAt_=millis(); cooling_=true;
    cooldownMs_=result==TelloResult::Timeout ? Config::TELLO_RESPONSE_QUARANTINE_MS : Config::TELLO_COMMAND_GAP_MS;
    snprintf(response_,sizeof(response_),"%s",response);
    if (result==TelloResult::Timeout) uncertain_=true;
    Serial.printf("[TELLO] Result: %s\n",response_);
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
    if (!wifi_.isTelloConnected()) { stop(); return; }
    // Bound work per update even under a datagram flood.
    for (unsigned i=0;i<4;++i) {
        const int length=commands_.parsePacket(); if (!length) break;
        if (commands_.remoteIP()!=IPAddress(Config::TELLO_IP) || commands_.remotePort()!=Config::TELLO_COMMAND_PORT || length>=96) { commands_.flush(); continue; }
        char reply[96]; const int n=commands_.read(reply,sizeof(reply)-1); commands_.flush();
        if (n<=0 || n!=length || memchr(reply,0,n)) continue;
        reply[n]=0;
        int end=n; while(end && isspace(static_cast<unsigned char>(reply[end-1]))) reply[--end]=0;
        if (!commandPending()) continue;
        if (kind_==TelloCommand::Battery) {
            char* tail; const long value=strtol(reply,&tail,10);
            if (tail!=reply && !*tail && value>=0 && value<=100) {
                queriedBattery_=value; batteryAt_=millis(); finish(TelloResult::Ok,reply);
            } else if (!strncmp(reply,"error",5)) finish(TelloResult::Rejected,reply);
        } else if (!strcmp(reply,"ok")) {
            if (kind_==TelloCommand::Sdk) sdkReady_=true;
            finish(TelloResult::Ok,reply);
        } else if (!strncmp(reply,"error",5)) finish(TelloResult::Rejected,reply);
    }
    for (unsigned i=0;i<4;++i) {
        const int length=states_.parsePacket(); if(!length) break;
        if (states_.remoteIP()!=IPAddress(Config::TELLO_IP) || length>511) { states_.flush(); continue; }
        char packet[512]; const int n=states_.read(packet,sizeof(packet)-1); states_.flush();
        if(n==length && n>0) telemetry_.parse(packet,n,millis());
    }
    const bool readOnly=kind_==TelloCommand::Sdk || kind_==TelloCommand::Battery;
    const uint32_t timeout=readOnly ? Config::TELLO_COMMAND_TIMEOUT_MS : Config::TELLO_FLIGHT_COMMAND_TIMEOUT_MS;
    if(commandPending() && uint32_t(millis()-sentAt_)>=timeout) {
        if(readOnly && retries_<Config::TELLO_COMMAND_RETRY_COUNT && transmit(command_)) {
            ++retries_; sentAt_=millis(); uncertain_=true;
        } else finish(TelloResult::Timeout,"timeout");
    }
    if (!commandPending() && isSdkReady() && (!rcSent_ || uint32_t(millis()-lastRcSentAt_)>=Config::TELLO_RC_INTERVAL_MS)) {
        if (uint32_t(millis()-rcAt_)>Config::TELLO_RC_MAX_AGE_MS) { cancelRc(); return; }
        if (rcQueued_) {
            char text[48]; snprintf(text,sizeof(text),"rc %d %d %d %d",rc_[0],rc_[1],rc_[2],rc_[3]);
            if (transmit(text)) {
                // Some SDK versions acknowledge RC; an untagged late ACK must
                // never authorize the ACK-only landing fallback in this session.
                uncertain_=true;
                rcMoving_=rc_[0] || rc_[1] || rc_[2] || rc_[3];
                lastRcSentAt_=millis(); rcSent_=true; rcQueued_=false;
            }
        }
    }
}
