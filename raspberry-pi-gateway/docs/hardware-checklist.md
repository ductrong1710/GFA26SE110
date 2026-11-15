# Hardware acceptance record

All items below are NOT TESTED in this session. Record date, firmware/OS versions, operator, observations and retained logs for every completed item. The user requested continued simulation.

- [ ] Pi Zero2W boots Raspberry Pi OS Lite64-bit, SSH keys work, service runs as dedicated user.
- [ ] `iw phy <usb_phy> info` lists AP mode. AP survives reboot and issues DHCP leases to the ESP8266.
- [ ] Stable adapter MAC mappings match configuration. Tello traffic routes through its interface. Sensor traffic routes through the AP. WAN traffic uses the independent WAN route.
- [ ] Firewall rejects WAN registration and sensor-to-Tello forwarding. Management SSH remains reachable.
- [ ] Real ESP8266 registration succeeds every20s. Disconnect is visible after30s. Invalid token and changed farm/zone fail.
- [ ] All seven readings preserve nulls. Saved record IDs and timestamps match sensor output.
- [ ] Controlled power interruption before/after commit and before ACK causes no missing records. Restore verified SQLite backup on a spare card.
- [ ] Remove WAN, collect offline, restore WAN and verify same batch key, per-record ACK and no duplicate readings.
- [ ] SDK battery and telemetry work in ground mode. Takeoff remains inhibited during boot/restart/bench.
- [ ] Verify Tello height/ToF readings against physical height and surface conditions. Measure command latency and stable landing evidence.
- [ ] Ground video test produces a valid H.264 file using installed FFmpeg/ffprobe. No file or stream appears with recording disabled.
- [ ] Interrupted capture stays quarantined. Disk reserve and video quota stop capture while preserving sensor data.
- [ ] Measure RSS, CPU, microSD writes, temperature and power draw over an extended run. Include20 registered sensors, backlog synchronization and video capture. Compare against512MB RAM and systemd limits.
- [ ] Explicit supervised flight only after ground checks, physical area clearance, sufficient battery and matching Web approval. Test low battery, stale telemetry, abort and recovery in a controlled environment.
- [ ] Observe safe return/landing at route limits and confirm no radio switch while landing is uncertain.
- [ ] Deployed media API enforces gateway/mission ownership, idempotency, checksum, size and offset. Web playback enforces farm/mission permissions.
- [ ] Aircraft-mounted use separately validates payload capacity, attachment, center of gravity, interference, supply, endurance and link stability.
