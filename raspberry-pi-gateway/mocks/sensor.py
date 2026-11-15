"""Loopback ESP8266 fixture. All addresses and credentials are local to the demo."""

import json
import threading
import time
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import parse_qs, urlsplit

from gateway.http import HttpClient


@contextmanager
def serving(server):
    thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": 0.05}, daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}"
    finally:
        server.shutdown()
        server.server_close()
        thread.join(2)


def mock_server(endpoint):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def dispatch(self):
            try:
                size = int(self.headers.get("Content-Length", "0"))
                if not 0 <= size <= 128 * 1024:
                    raise ValueError("Invalid length")
                body = json.loads(self.rfile.read(size)) if size else None
                status, data = endpoint(self.command, self.path, self.headers, body)
            except (ValueError, KeyError, TypeError):
                status, data = 400, {"success": False, "errorCode": "INVALID_REQUEST"}
            raw = json.dumps(data, allow_nan=False, separators=(",", ":")).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(raw)))
            self.end_headers()
            self.wfile.write(raw)

        do_GET = do_POST = dispatch

    return HTTPServer(("127.0.0.1", 0), Handler)


class SensorMock:
    device = {"deviceCode": "SENSOR-001", "token": "loopback-sensor-fixture", "farmId": 1, "zoneId": 2}
    headers = {"X-Device-Code": device["deviceCode"], "X-Device-Token": device["token"]}
    registration = {"deviceCode": device["deviceCode"], "farmId": 1, "zoneId": 2,
                    "firmwareVersion": "1.0.0", "deviceType": "ESP8266_SENSOR", "ipAddress": "127.0.0.1"}

    def __init__(self):
        self.records = {}
        for sequence, measured in ((1, 0), (2, int(time.time()) - 1)):
            record_id = f"SENSOR-001-{sequence}"
            self.records[record_id] = {
                "deviceCode": "SENSOR-001", "recordId": record_id, "sequence": sequence,
                "measuredAt": measured, "timeSynced": bool(measured), "uptimeMs": sequence * 1000,
                "state": "PENDING", "temperature": 24.5, "humidity": 75,
                "soilMoisture": None, "lightIntensity": None, "ph": None,
                "waterLevel": None, "batteryVoltage": None,
            }
        self.synced = False

    def register(self, base_url):
        response = HttpClient(max_response=511).request("POST", base_url + "/api/gateway/nodes/register",
                                                       headers=self.headers, body=self.registration)
        if response.get("success") is not True or response.get("authenticated") is not True or response.get("deviceCode") != self.device["deviceCode"]:
            raise RuntimeError("Sensor registration rejected")

    def request(self, method, path, headers, body):
        if any(headers.get(key) != value for key, value in self.headers.items()):
            return 401, {"success": False, "errorCode": "UNAUTHORIZED"}
        url = urlsplit(path)
        identity = {"success": True, "deviceCode": "SENSOR-001"}
        if method == "GET" and url.path == "/api/node/info":
            return 200, {**identity, **self.registration, "nodeId": 8, "pendingRecords": len(self.records)}
        if method == "GET" and url.path == "/api/node/health":
            return 200, {**identity, "status": "OK", "uptimeMs": 10000, "freeHeap": 32000,
                         "gatewayConnected": True, "wifiRSSI": -49, "pendingRecords": len(self.records),
                         "storageHealthy": True, "storageFull": False, "timeSynced": self.synced, "demoMode": True}
        if method == "GET" and url.path == "/api/node/data":
            amount = int(parse_qs(url.query).get("limit", [10])[0])
            if amount < 1:
                return 400, {"success": False, "errorCode": "INVALID_LIMIT"}
            records = list(self.records.values())[:min(10, amount)]
            return 200, {**identity, "count": len(records), "records": records}
        if method == "POST" and url.path == "/api/node/time" and 1577836800 <= body["unixTime"] <= 4102444800:
            self.synced = True
            return 200, {"success": True, "timeSynced": True}
        if method == "POST" and url.path == "/api/node/ack" and 1 <= len(body["recordIds"]) <= 10:
            acked = sum(self.records.pop(key, None) is not None for key in body["recordIds"])
            return 200, {"success": True, "ackedCount": acked}
        return 404, {"success": False, "errorCode": "NOT_FOUND"}


class LoopbackSensorHttp(HttpClient):
    """Route the firmware's fixed port 80 to an ephemeral local mock port."""
    def __init__(self, base_url):
        super().__init__(max_response=12288, source_address="127.0.0.1")
        if urlsplit(base_url).hostname != "127.0.0.1":
            raise ValueError("Simulation requires loopback")
        self.base_url = base_url

    def request(self, method, url, headers=None, body=None):
        if not url.startswith("http://127.0.0.1/api/node/"):
            raise ValueError("Mock sensor attempted external request")
        return super().request(method, self.base_url + url.removeprefix("http://127.0.0.1"), headers, body)
