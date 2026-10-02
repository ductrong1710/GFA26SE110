#include "CollectionManager.h"
#include "Logger.h"

void CollectionManager::finishNode() {
    response_.clear(); ackCount_ = 0; state_ = State::Select;
}
void CollectionManager::update() {
    if (!storage_.isReady()) return;
    if (state_ == State::Select) {
        if (nextNode_ == 0 && millis() - lastCycle_ < Config::NODE_COLLECTION_INTERVAL_MS) return;
        if (nextNode_ >= registry_.count()) { nextNode_ = 0; lastCycle_ = millis(); return; }
        const SensorNode* node = registry_.at(nextNode_++);
        if (!node || !node->authenticated || !node->online) return;
        node_ = *node; batches_ = 0; state_ = State::Info; return;
    }
    const SensorNode* current = registry_.findNode(node_.deviceCode);
    if (!current || !current->authenticated || !current->online || current->ipAddress != node_.ipAddress) { finishNode(); return; }
    switch (state_) {
    case State::Info:
        if (!client_.getInfo(node_, pending_)) { Logger::warn("Node info request failed"); finishNode(); return; }
        registry_.touchNode(node_.deviceCode);
        Serial.printf("[COLLECT] %s pending=%u\n", node_.deviceCode.c_str(), unsigned(pending_));
        if (time_.isTimeSynced()) state_ = State::SendTime;
        else if (!pending_) finishNode(); else state_ = State::Fetch;
        break;
    case State::SendTime:
        if (!client_.sendTime(node_, time_.unixTime())) Logger::warn("Node time sync failed; continuing collection");
        if (!pending_) finishNode(); else state_ = State::Fetch;
        break;
    case State::Fetch:
        if (batches_ >= Config::MAX_COLLECTION_BATCHES_PER_CYCLE || !client_.getData(node_, response_)) {
            Logger::warn("Collection batch stopped or invalid response"); finishNode(); return;
        }
        registry_.touchNode(node_.deviceCode); ++batches_;
        recordIndex_ = ackCount_ = 0;
        if (!response_["records"].size()) finishNode(); else state_ = State::Persist;
        break;
    case State::Persist: {
        JsonArrayConst array = response_["records"].as<JsonArrayConst>();
        if (recordIndex_ >= array.size()) { if (ackCount_) state_ = State::Ack; else finishNode(); return; }
        GatewayMeasurement record;
        if (!MeasurementCodec::fromJson(array[recordIndex_++].as<JsonObjectConst>(), node_.deviceCode, record)) {
            Logger::warn("Invalid sensor record skipped"); return;
        }
        record.collectedTimeSynced = time_.isTimeSynced(); record.collectedAt = time_.collectionTime();
        record.gatewayRssi = node_.rssi;
        const SaveResult result = storage_.saveMeasurement(record);
        if (result != SaveResult::Failed) ackIds_[ackCount_++] = record.recordId;
        else Logger::error("Sensor record not persisted; excluded from ACK");
        break;
    }
    case State::Ack:
        if (!client_.acknowledge(node_, ackIds_, ackCount_)) {
            Logger::warn("ACK failed; local records retained"); finishNode(); return;
        }
        registry_.touchNode(node_.deviceCode);
        Serial.printf("[COLLECT] ACK sent for %u persisted records\n", unsigned(ackCount_));
        pending_ = pending_ > ackCount_ ? pending_ - ackCount_ : 0;
        if (!pending_ || batches_ >= Config::MAX_COLLECTION_BATCHES_PER_CYCLE) finishNode(); else state_ = State::Fetch;
        break;
    default: break;
    }
}
