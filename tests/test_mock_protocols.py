"""Host tests for the mock HTTP tools, not hardware/firmware validation."""
import http.client
import json
import sys
import threading
import unittest
from http.server import ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import mock_sensor
import mock_backend


class HttpFixture(unittest.TestCase):
    def start(self, handler):
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.addCleanup(self.server.server_close)
        self.addCleanup(self.server.shutdown)

    def request(self, path, body=None, headers=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=2)
        try:
            connection.request("GET" if body is None else "POST", path,
                               json.dumps(body) if body is not None else None, headers or {})
            response = connection.getresponse()
            data = response.read()
            try: data = json.loads(data)
            except ValueError: pass
            return response.status, data
        finally: connection.close()


class SensorTests(HttpFixture):
    def setUp(self):
        self.state = mock_sensor.SensorState("SENSOR-001", "test-token", 25)
        self.start(mock_sensor.make_handler(self.state))
        self.headers = {"X-Device-Code": "SENSOR-001", "X-Device-Token": "test-token"}

    def test_25_records_in_10_record_batches(self):
        for expected in (10, 10, 5):
            status, batch = self.request("/api/node/data?limit=10", headers=self.headers)
            self.assertEqual((status, batch["count"]), (200, expected))
            ids = [r["recordId"] for r in batch["records"]]
            self.assertEqual(self.request("/api/node/ack", {"recordIds": ids}, self.headers)[0], 200)
        self.assertEqual(len(self.state.records), 0)

    def test_failed_ack_keeps_identical_records(self):
        before = self.request("/api/node/data?limit=10", headers=self.headers)[1]
        self.state.ack_fail_once = True
        self.assertEqual(self.request("/api/node/ack", {"recordIds": [before["records"][0]["recordId"]]}, self.headers)[0], 500)
        self.assertEqual(before, self.request("/api/node/data?limit=10", headers=self.headers)[1])

    def test_authentication(self):
        self.assertEqual(self.request("/api/node/info")[0], 401)

    def test_malformed_response(self):
        self.state.malformed_once = True
        self.assertEqual(self.request("/api/node/data?limit=10", headers=self.headers), (200, b"{"))

    def test_time_and_health(self):
        self.assertEqual(self.request("/api/node/time", {"unixTime": 1800000000}, self.headers)[0], 200)
        self.assertEqual(self.state.last_time, 1800000000)
        self.assertEqual(self.request("/api/node/health", headers=self.headers)[1]["deviceCode"], "SENSOR-001")


class BackendTests(HttpFixture):
    def setUp(self):
        self.state = mock_backend.BackendState("test-key")
        self.start(mock_backend.make_handler(self.state))
        self.headers = {"X-Gateway-Code": "GATEWAY-001", "X-Api-Key": "test-key"}
        self.path = "/api/device/gateways/1/sync"
        self.body = dict(batchKey="batch-1", missionId=None, collectionResults=[], records=[dict(
            sensorNodeCode="SENSOR-001", channelCode="temperature", sourceRecordKey="SENSOR-001-100",
            value=30.2, measuredAt="2026-09-30T00:00:00Z", collectedAt="2026-09-30T00:00:01Z", qualityStatus="VALID")])

    def test_repeat_and_new_batch_deduplicate(self):
        first = self.request(self.path, self.body, self.headers)
        self.assertEqual(first[1]["data"]["accepted"], 1)
        self.assertEqual(first, self.request(self.path, self.body, self.headers))
        self.body["batchKey"] = "batch-2"
        self.assertEqual(self.request(self.path, self.body, self.headers)[1]["data"]["duplicates"], 1)
        self.assertEqual(len(self.state.readings), 1)

    def test_batch_key_conflict(self):
        self.request(self.path, self.body, self.headers)
        self.body["records"][0]["value"] = 40
        self.assertEqual(self.request(self.path, self.body, self.headers)[0], 409)

    def test_failed_request_does_not_accept(self):
        self.state.fail_first = 1
        self.assertEqual(self.request(self.path, self.body, self.headers)[0], 500)
        self.assertEqual(len(self.state.readings), 0)

    def test_partial_rejection(self):
        self.state.reject_channel = "temperature"
        self.assertEqual(self.request(self.path, self.body, self.headers)[1]["data"]["rejected"], 1)
        self.assertEqual(len(self.state.readings), 0)

    def test_response_lost_after_commit(self):
        self.state.drop_once = True
        with self.assertRaises(http.client.RemoteDisconnected): self.request(self.path, self.body, self.headers)
        self.assertEqual(len(self.state.readings), 1)
        self.assertEqual(self.request(self.path, self.body, self.headers)[0], 200)
        self.assertEqual(len(self.state.readings), 1)


if __name__ == "__main__": unittest.main()
