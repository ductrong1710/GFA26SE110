# LittleFS first-use initialization

The firmware keeps `LittleFSConfig::setAutoFormat(false)`. If mount fails,
it checks the linker-defined filesystem range against physical flash capacity
and reads every byte of that range, yielding between 256-byte blocks.
Only an entirely erased (`0xFF`) partition may be formatted and mounted.
A nonblank partition, invalid range, or failed read is never formatted.
Existing measurements and sequence metadata must be recovered manually if
their filesystem is damaged. Do not use `erase` or `uploadfs` to fix that case.

On a fresh board the expected messages are:

```text
[NODE] LittleFS partition is entirely blank; initializing first-use filesystem
[NODE] LittleFS mounted
[STORAGE] Recovered 0 pending records
```

Later boots should report `LittleFS mounted` and the recovered pending count,
without the initialization message. Directory creation failures are reported
separately from mount failures.

Firmware upload preserves the filesystem partition with the current 4 MB /
1 MB filesystem linker layout. Changing that layout requires a data migration.
An interrupted first-use format can leave a nonblank, unmountable partition;
the safe policy is to stop and preserve it rather than format it again.

## COM3 verification — 2026-10-02

- ESP8266EX, physical flash 4 MB; linker filesystem range `0x300000..0x3FA000`.
- Before the fix, hardware printed `LittleFS mount failed`. A complete readback
  confirmed all 1,024,000 filesystem bytes were `0xFF`: no filesystem had been
  initialized. Backup: ignored `build-logs/littlefs-before-fix-20261002.bin`.
- All eight host test executables passed, including blank/nonblank partition,
  invalid range and flash read failure cases.
- PlatformIO `nodemcuv2_physical` build and COM3 firmware upload passed; upload
  verified the flash hash. Static RAM 34,336 bytes; application flash 362,415 bytes.
- Serial after upload: `LittleFS mounted`, then `Saved SENSOR-001-1`.
- Serial after reset: `LittleFS mounted`, `Recovered 1 pending records`.
- DHT11 returned temperature/humidity. Soil ADC was reported invalid and stored
  as null. Gateway was unavailable; gateway registration/collection/ACK were
  not validated in this check.
- Local serial/upload logs are in ignored `build-logs/littlefs-*.log`.

Large serial flash reads failed intermittently, so the backup was completed in
small chunks. A transient Windows file-access error interrupted the first upload
attempt before flashing; the subsequent PlatformIO upload completed successfully.
