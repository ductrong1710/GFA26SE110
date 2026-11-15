"""Durable local state. A successful transaction exits only after SQLite commits."""

from contextlib import contextmanager
from pathlib import Path
import shutil
import sqlite3
import threading


class Store:
    def __init__(self, path, max_bytes=256 * 1024**2, min_free_bytes=64 * 1024**2):
        self.path = Path(path).resolve()
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.max_bytes = max_bytes
        self.min_free_bytes = min_free_bytes
        self._lock = threading.RLock()
        self._db = sqlite3.connect(self.path, timeout=5, check_same_thread=False,
                                   isolation_level=None)
        self._db.row_factory = sqlite3.Row
        try:
            self._db.execute('PRAGMA busy_timeout=5000')
            self._db.execute('PRAGMA foreign_keys=ON')
            version = self._db.execute('PRAGMA user_version').fetchone()[0]
            if version > 2:
                raise RuntimeError('database schema is newer than this gateway')
            self._db.execute('PRAGMA journal_mode=WAL')
            self._db.execute('PRAGMA synchronous=FULL')
            self._db.execute('PRAGMA wal_autocheckpoint=256')
            if version == 0:
                sql = (Path(__file__).parent/'migrations'/'001_initial.sql').read_text('utf-8')
                self._db.executescript('BEGIN IMMEDIATE;\n' + sql + '\nPRAGMA user_version=1;\nCOMMIT;')
            if version < 2:
                sql = (Path(__file__).parent/'migrations'/'002_telemetry_retries.sql').read_text('utf-8')
                self._db.executescript('BEGIN IMMEDIATE;\n' + sql + '\nPRAGMA user_version=2;\nCOMMIT;')
            if self._db.execute('PRAGMA quick_check').fetchone()[0] != 'ok':
                raise RuntimeError('SQLite integrity check failed; restore verified backup')
        except BaseException:
            self._db.close()
            raise

    @contextmanager
    def transaction(self):
        with self._lock:
            self._db.execute('BEGIN IMMEDIATE')
            try:
                yield self._db
                self._db.execute('COMMIT')
            except BaseException:
                if self._db.in_transaction:
                    self._db.execute('ROLLBACK')
                raise

    def execute(self, sql, params=()):
        with self.transaction() as db:
            return db.execute(sql, params).lastrowid

    def query(self, sql, params=()):
        with self._lock:
            return self._db.execute(sql, params).fetchall()

    def check_space(self, ingestion=True):
        used = sum(p.stat().st_size for p in (self.path, Path(str(self.path)+'-wal'),
                                             Path(str(self.path)+'-shm')) if p.exists())
        reserve = self.min_free_bytes if ingestion else min(self.min_free_bytes,4*1024**2)
        if (ingestion and used >= self.max_bytes) or shutil.disk_usage(self.path.parent).free < reserve:
            raise OSError('gateway storage quota or free-space reserve exhausted')

    def backup(self, path):
        target = Path(path).resolve()
        if target == self.path or target.exists():
            raise ValueError('backup must use a new file outside the active database')
        target.parent.mkdir(parents=True, exist_ok=True)
        with self._lock, sqlite3.connect(target) as dest:
            self._db.backup(dest)
            if dest.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise RuntimeError('backup integrity check failed')

    def close(self):
        with self._lock:
            self._db.close()
