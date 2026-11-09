#include <Arduino.h>
#include "NodeApp.h"
NodeApp app;
void setup() { app.begin(); }
void loop() { app.update(); }
