"""Exercise the real gateway with loopback-only HTTP/UDP fixtures."""
from contextlib import ExitStack
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from gateway.backend import BackendClient, ResultWorker, SyncWorker, TelemetryWorker
from gateway.missions import MissionEngine
from gateway.sensors import Collector, RegistrationRegistry, make_server
from gateway.storage import Store
from gateway.tello import SafetyController
from mocks.backend import BackendMock
from mocks.sensor import SensorMock, LoopbackSensorHttp, mock_server, serving
from mocks.tello import TelloSimulator


def run():
    with TemporaryDirectory(prefix='pi-gateway-demo-') as directory, ExitStack() as stack:
        path = Path(directory)/'gateway.db'
        store = Store(path, min_free_bytes=0)
        stack.callback(store.close)
        sensor, backend = SensorMock(), BackendMock()
        registry = RegistrationRegistry(store, [sensor.device], '127.0.0.0/8')
        registration_url = stack.enter_context(serving(make_server(registry, '127.0.0.1', 0)))
        sensor_url = stack.enter_context(serving(mock_server(sensor.request)))
        backend_url = stack.enter_context(serving(mock_server(backend.request)))
        sensor.register(registration_url)
        client = BackendClient(backend_url, 2, backend.code, backend.key)
        collector = Collector(store, registry, LoopbackSensorHttp(sensor_url))
        simulator = stack.enter_context(TelloSimulator())
        safety = SafetyController('127.0.0.1', command_address=simulator.address,
            command_port=0, state_port=0, ground_test=False, command_timeout=1,
            flight_timeout=2, telemetry_timeout=.5, settle_seconds=.15,
            stable_samples=3, command_gap=.01)
        stack.callback(safety.close)
        simulator.telemetry_address = safety.state_address
        engine = MissionEngine(store, client, safety, collector)
        client.heartbeat()
        engine.download()
        ready = engine.ready()['state']
        backend.approved = True
        outcome = engine.launch(operator_confirmed=True)
        if outcome['status'] != 'COMPLETED':
            raise RuntimeError('simulated mission failed')
        if not SyncWorker(store, client).run_once():
            raise RuntimeError('sensor synchronization failed')
        if not TelemetryWorker(store, client).run_once():
            raise RuntimeError('telemetry delivery failed')
        if not ResultWorker(store, client).run_once():
            raise RuntimeError('mission result delivery failed')
        # An independent connection proves committed state can be read after reopening.
        reopened = Store(path, min_free_bytes=0)
        try:
            report = {
                'mode':'simulation', 'node':sensor.device['deviceCode'],
                'nodePending':len(sensor.records), 'readyState':ready,
                'consumes':backend.consumes, 'takeoffs':simulator.commands.count('takeoff'),
                'landed':not safety.airborne_possible,
                'mission':reopened.query('SELECT state FROM missions')[0][0],
                'backendMission':backend.status,
                'syncedMeasurements':reopened.query('SELECT COUNT(*) FROM measurements WHERE synced=1')[0][0],
                'syncedAttempts':reopened.query('SELECT COUNT(*) FROM collection_attempts WHERE synced=1')[0][0],
                'channelReadings':len(backend.readings), 'archivedSamples':len(backend.archives),
                'telemetry':len(backend.telemetry), 'reopened':True,
            }
        finally:
            reopened.close()
    print(json.dumps(report))
    return report


if __name__ == '__main__':
    run()
