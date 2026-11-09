$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\flight-tests | Out-Null
foreach ($mode in @(0,1)) {
    & $compiler -std=c++11 -Wall -Wextra -Werror "-DGATEWAY_GROUND_TEST_MODE=$mode" -DGATEWAY_FLIGHT_TEST_PROFILE=1 -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/flight_test_tests.cpp src/FlightTestManager.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightStateManager.cpp src/FlightSafetyController.cpp -o .tools/flight-tests/flight_test.exe -static
    if ($LASTEXITCODE -ne 0) { throw 'Flight test host compile failed' }
    & .\.tools\flight-tests\flight_test.exe
    if ($LASTEXITCODE -ne 0) { throw 'Flight test host execution failed' }
}
