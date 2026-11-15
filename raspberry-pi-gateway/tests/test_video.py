import hashlib
import json
from pathlib import Path
import time
from gateway.storage import Store

import pytest


@pytest.fixture
def store(tmp_path):
    result = Store(tmp_path / 'state.db', min_free_bytes=0)
    result.execute("INSERT INTO missions VALUES (1,'{}',1,'COMPLETED',NULL,'2026-01-01T00:00:00Z')")
    yield result
    result.close()


class Safety:
    def __init__(self):
        self.stream = []
        self.fail_off = False

    def set_stream(self, enabled):
        self.stream.append(enabled)
        if not enabled and self.fail_off:
            raise OSError('streamoff failed')


class Process:
    stdin = None

    def poll(self):
        return None

    def wait(self, timeout=None):
        return 0

    def terminate(self):
        pass

    def kill(self):
        pass


def recorder(store, tmp_path, safety=None, **options):
    from gateway.video import VideoRecorder
    def launch(args, **kwargs):
        assert store.query('SELECT state FROM videos')[0]['state'] == 'RECORDING'
        Path(args[-1]).write_bytes(b'video-data')
        return Process()
    return VideoRecorder(store, tmp_path / 'clips', safety or Safety(), launcher=launch,
                         probe=lambda path: {'codec': 'h264', 'durationSeconds': 1.0, 'width': 960, 'height': 720},
                         min_free_bytes=0, monitor_interval=options.pop('monitor_interval', 60), **options)


def test_opt_out_does_not_create_stream_or_files(store, tmp_path):
    safety = Safety()
    rec = recorder(store, tmp_path, safety)
    assert rec.start(1) is None
    assert not (tmp_path / 'clips').exists()
    assert not safety.stream
    assert not store.query('SELECT * FROM videos')


def test_record_finalize_metadata_and_stream_shutdown(store, tmp_path):
    safety = Safety()
    rec = recorder(store, tmp_path, safety)
    media_id = rec.start(1, requested=True)
    assert rec.stop() == media_id
    row = store.query('SELECT * FROM videos')[0]
    assert row['state'] == 'FINALIZED'
    assert Path(row['path']).read_bytes() == b'video-data'
    metadata = json.loads(row['metadata'])
    assert metadata['sha256'] == hashlib.sha256(b'video-data').hexdigest()
    assert metadata['totalBytes'] == 10
    assert safety.stream == [True, False]


def test_recovery_quarantines_interrupted_file_without_upload(store, tmp_path):
    path = tmp_path / 'interrupted.partial.mp4'
    path.write_bytes(b'partial')
    store.execute('INSERT INTO videos(mission_id,path,state) VALUES(1,?,?)', (str(path), 'RECORDING'))
    assert recorder(store, tmp_path).recover() == 1
    assert store.query('SELECT state FROM videos')[0]['state'] == 'QUARANTINED'
    assert path.read_bytes() == b'partial'


def test_stream_stop_failure_keeps_finalized_file(store, tmp_path):
    safety = Safety()
    rec = recorder(store, tmp_path, safety)
    rec.start(1, requested=True)
    safety.fail_off = True
    with pytest.raises(Exception, match='stream'):
        rec.stop()
    row = store.query('SELECT * FROM videos')[0]
    assert row['state'] == 'FINALIZED'
    assert row['error']


def test_quota_blocks_capture_without_stream(store, tmp_path):
    safety = Safety()
    rec = recorder(store, tmp_path, safety, max_bytes=1)
    (tmp_path / 'clips').mkdir()
    (tmp_path / 'clips' / 'existing.mp4').write_bytes(b'full')
    with pytest.raises(Exception, match='quota'):
        rec.start(1, requested=True)
    assert not safety.stream


def media_client(url):
    from gateway.video import MediaClient
    return MediaClient(url, 'GW-1', 'unit-test-only-key')


def finalized(store, tmp_path):
    rec = recorder(store, tmp_path)
    rec.start(1, requested=True)
    rec.stop()


def test_upload_requires_explicit_contract_capability(store, tmp_path):
    from gateway.video import VideoUploader
    finalized(store, tmp_path)
    class NoNetwork:
        def request(self, *args, **kwargs):
            pytest.fail('production contract is not enabled')
    assert not VideoUploader(store, NoNetwork()).upload_next()
    assert store.query('SELECT state FROM videos')[0]['state'] == 'FINALIZED'


@pytest.mark.parametrize('drop_option', ['drop_after_init_once', 'drop_after_patch_once', 'drop_after_complete_once'])
def test_upload_resumes_after_lost_response(store, tmp_path, drop_option):
    from gateway.video import VideoUploader
    from mocks.media import MediaServer
    finalized(store, tmp_path)
    with MediaServer() as server:
        setattr(server, drop_option, True)
        uploader = VideoUploader(store, media_client(server.url), contract_enabled=True, chunk_bytes=4, clock=lambda: 100)
        assert not uploader.upload_next()
        assert store.query('SELECT state FROM videos')[0]['state'] == 'UPLOADING'
        # Reconstruct worker: session and bytes come from persisted journal/server.
        assert not uploader.upload_next()  # Retry remains deferred even after an immediate worker rerun.
        uploader = VideoUploader(store, media_client(server.url), contract_enabled=True, chunk_bytes=4, clock=lambda: 106)
        assert uploader.upload_next()
        row = store.query('SELECT * FROM videos')[0]
        assert row['state'] == 'UPLOADED' and row['uploaded_bytes'] == 10
        assert len(server.sessions) == 1
        assert bytes(server.sessions[1]['content']) == b'video-data'


def test_untrusted_offset_cannot_mark_uploaded(store, tmp_path):
    from gateway.video import VideoUploader
    finalized(store, tmp_path)
    class BadClient:
        def request(self, *args, **kwargs):
            return {'success': True, 'data': {'id': 1, 'uploadedBytes': 999999, 'uploadStatus': 'UPLOADED'}}
    assert not VideoUploader(store, BadClient(), contract_enabled=True).upload_next()
    row = store.query('SELECT * FROM videos')[0]
    assert row['state'] == 'HELD' and row['uploaded_bytes'] == 0


def test_changed_file_is_quarantined_before_network(store, tmp_path):
    from gateway.video import VideoUploader
    finalized(store, tmp_path)
    Path(store.query('SELECT path FROM videos')[0]['path']).write_bytes(b'tampered')
    assert not VideoUploader(store, None, contract_enabled=True).upload_next()
    assert store.query('SELECT state FROM videos')[0]['state'] == 'QUARANTINED'


def test_quota_monitor_stops_and_quarantines_partial_video(store, tmp_path):
    safety = Safety()
    rec = recorder(store, tmp_path, safety, max_bytes=5, monitor_interval=.01)
    rec.start(1, requested=True)
    deadline = time.monotonic() + 2
    while store.query('SELECT state FROM videos')[0]['state'] in ('RECORDING', 'FINALIZING') and time.monotonic() < deadline:
        time.sleep(.01)
    row = store.query('SELECT * FROM videos')[0]
    assert row['state'] == 'QUARANTINED'
    assert Path(row['path']).read_bytes() == b'video-data'
    assert safety.stream == [True, False]
    assert store.query('SELECT state FROM missions')[0]['state'] == 'COMPLETED'


def test_mock_rejects_conflicting_identity_offset_checksum_and_completion(store, tmp_path):
    from gateway.http import HttpError
    from mocks.media import MediaServer
    finalized(store, tmp_path)
    metadata = json.loads(store.query('SELECT metadata FROM videos')[0][0]) | {'clientMediaId': 'stable-demo'}
    root = '/api/device/missions/1/media'
    with MediaServer() as server:
        client = media_client(server.url)
        created = client.request('POST', root, metadata)['data']
        target = root + '/' + str(created['id'])
        def fails(status, method, path, body, headers=None):
            with pytest.raises(HttpError) as error:
                client.request(method, path, body, headers)
            assert error.value.status == status
        fails(409, 'POST', root, metadata | {'totalBytes': 11})
        chunk_headers = {'Upload-Offset': '0', 'X-Chunk-SHA256': hashlib.sha256(b'video-data').hexdigest()}
        fails(409, 'PATCH', target + '/content', b'video-data', chunk_headers | {'Upload-Offset': '1'})
        fails(422, 'PATCH', target + '/content', b'corruption', chunk_headers)
        fails(422, 'POST', target + '/complete', {'totalBytes': 10, 'sha256': metadata['sha256']})
        assert client.request('GET', target)['data']['uploadedBytes'] == 0


def test_upload_yields_after_chunk_budget(store, tmp_path):
    from gateway.video import VideoUploader
    from mocks.media import MediaServer
    finalized(store, tmp_path)
    with MediaServer() as server:
        uploader = VideoUploader(store, media_client(server.url), contract_enabled=True,
                                 chunk_bytes=1, chunks_per_run=2)
        assert not uploader.upload_next()
        row = store.query('SELECT * FROM videos')[0]
        assert row['state'] == 'UPLOADING' and row['uploaded_bytes'] == 2
        for _ in range(4):
            uploader.upload_next()
        assert store.query('SELECT state FROM videos')[0][0] == 'UPLOADED'
