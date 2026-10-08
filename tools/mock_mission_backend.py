"""Development mission contract + existing sensor sync mock. No real backend changes."""
import argparse
import hmac
import json
import os
from http.server import ThreadingHTTPServer
from pathlib import Path
import mock_backend


def make_handler(state, mission_path, results_path):
    mission_path, results_path = Path(mission_path), Path(results_path)
    results = json.loads(results_path.read_text()) if results_path.exists() else {}
    base = mock_backend.make_handler(state)
    prefix = f"/api/device/gateways/{state.gateway_id}/missions/"

    class Handler(base):
        def authorized(self):
            return self.headers.get("X-Gateway-Code") == state.gateway_code and hmac.compare_digest(
                self.headers.get("X-Api-Key", ""), state.key)

        def do_GET(self):
            if self.path != prefix + "next":
                return self.send_json(404, {"success": False})
            if not self.authorized():
                return self.send_json(401, {"success": False})
            data = mission_path.read_bytes()
            try:
                complete = str(json.loads(data)["id"]) in results
            except (ValueError, KeyError, TypeError):
                complete = False  # Intentionally serves malformed fixtures for firmware tests.
            self.send_response(204 if complete else 200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", "0" if complete else str(len(data)))
            self.end_headers()
            if not complete:
                self.wfile.write(data)

        def do_POST(self):
            if not self.path.startswith(prefix):
                return super().do_POST()  # Reuse the existing sensor sync mock.
            if not self.authorized():
                return self.send_json(401, {"success": False})
            try:
                parts = self.path[len(prefix):].split("/")
                if len(parts) != 2 or parts[1] != "result":
                    return self.send_json(404, {"success": False})
                mission_id = int(parts[0])
                size = int(self.headers.get("Content-Length", "0"))
                if not 0 < size <= 8192:
                    return self.send_json(413, {"success": False})
                body = json.loads(self.rfile.read(size))
                if (body["missionId"] != mission_id or body["gatewayId"] != state.gateway_id
                        or type(body["fingerprint"]) is not int
                        or not 0 <= body["fingerprint"] <= 0xffffffff
                        or body["finalState"] not in ("COMPLETED", "FAILED", "CANCELLED")):
                    raise ValueError()
            except (ValueError, KeyError, TypeError):
                return self.send_json(400, {"success": False})
            with state.lock:
                previous = results.get(str(mission_id))
                if previous and previous != body:
                    return self.send_json(409, {"success": False})
                results[str(mission_id)] = body
                results_path.parent.mkdir(parents=True, exist_ok=True)
                temp = results_path.with_suffix(".tmp")
                temp.write_text(json.dumps(results))
                temp.replace(results_path)
            return self.send_json(200, {"success": True, "missionId": mission_id,
                                       "fingerprint": body["fingerprint"]})
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--gateway-id", type=int, default=1)
    parser.add_argument("--gateway-code", default="GATEWAY-001")
    parser.add_argument("--mission", default="tools/fixtures/mission-short.json")
    parser.add_argument("--results", default=".tools/mock-mission-results.json")
    parser.add_argument("--sensor-state", default=".tools/mock-mission-sensors.json")
    args = parser.parse_args()
    key = os.environ.get("MOCK_BACKEND_KEY", "")
    if not key:
        parser.error("Set MOCK_BACKEND_KEY to the configured development gateway key")
    state = mock_backend.BackendState(key, args.gateway_id, args.gateway_code, path=args.sensor_state)
    server = ThreadingHTTPServer(("0.0.0.0", args.port), make_handler(state, args.mission, args.results))
    print(f"[MOCK MISSION BACKEND] port={args.port}; no production backend integration")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
