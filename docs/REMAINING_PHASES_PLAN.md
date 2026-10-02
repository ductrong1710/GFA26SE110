# Sequential implementation and verification

- [x] Phase 5: bounded measurement codec; atomic per-record LittleFS files;
  verified saves, pending batches, synced rename, dedup receipts; build gate.
- [x] Phase 6: bounded HTTP client; cooperative collection state machine;
  save-before-ACK, laptop mock node and fault scenarios; build gate.
- [x] Phase 7: optional STA retry without disabling AP; asynchronous NTP;
  honest connectivity/time status, optional node time push; build gate.
- [x] Phase 8: repository backend adapter, channel mapping, per-channel outcomes,
  bounded retry, mock backend and procedures; build gate.
- [x] Final clean build, tests, compatibility review, full runbook.

Final verification: esp32dev clean build SUCCESS, 40.466 seconds, 48676 bytes
static RAM, 884125 bytes flash. No compiler warning/error lines in captured log.
storage_diagnostics build SUCCESS. Ten host mock protocol tests passed.
Each phase was built successfully before starting the next phase.
No upload or phases 5–8 hardware validation performed.

Backend references: backend/src/FarmMonitoring.Application/Features/Sync and
backend/src/FarmMonitoring.Api/Controllers/SyncController.cs. Do not change backend.
Do not auto-upload. Continue sequentially without confirmation, as requested.
ACK only verified local writes/receipts. Never remove PENDING payloads.
