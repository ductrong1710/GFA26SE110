"""Local fixture for the device API used by Mode A."""

import re
from datetime import datetime, timedelta, timezone


class BackendMock:
    root = "/api/device/gateways/2"
    code, key = "GATEWAY-DEMO", "loopback-backend-fixture"

    def __init__(self):
        now = datetime.now(timezone.utc)
        self.plan = {"id": 7, "name": "Loopback collection", "status": "SCHEDULED", "gatewayId": 2,
                     "uavId": 3, "scheduledStartAt": now.isoformat(), "scheduledEndAt": (now + timedelta(minutes=5)).isoformat(),
                     "planRevision": 1, "waypoints": [{"id": 10, "sequenceNo": 1, "latitude": None,
                         "longitude": None, "localX": 0.2, "localY": 0, "altitudeM": 1,
                         "actionType": "COLLECT", "plannedHoldSeconds": 0}],
                     "targets": [{"id": 5, "sensorNodeId": 8, "deviceCode": "SENSOR-001", "waypointId": 10,
                                  "waypointSequenceNo": 1, "sequenceNo": 1, "status": "PENDING"}]}
        self.session = None
        self.approved = False
        self.consumes = 0
        self.status = "SCHEDULED"
        self.batches, self.readings, self.archives, self.attempts = {}, {}, {}, {}
        self.telemetry = []

    def request(self, method, path, headers, body):
        if headers.get("X-Gateway-Code") != self.code or headers.get("X-Api-Key") != self.key:
            return 401, {"success": False}
        def ok(data):
            return 200, {"success": True, "data": data}

        if method == "POST" and path == self.root + "/heartbeat":
            return ok({"gatewayId": 2, "status": "ONLINE", "lastSeenAt": datetime.now(timezone.utc).isoformat()})
        if method == "GET" and path == self.root + "/missions/next":
            return ok(self.plan if self.status == "SCHEDULED" else None)
        if method != "POST":
            return 404, {"success": False}
        if path == self.root + "/missions/7/downloaded":
            return ok({"id": 7, "status": self.status, "startedAt": None, "completedAt": None, "failureReason": None})
        if path in (self.root + "/missions/7/launch/ready", self.root + "/missions/7/launch/consume"):
            if not re.fullmatch(r"[0-9a-f]{32}", body["session"]) or body["planRevision"] != 1:
                return 409, {"success": False}
            if self.session != body["session"]:
                self.session, self.approved = body["session"], False
            state = "CONSUMED" if self.consumes else "READY_TO_START" if self.approved else "AWAITING_CONFIRMATION"
            if path.endswith("/consume") and state == "READY_TO_START":
                self.consumes += 1
                self.status, state = "RUNNING", "START_ACCEPTED"
            return ok({"session": self.session, "planRevision": 1, "state": state,
                       "scheduledStartAt": self.plan["scheduledStartAt"], "expiresAt": self.plan["scheduledEndAt"],
                       "readyAt": datetime.now(timezone.utc).isoformat(), "approvedAt": datetime.now(timezone.utc).isoformat() if self.approved else None})
        if path == self.root + "/sync":
            if body.get('missionId') is not None and self.status in ('PENDING','SCHEDULED'):
                return 422, {'success':False}
            key = body["batchKey"]
            if key in self.batches:
                saved_body, saved_result = self.batches[key]
                return ok(saved_result) if body == saved_body else (409, {"success": False})
            result = {"batchKey": key, "accepted": 0, "duplicates": 0, "rejected": 0}
            for group, table in (("records", self.readings), ("unresolvedRecords", self.archives), ("collectionResults", self.attempts)):
                outcomes = []
                for index, item in enumerate(body[group]):
                    if group == "collectionResults":
                        identity = (body["missionId"], item["sensorNodeId"], item["attemptNo"])
                        outcome = {"sensorNodeId": item["sensorNodeId"], "attemptNo": item["attemptNo"]}
                    else:
                        if item["sensorNodeCode"] != "SENSOR-001":
                            return 422, {"success": False}
                        identity = (item["sensorNodeCode"], item.get("channelCode"), item["sourceRecordKey"])
                        outcome = {"sourceRecordKey": item["sourceRecordKey"]}
                    status = "ACCEPTED" if identity not in table else "DUPLICATE" if table[identity] == item else "REJECTED"
                    if status == "ACCEPTED":
                        table[identity] = item
                    outcomes.append({**outcome, "index": index, "status": status, "error": None})
                    if group == "records":
                        result[{"ACCEPTED": "accepted", "DUPLICATE": "duplicates", "REJECTED": "rejected"}[status]] += 1
                result[group] = outcomes
            self.batches[key] = body, result
            return ok(result)
        if path == "/api/device/telemetry" and body.get("missionId") == 7 and body.get("recordedAt"):
            self.telemetry.append(body)
            return ok({**body, "id": len(self.telemetry)})
        if path == self.root + "/missions/7/complete" and self.status == "RUNNING":
            self.status = "COMPLETED"
            return ok({"id": 7, "status": self.status})
        if path == self.root + '/missions/7/cancel' and self.status in ('SCHEDULED','RUNNING','CANCELLED'):
            self.status = 'CANCELLED'
            return ok({'id':7,'status':self.status})
        return 404, {"success": False}
