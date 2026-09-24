param(
    [Parameter(Mandatory = $true)][string]$PostgresBin,
    [string]$Dotnet = 'dotnet',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$backendRoot = Split-Path -Parent $PSScriptRoot
$runPath = Join-Path $backendRoot ('.local/postgres-' + [Guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $runPath 'data'
$passwordPath = Join-Path $runPath 'password.txt'
$previousConnection = $env:TEST_POSTGRES_CONNECTION
$started = $false
New-Item -ItemType Directory -Path $runPath -Force | Out-Null
# Restrict the ephemeral cluster and password file to the current Windows account.
if ($IsWindows) {
    $account = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $runPath /inheritance:r /grant:r "${account}:(OI)(CI)F" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict temporary PostgreSQL directory permissions.' }
}
$testPassword = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
Set-Content -LiteralPath $passwordPath -Value $testPassword -NoNewline

Push-Location $backendRoot
try {
    # Ask the OS for an available local port. pg_ctl fails safely if it is taken meanwhile.
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    & (Join-Path $PostgresBin 'initdb.exe') -D $dataPath -U auth_test --auth=scram-sha-256 --pwfile=$passwordPath --encoding=UTF8 --locale=C
    if ($LASTEXITCODE -ne 0) { throw 'initdb failed.' }
    # Wait for pg_ctl only, not its long-lived postgres child. Separate file handles
    # avoid a redirected PowerShell pipeline waiting forever for child output streams.
    $serverLog = Join-Path $runPath 'server.log'
    $startArguments = @('-D', ('"' + $dataPath + '"'), '-l', ('"' + $serverLog + '"'),
        '-o', ('"-h 127.0.0.1 -p ' + $port + '"'), '-w', '-t', '30', 'start')
    $startProcess = Start-Process -FilePath (Join-Path $PostgresBin 'pg_ctl.exe') -ArgumentList $startArguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runPath 'start.out') -RedirectStandardError (Join-Path $runPath 'start.err')
    if (-not $startProcess.WaitForExit(45000) -or $startProcess.ExitCode -ne 0) {
        throw "PostgreSQL start failed. See $serverLog"
    }
    $started = $true
    $env:TEST_POSTGRES_CONNECTION = "Host=127.0.0.1;Port=$port;Database=postgres;Username=auth_test;Password=$testPassword;Include Error Detail=false"
    if (-not $NoRestore) {
        & $Dotnet restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
    }
    & $Dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    & $Dotnet test --no-build --logger 'console;verbosity=normal' --logger trx *> (Join-Path $backendRoot '.local/test-latest.log')
    $testResult = $LASTEXITCODE
    Get-Content (Join-Path $backendRoot '.local/test-latest.log') -Tail 90
    if ($testResult -ne 0) { throw 'dotnet test failed. See .local/test-latest.log and TestResults.' }
}
finally {
    if ($started) {
        & (Join-Path $PostgresBin 'pg_ctl.exe') -D $dataPath -m fast -w -t 30 stop
    }
    $env:TEST_POSTGRES_CONNECTION = $previousConnection
    # Verify the only file being removed belongs to this generated test workspace.
    $resolvedPassword = [System.IO.Path]::GetFullPath($passwordPath)
    $allowedRun = [System.IO.Path]::GetFullPath($runPath) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPassword.StartsWith($allowedRun, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing cleanup outside the temporary PostgreSQL directory.'
    }
    Remove-Item -LiteralPath $resolvedPassword -ErrorAction SilentlyContinue
    Pop-Location
}
