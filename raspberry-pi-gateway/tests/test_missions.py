import copy
import json
import sqlite3
from contextlib import contextmanager

import pytest

from gateway.missions import MissionEngine, MissionError, plan_route, validate_plan


def mission():
    return {"id": 7, "gatewayId": 2, "uavId": 3, "name": "route", "status": "SCHEDULED",
            "planRevision": 4, "scheduledStartAt": "2026-11-06T00:00:00Z",
            "scheduledEndAt": "2026-11-06T00:10:00Z",
            "waypoints": [{"id": 10, "sequenceNo": 1, "localX": 1.1, "localY": 0.2,
                           "altitudeM": 1, "actionType": "COLLECT", "plannedHoldSeconds": 0}],
            "targets": [{"id": 5, "sensorNodeId": 8, "deviceCode": "NODE-8", "waypointId": 10}]}


class MemoryStore:
    def __init__(self):
        self.db = sqlite3.connect(":memory:")
        self.db.row_factory = sqlite3.Row
        self.db.executescript("""
        CREATE TABLE missions(id INTEGER PRIMARY KEY, plan TEXT, plan_revision INTEGER, state TEXT, result TEXT, updated_at TEXT);
        CREATE TABLE waypoints(mission_id INTEGER,id INTEGER,sequence_no INTEGER,payload TEXT,PRIMARY KEY(mission_id,id));
        CREATE TABLE collection_attempts(mission_id INTEGER,sensor_node_id INTEGER,attempt_no INTEGER,payload TEXT,synced INTEGER DEFAULT 0,PRIMARY KEY(mission_id,sensor_node_id,attempt_no));
        CREATE TABLE recovery_journal(id INTEGER PRIMARY KEY,mission_id INTEGER,state TEXT,details TEXT,created_at TEXT);
        CREATE TABLE telemetry(id INTEGER PRIMARY KEY,mission_id INTEGER,payload TEXT,state TEXT DEFAULT 'PENDING');
        """)

    @contextmanager
    def transaction(self):
        with self.db:
            yield self.db

    def execute(self, sql, params=()):
        with self.db:
            return self.db.execute(sql, params)

    def query(self, sql, params=()):
        return self.db.execute(sql, params).fetchall()

    def check_space(self):
        pass


class Backend:
    root = "/api/device/gateways/2"
    gateway_id = 2

    def __init__(self, store):
        self.store, self.calls = store, []
        self.consume_error = False
        self.wrong_session = False

    def request(self, method, path, body=None):
        self.calls.append((method, path, body))
        if path.endswith("/next"):
            return mission()
        if path.endswith("/downloaded"):
            assert self.store.query("SELECT state FROM missions")[0][0] == "DOWNLOADED"
            return {"id": 7, "status": "SCHEDULED"}
        if "/launch/" in path:
            if path.endswith("/consume"):
                assert self.store.query("SELECT state FROM missions")[0][0] == "CONSUME_INTENT"
                if self.consume_error:
                    raise TimeoutError("lost response")
            return {"state": "START_ACCEPTED" if path.endswith("/consume") else "READY_TO_START",
                    "session": "bad" if self.wrong_session else body["session"], "planRevision": 4}
        raise AssertionError(path)


class Safety:
    ground_test = False
    airborne_possible = False

    def __init__(self, store):
        self.store, self.actions = store, []

    def prepare(self):
        self.actions.append("prepare")

    def preflight(self):
        self.actions.append("preflight")

    def confirm_ground(self):
        self.airborne_possible = False

    def takeoff(self, owner, operator_confirmed=False):
        assert self.store.query("SELECT state FROM missions")[0][0] == "TAKEOFF_INTENT"
        assert operator_confirmed
        self.airborne_possible = True
        self.actions.append("takeoff")

    def altitude(self, owner, cm):
        self.actions.append(("altitude", cm))

    def move(self, owner, direction, cm):
        self.actions.append((direction, cm))

    def hold(self, owner, seconds):
        pass

    def land(self, owner):
        self.actions.append("land")
        self.airborne_possible = False

    def recover(self):
        self.airborne_possible = True

    def snapshot(self):
        return {"h": 100, "tof": 100, "bat": 90, "vgz": 0}


def engine():
    store = MemoryStore()
    backend, safety = Backend(store), Safety(store)
    return MissionEngine(store, backend, safety), store, backend, safety


def test_route_redistributes_short_tail_and_returns_home():
    route = plan_route(validate_plan(mission(), 2))
    assert route == [("altitude", 100, 10), ("move", ("forward", 90), 10),
                     ("move", ("forward", 20), 10), ("move", ("right", 20), 10),
                     ("waypoint", 0, 10), ("move", ("back", 90), 10),
                     ("move", ("back", 20), 10), ("move", ("left", 20), 10)]


@pytest.mark.parametrize("field,value", [("localX", .1), ("localX", 1.001),
    ("localX", float("nan")), ("localX", True), ("altitudeM", 1.21), ("plannedHoldSeconds", 31)])
def test_unsafe_plan_rejected_without_mutating_input(field, value):
    plan = mission()
    plan["waypoints"][0][field] = value
    with pytest.raises(MissionError):
        validate_plan(plan, 2)


def test_target_identity_and_gateway_binding():
    plan = mission()
    with pytest.raises(MissionError):
        validate_plan(plan, 99)
    plan["targets"].append(copy.deepcopy(plan["targets"][0]))
    with pytest.raises(MissionError):
        validate_plan(plan, 2)


def test_download_commits_before_ack_and_never_launches():
    eng, store, backend, safety = engine()
    eng.download()
    assert store.query("SELECT state FROM missions")[0][0] == "READY"
    assert safety.actions == []


def test_operator_gate_precedes_consume_and_takeoff():
    eng, store, backend, safety = engine()
    eng.download()
    with pytest.raises(MissionError):
        eng.launch()
    assert not any(path.endswith("/consume") for _, path, _ in backend.calls)
    assert safety.actions == []


def test_lost_consume_is_durable_recovery_and_never_retried():
    eng, store, backend, safety = engine()
    eng.download()
    backend.consume_error = True
    with pytest.raises(MissionError):
        eng.launch(operator_confirmed=True)
    assert store.query("SELECT state FROM missions")[0][0] == "RECOVERY_REQUIRED"
    with pytest.raises(MissionError):
        eng.launch(operator_confirmed=True)
    assert sum(path.endswith("/consume") for _, path, _ in backend.calls) == 1
    assert "takeoff" not in safety.actions


def test_wrong_session_does_not_consume():
    eng, store, backend, safety = engine()
    eng.download()
    backend.wrong_session = True
    with pytest.raises(MissionError):
        eng.launch(operator_confirmed=True)
    assert not any(path.endswith("/consume") for _, path, _ in backend.calls)


def test_reboot_ready_uses_new_session_and_does_not_launch():
    eng, store, backend, safety = engine()
    eng.download()
    reboot = MissionEngine(store, backend, safety)
    assert reboot.session != eng.session
    assert reboot.recover() == []
    reboot.ready()
    assert backend.calls[-1][2]["session"] == reboot.session
    assert "takeoff" not in safety.actions


def test_reboot_possible_flight_cannot_resume_route():
    eng, store, backend, safety = engine()
    eng.download()
    store.execute("UPDATE missions SET state='TAKEOFF_INTENT'")
    assert eng.recover() == [7]
    assert safety.airborne_possible
    assert store.query("SELECT state FROM missions")[0][0] == "RECOVERY_REQUIRED"


def test_execution_persists_collection_and_result_after_landing():
    eng, store, backend, safety = engine()

    class Collector:
        def collect(self, device_code, mission_id=None, *, deadline=None):
            assert (device_code, mission_id) == ("NODE-8", 7)
            return 2

    eng.collector = Collector()
    eng.download()
    result = eng.launch(operator_confirmed=True)
    assert result["status"] == "COMPLETED"
    assert safety.actions[-1] == "land"
    assert store.query("SELECT state FROM missions")[0][0] == "RESULT_PENDING"
    outcome = json.loads(store.query("SELECT payload FROM collection_attempts")[0][0])
    assert outcome == {"sensorNodeId": 8, "attemptNo": 1, "recordsReceived": 2, "status": "SUCCESS"}
    assert store.query("SELECT COUNT(*) FROM telemetry")[0][0] == 1
    telemetry = json.loads(store.query("SELECT payload FROM telemetry")[0][0])
    assert (telemetry["missionId"], telemetry["currentWaypointNo"], telemetry["altitudeM"]) == (7, 1, 1.)
    assert telemetry["localX"] is None


def test_cancelled_or_replaced_unconsumed_plan_does_not_block_next_mission():
    eng, store, backend, safety = engine()
    eng.download()
    original = backend.request
    backend.request = lambda method,path,body=None: None if path.endswith('/next') else original(method,path,body)
    assert eng.download() is None
    assert store.query('SELECT state FROM missions')[0][0] == 'RETIRED'
    replacement = mission()
    replacement['id'] = 8
    def request(method,path,body=None):
        if path.endswith('/next'):
            return replacement
        if path.endswith('/downloaded'):
            return {'id':8,'status':'SCHEDULED'}
        return original(method,path,body)
    backend.request = request
    assert eng.download()['id'] == 8
    assert store.query('SELECT state FROM missions WHERE id=8')[0][0] == 'READY'


def test_local_cancel_persists_result_without_consuming_launch():
    eng,store,backend,safety = engine()
    eng.download()
    eng.cancel(7, 'Operator cancelled before flight')
    row = store.query('SELECT * FROM missions')[0]
    assert row['state'] == 'RESULT_PENDING'
    assert json.loads(row['result'])['status'] == 'CANCELLED'
    assert not store.query('SELECT * FROM collection_attempts')
    assert not any(path.endswith('/consume') for _,path,_ in backend.calls)


def test_ready_requires_ground_preflight_before_advertising():
    eng, store, backend, safety = engine()
    eng.download()
    def unavailable():
        raise RuntimeError("low battery")
    safety.preflight = unavailable
    with pytest.raises(RuntimeError):
        eng.ready()
    assert not any(path.endswith("/ready") for _, path, _ in backend.calls)


def test_wrong_assigned_uav_blocks_download_ack():
    eng, store, backend, safety = engine()
    eng.uav_id = 99
    with pytest.raises(MissionError):
        eng.download()
    assert store.query("SELECT * FROM missions") == []


def test_reported_mission_is_safe_on_restart():
    eng, store, backend, safety = engine()
    eng.download()
    store.execute("UPDATE missions SET state='REPORTED'")
    assert eng.recover() == []
    assert not safety.airborne_possible


def test_reconciliation_requires_inspection_and_durable_recovery():
    eng, store, backend, safety = engine()
    eng.download()
    store.execute("UPDATE missions SET state='RECOVERY_REQUIRED'")
    with pytest.raises(MissionError):
        eng.reconcile(7)
    safety.airborne_possible = True
    eng.reconcile(7, operator_confirmed=True)
    assert not safety.airborne_possible
    assert store.query("SELECT state FROM missions")[0][0] == "RESULT_PENDING"
    assert json.loads(store.query('SELECT result FROM missions')[0][0])['status'] == 'CANCELLED'
    assert json.loads(store.query("SELECT payload FROM collection_attempts")[0][0])["status"] == "SKIPPED"


def test_camera_failure_does_not_change_collection_or_delay_land():
    eng, store, backend, safety = engine()
    class Collector:
        def collect(self, device_code, mission_id=None, *, deadline=None):
            return 1
    class Camera:
        def start(self, mission_id, requested=False):
            assert requested is True
            raise OSError("capture unavailable")
        def stop(self):
            assert not safety.airborne_possible
    eng.collector, eng.video = Collector(), Camera()
    eng.download()
    plan = mission() | {"recordVideo": True}
    store.execute("UPDATE missions SET plan=?", (json.dumps(plan),))
    assert eng.launch(True)["status"] == "COMPLETED"


def test_changed_revision_redownloads_without_consuming():
    from gateway.http import HttpError
    eng, store, backend, safety = engine()
    eng.download()
    request = backend.request
    def changed(method, path, body=None):
        if path.endswith("/ready"):
            exc = HttpError(409)
            exc.code = "PLAN_CHANGED_DOWNLOAD_AGAIN"
            raise exc
        if path.endswith("/next"):
            return mission() | {"planRevision": 5}
        return request(method, path, body)
    backend.request = changed
    assert eng.ready()["state"] == "PLAN_CHANGED_DOWNLOAD_AGAIN"
    assert store.query("SELECT plan_revision FROM missions")[0][0] == 5
    assert not any(path.endswith("/consume") for _, path, _ in backend.calls)
