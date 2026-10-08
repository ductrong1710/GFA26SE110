#pragma once
#include "NodeRegistry.h"
#include "SensorNodeClient.h"
#include "StorageManager.h"
#include "TimeManager.h"
#include "MissionCollection.h"

class CollectionManager : public MissionCollection {
public:
    CollectionManager(NodeRegistry& registry, SensorNodeClient& client, StorageManager& storage, TimeManager& time)
        : registry_(registry), client_(client), storage_(storage), time_(time), response_(Config::COLLECTION_JSON_CAPACITY) {}
    void update();
    void setMissionMode(bool enabled);
    bool requestTarget(const char* deviceCode) override;
    MissionCollectionState targetState() const override {return targetState_;}
    void cancelTarget() override;
private:
    enum class State { Select, Info, SendTime, Fetch, Persist, Ack };
    NodeRegistry& registry_;
    SensorNodeClient& client_;
    StorageManager& storage_;
    TimeManager& time_;
    DynamicJsonDocument response_;
    State state_ = State::Select;
    size_t nextNode_ = 0, recordIndex_ = 0, ackCount_ = 0;
    uint32_t lastCycle_ = 0, pending_ = 0;
    unsigned int batches_ = 0;
    SensorNode node_;
    String ackIds_[Config::COLLECTION_BATCH_SIZE];
    bool missionMode_=false,targetCycle_=false,cycleFailed_=false;
    size_t targetAcked_=0;
    MissionCollectionState targetState_=MissionCollectionState::Idle;
    void finishNode(bool success=false);
};
