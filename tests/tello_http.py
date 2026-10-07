"""Manual post-upload API checks: GETs and deliberately invalid control input only.

No prepare/takeoff/land or valid movement is sent. Run on UAV_GATEWAY:
python tests/tello_http.py [http://192.168.4.1]
"""
import http.client
import json
import sys
import unittest
from urllib.parse import urlsplit

BASE = urlsplit(sys.argv.pop(1) if len(sys.argv) > 1 else "http://192.168.4.1")


class TelloHttpTests(unittest.TestCase):
    def request(self, path, expected=200, body=None):
        connection = http.client.HTTPConnection(BASE.hostname, BASE.port or 80, timeout=5)
        try:
            connection.request("GET" if body is None else "POST", path, body,
                               {"Content-Type": "application/json"})
            response = connection.getresponse()
            self.assertEqual(response.status, expected)
            self.assertEqual(response.getheader("Content-Type"), "application/json")
            return json.loads(response.read())
        finally:
            connection.close()

    def test_status_and_existing_apis(self):
        tello = self.request("/api/tello/status")
        self.assertTrue(tello["success"])
        self.assertIn(tello["staTarget"], ("NONE", "TELLO", "INTERNET"))
        self.assertIsInstance(tello["telemetryFresh"], bool)
        status = self.request("/api/gateway/status")
        self.assertEqual(status["apIp"], "192.168.4.1")
        if status["staTarget"] == "TELLO":
            self.assertFalse(status["backendNetworkReady"])
            self.assertFalse(status["internetConnected"])
        self.assertTrue(self.request("/api/gateway/nodes")["success"])
        self.assertTrue(self.request("/api/gateway/time")["success"])

    def test_invalid_controls(self):
        for path, body in (
            ("move", '{"direction":"forward","distanceCm":19}'),
            ("move", '{"direction":"up;land","distanceCm":50}'),
            ("move", '{"direction":"up\\u0000bad","distanceCm":50}'),
            ("rotate", '{"direction":"cw","degrees":3601}'),
            ("rc", '{"leftRight":101,"forwardBack":0,"upDown":0,"yaw":0}'),
            ("rc", '{"leftRight":0,"forwardBack":0,"upDown":0}'),
            ("rc", '{'),
            ("move", '[]'),
        ):
            with self.subTest(path=path, body=body):
                self.assertFalse(self.request("/api/tello/" + path, 400, body)["success"])
        self.assertFalse(self.request("/api/tello/move", 413, "x" * 1025)["success"])


if __name__ == "__main__":
    unittest.main()
