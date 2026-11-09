$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\config-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/runtime_config_tests.cpp src/RuntimeConfig.cpp -o .tools/config-tests/config.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Configuration host compile failed' }
& .\.tools\config-tests\config.exe
if ($LASTEXITCODE -ne 0) { throw 'Configuration tests failed' }
