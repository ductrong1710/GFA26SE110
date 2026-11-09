param(
    [string]$BaseUrl = 'http://192.168.4.1',
    [Parameter(Mandatory=$true)][string]$Token,
    [int]$TimeoutSeconds = 30,
    [switch]$TestCapacity,
    [string]$Token2 = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.UseProxy = $false
$http = New-Object System.Net.Http.HttpClient($handler)
$http.Timeout = [TimeSpan]::FromSeconds(10)

function Assert-True($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Call-Api([string]$Path, [int]$Expected, [string]$Body = $null,
                  [string]$Code = '', [string]$Secret = '', [string]$ErrorCode = '') {
    $method = if ($Path -eq '/api/gateway/nodes/register') { 'POST' } else { 'GET' }
    $req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::new($method), "$BaseUrl$Path")
    if ($Code) { $req.Headers.Add('X-Device-Code', $Code) }
    if ($Secret) { $req.Headers.Add('X-Device-Token', $Secret) }
    if ($method -eq 'POST') {
        $req.Content = [System.Net.Http.StringContent]::new($Body, [Text.Encoding]::UTF8, 'application/json')
    }
    $res = $http.SendAsync($req).GetAwaiter().GetResult()
    try {
        Assert-True ([int]$res.StatusCode -eq $Expected) "Expected HTTP $Expected, got $([int]$res.StatusCode)"
        Assert-True ($res.Content.Headers.ContentType.MediaType -eq 'application/json') 'Wrong content type'
        $json = $res.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if ($ErrorCode) { Assert-True ($json.errorCode -eq $ErrorCode) "Expected $ErrorCode" }
        return $json
    } finally { $res.Dispose(); $req.Dispose() }
}

try {
    $path = '/api/gateway/nodes/register'
    $body = @{deviceCode='SENSOR-001'; farmId=1; zoneId=1; firmwareVersion='1.0.0'; deviceType='ESP8266_SENSOR'; ipAddress='192.168.4.250'}
    $payload = $body | ConvertTo-Json -Compress
    $initial = Call-Api '/api/gateway/nodes' 200
    Assert-True ($initial.count -eq 0) 'Reset ESP32 first: tests require an empty registry.'

    $r = Call-Api $path 200 $payload 'SENSOR-001' $Token
    Assert-True ($r.authenticated -eq $true) 'Authentication failed'
    Write-Host 'PASS 1: valid node'

    $null = Call-Api $path 401 $payload 'SENSOR-001' 'deliberately-wrong' 'AUTHENTICATION_FAILED'
    Write-Host 'PASS 2: wrong token'

    $unknown = $payload.Replace('SENSOR-001','SENSOR-999')
    $null = Call-Api $path 401 $unknown 'SENSOR-999' $Token 'UNKNOWN_DEVICE'
    Write-Host 'PASS 3: unknown node'

    $mismatch = $payload.Replace('SENSOR-001','SENSOR-002')
    $null = Call-Api $path 400 $mismatch 'SENSOR-001' $Token 'DEVICE_CODE_MISMATCH'
    Write-Host 'PASS 4: header/body mismatch'

    $null = Call-Api $path 200 $payload 'SENSOR-001' $Token
    $nodes = Call-Api '/api/gateway/nodes' 200
    Assert-True ($nodes.count -eq 1) 'Duplicate registry entry'
    Write-Host 'PASS 5: repeat registration does not duplicate'

    $oldIp = $nodes.nodes[0].ipAddress
    $body.firmwareVersion='1.0.1'; $body.farmId=2; $body.zoneId=3; $body.ipAddress='192.168.4.249'
    $payload = $body | ConvertTo-Json -Compress
    $null = Call-Api $path 200 $payload 'SENSOR-001' $Token
    $nodes = Call-Api '/api/gateway/nodes' 200
    Assert-True ($nodes.count -eq 1 -and $nodes.nodes[0].firmwareVersion -eq '1.0.1' -and $nodes.nodes[0].farmId -eq 2 -and $nodes.nodes[0].zoneId -eq 3) 'Metadata not updated'
    Assert-True ($nodes.nodes[0].ipAddress -eq $oldIp) 'Claimed JSON IP replaced actual remote IP'
    Write-Host 'PASS 6: metadata updated; spoofed JSON IP ignored'

    Assert-True ($nodes.onlineCount -eq 1 -and $nodes.nodes[0].online -and $nodes.nodes[0].authenticated) 'Bad real node data'
    Assert-True ($nodes.nodes[0].rssi -eq 0) 'RSSI must be unknown'
    Assert-True (-not ($nodes.nodes[0].PSObject.Properties.Name -contains 'deviceSecret')) 'Secret exposed'
    Write-Host 'PASS 7: registry data'
    $nodes | ConvertTo-Json -Depth 5

    Start-Sleep -Seconds ($TimeoutSeconds + 2)
    $nodes = Call-Api '/api/gateway/nodes' 200
    Assert-True ($nodes.count -eq 1 -and $nodes.onlineCount -eq 0 -and -not $nodes.nodes[0].online) 'Timeout failed'
    Write-Host 'PASS 8: offline after timeout, entry retained'

    $null = Call-Api $path 200 $payload 'SENSOR-001' $Token
    $nodes = Call-Api '/api/gateway/nodes' 200
    Assert-True ($nodes.count -eq 1 -and $nodes.onlineCount -eq 1 -and $nodes.nodes[0].online) 'Reconnect failed'
    Write-Host 'PASS 9: re-registration restores online status'

    $null = Call-Api $path 401 $payload 'SENSOR-001' '' 'MISSING_CREDENTIALS'
    $null = Call-Api $path 400 '{}' 'SENSOR-001' $Token 'MISSING_DEVICE_CODE'
    $null = Call-Api $path 400 '{' 'SENSOR-001' $Token 'INVALID_JSON'
    $null = Call-Api $path 400 ' ' 'SENSOR-001' $Token 'INVALID_JSON'
    $null = Call-Api $path 400 '' 'SENSOR-001' $Token 'INVALID_JSON'
    $body.ipAddress='192..4.2'
    $null = Call-Api $path 400 ($body | ConvertTo-Json -Compress) 'SENSOR-001' $Token 'INVALID_IP_ADDRESS'
    $null = Call-Api $path 413 ('x' * 1025) 'SENSOR-001' $Token 'BODY_TOO_LARGE'
    $null = Call-Api '/api/gateway/status' 200
    $null = Call-Api '/api/gateway/time' 200
    $null = Call-Api '/api/gateway/unknown' 404 $null '' '' 'NOT_FOUND'
    Write-Host 'PASS: malformed requests and existing API checks'
    if ($TestCapacity) {
        Assert-True (-not [string]::IsNullOrEmpty($Token2)) 'Supply Token2 for the capacity test'
        # This optional check requires a manually flashed MAX_SENSOR_NODES=1 build.
        $second = $payload.Replace('SENSOR-001','SENSOR-002')
        $null = Call-Api $path 503 $second 'SENSOR-002' $Token2 'NODE_REGISTRY_FULL'
        $null = Call-Api $path 200 $payload 'SENSOR-001' $Token
        Write-Host 'PASS: registry full rejects new node but permits existing-node update'
    }
} finally { $http.Dispose(); $handler.Dispose() }
