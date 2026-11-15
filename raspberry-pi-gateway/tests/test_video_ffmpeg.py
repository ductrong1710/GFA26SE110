"""Optional real FFmpeg test: synthetic H.264 stays on loopback; no aircraft I/O."""
import hashlib
import json
from pathlib import Path
import shutil
import socket
import subprocess
import time

import pytest

from gateway.storage import Store
from gateway.video import MediaClient, VideoRecorder, VideoUploader
from mocks.media import MediaServer


@pytest.mark.skipif(not all(shutil.which(name) for name in ('ffmpeg','ffprobe')),
                    reason='FFmpeg and ffprobe are required for synthetic capture')
def test_real_h264_udp_capture_finalize_and_upload(tmp_path):
    with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as port:
        port.bind(('127.0.0.1',0))
        address = f'udp://127.0.0.1:{port.getsockname()[1]}'
    stream_commands = []
    class Safety:
        def set_stream(self, enabled):
            stream_commands.append(enabled)
    store = Store(tmp_path/'state.db',min_free_bytes=0)
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'COMPLETED',NULL,'2026-01-01')")
    capture = VideoRecorder(store,tmp_path/'video',Safety(),input_url=address,
                            min_free_bytes=0)
    sender = None
    try:
        capture.start(1,requested=True)
        sender = subprocess.Popen(['ffmpeg','-hide_banner','-loglevel','error','-re',
            '-f','lavfi','-i','testsrc2=size=320x240:rate=30','-an',
            '-c:v','libx264','-preset','ultrafast','-tune','zerolatency','-g','30',
            '-f','h264',address+'?pkt_size=1316'],stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        path = Path(store.query('SELECT path FROM videos')[0][0])
        deadline = time.monotonic()+25
        while (not path.exists() or path.stat().st_size < 1024) and time.monotonic()<deadline:
            assert sender.poll() is None
            time.sleep(.1)
        assert path.exists() and path.stat().st_size >= 1024, 'no H.264 received'
        capture.stop()
        row = store.query('SELECT * FROM videos')[0]
        assert row['state'] == 'FINALIZED', dict(row)
        content = Path(row['path']).read_bytes()
        metadata = json.loads(row['metadata'])
        assert (metadata['codec'],metadata['width'],metadata['height']) == ('h264',320,240)
        assert metadata['durationSeconds'] > 0
        assert metadata['sha256'] == hashlib.sha256(content).hexdigest()
        assert stream_commands == [True,False]
        with MediaServer() as server:
            uploader = VideoUploader(store,MediaClient(server.url,'GW-1','test-only'),
                                     contract_enabled=True)
            for _ in range(20):
                if uploader.upload_next():
                    break
            assert store.query('SELECT state FROM videos')[0][0] == 'UPLOADED'
            assert bytes(server.sessions[1]['content']) == content
    finally:
        try:
            capture.stop()
        finally:
            if sender is not None:
                sender.terminate()
                sender.wait(timeout=5)
            store.close()
