import copy
import http.client
import importlib
import json
import sqlite3
import socket
import threading
import time
from contextlib import contextmanager

import pytest


TRUSTED = [{"deviceCode": "SENSOR-001", "token": "test-token", "farmId": 1, "zoneId": 2}]
HEADERS = {"X-Device-Code": "SENSOR-001", "X-Device-Token": "test-token"}
BODY = {"deviceCode": "SENSOR-001", "farmId": 1, "zoneId": 2,
        "firmwareVersion": "1.0.0", "deviceType": "ESP8266_SENSOR", "ipAddress": "192.168.4.99"}
RECORD = {"recordId": "SENSOR-001-1", "deviceCode": "SENSOR-001", "sequence": 1,
          "measuredAt": 0, "timeSynced": False, "uptimeMs": 1000, "state": "PENDING",
          "temperature": 23.5, "humidity": 50, "soilMoisture": None, "lightIntensity": None,
          "ph": None, "waterLevel": None, "batteryVoltage": None}


@pytest.fixture
def sensor_module():
    try:
        return importlib.import_module("gateway.sensors")
    except ModuleNotFoundError:
        pytest.fail("The sensor registration and collection implementation is missing")


@pytest.fixture
def store(tmp_path):
    from gateway.storage import Store
    result = Store(tmp_path / "gateway.db", min_free_bytes=0)
    yield result
    result.close()


@pytest.fixture
def registry(sensor_module, store):
    return sensor_module.RegistrationRegistry(store, TRUSTED)


def register(registry):
    return registry.register(HEADERS, BODY, "192.168.4.2")


def test_registration_uses_peer_and_expires(registry, store):
    assert register(registry) == {"success": True, "authenticated": True, "deviceCode": "SENSOR-001",
                                  "message": "Node registered"}
    node = registry.online()[0]
    assert node["ip"] == "192.168.4.2"
    assert node["token"] == "test-token"
    assert registry.online(now=time.time() + 31) == []
    assert "test-token" not in store.query("SELECT metadata FROM nodes")[0]["metadata"]


@pytest.mark.parametrize("headers,changes,peer,status,code", [
    ({}, {}, "192.168.4.2", 401, "MISSING_CREDENTIALS"),
    ({**HEADERS, "X-Device-Token": "bad"}, {}, "192.168.4.2", 401, "AUTHENTICATION_FAILED"),
    (HEADERS, {"deviceCode": "OTHER"}, "192.168.4.2", 400, "DEVICE_CODE_MISMATCH"),
    (HEADERS, {"farmId": 9}, "192.168.4.2", 403, "NODE_SCOPE_MISMATCH"),
    (HEADERS, {"zoneId": True}, "192.168.4.2", 400, "INVALID_NODE_METADATA"),
    (HEADERS, {"firmwareVersion": "x" * 33}, "192.168.4.2", 400, "INVALID_NODE_METADATA"),
    (HEADERS, {"ipAddress": "example.org"}, "192.168.4.2", 400, "INVALID_IP_ADDRESS"),
    (HEADERS, {}, "192.168.10.1", 403, "INVALID_PEER_ADDRESS"),
])
def test_registration_rejects_untrusted_input(sensor_module, registry, headers, changes, peer, status, code):
    with pytest.raises(sensor_module.RegistrationError) as error:
        registry.register(headers, {**BODY, **changes}, peer)
    assert (error.value.status, error.value.code) == (status, code)
    assert registry.online() == []


def test_registration_http_framing_and_limits(sensor_module, store):
    registry = sensor_module.RegistrationRegistry(store, TRUSTED, subnet="127.0.0.0/8")
    server = sensor_module.make_server(registry, "127.0.0.1", 0)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        connection = http.client.HTTPConnection(*server.server_address, timeout=3)
        connection.request("POST", "/api/gateway/nodes/register", json.dumps(BODY),
                           {**HEADERS, "Content-Type": "application/json"})
        response = connection.getresponse()
        data = response.read()
        assert response.status == 200
        assert int(response.getheader("Content-Length")) == len(data) < 512
        assert response.getheader("Transfer-Encoding") is None
        assert json.loads(data)["authenticated"] is True
        connection.close()
        for body, extra, expected in [("x" * 1025, {}, 413), ("{}{}", {}, 400),
                                      ('{"a":1,"a":2}', {}, 400),
                                      ("{}", {"Transfer-Encoding": "chunked"}, 415)]:
            connection = http.client.HTTPConnection(*server.server_address, timeout=3)
            connection.request("POST", "/api/gateway/nodes/register", body,
                               {**HEADERS, "Content-Type": "application/json", **extra})
            response = connection.getresponse()
            assert response.status == expected
            assert json.loads(response.read())["success"] is False
            connection.close()
    finally:
        server.shutdown()
        server.server_close()
        thread.join(3)


def test_registration_direct_body_limit(sensor_module, registry):
    with pytest.raises(sensor_module.RegistrationError) as error:
        registry.register(HEADERS, {**BODY, "extra": "x" * 1024}, "192.168.4.2")
    assert error.value.status == 413


def test_registration_returns_503_on_database_error(sensor_module, store, monkeypatch):
    registry = sensor_module.RegistrationRegistry(store, TRUSTED, subnet="127.0.0.0/8")
    def unavailable(*args, **kwargs):
        raise sqlite3.OperationalError("disk I/O error")
    monkeypatch.setattr(store, "execute", unavailable)
    server = sensor_module.make_server(registry, "127.0.0.1", 0)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        connection = http.client.HTTPConnection(*server.server_address, timeout=3)
        connection.request("POST", "/api/gateway/nodes/register", json.dumps(BODY),
                           {**HEADERS, "Content-Type": "application/json"})
        response = connection.getresponse()
        assert response.status == 503
        assert json.loads(response.read())["errorCode"] == "STORAGE_ERROR"
        connection.close()
    finally:
        server.shutdown()
        server.server_close()
        thread.join(3)


class SensorHttp:
    def __init__(self, store, records=None, ack_error=False):
        self.store = store
        self.records = copy.deepcopy([RECORD] if records is None else records)
        self.ack_error = ack_error
        self.acks = []

    def request(self, method, url, headers=None, body=None):
        assert url.startswith("http://192.168.4.2/api/node/")
        assert headers == HEADERS
        if url.endswith("/info"):
            return {"success": True, "deviceCode": "SENSOR-001", "pendingRecords": len(self.records)}
        if url.endswith("/time"):
            return {"success": True, "timeSynced": True}
        if "/data?" in url:
            return {"success": True, "deviceCode": "SENSOR-001", "count": len(self.records),
                    "records": self.records}
        if url.endswith("/ack"):
            # A separate connection observes committed data, not the writer's transaction.
            with sqlite3.connect(self.store.path) as observer:
                assert observer.execute("SELECT COUNT(*) FROM measurements").fetchone()[0] > 0
            self.acks.append(body["recordIds"])
            if self.ack_error:
                raise TimeoutError("lost ACK response")
            self.records = []
            return {"success": True, "ackedCount": 0}
        raise AssertionError(url)


def test_collect_commits_before_ack_and_preserves_payload(sensor_module, registry, store):
    register(registry)
    http = SensorHttp(store)
    assert sensor_module.Collector(store, registry, http).collect("SENSOR-001") == 1
    row = store.query("SELECT * FROM measurements")[0]
    assert json.loads(row["payload"]) == RECORD
    assert row["node_acked"] == 1 and row["synced"] == 0
    assert http.acks == [["SENSOR-001-1"]]


def test_ack_timeout_keeps_record_and_duplicate_first_collection(sensor_module, registry, store):
    register(registry)
    http = SensorHttp(store, ack_error=True)
    collector = sensor_module.Collector(store, registry, http)
    with pytest.raises(TimeoutError):
        collector.collect("SENSOR-001")
    before = dict(store.query("SELECT * FROM measurements")[0])
    assert before["node_acked"] == 0
    http.ack_error = False
    # Unknown second mission must not replace the duplicate's original null association.
    assert collector.collect("SENSOR-001", mission_id=999) == 1
    after = dict(store.query("SELECT * FROM measurements")[0])
    assert after["mission_id"] is None
    assert after["collected_at"] == before["collected_at"]
    assert after["payload"] == before["payload"]


def test_failure_before_commit_never_acks(sensor_module, registry, store, monkeypatch):
    register(registry)
    original = store.transaction

    @contextmanager
    def fail_commit():
        with original() as connection:
            yield connection
            raise OSError("simulated commit failure")

    monkeypatch.setattr(store, "transaction", fail_commit)
    http = SensorHttp(store)
    with pytest.raises(OSError):
        sensor_module.Collector(store, registry, http).collect("SENSOR-001")
    assert store.query("SELECT * FROM measurements") == []
    assert http.acks == []


@pytest.mark.parametrize("changes", [{"sequence": 0}, {"sequence": True}, {"recordId": "SENSOR-001-01"},
                                      {"measuredAt": 1}, {"timeSynced": 1}, {"temperature": float("nan")},
                                      {"temperature": True}, {"state": "ACKED"}, {"humidity": "50"}])
def test_invalid_record_is_not_saved_or_acked(sensor_module, registry, store, changes):
    register(registry)
    http = SensorHttp(store, [{**RECORD, **changes}])
    with pytest.raises(ValueError):
        sensor_module.Collector(store, registry, http).collect("SENSOR-001")
    assert store.query("SELECT * FROM measurements") == []
    assert http.acks == []


def test_identity_collision_keeps_first_record(sensor_module, registry, store):
    register(registry)
    first = SensorHttp(store)
    sensor_module.Collector(store, registry, first).collect("SENSOR-001")
    second = SensorHttp(store, [{**RECORD, "temperature": 99}])
    with pytest.raises(ValueError, match="collision"):
        sensor_module.Collector(store, registry, second).collect("SENSOR-001")
    assert json.loads(store.query("SELECT payload FROM measurements")[0]["payload"])["temperature"] == 23.5
    assert second.acks == []


def test_address_change_after_fetch_never_acks(sensor_module, registry, store):
    register(registry)
    class ChangingHttp(SensorHttp):
        def request(self, method, url, headers=None, body=None):
            result = super().request(method, url, headers, body)
            if "/data?" in url:
                registry.register(HEADERS, BODY, "192.168.4.3")
            return result
    http = ChangingHttp(store)
    with pytest.raises(ValueError, match="address"):
        sensor_module.Collector(store, registry, http).collect("SENSOR-001")
    assert store.query("SELECT * FROM measurements") == []
    assert http.acks == []


@pytest.mark.parametrize("changes", [{"count": True}, {"count": 2}, {"success": False},
                                      {"deviceCode": "OTHER"}, {"records": [RECORD] * 11, "count": 11}])
def test_client_rejects_invalid_batch_envelope(sensor_module, changes):
    class Http:
        def request(self, *args, **kwargs):
            return {"success": True, "deviceCode": "SENSOR-001", "count": 1, "records": [RECORD], **changes}
    client = sensor_module.SensorClient({**TRUSTED[0], "ip": "192.168.4.2"}, Http())
    with pytest.raises(ValueError):
        client.fetch()


def test_health_and_time_use_node_credentials(sensor_module):
    calls = []
    class Http:
        def request(self, method, url, headers=None, body=None):
            calls.append((method, url, headers, body))
            return {"success": True, "deviceCode": "SENSOR-001", "timeSynced": True}
    client = sensor_module.SensorClient({**TRUSTED[0], "ip": "192.168.4.2"}, Http())
    assert client.health()["deviceCode"] == "SENSOR-001"
    assert client.sync_time(1700000000)["timeSynced"] is True
    assert calls == [("GET", "http://192.168.4.2/api/node/health", HEADERS, None),
                     ("POST", "http://192.168.4.2/api/node/time", HEADERS, {"unixTime": 1700000000})]


def test_expired_target_deadline_avoids_sensor_io(sensor_module, registry, store):
    register(registry)
    class NoIo:
        def request(self, *args, **kwargs):
            pytest.fail("I/O started after target timeout")
    with pytest.raises(TimeoutError):
        sensor_module.Collector(store, registry, NoIo()).collect("SENSOR-001", deadline=time.monotonic()-1)


def test_slow_registration_has_total_deadline(sensor_module, store):
    registry = sensor_module.RegistrationRegistry(store, TRUSTED, subnet="127.0.0.0/8")
    server = sensor_module.make_server(registry, "127.0.0.1", 0)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        with socket.create_connection(server.server_address, timeout=4) as connection:
            connection.sendall(b"POST /api/gateway/nodes/register HTTP/1.0\r\nX-Slow: ")
            start = time.monotonic()
            while time.monotonic() - start < 3:
                try:
                    connection.sendall(b"a")
                except OSError:
                    break
                time.sleep(0.2)
            connection.settimeout(0.5)
            try:
                assert connection.recv(1) == b""
            except (ConnectionResetError, ConnectionAbortedError):
                pass  # Windows may abort a socket with a half-written header.
    finally:
        server.shutdown()
        server.server_close()
        thread.join(3)
