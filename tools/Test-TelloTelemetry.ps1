param(
    [ValidateRange(3,60)][int]$ListenSeconds = 15,
    [ValidateSet('192.168.10.1','127.0.0.1')][string]$TelloAddress = '192.168.10.1'
)
$ErrorActionPreference = 'Stop'
# Diagnostic only: this script sends exactly command and battery?, never flight commands.
if ($TelloAddress -ne '127.0.0.1' -and -not (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like '192.168.10.*' })) {
    throw 'Connect this laptop to Tello Wi-Fi for this diagnostic first. No network settings have been changed.'
}
$commandSocket = $null
$stateSocket = $null
try {
    $stateSocket = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Any,8890))
    # Loopback simulation shares a host with its mock server, so use an ephemeral
    # client port there. The real Tello diagnostic binds the documented port 8889.
    $localCommandPort = if ($TelloAddress -eq '127.0.0.1') { 0 } else { 8889 }
    $commandSocket = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Any,$localCommandPort))
    $commandSocket.Client.ReceiveTimeout = 3000
    $target = [Net.IPEndPoint]::new([Net.IPAddress]::Parse($TelloAddress),8889)
    foreach ($instruction in @('command','battery?')) {
        $bytes = [Text.Encoding]::ASCII.GetBytes($instruction)
        [void]$commandSocket.Send($bytes,$bytes.Length,$target)
        Write-Output "TX $instruction -> ${TelloAddress}:8889"
        $deadline = [Diagnostics.Stopwatch]::StartNew()
        $received = $false
        while ($deadline.Elapsed.TotalSeconds -lt 3) {
            if ($commandSocket.Available -eq 0) { Start-Sleep -Milliseconds 20; continue }
            $sender = [Net.IPEndPoint]::new([Net.IPAddress]::Any,0)
            $reply = $commandSocket.Receive([ref]$sender)
            if ($sender.Address.ToString() -ne $TelloAddress -or $sender.Port -ne 8889) { continue }
            Write-Output "RX $($sender): $([Text.Encoding]::ASCII.GetString($reply).Trim()) HEX=$([BitConverter]::ToString($reply))"
            $received = $true
            break
        }
        if (-not $received) { Write-Output 'No command response within 3 seconds.' }
        Start-Sleep -Milliseconds 500
    }
    $packets = 0
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Write-Output "Listening on 0.0.0.0:8890 for $ListenSeconds seconds..."
    while ($watch.Elapsed.TotalSeconds -lt $ListenSeconds) {
        if ($stateSocket.Available -eq 0) { Start-Sleep -Milliseconds 20; continue }
        $sender = [Net.IPEndPoint]::new([Net.IPAddress]::Any,0)
        $data = $stateSocket.Receive([ref]$sender)
        if ($sender.Address.ToString() -ne $TelloAddress) { continue }
        ++$packets
        if ($packets -le 3) { Write-Output "STATE $($sender): $([Text.Encoding]::ASCII.GetString($data).Trim())" }
    }
    Write-Output "Telemetry packets received: $packets"
    if ($packets -eq 0) {
        Write-Output 'No telemetry reached this listener. Check Tello firmware and the laptop UDP 8890 firewall path; this result alone cannot distinguish them.'
    }
} finally {
    if ($commandSocket) { $commandSocket.Dispose() }
    if ($stateSocket) { $stateSocket.Dispose() }
}
