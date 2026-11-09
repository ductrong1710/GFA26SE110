# Contract verified against the workspace ESP32 implementation

Inspected without modifying ESP32: src/ApiServer.cpp, SensorNodeClient.cpp,
CollectionManager.cpp, GatewayMeasurement.cpp, BoundedHttp.cpp,
include/Config.h and include/Secrets.example.h on 2026-10-01.

| Operation | Actual contract / node adaptation |
|---|---|
| Register | POST http://192.168.4.1/api/gateway/nodes/register, Content-Type application/json, X-Device-Code and X-Device-Token. Body deviceCode, positive farmId/zoneId, firmwareVersion, deviceType, ipAddress. Gateway trusts observed remote IP, not supplied IP. |
| Registration success | Require HTTP 2xx plus success:true, authenticated:true and matching deviceCode. |
| Authentication on ALL node requests | ESP32 sends the NODE's X-Device-Code and X-Device-Token. Node requires these by default. X-Gateway-* is optional and disabled because ESP32 does not send it. |
| GET /api/node/info | deviceCode and integer pendingRecords required. Node returns 503 if pending count cannot be trusted. |
| GET /api/node/data?limit=N | deviceCode, integer count and records array; count must equal array length and be <=10. Oldest pending first. GET never changes state. |
| Records | Canonical recordId=deviceCode-sequence; uint32 sequence; integer Unix seconds, bool timeSynced; nullable numbers. Synchronized times restricted to 1577836800..4102444800. Node also preserves uptimeMs and state; gateway currently ignores those two fields. |
| POST /api/node/ack | recordIds array, <=10; response success:true. ackedCount counts newly transitioned records, so duplicate/unknown IDs return zero. No range ACKs. |
| GET /api/node/health | Must include deviceCode although master prompt's example omits it. |
| POST /api/node/time | unixTime in seconds; success:true. ESP32 calls this during collection only when its own clock is synchronized. |
| HTTP response framing | ESP32 uses HTTP/1.0, rejects Transfer-Encoding, bounds responses to 12288 bytes. Node sends explicit Content-Length and at most 10 records. |
| Presence | Gateway NODE_TIMEOUT_MS=30000; registration heartbeat is 20000ms, shorter than expiry. Collection also touches presence. |

ESP32 only ACKs records accepted by its durable storage. Its collection loop reads
info, optionally sends time, fetches data, persists each accepted record, and ACKs
the exact successful IDs. A duplicate node delivery therefore relies on the
gateway's existing duplicate detection. No ESP32 source changes are needed.

The gateway is synchronous too. A node registration and gateway poll can overlap:
a bounded request can time out and retry. Node records remain pending until the
actual ACK. An IP address is a transport address, never the durable identity.

No direct backend API, MQTT, NTP client, database or flight control is implemented
on the ESP8266. Configure matching existing ESP32 trusted-device credentials
locally before hardware integration.
