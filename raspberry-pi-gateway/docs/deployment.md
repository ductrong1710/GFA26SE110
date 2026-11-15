# Raspberry Pi deployment and SSH

Hardware execution is NOT TESTED. The current acceptance run uses local simulators at the user's request. Keep the gateway on the ground until payload, power, flight time and Wi-Fi stability are measured for an aircraft-mounted installation.

## OS and administration

Use Raspberry Pi Imager to install Raspberry Pi OS Lite 64-bit. In customization set hostname `uav-gateway`, a dedicated administrator login, SSH public-key access and the management network. Boot with a stable power supply. Connect through `ssh <admin>@uav-gateway.local`. Record the Pi's IP if mDNS is unavailable. From a Windows laptop, use OpenSSH and `scp` to transfer the source or a built wheel.

```sh
sudo apt update
sudo apt install python3-venv python3-pip ffmpeg iw network-manager sqlite3 nftables git
sudo useradd --system --home-dir /var/lib/pi-gateway --shell /usr/sbin/nologin pi-gateway
sudo install -d -o pi-gateway -g pi-gateway -m 0700 /var/lib/pi-gateway
sudo install -d -m 0755 /opt/pi-gateway /etc/pi-gateway
sudo python3 -m venv /opt/pi-gateway/venv
sudo /opt/pi-gateway/venv/bin/pip install /path/to/raspberry-pi-gateway
sudo install -m 0644 config/gateway.example.json /etc/pi-gateway/gateway.json
sudo install -m 0600 .env.example /etc/pi-gateway/gateway.env
sudoedit /etc/pi-gateway/gateway.json
sudoedit /etc/pi-gateway/gateway.env
sudo install -m 0644 systemd/gateway.service /etc/systemd/system/gateway.service
sudo systemctl daemon-reload
sudo systemctl enable --now gateway.service
```

Fill API key and sensor tokens through the private environment file. Configure gateway/UAV IDs, trusted farm/zone IDs and actual network source IPs. Runtime uses environment variables directly. For foreground administration, load the private environment with a root-owned launcher or use `systemd-run` with `EnvironmentFile`. Never paste secrets into shell history or command-line arguments.

## Two Wi-Fi interfaces and WAN

Inspect `ip link`, `iw dev`, and `iw list`. Verify AP support on the USB adapter's own phy. Match interfaces by permanent MAC through NetworkManager profiles or `.link` files, then use those names in JSON. The labels wlan0/wlan1 in the example require verification on the specific Pi.

With the aircraft physically grounded and a separate SSH connection, run `sudo bash scripts/setup-ap.sh --grounded wlan1`. The USB AP uses SSID `UAV_GATEWAY`, address `192.168.4.1/24` and NetworkManager's DHCP sharing. Configure WPA2 credentials privately with `sudo nmtui` for boot persistence. Verify ESP8266 obtains an address and can reach TCP80 on the Pi. Avoid running a second DHCP server on the same interface.

Create a Tello Wi-Fi profile on the integrated adapter with `sudo nmtui`. Set its MAC restriction, IPv4 `never-default=yes`, IPv6 disabled and automatic reconnection only to the Tello profile during flight operations. Set the actual acquired Tello-facing IP in configuration. Verify `ip route get 192.168.10.1 from <tello_ip>` reports the configured interface.

WAN can use USB Ethernet or a separate network interface. Configure its source IPv4 and interface in JSON. If that link is absent, network workers back off and records stay in SQLite. Keep Tello and sensor routes more specific than the WAN default. Runtime checks the kernel route and binds source addresses before device HTTP/UDP traffic.

For a two-radio installation without independent WAN, download/approve while WAN is available, retain the Tello link throughout launch/flight, and synchronize after verified landing. This implementation requires WAN for the live launch handshake and therefore needs an independent WAN path for real scheduled flights. Offline sensor collection and queued post-mission delivery remain available. Automatic radio switching is not implemented. A future switch workflow must preserve the launch freshness window and recovery lock.

Apply host firewall rules appropriate to the site's existing policy. Permit TCP80 only from `192.168.4.0/24` on the sensor interface, UDP67 for DHCP there, UDP8889/8890/11111 only from `192.168.10.1` on the Tello interface, and SSH only from the management subnet. Permit established replies, loopback and required WAN DNS/HTTPS. Block sensor-to-Tello forwarding and unsolicited WAN access to gateway APIs. Validate rules with `nft -c -f <rules>` before loading them. Keep the current SSH session open and test a second connection before saving a restrictive ruleset.

## Commands through SSH

```sh
systemctl status gateway.service
journalctl -u gateway.service -n 100 --no-pager
sudo systemctl restart gateway.service
sudo systemctl stop gateway.service
sudo systemctl disable gateway.service
bash scripts/check-network.sh wlan1 wlan0
/opt/pi-gateway/venv/bin/python -m gateway.main simulate
```

The systemd service always forces ground mode. Bench and explicit flight commands share an OS process lock with the service. Stop the service before either command. A foreground command needs the same private environment and permission to bind TCP80 and the Tello interface. Use a transient systemd unit with `User=pi-gateway`, `EnvironmentFile=/etc/pi-gateway/gateway.env`, `AmbientCapabilities=CAP_NET_BIND_SERVICE CAP_NET_RAW`, and `WorkingDirectory=/opt/pi-gateway`. For a bench check execute `python -m gateway.main --config /etc/pi-gateway/gateway.json bench` through that unit.

An explicit flight command is `flight <mission_id> --confirm 'FLY <mission_id>'`. It also requires `GROUND_TEST_MODE=false`, a verified clear area and aircraft, and current owner approval in the Web launch workflow. It waits for matching Backend readiness and consumes once. Return the configuration to ground mode after testing. The hardware checklist must be completed before using this command on an aircraft.

After physical inspection of a recovered aircraft, `reconcile <mission_id> --confirm 'LANDED <mission_id>'` also requires fresh stable ground telemetry. It retains audit history and queues cancellation, valid whether the ambiguous launch reached the Backend or not. Resume the ordinary service to acknowledge cancellation before synchronizing retained data.

Ground camera testing uses `bench --video-mission <mission_id> --seconds 10` through the same privileged launcher. The mission must already be persisted and explicitly contain `recordVideo=true`; missing/false flags refuse recording. Duration is bounded to1–30 seconds, ground mode is forced and no launch request is sent. The audited production Backend does not yet emit this flag; enable it only through the agreed extension or a bench fixture. The host-only synthetic test `python -m pytest tests/test_video_ffmpeg.py -q` needs FFmpeg/ffprobe on PATH and never contacts an aircraft.

## Updates and data recovery

Stop service after confirmed landing. Use `backup /absolute/new-backup.db` to create a consistent SQLite backup. Copy the backup and finalized video directory to another machine. Verify `sqlite3 backup.db 'PRAGMA integrity_check;'` returns `ok`. Keep the original database, WAL and SHM together when collecting a stopped database for forensic recovery. Never copy only the live database file as a backup.

Update using a reviewed Git commit or transfer a wheel with `scp`. Install it into the existing venv, restart and inspect logs. Future schema versions are refused by older gateway builds. Test rollback with a separately verified backup rather than downgrading an active database.

Disk quota prevents new collection before sensor ACK. Archive verified uploaded video to free space. Keep rejected records and ambiguous telemetry for diagnosis. `retry-held <outbox_id>` re-enables the unchanged batch after credentials, channel mapping or backend scope have been corrected. A changed batch payload requires a new identity and explicit reconciliation, never an overwrite of the persisted request.

Raspberry Pi primary documentation covers [network configuration](https://www.raspberrypi.com/documentation/computers/configuration.html) and [remote access](https://www.raspberrypi.com/documentation/computers/remote-access.html).
