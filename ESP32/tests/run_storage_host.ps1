$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\storage-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -include tests/host/measurement_test_support.h -Itests/host/storage -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/storage_lifecycle_tests.cpp src/GatewayMeasurement.cpp src/StorageManager.cpp -o .tools/storage-tests/storage.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Storage host compile failed' }
& .\.tools\storage-tests\storage.exe
if ($LASTEXITCODE -ne 0) { throw 'Storage host tests failed' }
