ALTER TABLE telemetry ADD COLUMN attempts INTEGER NOT NULL DEFAULT 0;
ALTER TABLE telemetry ADD COLUMN next_attempt REAL NOT NULL DEFAULT 0;
CREATE INDEX telemetry_pending ON telemetry(state,next_attempt);
CREATE INDEX outbox_pending ON outbox(kind,state,next_attempt);
