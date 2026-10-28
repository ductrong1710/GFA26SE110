# Backend heartbeat

All normal firmware profiles attempt `POST /device/gateways/{id}/heartbeat` on
startup once safe and connected, then at most once every 30 seconds. The existing
configured backend URL, gateway code and API key are used. No NTP or pending
sensor records are needed. Storage diagnostics builds do not send heartbeat.

Serial output: `[HEARTBEAT] gatewayId=... http=200 acknowledged=1`.
401 indicates rejected credentials; 403 indicates a mismatched gateway identity;
404 can indicate an older backend without the heartbeat route. Negative HTTP
status indicates a transport failure. Credentials are never logged.

Heartbeat pauses during flight tests, mission flight ownership, unsafe flight
states, pending Tello commands, collection work or loss of Internet Wi-Fi. It
never switches Wi-Fi or issues flight commands. The existing bounded HTTP
transport limits the request duration. A failed heartbeat is retried on the
normal 30-second interval, without a tight retry loop.

The backend reports OFFLINE after 90 seconds without contact. This can be normal
while connected to Tello Wi-Fi; it is not proof that the ESP32 is powered off.
The equipment page refreshes every 10 seconds while visible. Restart the updated
backend and upload the rebuilt firmware (mission_flight for mission operation).
