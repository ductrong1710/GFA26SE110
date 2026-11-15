# Mission video and resumable upload

**Gateway implementation and proposed Backend/Web API.** The audited `origin/BackEnd` at `4f4f80d`, `origin/FrontEnd` at `ebf7604` and `origin/fe` at `49051b6` lack mission video support. Their mission note attachments accept PNG/JPEG. This gateway exercises a separate media contract against `mocks/media.py`, a loopback-only HTTP service that holds fixtures in memory.

## Capture on the Pi

A proposed `recordVideo` boolean on the human and device mission DTOs defaults to false when absent. An enabled mission calls `VideoRecorder.start(mission_id, requested=True)` with the single flight-safety owner. SQLite records intent before `streamon` or FFmpeg launch. FFmpeg listens for the Tello H.264 stream on UDP 11111, copies the compressed stream into a fragmented MP4 and uses a `.partial.mp4` filename. For hardware deployment select the Tello-facing local IP and restrict inbound UDP 11111 to the aircraft. Install both `ffmpeg` and `ffprobe` on the Pi. Keep camera start, capture failure and finalization independent from flight safety and mission outcomes.

On normal stop the recorder closes the capture process, calls `streamoff`, inspects H.264 codec, dimensions and duration, flushes the file, calculates SHA-256 and atomically renames it. On Linux it also flushes the directory before committing `FINALIZED`. The monitor checks video directory size, free-space reserve, database space and recorder process health. An interruption or quota stop sets `QUARANTINED` and retains the partial file for manual inspection. Startup `recover()` also reconciles a crash between rename and database commit. Local retention includes confirmed uploads. An operator must free space by archiving verified clips. Recording stops when the quota is reached.

## Proposed transport

The media worker has its own queue in `videos`. It requires `contract_enabled=True`. Production requires a matching implementation on the configured HTTPS Backend origin. The proposed API uses the existing `X-Gateway-Code` and `X-Api-Key` scheme, authorizes the gateway assigned to each mission, and returns the existing `success`/`data` JSON envelope. Use an integer server media ID. Treat the client identifier as an idempotency key scoped to gateway and mission.

| Request | Body or headers | Successful result |
|---|---|---|
| `POST /api/device/missions/{missionId}/media` | JSON `clientMediaId`, `contentType`, `startedAt`, `endedAt`, `durationSeconds`, `width`, `height`, `codec`, `totalBytes`, `sha256` | Create a session or return the existing one for an identical manifest. Conflicting immutable metadata returns 409. |
| `GET /api/device/missions/{missionId}/media/{id}` | None | Return authoritative session state and durable byte offset. |
| `PATCH /api/device/missions/{missionId}/media/{id}/content` | Binary data, `Content-Type: application/octet-stream`, decimal `Upload-Offset`, lowercase hex `X-Chunk-SHA256` | Verify checksum, append at the requested offset, persist and acknowledge the updated offset. Wrong offset returns 409. Oversize or checksum failure returns 422. |
| `POST /api/device/missions/{missionId}/media/{id}/complete` | JSON `totalBytes`, `sha256` | Verify the stored file's size and SHA-256, then mark `UPLOADED`. Repeating verified completion succeeds. |

All responses echo `id`, `missionId`, `clientMediaId`, `sha256`, `totalBytes`, `uploadedBytes` and `uploadStatus` (`UPLOADING` or `UPLOADED`). The gateway validates them on each request. It advances local byte progress only after a matching acknowledgment. If a receipt is lost it reuses the same session or creation key and fetches the server's offset before reading the next chunk. Whole-file hashing uses bounded memory. Files remain available locally after confirmation.

## Backend and Web work required for production

1. Add an unchecked recording option to the mission wizard and include the optional field in mission create, edit, read and device-download DTOs. Include changes in plan revision tracking. Existing plans continue with recording disabled.
2. Persist media metadata and resumable sessions, store binary files separately, and advertise the supported contract to the operator. Report capture failure separately from upload progress and sensor collection success. Apply upload size limits and verify actual media before exposing it.
3. Add mission-scoped human metadata reads and authenticated byte-range playback for completed uploads. Reuse mission ownership and farm access checks. A playback URL must retain that authorization or use a narrowly scoped, short-lived signed URL. Expose device upload status and progress in the mission detail page.

`tests/test_video.py` exercises a local SQLite database, a fake capture process and the HTTP mock. Physical Tello capture, camera timing, SD write durability and deployed Backend/Web playback require separate acceptance checks.
