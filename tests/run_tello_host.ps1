$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
if (-not (Test-Path $compiler)) { throw "Native compiler missing: $compiler" }
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\tello-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -DGATEWAY_GROUND_TEST_MODE=0 -Itests/host -Iinclude tests/host/tello_tests.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightStateManager.cpp src/FlightSafetyController.cpp -o .tools/tello-tests/tello_tests.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Host compile failed' }
& .\.tools\tello-tests\tello_tests.exe
if ($LASTEXITCODE -ne 0) { throw 'Host tests failed' }
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude tests/host/network_tests.cpp src/GatewayWiFiManager.cpp src/TimeManager.cpp -o .tools/tello-tests/network_tests.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Network host compile failed' }
& .\.tools\tello-tests\network_tests.exe
if ($LASTEXITCODE -ne 0) { throw 'Network host tests failed' }
