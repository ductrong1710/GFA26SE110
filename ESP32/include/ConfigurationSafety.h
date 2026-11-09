#pragma once
#include "FlightStateManager.h"
#include "MissionModel.h"

inline bool configurationSafe(FlightState flight, bool mayBeAirborne, bool commandPending, MissionState mission) {
    if(mayBeAirborne || commandPending) return false;
    if(flight!=FlightState::IdleInternet && flight!=FlightState::InternetConnecting &&
       flight!=FlightState::Syncing && flight!=FlightState::SyncComplete && flight!=FlightState::SyncBlocked) return false;
    return mission==MissionState::Empty || mission==MissionState::Syncing ||
        mission==MissionState::Completed || mission==MissionState::Failed || mission==MissionState::Aborted;
}
