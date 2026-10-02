#pragma once
#include "GatewayMeasurement.h"

class BackendAdapter {
public:
    bool build(const GatewayMeasurement* records, size_t count, JsonDocument& document, String& batchKey);
    bool accepted(const JsonDocument& response, const String& batchKey,
                  const GatewayMeasurement* records, size_t count, bool* acceptedRecords) const;
private:
    size_t rowCount_ = 0;
    size_t owners_[Config::BACKEND_BATCH_SIZE * 7] = {};
    bool included_[Config::BACKEND_BATCH_SIZE] = {};
};
