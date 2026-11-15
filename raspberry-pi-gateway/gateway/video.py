"""Optional local capture and an explicitly gated, proposed media contract."""
import hashlib
import json
import math
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import threading
import time
from datetime import datetime, timezone
from urllib.parse import urlsplit

from .http import HttpClient, HttpError


class VideoError(RuntimeError):
    pass


def _utc():
    return datetime.now(timezone.utc).isoformat()


def _digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def probe_video(path):
    result = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0',
                             '-show_entries', 'stream=codec_name,width,height:format=duration',
                             '-of', 'json', str(path)], capture_output=True, timeout=30, check=True)
    data = json.loads(result.stdout)
    stream = data['streams'][0]
    return {'codec': stream['codec_name'], 'width': stream['width'], 'height': stream['height'],
            'durationSeconds': float(data['format']['duration'])}


class VideoRecorder:
    def __init__(self, store, directory, safety, *, launcher=subprocess.Popen, probe=probe_video,
                 max_bytes=512 * 1024 * 1024, min_free_bytes=128 * 1024 * 1024,
                 input_url='udp://0.0.0.0:11111?fifo_size=4096&overrun_nonfatal=1', monitor_interval=1):
        if max_bytes <= 0 or min_free_bytes < 0 or monitor_interval <= 0:
            raise ValueError('Invalid video storage limits')
        self.store, self.directory, self.safety = store, Path(directory).resolve(), safety
        self.launcher, self.probe = launcher, probe
        self.max_bytes, self.min_free_bytes = max_bytes, min_free_bytes
        self.input_url, self.monitor_interval = input_url, monitor_interval
        self._id, self._process = None, None
        self._lock = threading.RLock()
        self._finished = threading.Event()

    def check_quota(self):
        self.store.check_space()
        used = sum(p.stat().st_size for p in self.directory.iterdir() if p.is_file()) if self.directory.exists() else 0
        parent = self.directory if self.directory.exists() else self.directory.parent
        if used >= self.max_bytes or shutil.disk_usage(parent).free < self.min_free_bytes:
            raise VideoError('Video storage quota exhausted')
        return True

    def start(self, mission_id, requested=False):
        if requested is not True:
            return None
        if type(mission_id) is not int or mission_id <= 0:
            raise ValueError('Invalid mission identity')
        with self._lock:
            if self._id is not None:
                raise VideoError('A recording is already active')
            self.directory.mkdir(parents=True, exist_ok=True)
            self.check_quota()
            metadata = {'startedAt': _utc(), 'contentType': 'video/mp4'}
            path = self.directory / f'{mission_id}-{secrets.token_hex(12)}.partial.mp4'
            media_id = self.store.execute('INSERT INTO videos(mission_id,state,metadata,path) VALUES(?,?,?,?)',
                                          (mission_id, 'RECORDING', json.dumps(metadata), str(path)))
            self._id = media_id
            try:
                self.safety.set_stream(True)
                self._process = self.launcher(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-n',
                    '-f', 'h264', '-i', self.input_url, '-map', '0:v:0', '-c:v', 'copy',
                    '-movflags', '+frag_keyframe+empty_moov', '-f', 'mp4', str(path)],
                    stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            except Exception:
                self.store.execute('UPDATE videos SET state=?,error=? WHERE id=?',
                                   ('QUARANTINED', 'Capture startup failed', media_id))
                self._id = None
                try:
                    self.safety.set_stream(False)
                except Exception:
                    pass
                raise
            self._finished = threading.Event()
            threading.Thread(target=self._monitor, args=(self._finished,), daemon=True, name='video-quota').start()
            return media_id

    def _monitor(self, finished):
        while not finished.wait(self.monitor_interval):
            with self._lock:
                if finished.is_set() or self._finished is not finished:
                    return
                try:
                    self.check_quota()
                    if self._process.poll() is not None:
                        raise VideoError('Capture process exited unexpectedly')
                except Exception:
                    try:
                        self.stop(reason='Capture interrupted by storage/process monitor')
                    except Exception:
                        pass  # stop preserves the artifact and records the capture failure.
                    return

    def _finalize(self, path, metadata):
        if not path.is_file() or path.stat().st_size == 0:
            raise VideoError('Capture contains no video')
        details = self.probe(path)
        duration = details.get('durationSeconds')
        if (details.get('codec') != 'h264' or type(duration) not in (float, int)
                or not math.isfinite(duration) or duration <= 0):
            raise VideoError('Video integrity check failed')
        if any(type(details.get(k)) is not int or details[k] <= 0 for k in ('width', 'height')):
            raise VideoError('Video dimensions are invalid')
        with path.open('r+b') as source:
            os.fsync(source.fileno())
        metadata.update(details, totalBytes=path.stat().st_size, sha256=_digest(path))
        final_path = path.with_name(path.name.replace('.partial.mp4', '.mp4'))
        path.replace(final_path)
        if os.name != 'nt':
            directory_fd = os.open(self.directory, os.O_RDONLY)
            try:
                os.fsync(directory_fd)
            finally:
                os.close(directory_fd)
        return final_path

    def stop(self, *, reason=None):
        with self._lock:
            if self._id is None:
                return None
            media_id, process = self._id, self._process
            self._finished.set()
            failure, stream_failure = None, None
            try:
                self.store.execute('UPDATE videos SET state=? WHERE id=?', ('FINALIZING', media_id))
                if process.stdin is not None:
                    process.stdin.write(b'q\n')
                    process.stdin.flush()
                else:
                    process.terminate()
                if process.wait(timeout=10) != 0:
                    raise VideoError('Capture process failed')
            except Exception as exc:
                failure = exc
                if process.poll() is None:
                    process.kill()
                    process.wait(timeout=5)
            finally:
                try:
                    self.safety.set_stream(False)
                except Exception as exc:
                    stream_failure = exc
                self._id, self._process = None, None
                if process.stdin is not None:
                    try:
                        process.stdin.close()
                    except OSError:
                        pass
            row = self.store.query('SELECT * FROM videos WHERE id=?', (media_id,))[0]
            path = Path(row['path'])
            metadata = json.loads(row['metadata'])
            metadata['endedAt'] = _utc()
            if not failure and not reason:
                try:
                    path = self._finalize(path, metadata)
                except Exception as exc:
                    failure = exc
            state = 'QUARANTINED' if failure or reason else 'FINALIZED'
            error = reason or ('Capture finalization failed' if failure else None)
            if stream_failure:
                error = (error + '. ' if error else '') + 'Video stream shutdown failed'
            if not path.exists():
                renamed = path.with_name(path.name.replace('.partial.mp4', '.mp4'))
                if renamed.exists():
                    path = renamed
            self.store.execute('UPDATE videos SET state=?,path=?,metadata=?,error=? WHERE id=?',
                               (state, str(path), json.dumps(metadata), error, media_id))
            if failure or stream_failure:
                raise VideoError(error) from (failure or stream_failure)
            return media_id

    def recover(self):
        with self._lock:
            if self._id is not None:
                raise VideoError('Cannot recover while recording')
            rows = self.store.query("SELECT id,path FROM videos WHERE state IN ('RECORDING','FINALIZING')")
            for row in rows:
                path = Path(row['path'])
                renamed = path.with_name(path.name.replace('.partial.mp4', '.mp4'))
                retained = renamed if not path.exists() and renamed.exists() else path
                self.store.execute('UPDATE videos SET state=?,path=?,error=? WHERE id=?',
                                   ('QUARANTINED', str(retained), 'Interrupted capture; manual inspection required', row['id']))
            return len(rows)


class MediaClient:
    """Same-origin, device-authenticated adapter for the proposed binary upload endpoint."""
    def __init__(self, base_url, gateway_code, api_key, http=None):
        origin = urlsplit(base_url)
        if (not origin.hostname or origin.username or origin.password or origin.path not in ('', '/') or
                origin.query or origin.fragment or not (origin.scheme == 'https' or
                (origin.scheme == 'http' and origin.hostname in ('127.0.0.1', '::1')))):
            raise ValueError('Media origin must use HTTPS or loopback HTTP')
        self.base_url = base_url.rstrip('/')
        self.credentials = {'X-Gateway-Code': gateway_code, 'X-Api-Key': api_key}
        self.http = http or HttpClient()

    def request(self, method, path, body=None, headers=None):
        if not re.fullmatch(r'/api/device/missions/[1-9][0-9]*/media(?:/[1-9][0-9]*(?:/(?:content|complete))?)?', path):
            raise ValueError('Invalid media path')
        return self.http.request(method, self.base_url + path, headers={**(headers or {}), **self.credentials}, body=body)


class VideoUploader:
    def __init__(self, store, client, *, contract_enabled=False, chunk_bytes=256 * 1024, clock=time.time, chunks_per_run=4):
        if type(chunk_bytes) is not int or not 1 <= chunk_bytes <= 8 * 1024 * 1024:
            raise ValueError('Invalid media chunk size')
        self.store, self.client = store, client
        self.contract_enabled, self.chunk_bytes = contract_enabled is True, chunk_bytes
        self.clock = clock
        if type(chunks_per_run) is not int or not 1 <= chunks_per_run <= 16:
            raise ValueError('Invalid upload work budget')
        self.chunks_per_run = chunks_per_run
        self._lock = threading.Lock()

    def _request(self, method, path, body=None, headers=None):
        response = self.client.request(method, path, body=body, headers=headers)
        if not isinstance(response, dict) or response.get('success') is not True or not isinstance(response.get('data'), dict):
            raise VideoError('Invalid media response envelope')
        return response['data']

    @staticmethod
    def _validate(data, row, metadata, client_id, session=None):
        media_id, offset = data.get('id'), data.get('uploadedBytes')
        if type(media_id) is not int or media_id <= 0 or (session is not None and str(media_id) != str(session)):
            raise VideoError('Invalid media session identity')
        if (type(data.get('missionId')) is not int or data['missionId'] != row['mission_id'] or
                data.get('clientMediaId') != client_id or data.get('sha256') != metadata['sha256'] or
                type(data.get('totalBytes')) is not int or data['totalBytes'] != metadata['totalBytes']):
            raise VideoError('Media response identity/content mismatch')
        if type(offset) is not int or not 0 <= offset <= metadata['totalBytes']:
            raise VideoError('Invalid media upload offset')
        if data.get('uploadStatus') not in ('UPLOADING', 'UPLOADED'):
            raise VideoError('Invalid media upload state')
        if data['uploadStatus'] == 'UPLOADED' and offset != metadata['totalBytes']:
            raise VideoError('Incomplete media marked uploaded')
        return offset

    def upload_next(self):
        if not self.contract_enabled:
            return False
        with self._lock:
            rows = self.store.query("SELECT * FROM videos WHERE state IN ('FINALIZED','UPLOADING') "
                                    "AND COALESCE(json_extract(metadata,'$._retry.next'),0)<=? ORDER BY id LIMIT 1", (self.clock(),))
            if not rows:
                return False
            row = rows[0]
            try:
                metadata, path = json.loads(row['metadata']), Path(row['path'])
                retry = metadata.pop('_retry', {})
                if (path.stat().st_size != metadata['totalBytes'] or
                        row['state'] == 'FINALIZED' and _digest(path) != metadata['sha256']):
                    raise VideoError('Finalized video content changed')
            except Exception:
                self.store.execute('UPDATE videos SET state=?,error=? WHERE id=?',
                                   ('QUARANTINED', 'Finalized video is missing or changed', row['id']))
                return False
            client_id = f"gateway-video-{row['id']}-{metadata['sha256']}"
            root = f"/api/device/missions/{row['mission_id']}/media"
            try:
                self.store.execute('UPDATE videos SET state=? WHERE id=?', ('UPLOADING', row['id']))
                session = row['upload_id']
                if session is None:
                    data = self._request('POST', root, {**metadata, 'clientMediaId': client_id})
                    self._validate(data, row, metadata, client_id)
                    session = str(data['id'])
                    self.store.execute('UPDATE videos SET upload_id=? WHERE id=?', (session, row['id']))
                elif not str(session).isdigit() or int(session) <= 0:
                    raise VideoError('Invalid persisted upload session')
                target = root + '/' + str(session)
                data = self._request('GET', target)
                offset = self._validate(data, row, metadata, client_id, session)
                self.store.execute('UPDATE videos SET uploaded_bytes=? WHERE id=?', (offset, row['id']))
                with path.open('rb') as source:
                    source.seek(offset)
                    for _ in range(self.chunks_per_run):
                        if offset == metadata['totalBytes']:
                            break
                        chunk = source.read(min(self.chunk_bytes, metadata['totalBytes'] - offset))
                        if not chunk:
                            raise VideoError('Video shortened during upload')
                        data = self._request('PATCH', target + '/content', chunk, {
                            'Content-Type': 'application/octet-stream', 'Upload-Offset': str(offset),
                            'X-Chunk-SHA256': hashlib.sha256(chunk).hexdigest()})
                        confirmed = self._validate(data, row, metadata, client_id, session)
                        if confirmed != offset + len(chunk):
                            raise VideoError('Media chunk acknowledgment mismatch')
                        offset = confirmed
                        self.store.execute('UPDATE videos SET uploaded_bytes=? WHERE id=?', (offset, row['id']))
                if offset < metadata['totalBytes']:
                    return False
                data = self._request('POST', target + '/complete', {'totalBytes': metadata['totalBytes'], 'sha256': metadata['sha256']})
                self._validate(data, row, metadata, client_id, session)
                if data['uploadStatus'] != 'UPLOADED':
                    raise VideoError('Media completion not confirmed')
                self.store.execute('UPDATE videos SET state=?,metadata=?,error=NULL WHERE id=?',
                                   ('UPLOADED', json.dumps(metadata), row['id']))
                return True
            except Exception as exc:
                status = exc.status if isinstance(exc, HttpError) else None
                held = isinstance(exc, (ValueError, VideoError)) or status in (400, 401, 403, 404, 405, 413, 415, 422)
                if not held:
                    attempts = retry.get('attempts', 0) + 1
                    delay = min(300, 5 * 2 ** min(attempts - 1, 6))
                    if isinstance(exc, HttpError) and exc.retry_after is not None:
                        delay = max(delay, min(exc.retry_after, 3600))
                    metadata['_retry'] = {'attempts': attempts, 'next': self.clock() + delay}
                error = 'Upload held for operator review' if held else 'Upload unconfirmed; retry will reconcile server progress'
                if status:
                    error += f' (HTTP {status})'
                self.store.execute('UPDATE videos SET state=?,error=?,metadata=? WHERE id=?',
                                   ('HELD' if held else 'UPLOADING', error, json.dumps(metadata), row['id']))
                return False
