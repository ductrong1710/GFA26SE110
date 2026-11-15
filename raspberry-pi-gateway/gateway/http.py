"""Bounded HTTP without proxy inheritance, redirects, or hidden retries."""

import http.client
import json
import ssl
import socket
import threading
from urllib.parse import urlsplit


class HttpError(OSError):
    def __init__(self, status, retry_after=None, code=None):
        self.code = code
        self.status = status
        self.retry_after = retry_after
        super().__init__(f'HTTP {status}')


class UnsentError(OSError):
    """Connection/routing failed before sending any HTTP request bytes."""


class HttpClient:
    def __init__(self, timeout=3, max_response=256 * 1024, source_address=None):
        self.timeout = timeout
        self.max_response = max_response
        self.source_address = source_address

    def request(self, method, url, headers=None, body=None):
        parsed = urlsplit(url)
        if parsed.scheme not in ('http','https') or not parsed.hostname or parsed.username or parsed.password or parsed.fragment:
            raise ValueError('invalid HTTP URL')
        options = {'timeout':self.timeout}
        if self.source_address:
            options['source_address'] = (self.source_address,0)
        connection_type = http.client.HTTPSConnection if parsed.scheme == 'https' else http.client.HTTPConnection
        if parsed.scheme == 'https':
            options['context'] = ssl.create_default_context()
        connection = connection_type(parsed.hostname, parsed.port, **options)
        request_headers = dict(headers or {})
        if body is not None and not isinstance(body, bytes):
            body = json.dumps(body,allow_nan=False,separators=(',',':')).encode('utf-8')
            request_headers.setdefault('Content-Type','application/json')
        request_headers.setdefault('Connection','close')
        expired = threading.Event()
        connected_socket = None

        def expire():
            expired.set()
            sock = connected_socket or connection.sock
            if sock is not None:
                try:
                    sock.shutdown(socket.SHUT_RDWR)
                except OSError:
                    pass

        timer = threading.Timer(self.timeout, expire)
        timer.daemon = True
        timer.start()
        try:
            try:
                connection.connect()
            except OSError as exc:
                raise UnsentError('HTTP connection unavailable') from exc
            connected_socket = connection.sock
            if expired.is_set():
                raise UnsentError('HTTP connection deadline expired')
            path = parsed.path or '/'
            if parsed.query:
                path += '?' + parsed.query
            connection.request(method, path, body, request_headers)
            response = connection.getresponse()
            if not 200 <= response.status < 300:
                retry = response.getheader('Retry-After')
                code = None
                if response.status == 409:
                    try:
                        raw = response.read(4097)
                        error = json.loads(raw) if len(raw) <= 4096 else None
                        if isinstance(error, dict) and error.get('message') == 'PLAN_CHANGED_DOWNLOAD_AGAIN':
                            code = 'PLAN_CHANGED_DOWNLOAD_AGAIN'
                    except (ValueError, OSError, http.client.HTTPException):
                        pass
                raise HttpError(response.status, int(retry) if retry and retry.isdigit() else None, code)
            length = response.getheader('Content-Length')
            if length is not None and (not length.isdigit() or int(length) > self.max_response):
                raise ValueError('invalid or oversized HTTP response')
            raw = response.read(self.max_response+1)
            if expired.is_set():
                raise TimeoutError('HTTP exchange deadline expired')
            if len(raw) > self.max_response:
                raise ValueError('oversized HTTP response')
            if length is not None and len(raw) != int(length):
                raise ValueError('truncated HTTP response')
            return json.loads(raw, parse_constant=lambda _: (_ for _ in ()).throw(ValueError('nonfinite JSON')))
        except http.client.HTTPException as exc:
            raise OSError('incomplete HTTP exchange') from exc
        finally:
            timer.cancel()
            connection.close()
