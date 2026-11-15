"""ESP8266 registration and bounded, commit-before-ACK collection."""

import hmac
import ipaddress
import json
import math
import socket
import sqlite3
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, HTTPServer
from socketserver import ThreadingMixIn


READINGS = ("temperature", "humidity", "soilMoisture", "lightIntensity", "ph", "waterLevel", "batteryVoltage")


def _integer(value, low, high):
    return type(value) is int and low <= value <= high


def _text(value, maximum, spaces=False):
    return (isinstance(value, str) and 0 < len(value) <= maximum and bool(value.strip())
            and all((32 if spaces else 33) <= ord(char) <= 126 for char in value))


class RegistrationError(ValueError):
    def __init__(self, status, code):
        super().__init__(code)
        self.status = status
        self.code = code


class RegistrationRegistry:
    def __init__(self, store, trusted, subnet="192.168.4.0/24"):
        self.store = store
        self.trusted = {node["deviceCode"]: dict(node) for node in trusted}
        if len(self.trusted) != len(trusted) or len(trusted) > 20:
            raise ValueError("Trusted registry must have at most 20 unique devices")
        self.subnet = ipaddress.IPv4Network(subnet)
        for node in self.trusted.values():
            if (not _text(node["deviceCode"], 64) or not isinstance(node.get("token"), str)
                    or not node["token"] or not _integer(node.get("farmId"), 1, 2147483647)
                    or not _integer(node.get("zoneId"), 1, 2147483647)):
                raise ValueError("Invalid trusted device configuration")

    def _peer(self, value):
        try:
            address = ipaddress.IPv4Address(value)
        except ipaddress.AddressValueError:
            raise RegistrationError(403, "INVALID_PEER_ADDRESS") from None
        if (address not in self.subnet or address in (self.subnet.network_address, self.subnet.broadcast_address)
                or address.is_multicast or address.is_unspecified):
            raise RegistrationError(403, "INVALID_PEER_ADDRESS")
        return str(address)

    def register(self, headers, body, peer_ip):
        if not isinstance(body, dict):
            raise RegistrationError(400, "INVALID_JSON")
        try:
            encoded = json.dumps(body, allow_nan=False, ensure_ascii=False, separators=(",", ":")).encode()
        except (TypeError, ValueError, RecursionError):
            raise RegistrationError(400, "INVALID_JSON") from None
        if len(encoded) > 1024:
            raise RegistrationError(413, "BODY_TOO_LARGE")
        code = body.get("deviceCode")
        if not isinstance(code, str) or not code.strip():
            raise RegistrationError(400, "MISSING_DEVICE_CODE")
        if not _text(code, 64):
            raise RegistrationError(400, "INVALID_DEVICE_CODE")
        if (not _integer(body.get("farmId"), 1, 2147483647)
                or not _integer(body.get("zoneId"), 1, 2147483647)
                or not _text(body.get("firmwareVersion"), 32, spaces=True)
                or not _text(body.get("deviceType"), 32, spaces=True)):
            raise RegistrationError(400, "INVALID_NODE_METADATA")
        if "ipAddress" in body:
            try:
                if not isinstance(body["ipAddress"], str):
                    raise ValueError()
                ipaddress.IPv4Address(body["ipAddress"])
            except ValueError:
                raise RegistrationError(400, "INVALID_IP_ADDRESS") from None
        lowered = {key.lower(): value for key, value in headers.items()}
        header_code, token = lowered.get("x-device-code"), lowered.get("x-device-token")
        if not header_code or not token:
            raise RegistrationError(401, "MISSING_CREDENTIALS")
        if header_code != code:
            raise RegistrationError(400, "DEVICE_CODE_MISMATCH")
        trusted = self.trusted.get(code)
        if trusted is None:
            raise RegistrationError(401, "UNKNOWN_DEVICE")
        if not isinstance(token, str) or not hmac.compare_digest(token.encode(), trusted["token"].encode()):
            raise RegistrationError(401, "AUTHENTICATION_FAILED")
        if (body["farmId"], body["zoneId"]) != (trusted["farmId"], trusted["zoneId"]):
            raise RegistrationError(403, "NODE_SCOPE_MISMATCH")
        peer = self._peer(peer_ip)
        metadata = json.dumps({key: body[key] for key in ("firmwareVersion", "deviceType")}, separators=(",", ":"))
        self.store.check_space()
        self.store.execute("""INSERT INTO nodes(device_code,farm_id,zone_id,ip,metadata,last_seen)
            VALUES(?,?,?,?,?,?) ON CONFLICT(device_code) DO UPDATE SET
            farm_id=excluded.farm_id, zone_id=excluded.zone_id, ip=excluded.ip,
            metadata=excluded.metadata, last_seen=excluded.last_seen""",
            (code, body["farmId"], body["zoneId"], peer, metadata, time.time()))
        return {"success": True, "authenticated": True, "deviceCode": code, "message": "Node registered"}

    def online(self, now=None):
        now = time.time() if now is None else now
        nodes = []
        for row in self.store.query("SELECT * FROM nodes WHERE last_seen >= ? AND last_seen <= ? ORDER BY device_code", (now - 30, now)):
            trusted = self.trusted.get(row["device_code"])
            if trusted and (row["farm_id"], row["zone_id"]) == (trusted["farmId"], trusted["zoneId"]):
                self._peer(row["ip"])
                nodes.append({**json.loads(row["metadata"]), **trusted, "ip": row["ip"]})
        return nodes


def _json_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON property")
        result[key] = value
    return result


def _json_depth(value):
    if isinstance(value, dict):
        return 1 + max((_json_depth(item) for item in value.values()), default=0)
    if isinstance(value, list):
        return 1 + max((_json_depth(item) for item in value), default=0)
    return 0


class _RegistrationServer(ThreadingMixIn, HTTPServer):
    daemon_threads = True
    request_queue_size = 8

    def __init__(self, address, handler):
        self.slots = threading.BoundedSemaphore(4)
        super().__init__(address, handler)

    def get_request(self):
        connection, address = super().get_request()
        connection.settimeout(2)
        return connection, address

    def process_request(self, request, client_address):
        if not self.slots.acquire(blocking=False):
            self.shutdown_request(request)
            return
        try:
            super().process_request(request, client_address)
        except Exception:
            self.slots.release()
            raise

    def process_request_thread(self, request, client_address):
        def expire():
            try:
                request.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass

        deadline = threading.Timer(2.5, expire)
        deadline.start()
        try:
            super().process_request_thread(request, client_address)
        finally:
            deadline.cancel()
            self.slots.release()


def make_server(registry, host, port):
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.0"

        def log_message(self, *_):
            pass

        def handle(self):
            try:
                super().handle()
            except ConnectionError:
                pass  # Peer disconnect or total request deadline.

        def reply(self, status, data):
            payload = json.dumps(data, separators=(",", ":"), allow_nan=False).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(payload)
            self.close_connection = True

        def do_POST(self):
            try:
                if self.path != "/api/gateway/nodes/register":
                    raise RegistrationError(404, "NOT_FOUND")
                if (self.headers.get("Transfer-Encoding") is not None
                        or self.headers.get_content_type() != "application/json"):
                    raise RegistrationError(415, "INVALID_REQUEST")
                for header in ("Content-Length", "X-Device-Code", "X-Device-Token"):
                    if len(self.headers.get_all(header, [])) > 1:
                        raise RegistrationError(400, "INVALID_REQUEST")
                size = self.headers.get("Content-Length", "")
                if not size.isascii() or not size.isdecimal():
                    raise RegistrationError(400, "INVALID_JSON")
                if len(size) > 5 or int(size) > 1024:
                    raise RegistrationError(413, "BODY_TOO_LARGE")
                raw = self.rfile.read(int(size))
                if len(raw) != int(size):
                    raise RegistrationError(400, "INVALID_JSON")
                try:
                    body = json.loads(raw, object_pairs_hook=_json_object,
                                      parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
                    if _json_depth(body) > 4:
                        raise ValueError()
                except (ValueError, RecursionError):
                    raise RegistrationError(400, "INVALID_JSON") from None
                self.reply(200, registry.register(self.headers, body, self.client_address[0]))
            except RegistrationError as error:
                self.reply(error.status, {"success": False, "errorCode": error.code, "message": error.code})
            except (TimeoutError, ConnectionError):
                self.close_connection = True
            except (OSError, RuntimeError, sqlite3.Error):
                self.reply(503, {"success": False, "errorCode": "STORAGE_ERROR"})

        def do_GET(self):
            self.reply(404, {"success": False, "errorCode": "NOT_FOUND"})

    return _RegistrationServer((host, port), Handler)


def _validate_record(record, code):
    if not isinstance(record, dict):
        raise ValueError("Sensor record must be an object")
    sequence, measured, synced = record.get("sequence"), record.get("measuredAt"), record.get("timeSynced")
    if (record.get("deviceCode") != code or not _integer(sequence, 1, 4294967295)
            or record.get("recordId") != f"{code}-{sequence}"
            or type(synced) is not bool or not _integer(measured, 0, 4102444800)
            or (synced and measured < 1577836800) or (not synced and measured != 0)
            or not _integer(record.get("uptimeMs"), 0, 4294967295) or record.get("state") != "PENDING"):
        raise ValueError("Invalid sensor record identity or timestamp")
    for key in READINGS:
        if key not in record:
            raise ValueError("Missing sensor reading")
        value = record[key]
        if value is not None and (type(value) not in (int, float) or not math.isfinite(value) or abs(value) > 1e12):
            raise ValueError("Invalid sensor reading")
    if _json_depth(record) > 3 or len(json.dumps(record, allow_nan=False)) > 4096:
        raise ValueError("Oversized sensor record")


class SensorClient:
    def __init__(self, node, http, deadline=None):
        self.node, self.http = node, http
        self.deadline = deadline
        self.url = "http://" + str(ipaddress.IPv4Address(node["ip"])) + "/api/node"
        self.headers = {"X-Device-Code": node["deviceCode"], "X-Device-Token": node["token"]}

    def _call(self, method, path, body=None, identity=True):
        previous_timeout = getattr(self.http,'timeout',None)
        if self.deadline is not None:
            remaining = self.deadline-time.monotonic()
            if remaining <= 0:
                raise TimeoutError('Sensor collection deadline expired')
            if previous_timeout is not None:
                self.http.timeout = min(previous_timeout,remaining)
        try:
            response = self.http.request(method, self.url + path, headers=self.headers, body=body)
        finally:
            if previous_timeout is not None:
                self.http.timeout = previous_timeout
        if (not isinstance(response, dict) or response.get("success") is not True
                or (identity and response.get("deviceCode") != self.node["deviceCode"])):
            raise ValueError("Invalid sensor response")
        return response

    def info(self):
        response = self._call("GET", "/info")
        if not _integer(response.get("pendingRecords"), 0, 4294967295):
            raise ValueError("Invalid pending record count")
        return response

    def health(self):
        return self._call("GET", "/health")

    def sync_time(self, unix_time=None):
        unix_time = int(time.time()) if unix_time is None else unix_time
        if not _integer(unix_time, 1577836800, 4102444800):
            raise ValueError("Invalid gateway clock")
        return self._call("POST", "/time", {"unixTime": unix_time}, identity=False)

    def fetch(self, limit=10):
        if not _integer(limit, 1, 10):
            raise ValueError("Batch limit must be 1..10")
        response = self._call("GET", f"/data?limit={limit}")
        records = response.get("records")
        if (not isinstance(records, list) or not _integer(response.get("count"), 0, limit)
                or response["count"] != len(records)):
            raise ValueError("Invalid sensor batch count")
        for record in records:
            _validate_record(record, self.node["deviceCode"])
        if len({record["recordId"] for record in records}) != len(records):
            raise ValueError("Duplicate identity within sensor batch")
        return records

    def ack(self, ids):
        if not isinstance(ids, list) or not 1 <= len(ids) <= 10 or not all(_text(item, 79) for item in ids):
            raise ValueError("Invalid ACK IDs")
        response = self._call("POST", "/ack", {"recordIds": ids}, identity=False)
        if not _integer(response.get("ackedCount"), 0, len(ids)):
            raise ValueError("Invalid ACK count")
        return response


class Collector:
    def __init__(self, store, registry, http, clock_is_synced=lambda: True):
        self.store, self.registry, self.http = store, registry, http
        self.clock_is_synced = clock_is_synced
        self.lock = threading.Lock()

    def _node(self, code, ip=None):
        node = next((node for node in self.registry.online() if node["deviceCode"] == code), None)
        if node is None or (ip is not None and node["ip"] != ip):
            raise ValueError("Node is offline or changed address")
        return node

    def collect(self, device_code, mission_id=None, deadline=None):
        def check_deadline():
            if deadline is not None and time.monotonic() >= deadline:
                raise TimeoutError('Sensor collection deadline expired')

        check_deadline()
        with self.lock:
            check_deadline()
            node = self._node(device_code)
            client = SensorClient(node, self.http, deadline)
            pending = client.info()["pendingRecords"]
            self.store.execute("UPDATE nodes SET last_seen=? WHERE device_code=? AND ip=?", (time.time(), device_code, node["ip"]))
            try:
                if self.clock_is_synced():
                    client.sync_time()
            except (OSError, ValueError):
                pass  # Sensor measurements keep their own timeSynced flag when clock update fails.
            total = 0
            for _ in range(3):
                check_deadline()
                if not pending:
                    break
                self._node(device_code, node["ip"])
                records = client.fetch()
                if not records:
                    break
                self.store.check_space()
                collected = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
                with self.store.transaction() as connection:
                    current = connection.execute("SELECT ip FROM nodes WHERE device_code=?", (device_code,)).fetchone()
                    if current is None or current["ip"] != node["ip"]:
                        raise ValueError("Node changed address")
                    for record in records:
                        existing = connection.execute("SELECT payload FROM measurements WHERE record_id=? OR (device_code=? AND sequence=?)",
                                                      (record["recordId"], device_code, record["sequence"])).fetchone()
                        if existing:
                            if json.loads(existing["payload"]) != record:
                                raise ValueError("Sensor identity collision")
                            continue
                        connection.execute("""INSERT INTO measurements(record_id,device_code,sequence,payload,collected_at,mission_id)
                            VALUES(?,?,?,?,?,?)""", (record["recordId"], device_code, record["sequence"],
                            json.dumps(record, separators=(",", ":"), sort_keys=True, allow_nan=False), collected, mission_id))
                self._node(device_code, node["ip"])
                ids = [record["recordId"] for record in records]
                check_deadline()
                client.ack(ids)
                with self.store.transaction() as connection:
                    connection.executemany("UPDATE measurements SET node_acked=1 WHERE record_id=?", [(item,) for item in ids])
                    connection.execute("UPDATE nodes SET last_seen=? WHERE device_code=? AND ip=?", (time.time(), device_code, node["ip"]))
                total += len(records)
                pending = max(0, pending - len(records))
            return total
