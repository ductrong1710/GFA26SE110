$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\heartbeat-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -Iinclude tests/host/heartbeat_tests.cpp -o .tools/heartbeat-tests/heartbeat.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Heartbeat host compile failed' }
& .\.tools\heartbeat-tests\heartbeat.exe
if ($LASTEXITCODE -ne 0) { throw 'Heartbeat tests failed' }
