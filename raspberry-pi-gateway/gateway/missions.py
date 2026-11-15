"""Backend-compatible plans and durable, explicitly authorized mission execution."""

import copy
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
import json
import secrets
import threading
import time
from .http import HttpError


class MissionError(RuntimeError):
    pass


def _integer(value, minimum=1):
    if type(value) is not int or value < minimum:
        raise MissionError("invalid integer in mission")
    return value


def _cm(value):
    if type(value) not in (float, int):
        raise MissionError("numeric local coordinates required")
    try:
        cm = Decimal(str(value)) * 100
        if not cm.is_finite() or abs(cm) > 10000 or cm != cm.to_integral_value():
            raise MissionError("whole centimeters within 100 meters required")
        return int(cm)
    except InvalidOperation as exc:
        raise MissionError("invalid coordinate") from exc


def validate_plan(plan, gateway_id):
    if not isinstance(plan, dict):
        raise MissionError("mission object required")
    result = copy.deepcopy(plan)
    for key in ("id", "uavId", "gatewayId"):
        _integer(result.get(key))
    if result["gatewayId"] != gateway_id or result.get("status") != "SCHEDULED":
        raise MissionError("mission gateway or scheduled status mismatch")
    name = result.get("name")
    if not isinstance(name, str) or not name.strip() or len(name) > 150 or any(ord(c) < 32 for c in name):
        raise MissionError("invalid mission name")
    _integer(result.get("planRevision"), 0)
    try:
        start, end = (datetime.fromisoformat(result[k].replace("Z", "+00:00"))
                      for k in ("scheduledStartAt", "scheduledEndAt"))
        if start.tzinfo is None or end.tzinfo is None or start >= end:
            raise ValueError()
    except (KeyError, ValueError, TypeError, AttributeError) as exc:
        raise MissionError("valid UTC-aware mission schedule required") from exc
    waypoints, targets = result.get("waypoints"), result.get("targets")
    if not isinstance(waypoints, list) or not 1 <= len(waypoints) <= 16:
        raise MissionError("one to sixteen waypoints required")
    if not isinstance(targets, list) or not 1 <= len(targets) <= 8:
        raise MissionError("one to eight collection targets required")
    ids, sequences = set(), set()
    for waypoint in waypoints:
        if not isinstance(waypoint, dict):
            raise MissionError("waypoint object required")
        ident, sequence = _integer(waypoint.get("id")), _integer(waypoint.get("sequenceNo"))
        if ident in ids or sequence in sequences:
            raise MissionError("duplicate waypoint identity or sequence")
        ids.add(ident)
        sequences.add(sequence)
        _cm(waypoint.get("localX"))
        _cm(waypoint.get("localY"))
        if not 80 <= _cm(waypoint.get("altitudeM")) <= 120:
            raise MissionError("altitude must be 80 to 120 centimeters")
        hold = waypoint.get("plannedHoldSeconds")
        hold = 0 if hold is None else _integer(hold, 0)
        if hold > 30:
            raise MissionError("hold exceeds thirty seconds")
        waypoint["plannedHoldSeconds"] = hold
        action = waypoint.get("actionType")
        if action not in ("MOVE", "HOLD", "COLLECT", "WAIT", "COLLECT_SENSOR"):
            raise MissionError("unsupported waypoint action")
        waypoint["actionType"] = {"WAIT": "HOLD", "COLLECT_SENSOR": "COLLECT"}.get(action, action)
    node_ids, codes = set(), set()
    for target in targets:
        if not isinstance(target, dict):
            raise MissionError("target object required")
        node = _integer(target.get("sensorNodeId"))
        waypoint_id = _integer(target.get("waypointId"))
        code = target.get("deviceCode")
        if not isinstance(code, str) or not code.strip() or len(code.encode()) > 64 or any(ord(c) < 32 for c in code):
            raise MissionError("target device identity required")
        if node in node_ids or code in codes or waypoint_id not in ids:
            raise MissionError("duplicate target or unknown waypoint")
        node_ids.add(node)
        codes.add(code)
    for waypoint in waypoints:
        has_target = any(t["waypointId"] == waypoint["id"] for t in targets)
        if has_target != (waypoint["actionType"] == "COLLECT"):
            raise MissionError("targets must map exactly to COLLECT waypoints")
    result["waypoints"].sort(key=lambda w: w["sequenceNo"])
    plan_route(result)
    return result


def plan_route(plan):
    route = []

    def axis(delta, positive, negative, waypoint_id):
        remaining = abs(delta)
        if 0 < remaining < 20:
            raise MissionError("route axis is below SDK minimum")
        while remaining:
            segment = min(remaining, 100)
            if 0 < remaining - segment < 20:
                segment = remaining - 20
            route.append(("move", (positive if delta > 0 else negative, segment), waypoint_id))
            remaining -= segment

    x = y = 0
    waypoints = sorted(plan["waypoints"], key=lambda w: w["sequenceNo"])
    for waypoint in waypoints:
        ident = waypoint["id"]
        nx, ny = _cm(waypoint["localX"]), _cm(waypoint["localY"])
        route.append(("altitude", _cm(waypoint["altitudeM"]), ident))
        axis(nx - x, "forward", "back", ident)
        axis(ny - y, "right", "left", ident)
        route.append(("waypoint", waypoint["plannedHoldSeconds"], ident))
        x, y = nx, ny
    axis(-x, "forward", "back", ident)
    axis(-y, "right", "left", ident)
    if len(route) > 128:
        raise MissionError("route exceeds 128 steps including return")
    return route


class MissionEngine:
    def __init__(self, store, backend, safety, collector=None, video=None, *, uav_id=None):
        self.store, self.backend, self.safety = store, backend, safety
        self.collector, self.video = collector, video
        self.uav_id = uav_id
        self.session = secrets.token_hex(16)
        self._lock = threading.RLock()

    def _active(self):
        rows = self.store.query("SELECT * FROM missions WHERE state NOT IN ('COMPLETED','FAILED','CANCELLED','REPORTED','RETIRED') ORDER BY id")
        if len(rows) != 1:
            raise MissionError("exactly one active mission required")
        return rows[0]

    def _state(self, mission_id, state, details="", *, expected=None):
        now = datetime.now(timezone.utc).isoformat()
        with self.store.transaction() as db:
            updated = db.execute("UPDATE missions SET state=?,updated_at=? WHERE id=? AND (? IS NULL OR state=?)",
                                 (state, now, mission_id, expected, expected))
            if not updated.rowcount:
                raise MissionError("concurrent mission transition rejected")
            db.execute("INSERT INTO recovery_journal(mission_id,state,details,created_at) VALUES(?,?,?,?)",
                       (mission_id, state, details, now))

    def _validate(self, payload):
        plan = validate_plan(payload, self.backend.gateway_id)
        if self.uav_id is not None and plan["uavId"] != self.uav_id:
            raise MissionError("mission belongs to another UAV")
        return plan

    def download(self):
        with self._lock:
            rows = self.store.query("SELECT * FROM missions WHERE state NOT IN ('COMPLETED','FAILED','CANCELLED','REPORTED','RETIRED')")
            if rows and (len(rows) != 1 or rows[0]["state"] not in ("READY", "DOWNLOADED")):
                raise MissionError("active or unresolved mission retained")
            if self.safety.airborne_possible:
                raise MissionError("possible flight blocks downloads")
            payload = self.backend.request("GET", self.backend.root + "/missions/next")
            if payload is None:
                if rows:
                    self._state(rows[0]['id'], 'RETIRED', 'backend no longer offers unconsumed plan')
                return None
            plan = self._validate(payload)
            if rows and rows[0]["id"] != plan["id"]:
                self._state(rows[0]['id'], 'RETIRED', 'backend offers another unconsumed plan')
            old = self.store.query("SELECT state FROM missions WHERE id=?", (plan["id"],))
            if old and old[0]["state"] not in ("READY", "DOWNLOADED", "RETIRED"):
                raise MissionError("processed mission cannot be replayed")
            self.store.check_space()
            now = datetime.now(timezone.utc).isoformat()
            with self.store.transaction() as db:
                if db.execute("SELECT 1 FROM missions WHERE state NOT IN ('READY','DOWNLOADED','REPORTED','RETIRED','COMPLETED','FAILED','CANCELLED') OR (id<>? AND state IN ('READY','DOWNLOADED'))", (plan["id"],)).fetchone():
                    raise MissionError("another operation reserved this mission")
                db.execute("INSERT INTO missions(id,plan,plan_revision,state,updated_at) VALUES(?,?,?,'DOWNLOADED',?) "
                           "ON CONFLICT(id) DO UPDATE SET plan=excluded.plan,plan_revision=excluded.plan_revision,state='DOWNLOADED',updated_at=excluded.updated_at",
                           (plan["id"], json.dumps(plan, allow_nan=False), plan["planRevision"], now))
                db.execute("DELETE FROM waypoints WHERE mission_id=?", (plan["id"],))
                for waypoint in plan["waypoints"]:
                    db.execute("INSERT INTO waypoints(mission_id,id,sequence_no,payload) VALUES(?,?,?,?)",
                               (plan["id"], waypoint["id"], waypoint["sequenceNo"], json.dumps(waypoint)))
                db.execute("INSERT INTO recovery_journal(mission_id,state,details,created_at) VALUES(?,'DOWNLOADED','plan committed before acknowledgment',?)", (plan["id"], now))
            response = self.backend.request("POST", self.backend.root + f"/missions/{plan['id']}/downloaded")
            if not isinstance(response, dict) or response.get("id") != plan["id"] or response.get("status") != "SCHEDULED":
                raise MissionError("download acknowledgment mismatch")
            self._state(plan["id"], "READY", expected="DOWNLOADED")
            return plan

    def _launch_request(self, row, action):
        response = self.backend.request("POST", self.backend.root + f"/missions/{row['id']}/launch/{action}",
                                        {"session": self.session, "planRevision": row["plan_revision"]})
        if not isinstance(response, dict) or type(response.get("planRevision")) is not int or response["planRevision"] != row["plan_revision"]:
            raise MissionError("launch session or plan revision mismatch")
        if response.get("state") not in ("CONSUMED", "MISSED", "TERMINAL") and response.get("session") != self.session:
            raise MissionError("launch session mismatch")
        return response

    def ready(self):
        with self._lock:
            row = self._active()
            if row["state"] != "READY" or self.safety.airborne_possible:
                raise MissionError("mission is not ready")
            self._validate(json.loads(row["plan"]))
            self.safety.prepare()
            self.safety.preflight()
            self.store.check_space()
            try:
                response = self._launch_request(row, "ready")
            except HttpError as exc:
                if exc.status != 409 or getattr(exc, "code", None) != "PLAN_CHANGED_DOWNLOAD_AGAIN":
                    raise
                self.download()
                return {"state": "PLAN_CHANGED_DOWNLOAD_AGAIN"}
            if response.get("state") == "CONSUMED":
                self._state(row["id"], "RECOVERY_REQUIRED", "launch already consumed")
                self.safety.recover()
                raise MissionError("launch already consumed")
            if response.get("state") in ("TERMINAL", "MISSED"):
                self._state(row["id"], "RETIRED", "backend confirms plan no longer executable")
            return response

    def launch(self, operator_confirmed=False):
        with self._lock:
            if operator_confirmed is not True or self.safety.ground_test:
                raise MissionError("explicit operator confirmation and flight mode required")
            row = self._active()
            if row["state"] != "READY":
                raise MissionError("mission is not ready")
            plan = self._validate(json.loads(row["plan"]))
            if self.ready().get("state") != "READY_TO_START":
                raise MissionError("backend launch is not approved and due")
            self.store.check_space()
            self.safety.preflight()
            self._state(row["id"], "CONSUME_INTENT", expected="READY")
            try:
                if self._launch_request(row, "consume").get("state") != "START_ACCEPTED":
                    raise MissionError("launch was not accepted")
            except Exception as exc:
                try:
                    self._state(row["id"], "RECOVERY_REQUIRED", "consume response uncertain; operator reconciliation required")
                finally:
                    self.safety.recover()
                raise MissionError("uncertain consume; automatic retry prohibited") from exc
            return self._execute(plan)

    def _execute(self, plan):
        mission_id = plan["id"]
        owner = f"mission:{mission_id}"
        result = {"status": "COMPLETED", "failureReason": None, "completedWaypoints": 0}
        try:
            self._state(mission_id, "PREPARING_TELLO")
            self.safety.preflight()
            self._state(mission_id, "TAKEOFF_INTENT")
            self.safety.takeoff(owner, operator_confirmed=True)
            self._state(mission_id, "EXECUTING")
            if self.video:
                try:
                    self.video.start(mission_id, requested=plan.get("recordVideo") is True)
                except Exception:
                    pass  # The recorder persists its own capture failure.
            for kind, value, waypoint_id in plan_route(plan):
                self.store.check_space()
                if kind == "altitude":
                    self.safety.altitude(owner, value)
                elif kind == "move":
                    self.safety.move(owner, *value)
                else:
                    self.safety.hold(owner, value)
                    sample = self.safety.snapshot()
                    waypoint = next(w for w in plan["waypoints"] if w["id"] == waypoint_id)
                    telemetry = {"missionId": mission_id, "gatewayId": plan["gatewayId"], "uavId": plan["uavId"],
                                 "recordedAt": datetime.now(timezone.utc).isoformat(), "currentWaypointNo": waypoint["sequenceNo"],
                                 "latitude": None, "longitude": None, "localX": None, "localY": None,
                                 "altitudeM": sample["h"]/100, "batteryPercent": sample["bat"], "flightStatus": "AT_WAYPOINT"}
                    self.store.execute("INSERT INTO telemetry(mission_id,payload) VALUES(?,?)", (mission_id, json.dumps(telemetry)))
                    for target in plan["targets"]:
                        if target["waypointId"] != waypoint_id:
                            continue
                        count, status = 0, "FAILED"
                        try:
                            if self.collector is None:
                                raise MissionError("sensor collector unavailable")
                            count = self.collector.collect(target["deviceCode"], mission_id=mission_id, deadline=time.monotonic()+30)
                            _integer(count)
                            status = "SUCCESS"
                        except (OSError, ValueError, RuntimeError):
                            result.update(status="FAILED", failureReason="SENSOR_COLLECTION_FAILED")
                        self._outcome(mission_id, target["sensorNodeId"], count, status)
                    result["completedWaypoints"] += 1
        except Exception:
            result.update(status="FAILED", failureReason="FLIGHT_OR_STORAGE_FAILURE")
        finally:
            if self.safety.airborne_possible:
                try:
                    self._state(mission_id, "LANDING")
                finally:
                    try:
                        self.safety.land(owner)
                    except Exception:
                        self._state(mission_id, "RECOVERY_REQUIRED", "landing not confirmed")
                        raise MissionError("landing unconfirmed; retain aircraft link")
            if self.video:
                try:
                    self.video.stop()
                except Exception:
                    pass  # Optional recording cannot block the durable mission result.
        self._finish(plan, result)
        return result

    def _finish(self, plan, result):
        mission_id = plan["id"]
        for target in plan["targets"]:
            if not self.store.query("SELECT 1 FROM collection_attempts WHERE mission_id=? AND sensor_node_id=?", (mission_id, target["sensorNodeId"])):
                self._outcome(mission_id, target["sensorNodeId"], 0, "SKIPPED")
        with self.store.transaction() as db:
            db.execute("UPDATE missions SET result=?,state='RESULT_PENDING',updated_at=? WHERE id=?",
                       (json.dumps(result), datetime.now(timezone.utc).isoformat(), mission_id))

    def _outcome(self, mission_id, node_id, count, status):
        payload = {"sensorNodeId": node_id, "attemptNo": 1, "recordsReceived": count, "status": status}
        self.store.execute("INSERT INTO collection_attempts(mission_id,sensor_node_id,attempt_no,payload) VALUES(?,?,1,?)",
                           (mission_id, node_id, json.dumps(payload)))

    def recover(self):
        with self._lock:
            rows = self.store.query("SELECT id,state FROM missions WHERE state NOT IN ('READY','DOWNLOADED','RESULT_PENDING','COMPLETED','FAILED','CANCELLED','REPORTED','RETIRED')")
            for row in rows:
                self._state(row["id"], "RECOVERY_REQUIRED", "restart does not resume flight")
            if rows:
                self.safety.recover()
            return [row["id"] for row in rows]

    def reconcile(self, mission_id, operator_confirmed=False):
        with self._lock:
            row = self._active()
            if row["id"] != mission_id or operator_confirmed is not True or row["state"] != "RECOVERY_REQUIRED":
                raise MissionError("inspect aircraft on the ground before reconciling the matching recovery mission")
            self.safety.confirm_ground()
            plan = self._validate(json.loads(row["plan"]))
            # Consume may never have arrived: cancellation is legal from both SCHEDULED and RUNNING.
            result = {"status": "CANCELLED", "reason": "OPERATOR_RECONCILED_RECOVERY", "completedWaypoints": 0}
            self._finish(plan, result)
            return result

    def cancel(self, mission_id, reason):
        with self._lock:
            row = self._active()
            if row['id'] != mission_id or row['state'] not in ('READY','DOWNLOADED') or self.safety.airborne_possible:
                raise MissionError('only an unconsumed grounded mission can be cancelled locally')
            if not isinstance(reason,str) or not reason.strip() or len(reason)>500 or '\0' in reason:
                raise MissionError('a bounded cancellation reason is required')
            result = {'status':'CANCELLED','reason':reason}
            self.store.execute("UPDATE missions SET state='RESULT_PENDING',result=? WHERE id=?",
                               (json.dumps(result),mission_id))
            return result
