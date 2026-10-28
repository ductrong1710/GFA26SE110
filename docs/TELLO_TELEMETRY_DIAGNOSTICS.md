# No telemetry on UDP 8890

`command -> OK` and `battery? -> 90` prove the command channel works. They do not
prove state telemetry is reaching ESP32. `packets=0` means nothing reached the
state listener, before payload parsing or source filtering. Keep ground telemetry
checks enabled; do not substitute a battery reply or guessed zero height.

The state listener uses 0.0.0.0:8890 per Ryze SDK 1.3 and still filters source IP.
Reference: https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf

For one independent diagnostic (not the normal mission workflow):

1. Stop mission attempts. Keep Tello on the ground. Power off ESP32 so it cannot
   issue commands during the test.
2. Temporarily connect the laptop to Tello Wi-Fi. No Internet is required.
3. Run in PowerShell:

   ```powershell
   powershell -NoProfile -File D:\Learning\SEP\Code\ESP32\tools\Test-TelloTelemetry.ps1
   ```

4. Save the TX/RX/STATE output and total packet count. Then return the laptop to
   its original network and restore its hotspot before reconnecting ESP32.

The script binds two UDP ports and sends only `command` and `battery?`. It does
not fly, land, alter firewall/network settings, or install anything. Port bind
failure may mean another process owns 8889/8890. Zero packets on the laptop alone
cannot distinguish a Tello transmission problem from a laptop firewall issue.
Packets present on the laptop but absent on ESP32 narrow the fault to the ESP32
receive path/network configuration. `127.0.0.1` is only for a simulated test.

Preflight telemetry timeout now cancels preparation when no takeoff was attempted,
then reconnects to Internet to report the failed mission. Recovery after an actual
or uncertain takeoff still requires the existing landing checks.

If a saved mission is `RECOVERY_REQUIRED`, rebooting does not clear it. After
uploading `mission_flight`, with Tello powered on and the aircraft on the ground,
connect to `UAV_GATEWAY` and request operator abort:

```powershell
Invoke-RestMethod -Method Post -Uri "http://192.168.4.1/api/mission/abort"
Invoke-RestMethod -Uri "http://192.168.4.1/api/mission/status" | ConvertTo-Json
```

A rejected `land` response now keeps the link and waits for new ground evidence.
It requires fresh, distinct samples over at least 5 seconds, height exactly zero,
ToF present and at most 15 cm, and zero vertical velocity. Missing, stale or
contradictory data cannot release the lock; the confirmation deadline returns to
recovery. Command timeout/disconnection still fails without unlocking. No flight
command is automatically retried. Confirmed ground permits abort completion and
Internet reconnection to upload the result; backend acknowledgement is still
required. No stored mission or measurement files need to be erased.

Altitude assessment keeps `h` as the control source throughout a flight. Missing
`h` blocks vertical correction instead of silently switching to ToF. The larger
of `h` and `tof` still enforces the configured ceiling (120 cm in mission_flight).
Fresh readings differing by more than 25 cm block vertical correction and pause
mission progression. If distinct fresh packets sustain that difference for 2
seconds, a flying mission lands with `ALTITUDE_SENSOR_DISAGREEMENT`. Agreement,
missing data or a gap beyond the telemetry freshness limit resets that window;
repeated reads of the same packet do not advance it. Landing remains available.

`[ALTITUDE]` logs report both readings, selected source/value, configured target,
ceiling, difference, and the decision/reason on changes and at most once per
second otherwise. `configuredTarget` is the profile target, not necessarily a
later waypoint's altitude. The mission_flight profile accepts height within
10 cm of target, held for 2 seconds with fresh samples and low vertical speed.
For a 100 cm target this is 90–110 cm inclusive; the 120 cm ceiling and sensor
agreement checks still apply. Other profiles retain 3 cm / 1 second. All profiles
retain the 20 second deadline. It diagnoses and stops conflicting readings;
it does not calibrate sensors or guarantee completion of the same flight.

For stabilization timeouts with `ALLOW_CONTROL`, inspect `[ALTITUDE-CONTROL]`
and `[TELLO-RC]` together. `accepted=1` means the request was queued;
`TX rc ... udpSent=1` means the local UDP send succeeded, not that the drone
executed the command. Ascent correction is RC vertical 10 when at least 20 cm
below target, otherwise 3, for a 300 ms requested pulse, followed by neutral and
500 ms reassessment. The shared safety filter still clamps ascent near the
ceiling, on stale/missing height or sensor disagreement. The 20 second timeout
still lands a mission that cannot stabilize. After mission 14 showed only slow
height progress, pulse duration was increased from 200 to 300 ms; RC amplitude,
reassessment delay and ceiling remain unchanged. This tuning requires hardware validation. Pulse-end
logs include height, ToF and vertical speed. Compare these with subsequent fresh
telemetry to establish whether the aircraft responded. Timeout logs include
command-pending and send-availability state. Do not infer RC delivery from
`ALLOW_CONTROL` alone or increase control strength without this evidence.

Official documentation checked for ordinary Tello:

- [Tello SDK 1.3](https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf):
  `up/down` accept 20–500 cm; RC channels accept -100 to 100. The SDK defines
  height and ToF telemetry in cm, but does not guarantee centimetre accuracy or
  prescribe a 100 cm target/tolerance. Lack of command input for 15 seconds
  triggers automatic landing.
- [Tello User Manual v1.4, pp. 7–8](https://dl-cdn.ryzerobotics.com/downloads/Tello/Tello%20User%20Manual%20v1.4.pdf):
  vision positioning works best at 0.3–6 m. Surface texture, reflectivity,
  transparency and lighting affect positioning. Around 1 m is within that range,
  but this does not guarantee absolute 10 cm measurement accuracy.

The accepted band is an application requirement, not sensor calibration. In-band
height with unsettled vertical motion waits without issuing a correction toward
the exact target; invalid motion data resets the stable window. Completion
requires a new telemetry sample. `stable` logs show the accepted reading and
elapsed stable time before mission execution.
