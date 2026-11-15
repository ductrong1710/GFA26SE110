import time

import pytest

from gateway.tello import FlightError, SafetyController, parse_telemetry
from mocks.tello import TelloSimulator


def controller(sim, **kwargs):
    safety = SafetyController("127.0.0.1", command_address=sim.address, command_port=0,
                              state_port=0, command_timeout=.3, flight_timeout=.5,
                              telemetry_timeout=.25, settle_seconds=.12, stable_samples=3,
                              command_gap=.01, **kwargs)
    sim.telemetry_address = safety.state_address
    return safety


def test_partial_telemetry_does_not_reuse_old_landing_fields():
    assert parse_telemetry(b"bat:80;") == {"bat": 80}
    assert parse_telemetry(b"h:0;tof:10;vgz:0;bat:80;")["h"] == 0
    with pytest.raises(FlightError):
        parse_telemetry(b"h:0;h:100;")


def test_default_ground_mode_blocks_takeoff_but_allows_queries_and_video():
    with TelloSimulator() as sim:
        safety = controller(sim)
        try:
            safety.prepare()
            safety.set_stream(True)
            with pytest.raises(FlightError):
                safety.takeoff("mission", operator_confirmed=True)
            assert "takeoff" not in sim.commands
            assert "streamon" in sim.commands
        finally:
            safety.close()


def test_low_battery_and_missing_operator_permission_block_takeoff():
    with TelloSimulator(battery=20) as sim:
        safety = controller(sim, ground_test=False)
        try:
            safety.prepare()
            with pytest.raises(FlightError):
                safety.takeoff("mission", operator_confirmed=True)
            sim.battery = 90
            time.sleep(.05)
            with pytest.raises(FlightError):
                safety.takeoff("mission")
            assert "takeoff" not in sim.commands
        finally:
            safety.close()


def test_timeout_takeoff_is_never_retried_and_requires_recovery():
    with TelloSimulator(drop={"takeoff"}) as sim:
        safety = controller(sim, ground_test=False)
        try:
            safety.prepare()
            with pytest.raises(FlightError):
                safety.takeoff("mission", operator_confirmed=True)
            assert safety.airborne_possible
            with pytest.raises(FlightError):
                safety.move("mission", "forward", 20)
            assert sim.commands.count("takeoff") == 1
            safety.land("mission")
            assert not safety.airborne_possible
        finally:
            safety.close()


def test_wrong_owner_cannot_move_and_watchdog_lands_on_stale_telemetry():
    with TelloSimulator() as sim:
        safety = controller(sim, ground_test=False)
        try:
            safety.prepare()
            safety.takeoff("mission", operator_confirmed=True)
            with pytest.raises(FlightError):
                safety.move("other", "forward", 20)
            assert "forward 20" not in sim.commands
            sim.telemetry_enabled = False
            deadline = time.monotonic() + 1
            while "land" not in sim.commands and time.monotonic() < deadline:
                time.sleep(.02)
            assert "land" in sim.commands
            assert safety.airborne_possible  # Sending land cannot prove landing.
        finally:
            safety.close()


def test_land_ack_without_ground_evidence_keeps_flight_lock():
    with TelloSimulator(ignore_land=True) as sim:
        safety = controller(sim, ground_test=False)
        try:
            safety.prepare()
            safety.takeoff("mission", operator_confirmed=True)
            with pytest.raises(FlightError):
                safety.land("mission")
            assert safety.airborne_possible
        finally:
            safety.close()


def test_route_move_requires_fresh_altitude_and_landing_releases_owner():
    with TelloSimulator() as sim:
        safety = controller(sim, ground_test=False)
        try:
            safety.prepare()
            safety.takeoff("mission", operator_confirmed=True)
            safety.altitude("mission", 100)
            safety.move("mission", "forward", 20)
            safety.land("mission")
            assert not safety.airborne_possible
            assert safety.owner is None
        finally:
            safety.close()


def test_rotation_and_abort_keep_owner_and_ground_guards():
    with TelloSimulator() as sim:
        safety = controller(sim,ground_test=False)
        try:
            with pytest.raises(FlightError):
                safety.rotate('mission','cw',90)
            safety.prepare()
            safety.takeoff('mission',operator_confirmed=True)
            safety.altitude('mission',100)
            safety.rotate('mission','cw',90)
            assert 'cw 90' in sim.commands
            with pytest.raises(FlightError):
                safety.abort('other')
            safety.abort('mission')
            assert not safety.airborne_possible
        finally:
            safety.close()


def test_configured_battery_floor_cannot_be_bypassed():
    with TelloSimulator(battery=50) as sim:
        safety = controller(sim, ground_test=False, min_battery=60)
        try:
            safety.prepare()
            with pytest.raises(FlightError):
                safety.takeoff("mission", operator_confirmed=True)
            assert "takeoff" not in sim.commands
        finally:
            safety.close()


def test_stale_telemetry_interrupts_pending_movement_before_command_deadline():
    with TelloSimulator(drop={"forward 20"}) as sim:
        safety = controller(sim, ground_test=False)
        try:
            safety.prepare()
            safety.takeoff("mission", operator_confirmed=True)
            safety.altitude("mission", 100)
            sim.telemetry_enabled = False
            started = time.monotonic()
            with pytest.raises(FlightError):
                safety.move("mission", "forward", 20)
            assert time.monotonic() - started < .45
        finally:
            safety.close()
