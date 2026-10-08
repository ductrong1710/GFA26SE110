"""Tests the development HTTP fixture, not the production backend or hardware."""
import json
import tempfile
import unittest
from pathlib import Path
from test_mock_protocols import HttpFixture
import mock_backend
import mock_mission_backend


class MissionMockTests(HttpFixture):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "mission.json"
        self.path.write_text(json.dumps({"id": 15, "gatewayId": 1}))
        state = mock_backend.BackendState("test-key")
        self.start(mock_mission_backend.make_handler(state, self.path, Path(self.temp.name) / "results.json"))
        self.headers = {"X-Gateway-Code": "GATEWAY-001", "X-Api-Key": "test-key"}
        self.next = "/api/device/gateways/1/missions/next"
        self.result = "/api/device/gateways/1/missions/15/result"

    def test_authentication(self):
        self.assertEqual(self.request(self.next)[0], 401)

    def test_pull_then_idempotent_result(self):
        self.assertEqual(self.request(self.next, headers=self.headers)[1]["id"], 15)
        result = {"missionId": 15, "fingerprint": 7, "gatewayId": 1, "finalState": "COMPLETED"}
        first = self.request(self.result, result, self.headers)
        self.assertEqual(first[0], 200)
        self.assertEqual(first, self.request(self.result, result, self.headers))
        self.assertEqual(self.request(self.next, headers=self.headers)[0], 204)
        result["finalState"] = "FAILED"
        self.assertEqual(self.request(self.result, result, self.headers)[0], 409)

    def test_malformed_download_fixture(self):
        self.path.write_text("{")
        self.assertEqual(self.request(self.next, headers=self.headers), (200, b"{"))

    def test_wrong_result_identity(self):
        self.assertEqual(self.request(self.result, {"missionId": 99}, self.headers)[0], 400)


if __name__ == "__main__":
    unittest.main()
