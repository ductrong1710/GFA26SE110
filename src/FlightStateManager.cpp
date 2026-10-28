#include "FlightStateManager.h"
// The coordinator owns physical flight actions. External adapters retain only
// persistence, scheduling and reporting; all timing below is non-blocking.
void FlightStateManager::stopMotion() {
    tello_.cancelRc();
    altitude_.invalidateStability();
}
void FlightStateManager::beginAltitude(int targetCm) {
    altitude_.begin(targetCm,tello_.telemetry(),millis());
    altitudeAction_=AltitudeAction::Wait;
}
int FlightStateManager::adjustAltitude(int targetCm) {
    if(!authorized(FlightOwner::Mission) || state_!=FlightState::Flying) return -1;
    if(!altitude_.active() || altitude_.target()!=targetCm) beginAltitude(targetCm);
    const auto action=altitude_.update(tello_.telemetry(),tello_.safety(),tello_.hasFreshTelemetry(),millis());
    if(action==AltitudeAction::Failed) {
        Serial.printf("[ALTITUDE-CONTROL] failed target=%d initial=%d maximum=%d current=%d reason=%s\n",
            targetCm,altitude_.initialHeight(),altitude_.maximumHeight(),tello_.telemetry().heightCm,altitude_.error());
        stopMotion();reject(altitude_.error());return -1;
    }
    if(altitudeAction_==AltitudeAction::Pulse) {
        if(tello_.rcFailed()) {altitude_.actionFailed("RC_SEND_FAILED");reject("RC_SEND_FAILED");return -1;}
        if(!tello_.rcBusy()) {altitudeAction_=AltitudeAction::Wait;altitude_.actionFinished(tello_.telemetry(),millis());}
        return 0;
    }
    if(altitudeAction_==AltitudeAction::Down20 || altitudeAction_==AltitudeAction::Up20) {
        if(tello_.commandPending()) return 0;
        if(tello_.lastCommand()!=TelloCommand::Move || tello_.lastResult()!=TelloResult::Ok) {
            altitude_.actionFailed("ALTITUDE_ADJUSTMENT_FAILED");reject("ALTITUDE_ADJUSTMENT_FAILED");return -1;
        }
        altitudeAction_=AltitudeAction::Wait;altitude_.actionFinished(tello_.telemetry(),millis());return 0;
    }
    if(action==AltitudeAction::Done) {
        Serial.printf("[ALTITUDE-CONTROL] stable target=%d selected=%d tolerance=%d stableMs=%lu\n",
            targetCm,tello_.telemetry().heightCm,Config::TELLO_ALTITUDE_TOLERANCE_CM,static_cast<unsigned long>(Config::TELLO_ALTITUDE_STABLE_MS));
        return 1;
    }
    if(action==AltitudeAction::Wait || !tello_.canSend()) return 0;
    const bool sent=action==AltitudeAction::Up20 ? move("up",20,FlightOwner::Mission) :
        action==AltitudeAction::Down20 ? move("down",20,FlightOwner::Mission) :
        tello_.sendRcPulse(altitude_.vertical(),Config::TELLO_VERTICAL_PULSE_MS);
    if(sent) {altitudeAction_=action;altitude_.actionStarted();}
    return 0;
}
void FlightStateManager::update(size_t pendingRecords,bool storageReady,bool backendConfigured) {
    switch(state_) {
    case FlightState::ConnectingTello:
    case FlightState::EnteringSdk:
    case FlightState::QueryingBattery: updatePreparation();return;
    case FlightState::LandedReady:
        observeGround();if(!wifi_.isTelloConnected()) fail("TELLO_DISCONNECTED");return;
    case FlightState::TakingOff:
        if(tello_.commandPending()) return;
        if(tello_.lastCommand()==TelloCommand::Takeoff && tello_.lastResult()==TelloResult::Ok) transition(FlightState::Flying);
        else fail("TAKEOFF_FAILED_OR_UNCERTAIN");
        return;
    case FlightState::Flying:
        if(!wifi_.isTelloConnected()) fail("TELLO_LINK_LOST_AIRCRAFT_STATE_UNKNOWN");
        else if(tello_.rcFailed()) fail("RC_SEND_FAILED");
        else if(tello_.safety().disagreementConfirmed()) {stopMotion();fail("ALTITUDE_SENSOR_DISAGREEMENT");}
        else if(!tello_.commandPending() && (tello_.lastResult()==TelloResult::Timeout || tello_.lastResult()==TelloResult::Rejected))
            fail("FLIGHT_COMMAND_FAILED_OR_UNCERTAIN");
        return;
    case FlightState::Landing: updateLanding();return;
    case FlightState::LandedConfirmed:
        altitude_.reset();tello_.stop();transition(FlightState::SwitchingToInternet);return;
    case FlightState::SwitchingToInternet:
        owner_=FlightOwner::None;wifi_.requestStaTarget(StaTarget::Internet);transition(FlightState::InternetConnecting);return;
    case FlightState::InternetConnecting:
    case FlightState::Syncing:
    case FlightState::SyncComplete:
    case FlightState::SyncBlocked: updateInternet(pendingRecords,storageReady,backendConfigured);return;
    case FlightState::Error: if(airbornePossible_) updateRecovery();return;
    default:return;
    }
}
const char* FlightStateManager::stateName() const {
    switch(state_) {
    case FlightState::IdleInternet: return "IDLE_INTERNET";
    case FlightState::ConnectingTello: return "CONNECTING_TELLO";
    case FlightState::EnteringSdk: return "ENTERING_SDK";
    case FlightState::QueryingBattery: return "QUERYING_BATTERY";
    case FlightState::LandedReady: return "LANDED_READY";
    case FlightState::TakingOff: return "TAKING_OFF";
    case FlightState::Flying: return "FLYING";
    case FlightState::Landing: return "LANDING";
    case FlightState::LandedConfirmed: return "LANDED_CONFIRMED";
    case FlightState::SwitchingToInternet: return "SWITCHING_TO_INTERNET";
    case FlightState::InternetConnecting: return "INTERNET_CONNECTING";
    case FlightState::Syncing: return "SYNCING";
    case FlightState::SyncComplete: return "SYNC_COMPLETE";
    case FlightState::SyncBlocked: return "SYNC_BLOCKED";
    default: return "ERROR";
    }
}
void FlightStateManager::transition(FlightState state) {
    state_=state; stateAt_=millis();
    Serial.printf("[FLIGHT] %s\n",stateName());
    if (state == FlightState::Error) Serial.printf("[FLIGHT] Error: %s\n", error_);
}
bool FlightStateManager::authorized(FlightOwner requester) {
    return requester!=FlightOwner::None && owner_==requester ? true : reject("FLIGHT_OWNER_CONFLICT");
}
void FlightStateManager::restoreRecovery(FlightOwner requester) {
    if(owner_==FlightOwner::None) owner_=requester;
    altitude_.reset();altitudeAction_=AltitudeAction::Wait;
    airbornePossible_=true;flightUncertain_=true;strictLanding_=true;recoveryStarted_=false;
    error_="MISSION_RECOVERY_REQUIRED";transition(FlightState::Error);
}
bool FlightStateManager::cancelPreparation(FlightOwner requester) {
    // LANDED_READY means SDK/battery preparation, not a takeoff attempt.
    // With no possible flight, missing telemetry must not trap preflight cleanup.
    if(airbornePossible_) return reject("GROUND_CONFIRMATION_REQUIRED");
    if(owner_!=FlightOwner::None && !authorized(requester)) return false;
    tello_.stop();wifi_.requestStaTarget(StaTarget::Internet);
    owner_=FlightOwner::None;altitude_.reset();
    error_="";transition(FlightState::InternetConnecting);return true;
}
bool FlightStateManager::prepare(FlightOwner requester) {
    if(requester==FlightOwner::None || (owner_!=FlightOwner::None && !authorized(requester))) return false;
    if (!Config::TELLO_ENABLED) return reject("TELLO_DISABLED");
    if (airbornePossible_) return reject("AIRCRAFT_MAY_BE_AIRBORNE");
    if (state_==FlightState::ConnectingTello || state_==FlightState::EnteringSdk || state_==FlightState::QueryingBattery)
        return reject("PREPARATION_IN_PROGRESS");
    if (!wifi_.targetConfigured(StaTarget::Tello)) return reject("TELLO_SSID_NOT_CONFIGURED");
    owner_=requester;altitude_.reset();altitudeAction_=AltitudeAction::Wait;
    tello_.stop();
    wifi_.requestStaTarget(StaTarget::Tello);
    error_=""; batteryRequested_=false; groundSamples_=0; observedSample_=0;
    landingEvidence_="NONE"; recoveryStarted_=false; flightUncertain_=false;
    transition(FlightState::ConnectingTello);
    return true;
}
bool FlightStateManager::groundedTelemetry() const {
    const auto& t=tello_.telemetry();
    return tello_.hasFreshTelemetry() && t.landingFieldsValid &&
        abs(t.verticalVelocity)<=Config::TELLO_LAND_MAX_VERTICAL_SPEED &&
        (t.heightCm<0 || t.heightCm<=Config::TELLO_LAND_MAX_HEIGHT_CM) &&
        (t.tofCm<0 || t.tofCm<=Config::TELLO_LAND_MAX_HEIGHT_CM);
}
void FlightStateManager::observeGround() {
    const auto& t=tello_.telemetry();
    if(!groundedTelemetry()) { groundSamples_=0; return; }
    if(observedSample_==t.sample) return;
    observedSample_=t.sample;
    const int height=t.heightCm>=0 ? t.heightCm : t.tofCm;
    if(!groundSamples_ || uint32_t(t.lastReceivedMs-groundLastAt_)>Config::TELLO_TELEMETRY_TIMEOUT_MS || abs(height-groundHeight_)>3) {
        groundSamples_=0; groundSince_=t.lastReceivedMs;
    }
    if(groundSamples_<Config::TELLO_LAND_STABLE_SAMPLES) ++groundSamples_;
    groundLastAt_=t.lastReceivedMs; groundHeight_=height;
}
bool FlightStateManager::stableGround() const {
    return groundedTelemetry() && groundSamples_>=Config::TELLO_LAND_STABLE_SAMPLES &&
        uint32_t(millis()-groundSince_)>=Config::TELLO_LAND_SETTLE_MS;
}
bool FlightStateManager::disconnect(FlightOwner requester) {
    if(!authorized(requester)) return false;
    if(state_!=FlightState::LandedReady || airbornePossible_ || tello_.commandPending() || !stableGround())
        return reject("STABLE_GROUND_REQUIRED");
    landingEvidence_="PRE_FLIGHT_GROUND_TELEMETRY";
    error_=""; transition(FlightState::LandedConfirmed); return true;
}
bool FlightStateManager::takeoff(FlightOwner requester) {
    if(!authorized(requester)) return false;
    if(Config::GROUND_TEST_MODE) return reject("GROUND_TEST_MODE");
    if(!tello_.safety().configurationValid()) return reject("INVALID_ALTITUDE_CONFIGURATION");
    if(state_!=FlightState::LandedReady || !wifi_.isTelloConnected() || !tello_.isSdkReady()) return reject("TAKEOFF_STATE_INVALID");
    if(tello_.batteryPercent()<0) return reject("BATTERY_UNAVAILABLE");
    if(tello_.batteryPercent()<Config::TELLO_MIN_TAKEOFF_BATTERY_PERCENT) return reject("BATTERY_TOO_LOW");
    if(!groundedTelemetry()) return reject("GROUND_TELEMETRY_REQUIRED");
    if(!tello_.takeoff()) return reject("COMMAND_BUSY");
    airbornePossible_=true; groundSamples_=0; error_=""; transition(FlightState::TakingOff); return true;
}
bool FlightStateManager::land() {
    if(state_==FlightState::Landing) return true;
    if(!airbornePossible_ || !wifi_.isTelloConnected() || !tello_.isSdkReady()) return reject("LAND_STATE_INVALID");
    stopMotion();
    landSent_=landResponseReceived_=landRejected_=false; contradictoryLandingTelemetry_=false;
    groundSamples_=0; landingEvidence_="NONE"; error_=""; transition(FlightState::Landing); return true;
}
bool FlightStateManager::move(const char* direction,int cm,FlightOwner requester) {
    if(!authorized(requester)) return false;
    if(!TelloController::validMove(direction,cm)) return reject("INVALID_MOVE");
    if(state_!=FlightState::Flying) return reject("NOT_FLYING");
    tello_.refreshSafety();
    if(!tello_.safety().allowMove(direction,cm)) return reject(tello_.safety().error());
    if(!tello_.move(direction,cm)) return reject("COMMAND_BUSY_OR_DISCONNECTED");
    error_=""; return true;
}
bool FlightStateManager::rotate(const char* direction,int degrees,FlightOwner requester) {
    if(!authorized(requester)) return false;
    if(!TelloController::validRotation(direction,degrees)) return reject("INVALID_ROTATION");
    if(state_!=FlightState::Flying) return reject("NOT_FLYING");
    if(!tello_.rotate(direction,degrees)) return reject("COMMAND_BUSY_OR_DISCONNECTED");
    error_=""; return true;
}
bool FlightStateManager::rc(int a,int b,int c,int d,FlightOwner requester) {
    if(!authorized(requester)) return false;
    if(!TelloController::validRc(a,b,c,d)) return reject("INVALID_RC");
    if(state_!=FlightState::Flying) return reject("NOT_FLYING");
    if(!tello_.sendRc(a,b,c,d)) return reject("COMMAND_BUSY_OR_DISCONNECTED");
    error_=""; return true;
}
void FlightStateManager::updateInternet(size_t pendingRecords,bool storageReady,bool backendConfigured) {
        FlightState next;
        if(!wifi_.isInternetNetworkReady()) next=FlightState::InternetConnecting;
        else if(!storageReady || !backendConfigured) next=FlightState::SyncBlocked;
        else next=pendingRecords ? FlightState::Syncing : FlightState::SyncComplete;
        error_=!storageReady ? "STORAGE_UNAVAILABLE" : !backendConfigured ? "BACKEND_NOT_CONFIGURED" : "";
        if(next!=state_) transition(next);
        return;
}
void FlightStateManager::updateRecovery() {
        // Re-establish only the protocol after radio recovery, never assume landing
        // or automatically replay takeoff/movement. Operator may request land.
        if(!wifi_.isTelloConnected()) { recoveryStarted_=false; return; }
        if(recoveryStarted_ && !tello_.commandPending() && !tello_.isSdkReady() &&
           uint32_t(millis()-recoveryAt_)>=Config::TELLO_RECOVERY_RETRY_MS) recoveryStarted_=false;
        if(!tello_.isSdkReady() && !recoveryStarted_) {
            recoveryAt_=millis();
            recoveryStarted_=true; // Back off even if opening/sending UDP fails.
            if(tello_.begin()) tello_.requestSdkMode();
        }
        return;
}
void FlightStateManager::updateLanding() {
        if(!wifi_.isTelloConnected()) { fail("LAND_LINK_LOST_NOT_CONFIRMED"); return; }
        if(!landSent_) {
            if(tello_.land()) landSent_=true;
            else if(uint32_t(millis()-stateAt_)>=Config::TELLO_LAND_CONFIRM_TIMEOUT_MS) fail("LAND_SEND_FAILED_LINK_RETAINED");
            return;
        }
        if(tello_.commandPending()) return;
        if(!landResponseReceived_) {
            if(tello_.lastCommand()!=TelloCommand::Land ||
               (tello_.lastResult()!=TelloResult::Ok && tello_.lastResult()!=TelloResult::Rejected)) {
                fail("LAND_FAILED_NOT_CONFIRMED"); return;
            }
            landRejected_=tello_.lastResult()==TelloResult::Rejected;
            landResponseReceived_=true; landAckAt_=millis();
            if(landRejected_) Serial.printf("[FLIGHT] Land rejected; waiting for stable ground telemetry, link retained\n");
            groundSamples_=0; observedSample_=tello_.telemetry().sample;
            return;
        }
        const auto& t=tello_.telemetry();
        const bool postAckSample=int32_t(t.lastReceivedMs-landAckAt_)>0;
        if(tello_.hasFreshTelemetry() && postAckSample && !groundedTelemetry()) contradictoryLandingTelemetry_=true;
        // Without an ACK, require both height sensors and zero vertical speed.
        // A generic error alone never releases the flight lock.
        const bool rejectedGroundValid=t.heightCm==0 && t.tofCm>=0 &&
            t.tofCm<=Config::TELLO_LAND_MAX_HEIGHT_CM && t.verticalVelocity==0;
        if(postAckSample) {
            if(landRejected_ && !rejectedGroundValid) groundSamples_=0;
            else observeGround();
        }
        const uint32_t elapsed=millis()-landAckAt_;
        if(stableGround() && elapsed>=Config::TELLO_LAND_SETTLE_MS)
            landingEvidence_=landRejected_ ? "LAND_REJECTED_BUT_STABLE_GROUND_TELEMETRY" : "LAND_ACK_AND_STABLE_TELEMETRY";
        else if(!landRejected_ && !strictLanding_ && !tello_.hasFreshTelemetry() && !contradictoryLandingTelemetry_ && !flightUncertain_ && !tello_.responseUncertain() &&
                elapsed>=Config::TELLO_LAND_ACK_ONLY_SETTLE_MS) {
            landingEvidence_="LAND_ACK_AND_CONSERVATIVE_TIMEOUT";
            Serial.printf("[FLIGHT] Landing confirmation uses land ACK + timeout; no fresh telemetry\n");
        } else {
            if(elapsed>=Config::TELLO_LAND_CONFIRM_TIMEOUT_MS) fail("LAND_EVIDENCE_INSUFFICIENT_LINK_RETAINED");
            return;
        }
        airbornePossible_=false; transition(FlightState::LandedConfirmed);
        return;
}
void FlightStateManager::updatePreparation() {
    if (state_==FlightState::ConnectingTello) {
        if (wifi_.isTelloConnected()) {
            if (!tello_.begin() || !tello_.requestSdkMode()) { fail("UDP_START_FAILED"); return; }
            transition(FlightState::EnteringSdk);
        } else if (uint32_t(millis()-stateAt_)>=Config::TELLO_CONNECT_TIMEOUT_MS) fail("TELLO_CONNECT_TIMEOUT");
    } else if (state_==FlightState::EnteringSdk) {
        if (!wifi_.isTelloConnected()) { fail("TELLO_DISCONNECTED"); return; }
        if (tello_.commandPending()) return;
        if (!tello_.isSdkReady()) { fail("SDK_HANDSHAKE_FAILED"); return; }
        transition(FlightState::QueryingBattery);
    } else if (state_==FlightState::QueryingBattery) {
        if (!wifi_.isTelloConnected()) { fail("TELLO_DISCONNECTED"); return; }
        if (!batteryRequested_) {
            if(tello_.queryBattery()) batteryRequested_=true;
            else if(uint32_t(millis()-stateAt_)>=Config::TELLO_CONNECT_TIMEOUT_MS) fail("BATTERY_REQUEST_FAILED");
            return;
        }
        if (tello_.commandPending()) return;
        if (tello_.lastResult()!=TelloResult::Ok || tello_.batteryPercent()<0) { fail("BATTERY_UNAVAILABLE"); return; }
        transition(FlightState::LandedReady);
    }
}
