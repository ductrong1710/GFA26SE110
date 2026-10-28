# Local gateway configuration

Upload this firmware once from `D:\Learning\SEP\Code\ESP32` with the board connected:

```powershell
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t upload
```

Subsequent Wi-Fi/backend changes use the browser and do not require another upload.

For the current Windows hotspot setup, the computer's hotspot address is
`192.168.137.1`; use `http://192.168.137.1:5043/api` as the backend base URL.
Start the backend with `dotnet run --launch-profile device-http` so it listens
on the LAN interface. An ESP32 address such as `192.168.137.202` is the device's
address, not the backend address.

At boot, `[CONFIG] source=FIRMWARE_DEFAULTS` means compiled settings are in use.
`[CONFIG] source=SAVED_NVS backend=...` means previously saved settings override
the compiled defaults. Uploading firmware alone does not replace saved settings.
The log includes the validated backend URL and numeric gateway ID, never the API key.

Join the gateway's `UAV_GATEWAY` Wi-Fi and open `http://192.168.4.1/config`.
Sign in as `admin` using the gateway AP password configured in `Secrets.h`.
This initial shared credential is deliberately restricted to the AP interface;
the page and configuration API reject requests addressed to the Internet/STA IP.
All trusted AP users who know that password can administer configuration. HTTP
Digest avoids sending the admin password directly, but this local HTTP page does
not encrypt configuration traffic. Use a trusted gateway Wi-Fi network.

The page stores Internet SSID/password, backend base URL, gateway numeric ID,
gateway code and API key in ESP32 NVS. Secrets.h remains the first-boot source;
after a successful save the persisted configuration takes precedence. A successful
save schedules a restart; reconnect to the AP and reload afterward. Uploading
firmware with the same partition layout preserves settings and measurement files.
No filesystem upload or erase is required.
This feature does not enable flash/NVS encryption; stored credentials are not
protected against physical flash access. A corrupt saved configuration does not
fall back to compiled credentials. Collection waits until configuration is valid,
leaving uncollected records on sensors. If corrupt configuration coexists with
pending measurements or a saved mission, identity protection requires deliberate
recovery of the original configuration; do not erase those records to bypass it.

The backend URL must use `http://host[:port]/api`, with a host reachable through
Internet Wi-Fi. HTTPS is not supported by the current backend transport; use a
trusted LAN HTTP backend that does not redirect. `localhost` and loopback cannot
reach a backend on another computer. Use a different subnet from the gateway AP.
Obtain the numeric gateway ID, code and matching API key from backend provisioning.
Changing ID or code requires entering its API key again. Changing backend URL or
gateway identity is refused while pending measurements or a saved mission exist.

For legacy test records collected before first backend provisioning, the explicit
"I confirm pending records are test data" checkbox allows assigning their upload
destination to the configured gateway. It is unchecked by default and available
only with pending data, ready storage, no saved mission, and no existing API key.
This exception cannot reassign an already provisioned gateway. Original measurement
files (including their original gateway code) remain untouched; the existing sync
transport uses the newly saved gateway ID/code/key after reboot. Records are marked
synced only after backend acknowledgment. Sensor codes, channels and timestamps must
still satisfy backend validation. A failed configuration write leaves data intact.

Empty password and API-key inputs preserve their saved values. For an open Wi-Fi
network, select the explicit clear-password checkbox. The page never pre-fills
secrets, and the read API returns only `hasWifiPassword` and `hasApiKey` flags.
Saving requires a complete configuration: a nonempty SSID, backend URL, positive
gateway ID, gateway code and a provisioned API key of 32-256 bytes. A saved key can
be kept by leaving its input empty. API-key deletion is not offered by this page.
Gateway code is limited to 64 bytes to match measurement storage.
Configuration writes are blocked during flight, mission preparation/execution,
or recovery. Ground synchronization permits Wi-Fi/key repair, while the identity
restriction above still applies. Wait until safe, then reload the page.

## API and safeguards

- `GET /config`: embedded page, with no external assets.
- `GET /api/config`: nonsecret settings and the current boot's random CSRF token.
- `POST /api/config`: JSON with `internetSsid`, `internetPassword`,
  `backendBaseUrl`, `gatewayId` (integer), `gatewayCode`, `apiKey` and
  `clearWifiPassword` (boolean); requires `X-Config-Token` from the authenticated GET.
- Every route requires Digest authentication. Basic authentication is rejected.
- Responses use `Cache-Control: no-store`; framing is blocked. No cross-origin
  permission is granted. Browser scripts insert data through `value`/`textContent`.
- The transport rejects request bodies over 1024 bytes before buffering them.
  Invalid/truncated JSON, trailing data, embedded null strings and wrong field
  types are rejected. No response or log includes submitted secrets.
- Only one successful save is accepted per boot. Repeated requests during the
  restart interval cannot overwrite the committed settings.

## Automated page check

From `Code/ESP32`, run `node tests/configuration_page.test.cjs`. The isolated DOM
and fetch harness checks loading, text-safe rendering, empty secret inputs,
secret-preserving requests, the CSRF header, locked-state behavior, open Wi-Fi,
one successful save and rejected-save correction. It uses synthetic values and
does not connect to a device. It does not verify server-side authentication or NVS.

## Hardware verification (requires a safe, grounded gateway)

1. On AP Wi-Fi, opening `/config` must challenge for credentials; wrong credentials
   and Basic authentication must fail. Correct Digest login loads the form.
2. Request both routes via the STA IP: expect 403 even with valid admin credentials.
3. Inspect authenticated GET JSON: only nonsecret values, secret-presence flags and
   a CSRF token appear. Password/key inputs start empty after each load.
4. POST without or with an incorrect `X-Config-Token`: expect 403 and no changes.
   Submit form-encoded content, a body over 1024 bytes, malformed/trailing JSON,
   null/array/numeric string fields, and embedded nulls: all must fail.
5. Save valid settings. Confirm a success message before restart, then reload and
   verify nonsecret values and connectivity. Restart again to verify persistence.
6. Change only SSID with password/key blank: saved secrets must remain usable.
   Select clear password on an open test network: connection must work without a
   password. Changing gateway identity without a new API key must fail.
7. With pending measurements or a persisted mission, changing backend identity
   must fail. Do not delete real data to prepare this test.
8. While mission/flight state is unsafe, attempt a direct POST with an otherwise
   valid token: expect 409 and no NVS changes. Verify normal idle saving afterward.
9. Disconnect power after a successful save and verify persisted values after boot.
   Test interruption during writes only on a disposable setup; verify that either
   the previous or newly committed complete configuration loads.

These are acceptance checks, not a claim that hardware testing has been performed.
