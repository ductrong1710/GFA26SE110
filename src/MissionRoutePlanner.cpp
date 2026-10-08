#include "MissionRoutePlanner.h"
#include <algorithm>
#include <cstring>
#include <cstdlib>
const char* MissionRoutePlanner::directionName(RouteDirection d) {
    switch(d){case RouteDirection::Back:return "back";case RouteDirection::Left:return "left";
    case RouteDirection::Right:return "right";default:return "forward";}
}
bool MissionRoutePlanner::build(Mission& m,int gatewayId,MissionRoutePlan& plan,const char*& error) const {
    plan.count=0;error="INVALID_MISSION";
    if(!limits_.valid()) {error="INVALID_ALTITUDE_CONFIGURATION";return false;}
    if(gatewayId<=0 || m.gatewayId!=gatewayId){error="MISSION_GATEWAY_MISMATCH";return false;}
    if(!m.waypointCount || m.waypointCount>Config::MAX_MISSION_WAYPOINTS || m.targetCount>Config::MAX_MISSION_TARGETS) return false;
    if(Config::MISSION_MAX_MOVE_SEGMENT_CM<20 || Config::MISSION_MAX_MOVE_SEGMENT_CM>500) {error="INVALID_ROUTE_SEGMENT_CONFIGURATION";return false;}
    std::sort(m.waypoints,m.waypoints+m.waypointCount,[](const MissionWaypoint& a,const MissionWaypoint& b){return a.sequenceNo<b.sequenceNo;});
    for(size_t i=0;i<m.waypointCount;++i) {
        const auto& w=m.waypoints[i];
        if(w.sequenceNo<=0 || w.id<=0 || (i && w.sequenceNo==m.waypoints[i-1].sequenceNo)) {error="MISSION_SEQUENCE_INVALID";return false;}
        for(size_t j=0;j<i;++j) if(w.id==m.waypoints[j].id){error="MISSION_WAYPOINT_ID_DUPLICATE";return false;}
        if(w.altitudeCm>limits_.maximum) {
            Serial.printf("[MISSION] Waypoint %d altitude %d cm exceeds configured maximum %d cm\n",w.sequenceNo,w.altitudeCm,limits_.maximum);
            error="MISSION_ALTITUDE_EXCEEDS_LIMIT";return false;
        }
        if(w.altitudeCm<limits_.minimum){error="MISSION_ALTITUDE_BELOW_MINIMUM";return false;}
        if(w.xCm < -Config::MISSION_MAX_LOCAL_CM || w.xCm>Config::MISSION_MAX_LOCAL_CM ||
           w.yCm < -Config::MISSION_MAX_LOCAL_CM || w.yCm>Config::MISSION_MAX_LOCAL_CM) return false;
        bool hasTarget=false;
        for(size_t j=0;j<m.targetCount;++j) if(m.targets[j].waypointId==w.id) hasTarget=true;
        if(w.action==MissionAction::Collect && !hasTarget){error="MISSION_COLLECT_TARGET_REQUIRED";return false;}
    }
    for(size_t i=0;i<m.targetCount;++i) {
        const auto& t=m.targets[i];bool found=false;
        if(t.sensorNodeId<=0 || !t.deviceCode[0]){error="MISSION_TARGET_IDENTITY_REQUIRED";return false;}
        for(size_t j=0;j<m.waypointCount;++j) if(m.waypoints[j].id==t.waypointId && m.waypoints[j].action==MissionAction::Collect) found=true;
        if(!found){error="MISSION_TARGET_WAYPOINT_INVALID";return false;}
        for(size_t j=0;j<i;++j) if(m.targets[j].sensorNodeId==t.sensorNodeId || !strcmp(m.targets[j].deviceCode,t.deviceCode)) {
            error="MISSION_TARGET_DUPLICATE";return false;
        }
    }
    auto add=[&](RouteStepKind kind,RouteDirection direction,int cm,size_t index) {
        if(plan.count==Config::MAX_MISSION_ROUTE_STEPS){error="MISSION_ROUTE_TOO_LONG";return false;}
        auto& step=plan.steps[plan.count++];step.kind=kind;step.direction=direction;step.cm=cm;step.waypointIndex=index;return true;
    };
    int x=0,y=0;
    for(size_t i=0;i<m.waypointCount;++i) {
        const auto& w=m.waypoints[i];
        if(!add(RouteStepKind::Altitude,RouteDirection::Forward,w.altitudeCm,i)) return false;
        const int delta[]={w.xCm-x,w.yCm-y};
        for(size_t axis=0;axis<2;++axis) {
            int remaining=abs(delta[axis]);
            if(remaining && remaining<20){error="MISSION_MOVE_BELOW_SDK_MINIMUM";return false;}
            RouteDirection d=axis==0 ? (delta[axis]>0 ? RouteDirection::Forward:RouteDirection::Back) : (delta[axis]>0 ? RouteDirection::Right:RouteDirection::Left);
            while(remaining) {
                int segment=std::min(remaining,Config::MISSION_MAX_MOVE_SEGMENT_CM);
                if(remaining>segment && remaining-segment<20) segment=remaining-20;
                if(segment<20){error="MISSION_MOVE_SEGMENT_INVALID";return false;}
                if(!add(RouteStepKind::Move,d,segment,i)) return false;
                remaining-=segment;
            }
        }
        if(!add(RouteStepKind::Waypoint,RouteDirection::Forward,0,i)) return false;
        x=w.xCm;y=w.yCm;
    }
    error="";return true;
}
