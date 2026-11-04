param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
foreach ($port in @(5179, 5189)) {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
    try { $listener.Start() } finally { $listener.Stop() }
}
if (!$env:TEST_POSTGRES_CONNECTION -or $env:TEST_POSTGRES_CONNECTION -notmatch 'Host=127.0.0.1;Port=\d+;Database=postgres;Username=auth_test;') {
    throw 'Run through backend/scripts/Test-Postgres.ps1 -BrowserTests to use its disposable database.'
}
$frontend = Split-Path -Parent $PSScriptRoot
$backend = [IO.Path]::GetFullPath((Join-Path $frontend '../backend'))
$log = Join-Path $backend '.local/browser-api'
$settings = @{
    ConnectionStrings__DefaultConnection = $env:TEST_POSTGRES_CONNECTION
    ASPNETCORE_ENVIRONMENT = 'Development'
    DOTNET_ENVIRONMENT = 'Development'
    Jwt__SecretKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    Jwt__Issuer = 'BrowserTests'
    Jwt__Audience = 'BrowserTests'
    SEED_ADMIN_EMAIL = 'browser-admin@example.com'
    SEED_ADMIN_PASSWORD = 'Browser-' + [Guid]::NewGuid().ToString('N') + '-1!'
    Monitoring__Enabled = 'false'
    Email__Enabled = 'false'
    MaintenanceScheduler__Enabled = 'true'
    MaintenanceScheduler__IntervalSeconds = '1'
    Cors__AllowedOrigins__0 = 'http://127.0.0.1:5179'
    VITE_API_BASE_URL = 'http://127.0.0.1:5189'
    E2E_API_URL = 'http://127.0.0.1:5189'
    RentalExpiration__Enabled = 'true'
    RentalExpiration__IntervalSeconds = '1'
}
$previous = @{}
$apiProcess = $null
foreach ($key in $settings.Keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key, $settings[$key]) }
Push-Location $backend
try {
    dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Disposable browser database migration failed.' }
    $dll = Join-Path $backend "src/FarmMonitoring.Api/bin/$Configuration/net10.0/FarmMonitoring.Api.dll"
    $apiProcess = Start-Process dotnet -ArgumentList @(('"' + $dll + '"'), '--urls', $env:E2E_API_URL) -WorkingDirectory (Join-Path $backend 'src/FarmMonitoring.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput "$log.out" -RedirectStandardError "$log.err"
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($apiProcess.HasExited) { throw "Browser API exited. Inspect $log.err and $log.out" }
        try { Invoke-WebRequest "$env:E2E_API_URL/swagger/v1/swagger.json" -TimeoutSec 2 | Out-Null; $ready = $true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (!$ready) { throw 'Browser API did not become ready.' }
    Set-Location $frontend
    node node_modules/@playwright/test/cli.js test
    if ($LASTEXITCODE -ne 0) { throw 'Browser tests failed. Inspect frontend/test-results.' }
}
finally {
    if ($apiProcess -and !$apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id; $apiProcess.WaitForExit() }
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key]) }
    Pop-Location
}
