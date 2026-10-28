$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio/packages/toolchain-gccmingw32/bin/g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools/flight-core-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -DGATEWAY_GROUND_TEST_MODE=0 -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/flight_ownership_tests.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightSafetyController.cpp src/FlightStateManager.cpp -o .tools/flight-core-tests/ownership.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Flight core compile failed' }
& ./.tools/flight-core-tests/ownership.exe
if ($LASTEXITCODE -ne 0) { throw 'Flight core tests failed' }
