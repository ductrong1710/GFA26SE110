$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\mission-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -include tests/host/measurement_test_support.h -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/measurement_mission_tests.cpp src/GatewayMeasurement.cpp src/BackendAdapter.cpp -o .tools/mission-tests/measurement.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Measurement host compile failed' }
& .\.tools\mission-tests\measurement.exe
if ($LASTEXITCODE -ne 0) { throw 'Measurement mission tests failed' }
