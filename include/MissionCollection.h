#pragma once
enum class MissionCollectionState { Idle, Pending, Collected, Failed };
class MissionCollection {
public:
    virtual ~MissionCollection()=default;
    virtual bool requestTarget(const char* deviceCode)=0;
    virtual MissionCollectionState targetState() const=0;
    virtual void cancelTarget()=0;
};
