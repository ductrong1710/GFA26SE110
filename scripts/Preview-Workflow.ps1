param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')
$ErrorActionPreference = 'Stop'
$frontendRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = [IO.Path]::GetFullPath((Join-Path $frontendRoot '../BE/GFA26SE110'))
$runPath = Join-Path $frontendRoot ('.local/preview-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runPath -Force | Out-Null
$passwordPath = Join-Path $runPath 'password.txt'
$randomPassword = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
Set-Content -LiteralPath $passwordPath -Value $randomPassword -NoNewline
$databaseStarted = $false
$apiProcess = $null
$viteProcess = $null
try {
    $dataPath = Join-Path $runPath 'data'
    & (Join-Path $PostgresBin 'initdb.exe') -D $dataPath -U preview_test --auth=scram-sha-256 --pwfile=$passwordPath --encoding=UTF8 --locale=C *> (Join-Path $runPath 'init.log')
    if ($LASTEXITCODE -ne 0) { throw 'Temporary PostgreSQL initialization failed.' }
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $starter = Start-Process -FilePath (Join-Path $PostgresBin 'pg_ctl.exe') -WindowStyle Hidden -PassThru -ArgumentList @('-D', ('"' + $dataPath + '"'), '-l', ('"' + (Join-Path $runPath 'postgres.log') + '"'), '-o', ('"-h 127.0.0.1 -p ' + $port + '"'), '-w', 'start') -RedirectStandardOutput (Join-Path $runPath 'start.log') -RedirectStandardError (Join-Path $runPath 'start.err')
    if (-not $starter.WaitForExit(30000) -or $starter.ExitCode -ne 0) { throw 'Could not start disposable database.' }
    $databaseStarted = $true
    $env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=$port;Database=postgres;Username=preview_test;Password=$randomPassword"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:Jwt__SecretKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    $env:Jwt__Issuer = 'WorkflowPreview'; $env:Jwt__Audience = 'WorkflowPreview'
    $env:Cors__AllowedOrigins__0 = 'http://127.0.0.1:5173'
    $env:Monitoring__Enabled = 'false'; $env:Email__Enabled = 'false'
    $env:MaintenanceScheduler__Enabled = 'false'; $env:RentalExpiration__Enabled = 'false'
    $env:SEED_ADMIN_EMAIL = 'preview-admin@example.test'; $env:SEED_ADMIN_PASSWORD = 'Preview-password-123!'
    Push-Location $backendRoot
    try { & dotnet ef database update --no-build --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api *> (Join-Path $runPath 'migration.log'); if ($LASTEXITCODE -ne 0) { throw 'Test migration failed.' } }
    finally { Pop-Location }
    # Explicit test fixture, independent of the application's optional bootstrap seeder.
    $salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $subkey = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($env:SEED_ADMIN_PASSWORD, $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA512, 32)
    $identityHash = [byte[]](1,0,0,0,2,0,1,134,160,0,0,0,16) + $salt + $subkey
    $encodedHash = [Convert]::ToBase64String($identityHash)
    $env:PGPASSWORD = $randomPassword
    & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p $port -U preview_test -d postgres -c "INSERT INTO users(email,password_hash,full_name,is_active,created_at,password_version) VALUES ('preview-admin@example.test','$encodedHash','Admin kiểm thử',true,CURRENT_TIMESTAMP,0); INSERT INTO user_roles(user_id,role_id) SELECT u.id,r.id FROM users u CROSS JOIN roles r WHERE u.email='preview-admin@example.test' AND r.name='FarmAdministrator';" *> (Join-Path $runPath 'fixture.log')
    if ($LASTEXITCODE -ne 0) { throw 'Could not seed disposable preview administrator.' }
    $apiDll = Join-Path $backendRoot 'src/FarmMonitoring.Api/bin/Debug/net10.0/FarmMonitoring.Api.dll'
    $apiProcess = Start-Process dotnet -ArgumentList @(('"' + $apiDll + '"'), '--urls', 'http://127.0.0.1:5087') -WorkingDirectory (Join-Path $backendRoot 'src/FarmMonitoring.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'api.log') -RedirectStandardError (Join-Path $runPath 'api.err')
    $ready = $false
    for ($i=0; $i -lt 40; $i++) {
        try { $login = Invoke-RestMethod 'http://127.0.0.1:5087/api/auth/login' -Method Post -ContentType 'application/json' -Body (@{email=$env:SEED_ADMIN_EMAIL;password=$env:SEED_ADMIN_PASSWORD} | ConvertTo-Json); $ready=$true; break } catch { $lastFailure=$_.Exception.Message; Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "Preview login failed: $lastFailure. See api.log." }
    $headers = @{Authorization='Bearer ' + $login.data.accessToken}
    function PostPreview($path,$body) { Invoke-RestMethod ('http://127.0.0.1:5087' + $path) -Method Post -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 10) }
    foreach($role in @('Technician','FarmEngineer','FarmOwner')) {
        PostPreview '/api/users' @{email=($role.ToLower()+'@example.test');fullName=@{Technician='Kỹ thuật viên kiểm thử';FarmEngineer='Kỹ sư kiểm thử';FarmOwner='Chủ vườn kiểm thử'}[$role];password='Preview-password-123!';roles=@($role)} | Out-Null
    }
    foreach($entry in @(@{kind='DEVICE';title='Cảm biến độ ẩm đất';body='Theo dõi độ ẩm theo khu vực. Vị trí được KTV xác định khi khảo sát thực tế.';estimatedMonthlyRentVnd=150000},@{kind='COMPONENT';title='Đầu đo độ ẩm';body='Linh kiện thay thế đầu đo. Miễn phí nếu được xác nhận thuộc bảo hành.';replacementPriceVnd=200000},@{kind='WARRANTY';title='Bảo hành và bảo trì';body='Miễn phí công bảo trì. Linh kiện thuộc bảo hành được miễn phí; các trường hợp khác chốt giá và ký giấy trước khi thay.'})) {
        $entry.expectedRevision=0; $content=PostPreview '/api/admin/public-content' $entry
        PostPreview ( '/api/admin/public-content/' + $content.data.id + '/publish') @{expectedRevision=1} | Out-Null
    }
    Invoke-RestMethod 'http://127.0.0.1:5087/api/consultations' -Method Post -ContentType 'application/json' -Body (@{customerName='Khách kiểm thử giao diện';phone='0900000000';farmLocation='Nông trại kiểm thử';serviceNeed='Tư vấn giám sát độ ẩm đất';content='Dữ liệu chỉ dùng trong môi trường kiểm thử tạm.'} | ConvertTo-Json) | Out-Null
    $env:VITE_API_BASE_URL = 'http://127.0.0.1:5087'
    $vitePath = Join-Path $frontendRoot 'node_modules/vite/bin/vite.js'
    $viteProcess = Start-Process node -ArgumentList @(('"' + $vitePath + '"'), '--host', '127.0.0.1', '--port', '5173', '--strictPort') -WorkingDirectory $frontendRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'vite.log') -RedirectStandardError (Join-Path $runPath 'vite.err')
    Write-Output ('Preview ready: http://127.0.0.1:5173 | API http://127.0.0.1:5087 | stop file: ' + (Join-Path $runPath 'stop'))
    Write-Output 'Disposable accounts: preview-admin@example.test / technician@example.test / farmengineer@example.test / farmowner@example.test; password: Preview-password-123!'
    while (-not (Test-Path (Join-Path $runPath 'stop'))) { Start-Sleep -Seconds 1 }
}
finally {
    if($viteProcess -and -not $viteProcess.HasExited) { Stop-Process -Id $viteProcess.Id }
    if($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    if($databaseStarted) { & (Join-Path $PostgresBin 'pg_ctl.exe') -D (Join-Path $runPath 'data') -m fast -w stop | Out-Null }
    if(Test-Path -LiteralPath $passwordPath) { Remove-Item -LiteralPath $passwordPath }
}
