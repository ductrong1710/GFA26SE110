#include "MissionManager.h"
#include <cstring>
#include <cstdio>
#include <cstdlib>
#include <algorithm>
bool MissionManager::reject(const char* error) {
    if(error!=error_ && strcmp(error_,error)) {
        snprintf(error_,sizeof(error_),"%s",error);
        Serial.printf("[MISSION] Blocked: %s state=%s\n",error_,stateName());
    }
    return false;
}
bool MissionManager::transition(MissionState state) {
    MissionStateRecord next=record_;next.state=state;
    if(hasActive_ && !storage_.saveState(next)) return reject("MISSION_STATE_WRITE_FAILED");
    record_=next;stateAt_=millis();Serial.printf("[MISSION] %s\n",stateName());return true;
}
bool MissionManager::begin() {
    backend_.beginLaunchSession();
    if(!tello_.safety().configurationValid())
        Serial.printf("[SAFETY] Invalid altitude configuration; autonomous start disabled\n");
    const bool mounted=storage_.begin();
    const MissionRestore restored=mounted ? storage_.restore(mission_,record_) : MissionRestore::RecoveryRequired;
    hasActive_=mission_.id>0;
    if(hasActive_ && storage_.loadResult(result_) &&
       (result_.missionId!=mission_.id || result_.fingerprint!=record_.fingerprint)) result_=MissionResult{};
    if(restored==MissionRestore::Empty) return true;
    if(restored!=MissionRestore::RecoveryRequired) {
        const char* error="";
        if(!planner_.build(mission_,gatewayId_,plan_,error)) {record_.state=MissionState::RecoveryRequired;reject(error);}
        else {
            return true;
        }
    } else {record_.state=MissionState::RecoveryRequired;reject("MISSION_RECOVERY_REQUIRED");}
    flight_.restoreRecovery(FlightOwner::Mission);return mounted;
}
bool MissionManager::ownsFlight() const {
    return state()==MissionState::PreparingTello || state()==MissionState::GroundReady ||
        MissionCodec::mayBeAirborne(state()) || state()==MissionState::Landed || state()==MissionState::ConnectingInternet;
}
bool MissionManager::requestPull() {
    if(state()!=MissionState::Empty && state()!=MissionState::Completed && state()!=MissionState::Failed && state()!=MissionState::Aborted)
        return reject("ACTIVE_MISSION_RETAINED");
    if(flight_.mayBeAirborne() || tello_.commandPending() || !wifi_.isInternetNetworkReady()) return reject("MISSION_PULL_REQUIRES_IDLE_INTERNET");
    if(hasActive_ && !result_.uploaded) return reject("MISSION_RESULT_PENDING");
    backend_.requestPull();return true;
}
void MissionManager::pull() {
    if(flight_.mayBeAirborne() || tello_.commandPending() || !sensorHealthy_ || !storage_.isReady()) return;
    if(state()!=MissionState::Empty && !(result_.uploaded && (state()==MissionState::Completed || state()==MissionState::Failed || state()==MissionState::Aborted))) return;
    Mission candidate;
    const MissionPull pulled=backend_.update(true,candidate);
    if(pulled==MissionPull::Error){reject(backend_.error());return;}
    if(pulled!=MissionPull::Downloaded) return;
    if(hasActive_ && candidate.id==mission_.id){reject("MISSION_ALREADY_PROCESSED");return;}
    const char* error="";
    if(!planner_.build(candidate,gatewayId_,plan_,error)){reject(error);return;}
    if(!storage_.saveActive(candidate)){reject("MISSION_ACTIVE_WRITE_FAILED");return;}
    mission_=candidate;hasActive_=true;record_=MissionStateRecord{};
    record_.missionId=mission_.id;record_.fingerprint=MissionCodec::fingerprint(mission_);
    if(!transition(MissionState::Ready)) {record_.state=MissionState::RecoveryRequired;flight_.restoreRecovery(FlightOwner::Mission);return;}
    result_=MissionResult{};error_[0]=0;
    downloadAcknowledged_=backend_.acknowledgeDownloaded(mission_);
    lastDownloadAck_=millis();
    if(!downloadAcknowledged_) reject(backend_.error());
}
bool MissionManager::start(bool remote) {
    if(state()!=MissionState::Ready || !hasActive_) return reject("MISSION_NOT_READY");
    if(!sensorHealthy_ || !storage_.isReady()) return reject("STORAGE_UNAVAILABLE");
    if(flight_.owner()!=FlightOwner::None || flight_.mayBeAirborne() || tello_.commandPending()) return reject("FLIGHT_BUSY");
    if(!tello_.safety().configurationValid()) return reject("INVALID_ALTITUDE_CONFIGURATION");
    if(!wifi_.isInternetNetworkReady()) return reject("MISSION_START_REQUIRES_INTERNET");
    const char* error="";if(!planner_.build(mission_,gatewayId_,plan_,error)) return reject(error);
    if(!downloadAcknowledged_) {
        if(!backend_.acknowledgeDownloaded(mission_)) return reject(backend_.error());
        downloadAcknowledged_=true;
    }
    result_=MissionResult{};result_.missionId=mission_.id;result_.fingerprint=record_.fingerprint;
    if(!storage_.saveResult(result_)) return reject("MISSION_RESULT_WRITE_FAILED");
    if(!transition(MissionState::PreparingTello)) return false;
    // Durable pre-flight checkpoint precedes backend start; reboot cannot replay takeoff.
    // A lost ACK keeps the same mission for an explicit idempotent start retry.
    if(!Config::GROUND_TEST_MODE && !(remote ? backend_.consumeLaunch(mission_) : backend_.startMission(mission_))) {
        if(remote) {
            // Never retry an uncertain remote consume. The durable preflight state
            // survives reboot; require an operator to reconcile with the backend.
            record_.state=MissionState::RecoveryRequired;storage_.saveState(record_);
            flight_.restoreRecovery(FlightOwner::Mission);return reject(backend_.error());
        }
        if(!transition(MissionState::Ready)) {record_.state=MissionState::RecoveryRequired;return false;}
        return reject(backend_.error());
    }
    flight_.requireTelemetryLanding(true);
    stopRequested_=false;stepIndex_=0;record_.waypointIndex=0;arrived_=waitingMove_=false;
    error_[0]=0;
    if(!flight_.prepare(FlightOwner::Mission)){failAndLand(flight_.error());return false;}
    return true;
}
bool MissionManager::abort() {
    if(!hasActive_ || (state()!=MissionState::Ready && !ownsFlight())) return reject("NO_ABORTABLE_MISSION");
    if(collection_ && state()==MissionState::Collecting && targetIndex_<mission_.targetCount)
        result_.targetRecords[targetIndex_]=collection_->targetRecordCount();
    result_.missionId=mission_.id;result_.fingerprint=record_.fingerprint;result_.finalState=MissionState::Aborted;
    snprintf(result_.failureReason,sizeof(result_.failureReason),"OPERATOR_ABORT");
    if(collection_) collection_->cancelTarget();
    flight_.stopMotion();stopRequested_=true;landRequested_=false;
    flight_.requireTelemetryLanding(true);
    if(state()==MissionState::Ready && !flight_.mayBeAirborne() &&
       wifi_.staTarget()==StaTarget::Internet && !tello_.commandPending()) {finishLanded();return true;}
    if(!transition(MissionState::Landing)) return false;
    return true;
}
bool MissionManager::landNow() {
    // Even corrupt mission metadata must not remove the operator's landing path.
    if(state()==MissionState::RecoveryRequired && !hasActive_) {
        if(!flight_.land()) return reject(flight_.error());
        recoveryLandRequested_=true;return true;
    }
    return abort();
}
void MissionManager::failAndLand(const char* reason) {
    if(collection_ && state()==MissionState::Collecting && targetIndex_<mission_.targetCount)
        result_.targetRecords[targetIndex_]=collection_->targetRecordCount();
    reject(reason);snprintf(result_.failureReason,sizeof(result_.failureReason),"%s",error_);
    result_.finalState=MissionState::Failed;
    if(record_.waypointIndex<mission_.waypointCount) result_.failedWaypointSequence=mission_.waypoints[record_.waypointIndex].sequenceNo;
    if(collection_) collection_->cancelTarget();
    flight_.stopMotion();stopRequested_=true;landRequested_=false;
    if(!transition(MissionState::Landing)) {
        // Persisted pre-command state already denotes possible flight. Even if
        // storage fails, stop route execution and make a best-effort normal land.
        record_.state=MissionState::Landing;
    }
}
void MissionManager::finishLanded() {
    for(size_t i=0;i<mission_.targetCount;++i) if(result_.targets[i]==TargetOutcome::Pending) result_.targets[i]=TargetOutcome::Skipped;
    if(!storage_.saveResult(result_)) {reject("MISSION_RESULT_WRITE_FAILED");return;}
    if(!transition(MissionState::Landed)) return;
    stopRequested_=false;
}
void MissionManager::updateLanding() {
    if(flight_.mayBeAirborne()) {
        if(flight_.state()==FlightState::Landing) return;
        if(flight_.state()==FlightState::Error && landRequested_) {
            record_.state=MissionState::RecoveryRequired;storage_.saveState(record_);stopRequested_=false;reject(flight_.error());return;
        }
        if(tello_.isSdkReady() && flight_.land()) landRequested_=true;
        return;
    }
    if(flight_.state()==FlightState::LandedReady) {
        // The airborne branch above handles every attempted/uncertain takeoff.
        // Here preparation failed before flight: no landing telemetry is needed.
        if(!flight_.cancelPreparation(FlightOwner::Mission)) return;
    } else if(flight_.state()!=FlightState::LandedConfirmed && flight_.state()!=FlightState::SwitchingToInternet &&
              flight_.state()!=FlightState::InternetConnecting && flight_.state()!=FlightState::Syncing &&
              flight_.state()!=FlightState::SyncComplete && flight_.state()!=FlightState::SyncBlocked) {
        if(!flight_.cancelPreparation(FlightOwner::Mission)) return;
    }
    finishLanded();
}
void MissionManager::keepAlive() {
    if(uint32_t(millis()-keepaliveAt_)<2000 || !tello_.canSend()) return;
    if(flight_.rc(0,0,0,0,FlightOwner::Mission)) keepaliveAt_=millis();
}
void MissionManager::updateRoute() {
    if(stepIndex_>=plan_.count) {
        result_.finalState=MissionState::Completed;
        for(size_t i=0;i<mission_.targetCount;++i)
            if(result_.targets[i]==TargetOutcome::Failed) result_.finalState=MissionState::Failed;
        Serial.printf("[MISSION] Route finished including return; landing result=%s\n",MissionCodec::stateName(result_.finalState));
        stopRequested_=true;
        if(!transition(MissionState::Landing)) failAndLand("MISSION_STATE_WRITE_FAILED");return;
    }
    const auto& step=plan_.steps[stepIndex_];record_.waypointIndex=step.waypointIndex;
    if(step.kind==RouteStepKind::Altitude) {
        int adjusted=flight_.adjustAltitude(step.cm);
        if(adjusted<0) failAndLand(error_);else if(adjusted>0) ++stepIndex_;
        return;
    }
    if(step.kind==RouteStepKind::Move) {
        if(waitingMove_) {
            if(tello_.commandPending()) return;
            if(tello_.lastCommand()!=TelloCommand::Move || tello_.lastResult()!=TelloResult::Ok){failAndLand("MISSION_MOVE_FAILED");return;}
            waitingMove_=false;++stepIndex_;return;
        }
        if(!tello_.safety().inOperatingRange()) {failAndLand("MISSION_ALTITUDE_OUTSIDE_RANGE");return;}
        if(flight_.move(MissionRoutePlanner::directionName(step.direction),step.cm,FlightOwner::Mission)) waitingMove_=true;
        return;
    }
    const auto& waypoint=mission_.waypoints[step.waypointIndex];
    if(!arrived_){arrived_=true;holdAt_=millis();}
    keepAlive();
    if(uint32_t(millis()-holdAt_)<waypoint.holdSeconds*1000u) return;
    if(waypoint.action==MissionAction::Collect) {
        targetIndex_=0;targetRequested_=false;targetAt_=millis();
        if(!transition(MissionState::Collecting)) failAndLand("MISSION_STATE_WRITE_FAILED");
        return;
    }
    ++result_.completedWaypoints;
    if(!saveProgress()) return;
    ++stepIndex_;arrived_=false;
}
void MissionManager::updateCollection() {
    if(!collection_){failAndLand("MISSION_COLLECTION_UNAVAILABLE");return;}
    const int waypointId=mission_.waypoints[record_.waypointIndex].id;
    while(targetIndex_<mission_.targetCount && mission_.targets[targetIndex_].waypointId!=waypointId) ++targetIndex_;
    if(targetIndex_==mission_.targetCount) {
        ++result_.completedWaypoints;
        if(!saveProgress()) return;
        ++stepIndex_;arrived_=false;
        if(!transition(MissionState::Executing)) failAndLand("MISSION_STATE_WRITE_FAILED");return;
    }
    keepAlive();
    if(uint32_t(millis()-targetAt_)>Config::MISSION_SENSOR_WAIT_TIMEOUT_MS) {
        failCollectionTarget("MISSION_SENSOR_TIMEOUT");return;
    }
    if(!targetRequested_) {targetRequested_=collection_->requestTarget(mission_.targets[targetIndex_].deviceCode);return;}
    if(collection_->targetState()==MissionCollectionState::Collected) {
        result_.targets[targetIndex_]=TargetOutcome::Collected;
        result_.targetRecords[targetIndex_]=collection_->targetRecordCount();
        if(!saveProgress()) return;
        ++targetIndex_;targetRequested_=false;targetAt_=millis();
    } else if(collection_->targetState()==MissionCollectionState::Failed) {
        failCollectionTarget("MISSION_SENSOR_COLLECTION_FAILED");
    }
}
void MissionManager::failCollectionTarget(const char* reason) {
    result_.targets[targetIndex_]=TargetOutcome::Failed;
    // A target which never started must not inherit the previous target's count.
    result_.targetRecords[targetIndex_]=targetRequested_ ? collection_->targetRecordCount() : 0;
    if(!result_.failureReason[0]) {
        snprintf(result_.failureReason,sizeof(result_.failureReason),"%s",reason);
        result_.failedWaypointSequence=mission_.waypoints[record_.waypointIndex].sequenceNo;
    }
    if(!saveProgress()) return; // Storage failure still takes the immediate landing path.
    collection_->cancelTarget();
    Serial.printf("[MISSION-COLLECT] target=%s waypoint=%d failed=%s records=%u; continuing remaining targets and route\n",
        mission_.targets[targetIndex_].deviceCode,mission_.waypoints[record_.waypointIndex].sequenceNo,reason,unsigned(result_.targetRecords[targetIndex_]));
    ++targetIndex_;targetRequested_=false;targetAt_=millis();
}
bool MissionManager::saveProgress() {
    if(storage_.saveResult(result_)) return true;
    failAndLand("MISSION_PROGRESS_WRITE_FAILED");return false;
}
void MissionManager::updateSync(size_t pendingRecords,bool timeSynced) {
    if(result_.missionId!=mission_.id || result_.fingerprint!=record_.fingerprint) {
        reject("MISSION_RESULT_UNAVAILABLE");return;
    }
    if(result_.uploaded) {transition(result_.finalState);return;}
    if(!sensorHealthy_ || !storage_.isReady()) {reject("STORAGE_UNAVAILABLE");return;}
    if(!wifi_.isInternetNetworkReady()) {reject("MISSION_WAITING_FOR_INTERNET");return;}
    if(!timeSynced) {reject("MISSION_WAITING_FOR_TIME_SYNC");return;}
    if(pendingRecords) {reject("MISSION_WAITING_FOR_SENSOR_SYNC");return;}
    if(uint32_t(millis()-lastResultAttempt_)<resultRetryMs_) return;
    lastResultAttempt_=millis();
    if(!backend_.uploadResult(mission_,result_)) {
        reject(backend_.error());resultRetryMs_=std::min(resultRetryMs_*2,Config::BACKEND_RETRY_MAX_MS);
        Serial.printf("[MISSION-SYNC] Retained missionId=%d error=%s retryInterval=%lu ms\n",
            mission_.id,backend_.error(),static_cast<unsigned long>(resultRetryMs_));return;
    }
    MissionResult accepted=result_;accepted.uploaded=true;accepted.mockUpload=backend_.isMock();
    // A server acknowledgement is not durable until this write succeeds.
    // Retry uses the same mission/fingerprint; the backend contract is idempotent.
    if(!storage_.saveResult(accepted)) {
        Serial.printf("[MISSION-SYNC] Flash acknowledgment write failed missionId=%d; will retry\n",mission_.id);
        reject("MISSION_RESULT_ACK_WRITE_FAILED");return;
    }
    Serial.printf("[MISSION-SYNC] Result saved as uploaded missionId=%d\n",mission_.id);
    result_=accepted;resultRetryMs_=Config::BACKEND_RETRY_BASE_MS;
    error_[0]=0;transition(result_.finalState);
}
void MissionManager::update(bool sensorStorageHealthy,size_t pendingRecords,bool timeSynced) {
    sensorHealthy_=sensorStorageHealthy;
    if(collection_) collection_->setMissionId(hasActive_ && ownsFlight() ? mission_.id : 0);
    if(state()==MissionState::RecoveryRequired) {
        if(recoveryLandRequested_ && !flight_.mayBeAirborne()) {
            // Keep corrupt files for diagnosis, without inventing a mission result.
            recoveryLandRequested_=false;reject("MISSION_FILES_REQUIRE_REPAIR_AIRCRAFT_LANDED");
        }
        return;
    }
    if(stopRequested_ || state()==MissionState::Landing) {updateLanding();return;}
    if(state()==MissionState::Landed) {transition(MissionState::ConnectingInternet);return;}
    if(state()==MissionState::ConnectingInternet) {
        if(wifi_.isInternetNetworkReady()) transition(MissionState::Syncing);return;
    }
    if(state()==MissionState::Syncing) {updateSync(pendingRecords,timeSynced);return;}
    if(state()==MissionState::Ready && !downloadAcknowledged_ && wifi_.isInternetNetworkReady() &&
       uint32_t(millis()-lastDownloadAck_)>=Config::BACKEND_RETRY_BASE_MS) {
        lastDownloadAck_=millis();downloadAcknowledged_=backend_.acknowledgeDownloaded(mission_);
        if(!downloadAcknowledged_) reject(backend_.error());
    }
    if(state()==MissionState::Ready && !Config::GROUND_TEST_MODE && timeSynced && sensorHealthy_ && storage_.isReady() &&
       wifi_.isInternetNetworkReady() && !flight_.mayBeAirborne() && !tello_.commandPending()) {
        const auto launch=backend_.pollLaunch(mission_);
        if(launch==RemoteLaunch::Due) {start(true);return;}
        if(launch==RemoteLaunch::Consumed) {
            transition(MissionState::RecoveryRequired);record_.state=MissionState::RecoveryRequired;
            flight_.restoreRecovery(FlightOwner::Mission);reject("REMOTE_LAUNCH_ALREADY_CONSUMED");return;
        }
        if(launch==RemoteLaunch::Reload) {
            Mission candidate;backend_.requestPull();
            if(backend_.update(true,candidate)!=MissionPull::Downloaded) return;
            const char* error="";
            if(!planner_.build(candidate,gatewayId_,plan_,error)){reject(error);return;}
            if(!storage_.saveActive(candidate)){reject("MISSION_ACTIVE_WRITE_FAILED");return;}
            mission_=candidate;record_.missionId=mission_.id;record_.fingerprint=MissionCodec::fingerprint(mission_);
            if(!transition(MissionState::Ready)){record_.state=MissionState::RecoveryRequired;return;}
            downloadAcknowledged_=false;return;
        }
        if(launch==RemoteLaunch::Retired) {
            // Server confirms cancellation/expiry/terminal status before any local flight.
            result_=MissionResult{};result_.missionId=mission_.id;result_.fingerprint=record_.fingerprint;
            result_.uploaded=true;result_.finalState=MissionState::Aborted;
            if(!storage_.saveResult(result_)){reject("MISSION_RESULT_WRITE_FAILED");return;}
            transition(MissionState::Aborted);return;
        }
    }
    if(!ownsFlight()) {pull();return;}
    if(!sensorHealthy_ || !storage_.isReady()) {failAndLand("STORAGE_UNAVAILABLE");return;}
    if(flight_.state()==FlightState::Error){failAndLand(flight_.error());return;}
    if(flight_.state()==FlightState::Flying && tello_.safety().sensorsDisagree()) {
        flight_.stopMotion();
        if(tello_.safety().disagreementConfirmed()) failAndLand("ALTITUDE_SENSOR_DISAGREEMENT");
        return; // Brief disagreement pauses the route; no guessed correction.
    }
    if(state()==MissionState::PreparingTello) {
        if(flight_.state()!=FlightState::LandedReady) return;
        if(Config::GROUND_TEST_MODE){transition(MissionState::GroundReady);return;}
        if(!flight_.hasGroundTelemetry()) {
            if(uint32_t(millis()-stateAt_)>=Config::TELLO_CONNECT_TIMEOUT_MS)
                failAndLand("GROUND_TELEMETRY_TIMEOUT");
            return;
        }
        if(!tello_.canSend()) return;
        // Durable possible-flight state precedes transmission, even if power is
        // lost between the command and its ACK.
        if(!transition(MissionState::TakingOff)) {failAndLand("MISSION_STATE_WRITE_FAILED");return;}
        if(!flight_.takeoff(FlightOwner::Mission)) failAndLand(flight_.error());
        return;
    }
    if(state()==MissionState::GroundReady) return;
    if(state()==MissionState::TakingOff) {
        if(flight_.state()==FlightState::Flying) {
            if(!transition(MissionState::Stabilizing)){failAndLand("MISSION_STATE_WRITE_FAILED");return;}
            flight_.beginAltitude(Config::TELLO_TARGET_ALTITUDE_CM);
        }
        return;
    }
    if(state()==MissionState::Stabilizing) {
        int adjusted=flight_.adjustAltitude(Config::TELLO_TARGET_ALTITUDE_CM);
        if(adjusted<0) failAndLand(error_);
        else if(adjusted>0 && !transition(MissionState::Executing)) failAndLand("MISSION_STATE_WRITE_FAILED");
        return;
    }
    if(state()==MissionState::Executing) updateRoute();
    else if(state()==MissionState::Collecting) updateCollection();
}
