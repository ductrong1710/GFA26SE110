"""Mock of the repository's device sync contract, never a replacement backend.

Set MOCK_BACKEND_KEY; python tools/mock_backend.py --port 5000
"""
import argparse
import hashlib
import hmac
import json
import os
import socket
import threading
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class BackendState:
    def __init__(self, key, gateway_id=1, gateway_code="GATEWAY-001", fail_first=0,
                 drop_once=False, reject_channel="", path=None):
        self.key, self.gateway_id, self.gateway_code = key, gateway_id, gateway_code
        self.fail_first, self.drop_once, self.reject_channel = fail_first, drop_once, reject_channel
        self.path = Path(path) if path else None
        self.readings, self.batches = {}, {}
        self.lock = threading.Lock()
        if self.path and self.path.exists():
            data = json.loads(self.path.read_text())
            self.readings, self.batches = data["readings"], data["batches"]

    def persist(self):
        if self.path:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temp = self.path.with_suffix(".tmp")
            temp.write_text(json.dumps(dict(readings=self.readings, batches=self.batches)))
            temp.replace(self.path)


def make_handler(state):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, fmt, *args): pass

        def send_json(self, status, value):
            data = json.dumps(value, separators=(",", ":")).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers(); self.wfile.write(data)

        def do_POST(self):
            if self.path != f"/api/device/gateways/{state.gateway_id}/sync":
                return self.send_json(404, {"success": False})
            if self.headers.get("X-Gateway-Code") != state.gateway_code or not hmac.compare_digest(
                    self.headers.get("X-Api-Key", ""), state.key):
                return self.send_json(401, {"success": False})
            with state.lock:
                if state.fail_first:
                    state.fail_first -= 1
                    return self.send_json(500, {"success": False})
                try:
                    size = int(self.headers.get("Content-Length", "0"))
                    if not 0 < size <= 12288: return self.send_json(413, {"success": False})
                    body = json.loads(self.rfile.read(size))
                    batch_key = body["batchKey"]
                    records = body["records"]
                    if not isinstance(batch_key, str) or not batch_key or not isinstance(records, list):
                        raise ValueError()
                except (ValueError, TypeError, KeyError):
                    return self.send_json(400, {"success": False})
                fingerprint = hashlib.sha256(json.dumps(body, sort_keys=True).encode()).hexdigest()
                previous = state.batches.get(batch_key)
                if previous:
                    if previous["hash"] != fingerprint: return self.send_json(409, {"success": False})
                    return self.send_json(200, previous["response"])
                results = []
                for index, record in enumerate(records):
                    outcome, error = "REJECTED", None
                    try:
                        code, channel, source = record["sensorNodeCode"], record["channelCode"], record["sourceRecordKey"]
                        measured = datetime.fromisoformat(record["measuredAt"].replace("Z", "+00:00"))
                        collected = datetime.fromisoformat(record["collectedAt"].replace("Z", "+00:00"))
                        if not code or not channel or not source or measured > collected or channel == state.reject_channel:
                            raise ValueError()
                        value = record["value"]
                        if type(value) not in (int, float): raise ValueError()
                        identity = json.dumps([code, channel, source])
                        if identity in state.readings:
                            if state.readings[identity] != record: raise ValueError()
                            outcome = "DUPLICATE"
                        else:
                            state.readings[identity] = record
                            outcome = "ACCEPTED"
                    except (ValueError, KeyError, TypeError, AttributeError):
                        error = "Mock rejected record"
                    results.append(dict(index=index, sourceRecordKey=record.get("sourceRecordKey"), status=outcome, error=error))
                response = dict(success=True, data=dict(batchKey=batch_key,
                    accepted=sum(r["status"] == "ACCEPTED" for r in results),
                    duplicates=sum(r["status"] == "DUPLICATE" for r in results),
                    rejected=sum(r["status"] == "REJECTED" for r in results),
                    records=results, collectionResults=[]), message=None)
                state.batches[batch_key] = dict(hash=fingerprint, response=response)
                state.persist()
                if state.drop_once:
                    state.drop_once = False
                    self.connection.shutdown(socket.SHUT_RDWR); self.connection.close(); return
                return self.send_json(200, response)
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--gateway-id", type=int, default=1)
    parser.add_argument("--gateway-code", default="GATEWAY-001")
    parser.add_argument("--fail-first", type=int, default=0)
    parser.add_argument("--drop-once", action="store_true")
    parser.add_argument("--reject-channel", default="")
    parser.add_argument("--state", default=".tools/mock-backend-state.json")
    args = parser.parse_args()
    key = os.environ.get("MOCK_BACKEND_KEY", "")
    if not key: parser.error("Set MOCK_BACKEND_KEY to the configured backend API key")
    state = BackendState(key, args.gateway_id, args.gateway_code, args.fail_first,
                         args.drop_once, args.reject_channel, args.state)
    server = ThreadingHTTPServer(("0.0.0.0", args.port), make_handler(state))
    print(f"[MOCK BACKEND] port={args.port} gatewayId={args.gateway_id}", flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()


if __name__ == "__main__": main()
