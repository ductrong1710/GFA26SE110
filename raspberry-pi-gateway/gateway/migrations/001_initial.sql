CREATE TABLE nodes (
 device_code TEXT PRIMARY KEY, farm_id INTEGER NOT NULL, zone_id INTEGER NOT NULL,
 ip TEXT NOT NULL, metadata TEXT NOT NULL, last_seen REAL NOT NULL
);
CREATE TABLE missions (
 id INTEGER PRIMARY KEY, plan TEXT NOT NULL, plan_revision INTEGER NOT NULL,
 state TEXT NOT NULL, result TEXT, updated_at TEXT NOT NULL
);
CREATE TABLE waypoints (
 mission_id INTEGER NOT NULL REFERENCES missions(id), id INTEGER NOT NULL,
 sequence_no INTEGER NOT NULL, payload TEXT NOT NULL,
 PRIMARY KEY(mission_id,id), UNIQUE(mission_id,sequence_no)
);
CREATE TABLE measurements (
 record_id TEXT PRIMARY KEY, device_code TEXT NOT NULL REFERENCES nodes(device_code),
 sequence INTEGER NOT NULL, payload TEXT NOT NULL, collected_at TEXT NOT NULL,
 mission_id INTEGER REFERENCES missions(id), node_acked INTEGER NOT NULL DEFAULT 0,
 synced INTEGER NOT NULL DEFAULT 0, UNIQUE(device_code,sequence)
);
CREATE INDEX measurements_pending ON measurements(synced,mission_id);
CREATE TABLE collection_attempts (
 mission_id INTEGER NOT NULL REFERENCES missions(id), sensor_node_id INTEGER NOT NULL,
 attempt_no INTEGER NOT NULL, payload TEXT NOT NULL, synced INTEGER NOT NULL DEFAULT 0,
 PRIMARY KEY(mission_id,sensor_node_id,attempt_no)
);
CREATE TABLE telemetry (
 id INTEGER PRIMARY KEY AUTOINCREMENT, mission_id INTEGER NOT NULL REFERENCES missions(id),
 payload TEXT NOT NULL, state TEXT NOT NULL DEFAULT 'PENDING'
);
CREATE TABLE outbox (
 id INTEGER PRIMARY KEY AUTOINCREMENT, kind TEXT NOT NULL, identity TEXT NOT NULL UNIQUE,
 mission_id INTEGER REFERENCES missions(id), payload TEXT NOT NULL, members TEXT NOT NULL,
 state TEXT NOT NULL DEFAULT 'PENDING', attempts INTEGER NOT NULL DEFAULT 0,
 next_attempt REAL NOT NULL DEFAULT 0, response TEXT, error TEXT
);
CREATE TABLE videos (
 id INTEGER PRIMARY KEY AUTOINCREMENT, mission_id INTEGER NOT NULL REFERENCES missions(id),
 path TEXT NOT NULL UNIQUE, state TEXT NOT NULL, metadata TEXT NOT NULL DEFAULT '{}',
 upload_id TEXT, uploaded_bytes INTEGER NOT NULL DEFAULT 0, error TEXT
);
CREATE TABLE recovery_journal (
 id INTEGER PRIMARY KEY AUTOINCREMENT, mission_id INTEGER REFERENCES missions(id),
 state TEXT NOT NULL, details TEXT NOT NULL, created_at TEXT NOT NULL
);
