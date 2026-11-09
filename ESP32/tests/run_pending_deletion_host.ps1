$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\pending-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -Iinclude tests/host/pending_deletion_tests.cpp -o .tools/pending-tests/pending.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Pending deletion host compile failed' }
& .\.tools\pending-tests\pending.exe
if ($LASTEXITCODE -ne 0) { throw 'Pending deletion tests failed' }
