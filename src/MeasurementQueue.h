#pragma once
#include "Config.h"
#include "Models.h"
#include "RecordStore.h"
enum class SaveResult { Saved, Full, Error, SequenceExhausted };
enum class AckResult { Acked, NotPending, Error };
class MeasurementQueue {
public:
    explicit MeasurementQueue(RecordStore& store) : store_(store) {}
    bool begin();
    SaveResult append(Measurement& record);
    bool readAt(size_t index, Measurement& record);
    AckResult acknowledge(const char* recordId);
    void update();
    size_t pendingCount() const { return count_; }
    bool healthy() const { return healthy_; }
    bool full() const { return full_; }
private:
    RecordStore& store_;
    uint32_t pending_[Config::MAX_LOCAL_RECORDS]{};
    size_t count_ = 0;
    bool healthy_ = false, full_ = false;
    uint32_t next_ = 0, reserved_ = 0;
    unsigned int slot_ = 0;
    static bool scan(const char* path, void* context);
    bool load(const char* path, Measurement& record);
    bool reserve();
};
