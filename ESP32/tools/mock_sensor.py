"""Laptop sensor mock; uses the same HTTP routes as a real ESP8266.

Set MOCK_DEVICE_TOKEN, connect laptop to UAV_GATEWAY, allow TCP 80 locally.
python tools/mock_sensor.py --gateway http://192.168.4.1 --records 25
"""
import argparse
import hmac
import json
import os
import socket
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs


class SensorState:
    def __init__(self, code, token, count=25, start=100, synced=True,
                 malformed_once=False, ack_fail_once=False, invalid_first=False,
                 duplicate=False, disconnect_after=-1):
        self.code, self.token = code, token
        self.malformed_once, self.ack_fail_once = malformed_once, ack_fail_once
        self.duplicate, self.disconnect_after, self.batches = duplicate, disconnect_after, 0
        stamp = int(time.time()) - 60
        self.records = [dict(recordId=f"{code}-{i}", deviceCode=code, sequence=i,
                             measuredAt=stamp if synced else 60, timeSynced=synced,
                             temperature=30.2, humidity=75.1, soilMoisture=48.5,
                             lightIntensity=None, ph=None, waterLevel=None, batteryVoltage=4.02)
                        for i in range(start, start + count)]
        if invalid_first and self.records:
            self.records[0]["sequence"] = -1
        self.acks = []
        self.last_time = None


def make_handler(state):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, fmt, *args):
            pass

        def send_json(self, status, value):
            data = json.dumps(value, separators=(",", ":")).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def authorized(self):
            return self.headers.get("X-Device-Code") == state.code and hmac.compare_digest(
                self.headers.get("X-Device-Token", ""), state.token)

        def do_GET(self):
            if not self.authorized():
                return self.send_json(401, {"success": False})
            url = urlsplit(self.path)
            if url.path == "/api/node/info":
                return self.send_json(200, dict(deviceCode=state.code, farmId=1, zoneId=1,
                    firmwareVersion="mock-1.0", pendingRecords=len(state.records)))
            if url.path == "/api/node/health":
                return self.send_json(200, dict(deviceCode=state.code, success=True))
            if url.path != "/api/node/data":
                return self.send_json(404, {"success": False})
            if state.disconnect_after >= 0 and state.batches >= state.disconnect_after:
                self.connection.shutdown(socket.SHUT_RDWR)
                self.connection.close()
                return
            if state.malformed_once:
                state.malformed_once = False
                self.send_response(200); self.send_header("Content-Length", "1"); self.end_headers()
                self.wfile.write(b"{"); return
            limit = min(20, max(1, int(parse_qs(url.query).get("limit", ["10"])[0])))
            records = state.records[:limit]
            if state.duplicate and len(records) > 1:
                records = [records[0]] + records[:limit-1]
            state.batches += 1
            return self.send_json(200, dict(deviceCode=state.code, count=len(records), records=records))

        def do_POST(self):
            if not self.authorized(): return self.send_json(401, {"success": False})
            try:
                size = int(self.headers.get("Content-Length", "0"))
                if not 0 < size <= 4096: return self.send_json(413, {"success": False})
                body = json.loads(self.rfile.read(size))
                if self.path == "/api/node/time":
                    state.last_time = body["unixTime"]
                    return self.send_json(200, {"success": True})
                if self.path != "/api/node/ack": return self.send_json(404, {"success": False})
                ids = body["recordIds"]
                if state.ack_fail_once:
                    state.ack_fail_once = False
                    # No ACK takes effect: the exact measurements will be retransmitted.
                    return self.send_json(500, {"success": False})
                if not isinstance(ids, list) or not all(isinstance(i, str) for i in ids):
                    return self.send_json(400, {"success": False})
                state.acks.extend(ids)
                state.records = [r for r in state.records if r["recordId"] not in ids]
                print(f"[MOCK] ACK count={len(ids)} pending={len(state.records)}", flush=True)
                return self.send_json(200, {"success": True})
            except (ValueError, KeyError, TypeError):
                return self.send_json(400, {"success": False})
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--gateway", default="http://192.168.4.1")
    parser.add_argument("--port", type=int, default=80)
    parser.add_argument("--code", default="SENSOR-001")
    parser.add_argument("--records", type=int, default=25)
    parser.add_argument("--start", type=int, default=100)
    parser.add_argument("--unsynced", action="store_true")
    parser.add_argument("--malformed-once", action="store_true")
    parser.add_argument("--ack-fail-once", action="store_true")
    parser.add_argument("--invalid-first", action="store_true")
    parser.add_argument("--duplicate", action="store_true")
    parser.add_argument("--disconnect-after", type=int, default=-1)
    args = parser.parse_args()
    token = os.environ.get("MOCK_DEVICE_TOKEN", "")
    if not token: parser.error("Set MOCK_DEVICE_TOKEN to the device's configured token")
    state = SensorState(args.code, token, args.records, args.start, not args.unsynced,
        args.malformed_once, args.ack_fail_once, args.invalid_first, args.duplicate, args.disconnect_after)
    server = ThreadingHTTPServer(("0.0.0.0", args.port), make_handler(state))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    print(f"[MOCK] Serving {args.code} on port {args.port}")
    try:
        while True:
            body = json.dumps(dict(deviceCode=args.code, farmId=1, zoneId=1,
                                   firmwareVersion="mock-1.0", deviceType="ESP8266_SENSOR")).encode()
            request = urllib.request.Request(args.gateway + "/api/gateway/nodes/register", data=body,
                headers={"Content-Type": "application/json", "X-Device-Code": args.code, "X-Device-Token": token})
            try:
                with opener.open(request, timeout=5) as response:
                    if response.status != 200: print("[MOCK] Registration not accepted")
            except Exception as error:
                print(f"[MOCK] Registration unavailable ({type(error).__name__})")
            time.sleep(5)
    except KeyboardInterrupt:
        server.shutdown(); server.server_close()


if __name__ == "__main__":
    main()
