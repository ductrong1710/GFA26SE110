#pragma once
#include "StorageManager.h"
#include "SensorManager.h"
#include "MeasurementQueue.h"
#include "ApiServer.h"
#include "WiFiManager.h"
#include "GatewayClient.h"
class NodeApp {
public:
    void begin();
    void update();
private:
    StorageManager storage_;
    MeasurementQueue queue_{storage_};
    SensorManager sensors_;
    TimeManager time_;
    WiFiManager wifi_;
    GatewayClient gateway_{wifi_};
    ApiServer api_{queue_,time_};
    uint32_t lastSample_ = 0;
    RetryTimer maintenance_;
};
