#include "FlightStateManager.h"
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
}
bool FlightStateManager::prepare() {
    if (!Config::TELLO_ENABLED) return reject("TELLO_DISABLED");
    if (airbornePossible_) return reject("AIRCRAFT_MAY_BE_AIRBORNE");
    if (state_==FlightState::ConnectingTello || state_==FlightState::EnteringSdk || state_==FlightState::QueryingBattery)
        return reject("PREPARATION_IN_PROGRESS");
    if (!wifi_.targetConfigured(StaTarget::Tello)) return reject("TELLO_SSID_NOT_CONFIGURED");
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
        (t.heightCm>=0 ? t.heightCm<=Config::TELLO_LAND_MAX_HEIGHT_CM : t.tofCm<=Config::TELLO_LAND_MAX_HEIGHT_CM);
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
bool FlightStateManager::disconnect() {
    if(state_!=FlightState::LandedReady || airbornePossible_ || tello_.commandPending() || !stableGround())
        return reject("STABLE_GROUND_REQUIRED");
    landingEvidence_="PRE_FLIGHT_GROUND_TELEMETRY";
    error_=""; transition(FlightState::LandedConfirmed); return true;
}
bool FlightStateManager::takeoff() {
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
    tello_.cancelRc();
    landSent_=landAcked_=false; contradictoryLandingTelemetry_=false;
    groundSamples_=0; landingEvidence_="NONE"; error_=""; transition(FlightState::Landing); return true;
}
bool FlightStateManager::move(const char* direction,int cm) {
    if(!TelloController::validMove(direction,cm)) return reject("INVALID_MOVE");
    if(state_!=FlightState::Flying) return reject("NOT_FLYING");
    if(!tello_.move(direction,cm)) return reject("COMMAND_BUSY_OR_DISCONNECTED");
    error_=""; return true;
}
bool FlightStateManager::rotate(const char* direction,int degrees) {
    if(!TelloController::validRotation(direction,degrees)) return reject("INVALID_ROTATION");
    if(state_!=FlightState::Flying) return reject("NOT_FLYING");
    if(!tello_.rotate(direction,degrees)) return reject("COMMAND_BUSY_OR_DISCONNECTED");
    error_=""; return true;
}
bool FlightStateManager::rc(int a,int b,int c,int d) {
    if(!TelloController::validRc(a,b,c,d)) return reject("INVALID_RC");
    if(state_!=FlightState::Flying) return reject("NOT_FLYING");
    if(!tello_.sendRc(a,b,c,d)) return reject("COMMAND_BUSY_OR_DISCONNECTED");
    error_=""; return true;
}
void FlightStateManager::update(size_t pendingRecords, bool storageReady, bool backendConfigured) {
    if(state_==FlightState::InternetConnecting || state_==FlightState::Syncing || state_==FlightState::SyncComplete || state_==FlightState::SyncBlocked) {
        FlightState next;
        if(!wifi_.isInternetNetworkReady()) next=FlightState::InternetConnecting;
        else if(!storageReady || !backendConfigured) next=FlightState::SyncBlocked;
        else next=pendingRecords ? FlightState::Syncing : FlightState::SyncComplete;
        error_=!storageReady ? "STORAGE_UNAVAILABLE" : !backendConfigured ? "BACKEND_NOT_CONFIGURED" : "";
        if(next!=state_) transition(next);
        return;
    }
    if(state_==FlightState::LandedReady) {
        observeGround();
        if(!wifi_.isTelloConnected()) fail("TELLO_DISCONNECTED");
        return;
    }
    if(state_==FlightState::Error && airbornePossible_) {
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
    if(state_==FlightState::LandedConfirmed) {
        tello_.stop();
        transition(FlightState::SwitchingToInternet);
        return;
    }
    if(state_==FlightState::SwitchingToInternet) {
        wifi_.requestStaTarget(StaTarget::Internet);
        transition(FlightState::InternetConnecting);
        return;
    }
    if (state_==FlightState::TakingOff) {
        if(tello_.commandPending()) return;
        if(tello_.lastCommand()==TelloCommand::Takeoff && tello_.lastResult()==TelloResult::Ok) transition(FlightState::Flying);
        else fail("TAKEOFF_FAILED_OR_UNCERTAIN");
        return;
    }
    if(state_==FlightState::Flying) {
        if(!wifi_.isTelloConnected()) fail("TELLO_LINK_LOST_AIRCRAFT_STATE_UNKNOWN");
        else if(!tello_.commandPending() && (tello_.lastResult()==TelloResult::Timeout || tello_.lastResult()==TelloResult::Rejected))
            fail("FLIGHT_COMMAND_FAILED_OR_UNCERTAIN");
        return;
    }
    if(state_==FlightState::Landing) {
        if(!wifi_.isTelloConnected()) { fail("LAND_LINK_LOST_NOT_CONFIRMED"); return; }
        if(!landSent_) {
            if(tello_.land()) landSent_=true;
            else if(uint32_t(millis()-stateAt_)>=Config::TELLO_LAND_CONFIRM_TIMEOUT_MS) fail("LAND_SEND_FAILED_LINK_RETAINED");
            return;
        }
        if(tello_.commandPending()) return;
        if(!landAcked_) {
            if(tello_.lastCommand()!=TelloCommand::Land || tello_.lastResult()!=TelloResult::Ok) { fail("LAND_FAILED_NOT_CONFIRMED"); return; }
            landAcked_=true; landAckAt_=millis();
            groundSamples_=0; observedSample_=tello_.telemetry().sample;
            return;
        }
        const auto& t=tello_.telemetry();
        const bool postAckSample=int32_t(t.lastReceivedMs-landAckAt_)>0;
        if(tello_.hasFreshTelemetry() && postAckSample && !groundedTelemetry()) contradictoryLandingTelemetry_=true;
        if(postAckSample) observeGround();
        const uint32_t elapsed=millis()-landAckAt_;
        if(stableGround() && elapsed>=Config::TELLO_LAND_SETTLE_MS) landingEvidence_="LAND_ACK_AND_STABLE_TELEMETRY";
        else if(!tello_.hasFreshTelemetry() && !contradictoryLandingTelemetry_ && !flightUncertain_ && !tello_.responseUncertain() &&
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
