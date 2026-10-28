$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compiler = Join-Path $env:USERPROFILE '.platformio\packages\toolchain-gccmingw32\bin\g++.exe'
$env:PATH = "$(Split-Path $compiler);$env:PATH"
New-Item -ItemType Directory -Force .tools\http-tests | Out-Null
& $compiler -std=c++11 -Wall -Wextra -Werror -include tests/host/Arduino.h -Itests/host/http -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/bounded_http_tests.cpp src/BoundedHttp.cpp -o .tools/http-tests/http.exe -static
if ($LASTEXITCODE -ne 0) { throw 'HTTP host compile failed' }
& .\.tools\http-tests\http.exe
if ($LASTEXITCODE -ne 0) { throw 'HTTP host tests failed' }
& $compiler -std=c++11 -Wall -Wextra -Werror -include tests/host/Arduino.h -Itests/host/http -Itests/host -Iinclude -I.pio/libdeps/esp32dev/ArduinoJson/src tests/host/control_auth_tests.cpp src/ConfigurationPortal.cpp src/RuntimeConfig.cpp tests/host/fake_nvs.cpp -o .tools/http-tests/auth.exe -static
if ($LASTEXITCODE -ne 0) { throw 'Control authentication host compile failed' }
& .\.tools\http-tests\auth.exe
if ($LASTEXITCODE -ne 0) { throw 'Control authentication host tests failed' }
