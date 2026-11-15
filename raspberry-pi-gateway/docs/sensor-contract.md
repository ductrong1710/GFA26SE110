# ESP8266 sensor integration

The Pi accepts the firmware on `origin/ESP6288` at `19a489e`. Register the node's device code, token, farmId and zoneId in the gateway configuration. Bind registration to the sensor AP at `192.168.4.1:80`. Keep the AP private because this firmware uses plain HTTP. Use a synchronized Pi clock before collecting measurements.

| Operation | Wire behavior |
|---|---|
| Register | `POST /api/gateway/nodes/register` with `X-Device-Code`, `X-Device-Token` and a JSON object containing `deviceCode`, `farmId`, `zoneId`, `firmwareVersion`, `deviceType` and optional `ipAddress`. The Pi validates the supplied address and calls the observed TCP peer on port 80. |
| Registration acknowledgment | HTTP 200 with `success:true`, `authenticated:true` and the exact `deviceCode`. The ESP requires explicit `Content-Length` between 1 and 511 and rejects `Transfer-Encoding`. |
| Presence | The node registers every 20 seconds after success and retries every 15 seconds after failure. Registration and authenticated polls refresh the 30-second presence window. |
| Node information | `GET /api/node/info` returns the matching `deviceCode` and unsigned `pendingRecords`. HTTP 503 indicates an unreliable queue. |
| Node health | `GET /api/node/health` includes `deviceCode`, uptime, heap, Wi-Fi, storage and clock status. Storage failures can still return HTTP 200. |
| Clock | `POST /api/node/time` accepts `{"unixTime":1700000000}`. The ESP accepts integer Unix seconds from 1577836800 through 4102444800. A failed clock update preserves each sample's original time flag. |
| Fetch | `GET /api/node/data?limit=10` returns `success`, `deviceCode`, `count` and `records`. The ESP serves oldest pending records first and leaves them pending. |
| Acknowledge | `POST /api/node/ack` accepts `{"recordIds":["SENSOR-001-4"]}`. Up to ten exact IDs, positive response `success:true` and `ackedCount`. A count of zero is valid on replay. |

All five node operations use the **node's** `X-Device-Code` and `X-Device-Token`. Both POST bodies use `application/json` and `Content-Length`. The maximum request body is 1,024 bytes. Code is printable nonspace ASCII up to 64 bytes. Firmware version and type allow 1–32 printable ASCII bytes. Both farm and zone IDs must be positive integers matching the provisioned scope. The Pi accepts at most twenty trusted device codes. More than four simultaneous registration requests can trigger a connection close and firmware retry.

Registration errors use `success:false` with `errorCode`. Malformed JSON, invalid metadata and header/body mismatch return 400. Missing, unknown or wrong credentials return 401. Farm/zone mismatches and peers outside the sensor subnet return 403. Oversize bodies return 413, unsupported framing or media type returns 415, and failed storage returns 503.

## Sample and replay behavior

```json
{"deviceCode":"SENSOR-001","recordId":"SENSOR-001-4","sequence":4,"measuredAt":0,"timeSynced":false,"uptimeMs":61101,"state":"PENDING","temperature":23.5,"humidity":50,"soilMoisture":null,"lightIntensity":null,"ph":null,"waterLevel":null,"batteryVoltage":null}
```

Sequence is a positive uint32. The ESP reserves blocks of 64, so gaps after reboot are expected. Each `recordId` is the exact device code, a hyphen and the sequence in decimal. `measuredAt` is Unix seconds when synchronized and zero otherwise. Each of the seven readings accepts a finite number within ±10¹² or JSON null. The Pi keeps the sensor payload, the first UTC collection timestamp and the original mission association.

The collector validates each batch, commits it to SQLite, verifies the node address still matches, and then sends the exact IDs. A malformed record, an identity collision with different content, or a transaction failure stops that batch before ACK. A lost ACK response leaves the committed records eligible for synchronization. On redelivery the collector checks the stored payload before acknowledging it. An ACK can partially succeed at the sensor before HTTP 503, so a replay may acknowledge fewer IDs than requested. After a timeout, zero remaining records on the sensor can leave the local `node_acked` flag false even when backend delivery succeeds. The two flags track different acknowledgments.

The backend receives one row per non-null channel or a single unresolved record when time or readings cannot be resolved. `sourceRecordKey` is the same sample ID on every channel. The backend must contain the active node and channel codes used by the sensor. An identity conflict requires investigation before any records are discarded.

## Reproduction

From `raspberry-pi-gateway`, run `python -m pytest tests/test_sensors.py -q`. The tests exercise the registration server over loopback and observe durability through a second SQLite connection. Physical sensor timing, AP connectivity, and SD-card power loss require a hardware run.
