import sqlite3
import subprocess
import sys
from pathlib import Path

import pytest

from gateway.storage import Store


def test_transaction_rollback_and_reopen(tmp_path):
    path = tmp_path / 'gateway.db'
    store = Store(path, min_free_bytes=0)
    assert store.query('PRAGMA journal_mode')[0][0] == 'wal'
    assert store.query('PRAGMA synchronous')[0][0] == 2
    assert store.query('PRAGMA foreign_keys')[0][0] == 1
    with pytest.raises(RuntimeError):
        with store.transaction() as db:
            db.execute("INSERT INTO nodes VALUES ('n',1,1,'192.168.4.2','{}',0)")
            raise RuntimeError('power lost before commit')
    store.close()
    store = Store(path, min_free_bytes=0)
    assert not store.query('SELECT * FROM nodes')
    store.execute("INSERT INTO nodes VALUES ('n',1,1,'192.168.4.2','{}',0)")
    store.close()
    store = Store(path, min_free_bytes=0)
    assert store.query('SELECT device_code FROM nodes')[0][0] == 'n'
    store.close()


def test_foreign_keys_and_backup(tmp_path):
    store = Store(tmp_path/'gateway.db', min_free_bytes=0)
    with pytest.raises(sqlite3.IntegrityError):
        store.execute("INSERT INTO waypoints VALUES (99,1,1,'{}')")
    store.execute("INSERT INTO nodes VALUES ('n',1,1,'192.168.4.2','{}',0)")
    store.backup(tmp_path/'backup.db')
    with sqlite3.connect(tmp_path/'backup.db') as db:
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert db.execute('SELECT COUNT(*) FROM nodes').fetchone()[0] == 1
    store.close()


def test_quota_prevents_new_writes_without_deleting_records(tmp_path):
    store = Store(tmp_path/'gateway.db', min_free_bytes=0)
    store.execute("INSERT INTO nodes VALUES ('n',1,1,'192.168.4.2','{}',0)")
    store.max_bytes = 1
    with pytest.raises(OSError):
        store.check_space()
    assert len(store.query('SELECT * FROM nodes')) == 1
    store.close()


def test_future_schema_is_rejected(tmp_path):
    path = tmp_path/'future.db'
    with sqlite3.connect(path) as db:
        db.execute('PRAGMA user_version=999')
    with pytest.raises(RuntimeError, match='newer'):
        Store(path, min_free_bytes=0)


@pytest.mark.parametrize('commit',[False, True])
def test_abrupt_process_exit_preserves_only_committed_records(tmp_path, commit):
    path = tmp_path/'crash.db'
    script = '''
import os,sys
from gateway.storage import Store
s=Store(sys.argv[1],min_free_bytes=0)
with s.transaction() as db:
    db.execute("INSERT INTO nodes VALUES ('n',1,1,'192.168.4.2','{}',0)")
    if sys.argv[2]=='False': os._exit(23)
os._exit(23)
'''
    result = subprocess.run([sys.executable,'-c',script,str(path),str(commit)],timeout=10)
    assert result.returncode == 23
    store = Store(path,min_free_bytes=0)
    try:
        assert len(store.query('SELECT * FROM nodes')) == int(commit)
        assert store.query('PRAGMA integrity_check')[0][0] == 'ok'
    finally:
        store.close()


def test_version_one_upgrade_preserves_pending_telemetry(tmp_path):
    path = tmp_path/'old.db'
    migration = Path(__file__).parents[1]/'gateway/migrations/001_initial.sql'
    with sqlite3.connect(path) as db:
        db.executescript(migration.read_text())
        db.execute('PRAGMA user_version=1')
        db.execute("INSERT INTO missions VALUES (1,'{}',1,'RESULT_PENDING',NULL,'2026-01-01')")
        db.execute("INSERT INTO telemetry(mission_id,payload) VALUES (1,'{\"missionId\":1}')")
    store = Store(path,min_free_bytes=0)
    try:
        assert store.query('PRAGMA user_version')[0][0] == 2
        row = store.query('SELECT * FROM telemetry')[0]
        assert row['payload'] == '{"missionId":1}' and row['state'] == 'PENDING'
        assert row['attempts'] == row['next_attempt'] == 0
    finally:
        store.close()
