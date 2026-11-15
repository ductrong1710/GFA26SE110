import json
import subprocess
import sys
import pytest


def test_loopback_demo_survives_restart_and_reports_mission():
    process = subprocess.run([sys.executable, "-m", "mocks.demo"], capture_output=True,
                             text=True, timeout=30)
    assert process.returncode == 0, process.stderr
    result = json.loads(process.stdout)
    assert result["mode"] == "simulation"
    assert result["node"] == "SENSOR-001" and result["nodePending"] == 0
    assert result["readyState"] == "AWAITING_CONFIRMATION"
    assert result["consumes"] == 1 and result["takeoffs"] == 1 and result["landed"]
    assert result["mission"] == "REPORTED" and result["backendMission"] == "COMPLETED"
    assert result["syncedMeasurements"] == 2 and result["syncedAttempts"] == 1
    assert result["channelReadings"] == 2 and result["archivedSamples"] == 1
    assert result["telemetry"] == 1 and result["reopened"] is True


@pytest.mark.parametrize('remote_state',['SCHEDULED','RUNNING'])
def test_ambiguous_launch_can_cancel_then_sync_over_http(tmp_path,remote_state):
    from gateway.backend import BackendClient, ResultWorker, SyncWorker
    from gateway.storage import Store
    from mocks.backend import BackendMock
    from mocks.sensor import mock_server, serving
    backend = BackendMock()
    backend.status = remote_state
    store = Store(tmp_path/'recovery.db',min_free_bytes=0)
    try:
        result = json.dumps({'status':'CANCELLED','reason':'OPERATOR_RECONCILED_RECOVERY'})
        store.execute("INSERT INTO missions VALUES (7,'{}',1,'RESULT_PENDING',?,'2026-01-01')",(result,))
        attempt = {'sensorNodeId':8,'attemptNo':1,'status':'SKIPPED','recordsReceived':0}
        store.execute('INSERT INTO collection_attempts VALUES (7,8,1,?,0)',(json.dumps(attempt),))
        with serving(mock_server(backend.request)) as url:
            client = BackendClient(url,2,backend.code,backend.key)
            assert not SyncWorker(store,client).run_once()
            assert ResultWorker(store,client).run_once()
            assert backend.status == 'CANCELLED'
            assert SyncWorker(store,client).run_once()
            assert store.query('SELECT synced FROM collection_attempts')[0][0] == 1
    finally:
        store.close()
