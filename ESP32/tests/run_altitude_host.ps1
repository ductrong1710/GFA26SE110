$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
if (-not (Test-Path $compiler)) { throw "Native compiler missing: $compiler" }
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\altitude-tests | Out-Null
foreach ($profile in @(0, 1)) {
    $output = ".tools/altitude-tests/altitude-$profile.exe"
    & $compiler -std=c++11 -Wall -Wextra -Werror "-DGATEWAY_MISSION_FLIGHT_PROFILE=$profile" -Itests/host -Iinclude tests/host/altitude_controller_tests.cpp src/FlightSafetyController.cpp -o $output -static
    if ($LASTEXITCODE -ne 0) { throw 'Altitude host compile failed' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Altitude host tests failed' }
}
