$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\mission-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_storage_tests.cpp src/MissionModel.cpp src/MissionStorage.cpp -o .tools/mission-tests/storage.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Mission host compile failed' }
& .\.tools\mission-tests\storage.exe
if ($LASTEXITCODE -ne 0) { throw 'Mission storage tests failed' }
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_backend_tests.cpp src/MissionModel.cpp src/MissionBackendClient.cpp -o .tools/mission-tests/backend.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Mission backend compile failed' }
& .\.tools\mission-tests\backend.exe
if ($LASTEXITCODE -ne 0) { throw 'Mission backend tests failed' }
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_planner_tests.cpp src/MissionRoutePlanner.cpp src/FlightSafetyController.cpp -o .tools/mission-tests/planner.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Mission planner compile failed' }
& .\.tools\mission-tests\planner.exe
if ($LASTEXITCODE -ne 0) { throw 'Mission planner tests failed' }
foreach ($ground in @(1,0)) {
    & $compiler -std=c++11 -Wall -Wextra -Werror "-DGATEWAY_GROUND_TEST_MODE=$ground" -Itests/host -Iinclude tests/host/tello_safety_tests.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightSafetyController.cpp -o .tools/mission-tests/safety.exe -static
    if ($LASTEXITCODE -ne 0) { throw 'Tello safety compile failed' }
    & .\.tools\mission-tests\safety.exe
    if ($LASTEXITCODE -ne 0) { throw 'Tello safety tests failed' }
}
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_collection_tests.cpp src/CollectionManager.cpp src/NodeRegistry.cpp -o .tools/mission-tests/collection.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Mission collection compile failed' }
& .\.tools\mission-tests\collection.exe
if ($LASTEXITCODE -ne 0) { throw 'Mission collection tests failed' }
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_sync_tests.cpp src/MissionManager.cpp src/MissionModel.cpp src/MissionStorage.cpp src/MissionBackendClient.cpp src/MissionRoutePlanner.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightSafetyController.cpp src/FlightStateManager.cpp -o .tools/mission-tests/sync.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Mission sync compile failed' }
& .\.tools\mission-tests\sync.exe
if ($LASTEXITCODE -ne 0) { throw 'Mission sync tests failed' }
& $compiler -std=c++11 -Wall -Wextra -Werror -DGATEWAY_GROUND_TEST_MODE=0 -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_end_to_end_tests.cpp src/MissionManager.cpp src/MissionModel.cpp src/MissionStorage.cpp src/MissionBackendClient.cpp src/MissionRoutePlanner.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightSafetyController.cpp src/FlightStateManager.cpp src/CollectionManager.cpp src/NodeRegistry.cpp -o .tools/mission-tests/end_to_end.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Mission integration compile failed' }
& .\.tools\mission-tests\end_to_end.exe
if ($LASTEXITCODE -ne 0) { throw 'Mission integration tests failed' }
foreach ($ground in @(1,0)) {
    & $compiler -std=c++11 -Wall -Wextra -Werror "-DGATEWAY_GROUND_TEST_MODE=$ground" -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/mission_manager_tests.cpp src/MissionManager.cpp src/MissionModel.cpp src/MissionStorage.cpp src/MissionBackendClient.cpp src/MissionRoutePlanner.cpp src/TelloController.cpp src/TelloTelemetry.cpp src/FlightSafetyController.cpp src/FlightStateManager.cpp -o .tools/mission-tests/manager.exe -static
    if ($LASTEXITCODE -ne 0) { throw 'Mission manager compile failed' }
    & .\.tools\mission-tests\manager.exe
    if ($LASTEXITCODE -ne 0) { throw 'Mission manager tests failed' }
}
