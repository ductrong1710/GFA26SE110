# Scheduled launch from the web

Use the `mission_flight` PlatformIO environment. The backend must include the
`ScheduledMissionLaunch` migration and the frontend must include the launch panel.
No physical flight was performed by the automated verification.

1. Start the backend on the LAN HTTP profile and keep the laptop awake, with its
   hotspot enabled if that is the gateway's Internet network. Configure the gateway's
   backend base URL to the reachable laptop address (for Windows hotspot, commonly
   `http://192.168.137.1:5043/api`). Do not use `localhost` on the gateway.
2. Create/schedule a mission on the main web app. Set UAV model to a Tello model.
3. Keep ESP32 connected to Internet. It downloads and persists the route, acknowledges
   it, synchronizes time, and posts readiness every 5 seconds while idle.
4. Open mission details. Within 10 minutes before departure, confirm aircraft readiness
   and that the flight area is clear. Tello must be powered on and on the ground.
5. At the scheduled start, gateway consumes approval exactly once, saves its durable
   preflight checkpoint, switches to Tello, and follows the existing SDK/battery/ground
   telemetry checks before takeoff. Laptop Wi-Fi stays unchanged. The durable checkpoint
   is actually written before the consume HTTP request, so a lost response is safe.
6. After landing, gateway reconnects to Internet and uploads results.

Approval expires 60 seconds after scheduled start, or at scheduled end if earlier.
This is the deadline for accepting preparation, not a guarantee of physical takeoff
at an exact second. No confirmation means no automatic launch. On the next readiness
request an expired scheduled mission is cancelled with `MISSED_LAUNCH_WINDOW`.
An offline gateway cannot execute a cached approval. Reboot invalidates approval;
plan/schedule changes increment the revision, invalidate approval and require re-download.

`[MISSION-LAUNCH] ... state=...` shows readiness, awaiting confirmation, waiting for
time, and due status. `consume ... accepted=1` means backend accepted the start,
not that the aircraft took off. If the consume response is lost, firmware enters
`RECOVERY_REQUIRED` and never automatically retries takeoff. Inspect backend status
and confirm the aircraft is grounded before using the existing local recovery flow.

While STA is connected to Tello, gateway has no backend link. The web page cannot
provide immediate landing/abort. Local gateway landing controls remain available;
keep the aircraft supervised. The existing explicit local start remains supported.
Ground-test mode never automatically consumes a launch or flies.
