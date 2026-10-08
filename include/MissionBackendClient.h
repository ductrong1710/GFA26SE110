#pragma once
#include "MissionModel.h"
#include "GatewayWiFiManager.h"
class MissionTransport {
public:
    virtual ~MissionTransport()=default;
    virtual bool configured() const=0;
    virtual bool request(const char* path,const char* method,const JsonDocument* body,JsonDocument& response,int& status)=0;
};
class MissionHttpTransport:public MissionTransport {
public:
    bool configured() const override;
    bool request(const char*,const char*,const JsonDocument*,JsonDocument&,int&) override;
};
enum class MissionPull { Idle, NoMission, Downloaded, Error };
class MissionBackendClient {
public:
    MissionBackendClient(GatewayWiFiManager& wifi,MissionTransport& transport,
        int gatewayId=Config::BACKEND_GATEWAY_ID,bool mock=Config::MOCK_MISSION_BACKEND)
        :wifi_(wifi),transport_(transport),gatewayId_(gatewayId),mock_(mock) {}
    MissionPull update(bool mayPull,Mission& candidate);
    void requestPull(){requested_=true;}
    bool uploadResult(const Mission& mission,const MissionResult& result);
    bool isMock() const {return mock_;}
    bool isConfigured() const {return gatewayId_>0 && (mock_ || transport_.configured());}
    const char* error() const {return error_;}
private:
    GatewayWiFiManager& wifi_;
    MissionTransport& transport_;
    int gatewayId_;
    bool mock_,requested_=false,attempted_=false;
    uint32_t lastAttempt_=0;
    const char* error_="";
};
