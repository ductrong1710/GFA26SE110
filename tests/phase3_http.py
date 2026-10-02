"""Run AFTER manual firmware upload while connected to UAV_GATEWAY.

Usage: python tests/phase3_http.py [http://192.168.4.1]
Uses only the Python standard library; never uploads firmware.
"""
import http.client
import json
import sys
import unittest
from urllib.parse import urlsplit

BASE = urlsplit(sys.argv.pop(1) if len(sys.argv) > 1 else "http://192.168.4.1")


class GatewayApiTests(unittest.TestCase):
    def request(self, path, expected=200, body=None, content_type="application/json"):
        connection = http.client.HTTPConnection(BASE.hostname, BASE.port or 80, timeout=10)
        try:
            connection.request("GET" if body is None else "POST", path, body,
                               {"Content-Type": content_type})
            response = connection.getresponse()
            self.assertEqual(response.status, expected)
            self.assertEqual(response.getheader("Content-Type"), "application/json")
            return json.loads(response.read())
        finally:
            connection.close()

    def test_status(self):
        data = self.request("/api/gateway/status")
        self.assertTrue(data["success"])
        self.assertEqual(data["gatewayCode"], "GATEWAY-001")
        self.assertEqual(data["status"], "ONLINE")
        self.assertEqual(data["apIp"], "192.168.4.1")
        self.assertGreater(data["freeHeap"], 0)
        self.assertGreaterEqual(data["connectedStations"], 1)
        self.assertIsInstance(data["uptimeMs"], int)
        self.assertFalse(data["internetConnected"])
        self.assertEqual(data["pendingUploadRecords"], 0)

    def test_time(self):
        data = self.request("/api/gateway/time")
        self.assertTrue(data["success"])
        self.assertEqual(data["unixTime"], 0)
        self.assertFalse(data["timeSynced"])
        self.assertIsInstance(data["uptimeMs"], int)

    def test_registration_does_not_store_node(self):
        body = json.dumps(dict(deviceCode="SENSOR-001", farmId=1, zoneId=1,
                               firmwareVersion="1.0.0", deviceType="ESP8266_SENSOR",
                               ipAddress="192.168.4.2"))
        data = self.request("/api/gateway/nodes/register", 202, body)
        self.assertTrue(data["success"])
        self.assertEqual(data["deviceCode"], "SENSOR-001")
        self.assertEqual(self.request("/api/gateway/nodes"),
                         dict(success=True, count=0, nodes=[]))

    def test_invalid_json(self):
        for body in ['{', '[]', '{"deviceCode":"SENSOR-001"}garbage']:
            with self.subTest(body=body):
                data = self.request("/api/gateway/nodes/register", 400, body)
                self.assertFalse(data["success"])
                self.assertEqual(data["errorCode"], "INVALID_JSON")

    def test_missing_device_code(self):
        for body in ['{}', '{"deviceCode":""}', '{"deviceCode":"  "}',
                     '{"deviceCode":123}', '{"deviceCode":null}']:
            with self.subTest(body=body):
                data = self.request("/api/gateway/nodes/register", 400, body)
                self.assertEqual(data["errorCode"], "MISSING_DEVICE_CODE")

    def test_body_boundary(self):
        body = '{"deviceCode":"SENSOR-001"}'
        self.request("/api/gateway/nodes/register", 202, body.ljust(1024))
        data = self.request("/api/gateway/nodes/register", 413, body.ljust(1025))
        self.assertEqual(data["errorCode"], "BODY_TOO_LARGE")

    def test_oversized_headers_rejected_before_body(self):
        connection = http.client.HTTPConnection(BASE.hostname, BASE.port or 80, timeout=5)
        try:
            connection.putrequest("POST", "/api/gateway/nodes/register")
            connection.putheader("Content-Type", "application/json")
            connection.putheader("Content-Length", "1000000")
            connection.endheaders()  # Deliberately send no body.
            response = connection.getresponse()
            self.assertEqual(response.status, 413)
            self.assertEqual(json.loads(response.read())["errorCode"], "BODY_TOO_LARGE")
        finally:
            connection.close()

    def test_unknown_route(self):
        self.assertEqual(self.request("/api/gateway/unknown", 404),
                         dict(success=False, errorCode="NOT_FOUND", message="Endpoint not found"))

    def test_wrong_content_type(self):
        data = self.request("/api/gateway/nodes/register", 415, "{}", "text/plain")
        self.assertEqual(data["errorCode"], "UNSUPPORTED_MEDIA_TYPE")


if __name__ == "__main__":
    unittest.main()
