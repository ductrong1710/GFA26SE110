"""Loopback-only example of the proposed media API; not a production service."""
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import re
import socket
import threading


class MediaServer:
    def __init__(self):
        self.sessions = {}
        self.drop_after_patch_once = False
        self.drop_after_init_once = False
        self.drop_after_complete_once = False
        self._lock = threading.Lock()
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args):
                pass

            def do_GET(self):
                self.handle_media('GET')

            def do_POST(self):
                self.handle_media('POST')

            def do_PATCH(self):
                self.handle_media('PATCH')

            def handle_media(self, method):
                try:
                    length = int(self.headers.get('Content-Length', '0'))
                    if not 0 <= length <= 8 * 1024 * 1024:
                        return self.reply(413, {'success': False})
                    body = self.rfile.read(length)
                    with owner._lock:
                        code, data, drop = owner._handle(method, self.path, body, self.headers)
                    if drop:
                        self.connection.shutdown(socket.SHUT_RDWR)
                        self.connection.close()
                        return
                    self.reply(code, data)
                except (ValueError, KeyError, TypeError):
                    self.reply(400, {'success': False, 'message': 'Invalid mock media request'})

            def reply(self, code, data):
                payload = json.dumps(data).encode()
                self.send_response(code)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

        self._server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.url = 'http://127.0.0.1:' + str(self._server.server_port)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)

    def __enter__(self):
        self._thread.start()
        return self

    def __exit__(self, *args):
        self._server.shutdown()
        self._server.server_close()
        self._thread.join(timeout=2)

    def _handle(self, method, path, body, headers):
        match = re.fullmatch(r'/api/device/missions/([1-9][0-9]*)/media(?:/([1-9][0-9]*)(?:/(content|complete))?)?', path)
        if not match:
            return 404, {'success': False}, False
        mission_id, media_id, action = match.groups()
        mission_id = int(mission_id)
        if media_id is None and method == 'POST':
            metadata = json.loads(body)
            if (not isinstance(metadata.get('clientMediaId'), str) or not 1 <= len(metadata['clientMediaId']) <= 200 or
                    type(metadata.get('totalBytes')) is not int or not 0 < metadata['totalBytes'] <= 512 * 1024 * 1024 or
                    not re.fullmatch('[0-9a-f]{64}', metadata.get('sha256', '')) or metadata.get('contentType') != 'video/mp4'):
                return 400, {'success': False}, False
            session = next((s for s in self.sessions.values() if s['missionId'] == mission_id and s['clientMediaId'] == metadata['clientMediaId']), None)
            if session:
                if session['metadata'] != metadata:
                    return 409, {'success': False}, False
            else:
                session = {**metadata, 'id': len(self.sessions) + 1, 'missionId': mission_id,
                           'uploadStatus': 'UPLOADING', 'content': bytearray(), 'metadata': metadata}
                self.sessions[session['id']] = session
            drop, self.drop_after_init_once = self.drop_after_init_once, False
            return 201, self._response(session), drop
        session = self.sessions.get(int(media_id or 0))
        if session is None or session['missionId'] != mission_id:
            return 404, {'success': False}, False
        if method == 'GET' and action is None:
            return 200, self._response(session), False
        if method == 'PATCH' and action == 'content':
            if (int(headers.get('Upload-Offset', '-1')) != len(session['content']) or
                    session['uploadStatus'] == 'UPLOADED'):
                return 409, {'success': False}, False
            if (not body or len(session['content']) + len(body) > session['totalBytes'] or
                    hashlib.sha256(body).hexdigest() != headers.get('X-Chunk-SHA256')):
                return 422, {'success': False}, False
            session['content'].extend(body)
            drop, self.drop_after_patch_once = self.drop_after_patch_once, False
            return 200, self._response(session), drop
        if method == 'POST' and action == 'complete':
            expected = json.loads(body)
            if (expected.get('sha256') != session['sha256'] or expected.get('totalBytes') != session['totalBytes'] or
                    len(session['content']) != session['totalBytes'] or
                    hashlib.sha256(session['content']).hexdigest() != session['sha256']):
                return 422, {'success': False}, False
            session['uploadStatus'] = 'UPLOADED'
            drop, self.drop_after_complete_once = self.drop_after_complete_once, False
            return 200, self._response(session), drop
        return 405, {'success': False}, False

    @staticmethod
    def _response(session):
        return {'success': True, 'data': {key: session[key] for key in
            ('id', 'missionId', 'clientMediaId', 'totalBytes', 'sha256', 'uploadStatus')} | {'uploadedBytes': len(session['content'])}}
