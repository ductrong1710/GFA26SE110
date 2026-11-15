from http.server import BaseHTTPRequestHandler, HTTPServer
import threading
import time

import pytest

from gateway.http import HttpClient, HttpError


def test_http_does_not_follow_redirect_or_accept_unbounded_body():
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path == '/redirect':
                self.send_response(302)
                self.send_header('Location','http://127.0.0.1:1/secrets')
                self.end_headers()
            else:
                self.send_response(200)
                self.send_header('Content-Length','99999')
                self.end_headers()
        def log_message(self,*args):
            pass
    server = HTTPServer(('127.0.0.1',0),Handler)
    thread = threading.Thread(target=server.serve_forever,daemon=True)
    thread.start()
    try:
        client = HttpClient(max_response=100)
        with pytest.raises(HttpError) as error:
            client.request('GET',f'http://127.0.0.1:{server.server_port}/redirect')
        assert error.value.status == 302
        with pytest.raises(ValueError):
            client.request('GET',f'http://127.0.0.1:{server.server_port}/big')
    finally:
        server.shutdown()
        server.server_close()
        thread.join()


def test_truncated_response_becomes_retryable_transport_error():
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            self.send_response(200)
            self.send_header('Content-Length','30')
            self.end_headers()
            self.wfile.write(b'{')
        def log_message(self,*args):
            pass
    server = HTTPServer(('127.0.0.1',0),Handler)
    thread = threading.Thread(target=server.serve_forever,daemon=True)
    thread.start()
    try:
        with pytest.raises((OSError,ValueError)):
            HttpClient().request('GET',f'http://127.0.0.1:{server.server_port}/')
    finally:
        server.shutdown()
        server.server_close()
        thread.join()


def test_slow_drip_cannot_extend_total_http_deadline():
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            self.send_response(200)
            self.send_header('Content-Length','100')
            self.end_headers()
            try:
                for _ in range(100):
                    self.wfile.write(b' ')
                    self.wfile.flush()
                    time.sleep(.03)
            except OSError:
                pass
        def log_message(self,*args):
            pass
    server = HTTPServer(('127.0.0.1',0),Handler)
    thread = threading.Thread(target=server.serve_forever,daemon=True)
    thread.start()
    try:
        started = time.monotonic()
        with pytest.raises((OSError,ValueError)):
            HttpClient(timeout=.15).request('GET',f'http://127.0.0.1:{server.server_port}/')
        assert time.monotonic()-started < .8
    finally:
        server.shutdown()
        server.server_close()
        thread.join()
