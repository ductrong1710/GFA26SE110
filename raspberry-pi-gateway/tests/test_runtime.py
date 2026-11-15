import json
from pathlib import Path
from unittest.mock import Mock
import pytest

from gateway.config import Settings
from gateway.main import Runtime, exclusive, main
from gateway.network import check_route

def config(tmp_path, monkeypatch, **changes):
    values = json.loads((Path(__file__).parents[1]/"config/gateway.example.json").read_text())
    values.update(data_dir=str(tmp_path), **changes)
    monkeypatch.setenv("GATEWAY_API_KEY", "fake-device-key")
    monkeypatch.setenv("SENSOR_001_TOKEN", "dummy-node")
    values["backend_url"] = "https://backend.example"
    path = tmp_path/"config.json"
    path.write_text(json.dumps(values))
    return Settings.load(path)

def test_safe_defaults_and_secrets_not_in_repr(tmp_path, monkeypatch):
    cfg = config(tmp_path,monkeypatch)
    assert cfg.ground_test and cfg.battery_min == 25
    assert "fake-device-key" not in repr(cfg)
    assert "dummy-node" not in repr(cfg)
    monkeypatch.setenv("GROUND_TEST_MODE","maybe")
    with pytest.raises(ValueError):
        Settings.load(tmp_path/"config.json")

def test_wrong_network_or_same_radio_refused(tmp_path, monkeypatch):
    with pytest.raises(ValueError):
        config(tmp_path,monkeypatch,sensor_interface="wlan0")
    with pytest.raises(ValueError):
        config(tmp_path,monkeypatch,sensor_ip="192.168.10.2")

def test_wrong_kernel_route_refused():
    with pytest.raises(OSError):
        check_route("192.168.10.1", "wlan0", "192.168.10.2",
                    lambda *a,**k: type("Result",(),{"stdout":'[{"dev":"wlan1","prefsrc":"192.168.4.1"}]'})())

def test_service_tick_never_consumes_or_takes_off(tmp_path,monkeypatch):
    runtime = Runtime(config(tmp_path,monkeypatch))
    try:
        runtime.engine = Mock()
        runtime.backend.heartbeat = Mock()
        runtime.tick()
        runtime.engine.launch.assert_not_called()
        assert runtime.safety._device is None
    finally:
        runtime.close()

def test_exclusive_service_and_operator_cannot_overlap(tmp_path):
    with exclusive(tmp_path):
        with pytest.raises(RuntimeError):
            with exclusive(tmp_path):
                pass

def test_entrypoint_starts_simulation_only_by_default(monkeypatch):
    demo = Mock()
    import mocks.demo
    monkeypatch.setattr(mocks.demo,"run",demo)
    main([])
    demo.assert_called_once()


@pytest.mark.parametrize('requested',[False,True])
def test_ground_bench_records_only_a_mission_that_requests_video(tmp_path,monkeypatch,requested):
    runtime = Runtime(config(tmp_path,monkeypatch))
    runtime.store.execute("INSERT INTO missions VALUES (7,?,1,'READY',NULL,'2026-01-01')",
                          (json.dumps({'recordVideo':requested}),))
    runtime.safety = Mock(ground_test=True,airborne_possible=False)
    runtime.safety.snapshot.return_value = {}
    runtime.recorder = Mock()
    monkeypatch.setattr('gateway.main.Runtime',lambda *a,**k:runtime)
    monkeypatch.setattr('gateway.main.time.sleep',lambda _:None)
    argv = ['--config',str(tmp_path/'config.json'),'bench','--video-mission','7','--seconds','1']
    if requested:
        assert main(argv) == 0
        runtime.recorder.start.assert_called_once_with(7,requested=True)
        runtime.recorder.stop.assert_called_once()
    else:
        with pytest.raises(ValueError,match='recordVideo'):
            main(argv)
        runtime.recorder.start.assert_not_called()
    runtime.safety.takeoff.assert_not_called()


def test_aircraft_reconciliation_clears_recovery_only_after_ground_evidence(tmp_path, monkeypatch):
    from gateway.main import Aircraft
    aircraft = Aircraft(config(tmp_path, monkeypatch))
    aircraft._device = Mock(airborne_possible=True)
    aircraft.recover()
    aircraft._device.confirm_ground.side_effect = RuntimeError('stale')
    with pytest.raises(RuntimeError):
        aircraft.confirm_ground()
    assert aircraft.airborne_possible
    aircraft._device.confirm_ground.side_effect = lambda: setattr(aircraft._device,'airborne_possible',False)
    aircraft.confirm_ground()
    assert not aircraft.airborne_possible


def test_system_clock_must_be_synchronized_before_flight(tmp_path,monkeypatch):
    from gateway.main import Aircraft, MissionError
    aircraft = Aircraft(config(tmp_path,monkeypatch))
    aircraft._device = Mock()
    monkeypatch.setattr('gateway.main.clock_is_synced',lambda:False)
    with pytest.raises(MissionError):
        aircraft.preflight()
    aircraft._device.preflight.assert_not_called()


def test_service_restart_with_flight_intent_retains_recovery_without_udp(tmp_path,monkeypatch):
    cfg = config(tmp_path,monkeypatch)
    runtime = Runtime(cfg)
    runtime.store.execute("INSERT INTO missions VALUES (1,'{}',1,'TAKEOFF_INTENT',NULL,'2026-01-01')")
    runtime.close()
    runtime = Runtime(cfg)
    try:
        runtime.tick()
        assert runtime.store.query('SELECT state FROM missions')[0][0] == 'RECOVERY_REQUIRED'
        assert runtime.safety.airborne_possible and runtime.safety._device is None
    finally:
        runtime.close()
