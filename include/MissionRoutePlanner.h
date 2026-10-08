#pragma once
#include "MissionModel.h"
#include "FlightSafetyController.h"
enum class RouteStepKind { Altitude, Move, Waypoint };
enum class RouteDirection { Forward, Back, Right, Left };
struct MissionRouteStep {
    RouteStepKind kind=RouteStepKind::Waypoint;
    RouteDirection direction=RouteDirection::Forward;
    int cm=0;
    size_t waypointIndex=0;
};
struct MissionRoutePlan {
    MissionRouteStep steps[Config::MAX_MISSION_ROUTE_STEPS];
    size_t count=0;
};
class MissionRoutePlanner {
public:
    explicit MissionRoutePlanner(AltitudeLimits limits=AltitudeLimits()):limits_(limits) {}
    bool build(Mission& mission,int gatewayId,MissionRoutePlan& plan,const char*& error) const;
    static const char* directionName(RouteDirection direction);
private:
    AltitudeLimits limits_;
};
