import json

import pytest

from gateway.backend import BackendClient, SyncWorker, ResultWorker, TelemetryWorker
from gateway.http import HttpError
from gateway.storage import Store


@pytest.fixture
def store(tmp_path):
    value = Store(tmp_path/'data.db', min_free_bytes=0)
    value.execute("INSERT INTO nodes VALUES ('NODE',1,1,'192.168.4.2','{}',0)")
    yield value
    value.close()


def measurement(store, sequence=1, mission=None, synced=True):
    payload = {'deviceCode':'NODE','recordId':f'NODE-{sequence}','sequence':sequence,
               'timeSynced':synced,'measuredAt':1735689600 if synced else 0,
               'temperature':23,'humidity':60,'soilMoisture':None}
    store.execute('INSERT INTO measurements(record_id,device_code,sequence,payload,collected_at,mission_id) VALUES (?,?,?,?,?,?)',
                  (payload['recordId'],'NODE',sequence,json.dumps(payload),'2025-01-01T00:01:00Z',mission))


class FakeBackend:
    root = '/api/device/gateways/7'
    gateway_id = 7

    def __init__(self):
        self.calls = []
        self.fail = None
        self.reject = False
        self.corrupt = False

    def request(self, method, path, body=None):
        self.calls.append((method,path,body))
        if self.fail:
            raise self.fail
        if path.endswith('/complete'):
            return {'id':body['id'] if body else 1, 'status':'COMPLETED'}
        if path.endswith('/cancel'):
            return {'id':1, 'status':'CANCELLED'}
        if path.endswith('/telemetry'):
            return {'id':1, **body}
        result = {'batchKey':body['batchKey']}
        for group in ('records','unresolvedRecords','collectionResults'):
            result[group] = []
            for index,item in enumerate(body[group]):
                identity = ({'sensorNodeId':item['sensorNodeId'],'attemptNo':item['attemptNo']}
                            if group == 'collectionResults' else {'sourceRecordKey':item['sourceRecordKey']})
                result[group].append({'index':index, **identity,
                                      'status':'REJECTED' if self.reject and index == 1 else 'ACCEPTED'})
        if self.corrupt and result['records']:
            result['records'][0]['sourceRecordKey'] = 'alien'
        return result


def test_lost_response_retry_uses_identical_persisted_batch_after_reopen(store):
    measurement(store)
    backend = FakeBackend()
    worker = SyncWorker(store, backend)
    backend.fail = TimeoutError('lost response')
    assert worker.run_once(now=10) is False
    first = backend.calls[-1][2]
    assert not store.query('SELECT synced FROM measurements')[0][0]
    worker = SyncWorker(store, backend)
    backend.fail = None
    assert worker.run_once(now=1000)
    assert first == backend.calls[-1][2]
    assert store.query('SELECT synced FROM measurements')[0][0] == 1


def test_partial_rejection_keeps_record_and_exact_batch(store):
    measurement(store)
    backend = FakeBackend()
    backend.reject = True
    worker = SyncWorker(store,backend)
    assert worker.run_once(now=10) is False
    body = backend.calls[-1][2]
    assert len(body['records']) == 2
    assert not store.query('SELECT synced FROM measurements')[0][0]
    backend.reject = False
    assert worker.run_once(now=1000)
    assert body == backend.calls[-1][2]


def test_wrong_identity_and_http_success_not_ack(store):
    measurement(store)
    backend = FakeBackend()
    backend.corrupt = True
    assert not SyncWorker(store,backend).run_once(now=10)
    assert not store.query('SELECT synced FROM measurements')[0][0]


def test_unresolved_preserves_original_and_no_fake_timestamp(store):
    measurement(store,synced=False)
    backend = FakeBackend()
    assert SyncWorker(store,backend).run_once(now=10)
    body = backend.calls[-1][2]
    assert body['records'] == []
    assert body['unresolvedRecords'][0]['payload']['measuredAt'] == 0
    assert store.query('SELECT synced FROM measurements')[0][0] == 1


def test_batches_do_not_mix_missions(store):
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'LANDED',NULL,'2025-01-01')")
    measurement(store,1,None)
    measurement(store,2,1)
    backend = FakeBackend()
    worker = SyncWorker(store,backend)
    assert worker.run_once(now=10)
    assert worker.run_once(now=11)
    assert {c[2]['missionId'] for c in backend.calls} == {None,1}
    for _,_,body in backend.calls:
        assert len({x['sourceRecordKey'] for x in body['records']}) == 1


@pytest.mark.parametrize('status',[401,403,409,422])
def test_configuration_errors_held_without_dropping_data(store,status):
    measurement(store)
    backend = FakeBackend()
    backend.fail = HttpError(status)
    worker = SyncWorker(store,backend)
    assert not worker.run_once(now=10)
    assert store.query('SELECT state FROM outbox')[0][0] == 'HELD'
    assert not worker.run_once(now=1000)
    assert len(backend.calls) == 1


def test_terminal_result_waits_for_sensor_and_attempt_ack(store):
    result = json.dumps({'status':'COMPLETED'})
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'LANDED',?,'2025-01-01')",(result,))
    measurement(store,mission=1)
    attempt = {'sensorNodeId':1,'attemptNo':1,'status':'SUCCESS','recordsReceived':1}
    store.execute('INSERT INTO collection_attempts VALUES (1,1,1,?,0)',(json.dumps(attempt),))
    backend = FakeBackend()
    terminal = ResultWorker(store,backend)
    assert not terminal.run_once(now=10)
    assert SyncWorker(store,backend).run_once(now=11)
    assert terminal.run_once(now=12)
    assert store.query('SELECT state FROM missions')[0][0] == 'REPORTED'
    assert not terminal.run_once(now=13)


def test_telemetry_uncertain_delivery_is_not_blindly_retried(store):
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'LANDED',NULL,'2025-01-01')")
    store.execute("INSERT INTO telemetry(mission_id,payload) VALUES (1,?)",(json.dumps({'missionId':1}),))
    backend = FakeBackend()
    backend.fail = TimeoutError()
    worker = TelemetryWorker(store,backend)
    assert not worker.run_once()
    backend.fail = None
    assert not worker.run_once()
    assert len(backend.calls) == 1
    assert store.query('SELECT state FROM telemetry')[0][0] == 'UNCERTAIN'


def test_recovery_cancellation_precedes_sync_of_a_possibly_unstarted_mission(store):
    result = json.dumps({'status':'CANCELLED','reason':'OPERATOR_RECONCILED_RECOVERY'})
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'RESULT_PENDING',?,'2025-01-01')",(result,))
    measurement(store, mission=1)
    attempt = {'sensorNodeId':1,'attemptNo':1,'status':'SKIPPED','recordsReceived':0}
    store.execute('INSERT INTO collection_attempts VALUES (1,1,1,?,0)',(json.dumps(attempt),))
    backend = FakeBackend()
    sync = SyncWorker(store,backend)
    assert not sync.run_once(now=10)
    assert not backend.calls
    # A blocked cancellation must not starve unrelated offline data.
    measurement(store, sequence=2)
    assert sync.run_once(now=11)
    assert backend.calls[-1][2]['missionId'] is None
    assert ResultWorker(store,backend).run_once(now=12)
    assert backend.calls[-1][1].endswith('/cancel')
    assert sync.run_once(now=13)
    assert store.query('SELECT synced FROM collection_attempts')[0][0] == 1


def test_backend_checks_envelope_and_does_not_use_jwt():
    class HTTP:
        def request(self,method,url,headers=None,body=None):
            assert headers == {'X-Gateway-Code':'GW','X-Api-Key':'secret'}
            return {'success':False,'data':{}}
    client = BackendClient('https://backend.example',7,'GW','secret',HTTP())
    with pytest.raises(ValueError):
        client.request('POST',client.root+'/heartbeat')


def test_held_backlog_cannot_starve_new_measurements(store):
    with store.transaction() as db:
        for i in range(1,1002):
            payload = {'deviceCode':'NODE','recordId':f'NODE-{i}','sequence':i,
                       'timeSynced':False,'measuredAt':0,'temperature':None}
            db.execute('INSERT INTO measurements(record_id,device_code,sequence,payload,collected_at) VALUES (?,?,?,?,?)',
                       (f'NODE-{i}','NODE',i,json.dumps(payload),'2025-01-01T00:01:00Z'))
        db.execute("INSERT INTO outbox(kind,identity,payload,members,state) VALUES ('sync','held','{}',?,'HELD')",
                   (json.dumps({'measurements':[f'NODE-{i}' for i in range(1,1001)],'attempts':[]}),))
    backend = FakeBackend()
    assert SyncWorker(store,backend).run_once(now=10)
    assert backend.calls[-1][2]['unresolvedRecords'][0]['sourceRecordKey'] == 'NODE-1001'


def test_sensor_success_survives_video_upload_failure(store,tmp_path):
    import hashlib
    from gateway.video import VideoUploader
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'RESULT_PENDING',NULL,'2025-01-01')")
    measurement(store,mission=1)
    assert SyncWorker(store,FakeBackend()).run_once(now=10)
    path = tmp_path/'video.mp4'
    path.write_bytes(b'fixture')
    metadata = {'totalBytes':7,'sha256':hashlib.sha256(b'fixture').hexdigest()}
    store.execute("INSERT INTO videos(mission_id,path,state,metadata) VALUES (1,?,'FINALIZED',?)",(str(path),json.dumps(metadata)))
    class Offline:
        def request(self,*args,**kwargs):
            raise TimeoutError()
    assert not VideoUploader(store,Offline(),contract_enabled=True).upload_next()
    assert store.query('SELECT synced FROM measurements')[0][0] == 1
    assert store.query('SELECT state FROM videos')[0][0] == 'UPLOADING'
    assert path.read_bytes() == b'fixture'


def test_lost_terminal_response_replays_same_result(store):
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'RESULT_PENDING',?,'2025-01-01')",(json.dumps({'status':'COMPLETED'}),))
    backend = FakeBackend()
    backend.fail = TimeoutError()
    assert not ResultWorker(store,backend).run_once(now=10)
    first = backend.calls[-1]
    backend.fail = None
    assert ResultWorker(store,backend).run_once(now=1000)
    assert backend.calls[-1] == first
    assert store.query('SELECT state FROM missions')[0][0] == 'REPORTED'


def test_ingestion_quota_leaves_space_for_backlog_delivery(store):
    measurement(store)
    store.max_bytes = 1
    with pytest.raises(OSError):
        store.check_space()
    backend = FakeBackend()
    assert SyncWorker(store,backend).run_once(now=10)
    assert store.query('SELECT synced FROM measurements')[0][0] == 1


def test_definitely_unsent_telemetry_can_retry(store):
    from gateway.http import UnsentError
    store.execute("INSERT INTO missions VALUES (1,'{}',1,'LANDED',NULL,'2025-01-01')")
    store.execute("INSERT INTO telemetry(mission_id,payload) VALUES (1,?)",(json.dumps({'missionId':1}),))
    backend = FakeBackend()
    backend.fail = UnsentError('network unavailable')
    worker = TelemetryWorker(store,backend)
    assert not worker.run_once(now=10)
    assert store.query('SELECT state FROM telemetry')[0][0] == 'PENDING'
    backend.fail = None
    assert not worker.run_once(now=11)
    assert worker.run_once(now=1000)
