"""Existing device API contracts and persistent, identity-checked delivery."""

from datetime import datetime, timezone
import json
import math
import secrets
import time

from .http import HttpClient, HttpError, UnsentError

FIELDS = ('temperature','humidity','soilMoisture','lightIntensity','ph','waterLevel','batteryVoltage')
ACKED = {'ACCEPTED','DUPLICATE'}
# Scheduled missions reject sync; acknowledge uncertain-launch cancellation first.
_UNREPORTED_CANCELLATIONS = """SELECT id FROM missions WHERE state<>'REPORTED'
    AND json_extract(result,'$.status')='CANCELLED'"""


def encode(value):
    return json.dumps(value,ensure_ascii=True,allow_nan=False,separators=(',',':'),sort_keys=True)


class BackendClient:
    def __init__(self, base_url, gateway_id, gateway_code, api_key, http=None):
        self.base_url = base_url.rstrip('/')
        self.gateway_id = gateway_id
        self.root = f'/api/device/gateways/{gateway_id}'
        self._headers = {'X-Gateway-Code':gateway_code,'X-Api-Key':api_key}
        self.http = http or HttpClient()

    def request(self, method, path, body=None):
        if not path.startswith('/api/device/') or '://' in path or '..' in path:
            raise ValueError('invalid device API path')
        response = self.http.request(method,self.base_url+path,headers=self._headers,body=body)
        if not isinstance(response,dict) or response.get('success') is not True or 'data' not in response:
            raise ValueError('invalid backend envelope')
        return response['data']

    def heartbeat(self):
        result = self.request('POST',self.root+'/heartbeat')
        if not isinstance(result,dict) or result.get('gatewayId') != self.gateway_id or result.get('status') != 'ONLINE' or not result.get('lastSeenAt'):
            raise ValueError('invalid heartbeat acknowledgment')
        return result


def _failure(store, row, error, now):
    attempts = row['attempts'] + 1
    hold = isinstance(error,HttpError) and error.status in (400,401,403,404,409,422)
    delay = min(300,5 * 2**min(attempts-1,6))
    if isinstance(error,HttpError) and error.retry_after is not None:
        delay = max(delay,min(error.retry_after,3600))
    store.execute('UPDATE outbox SET state=?,attempts=?,next_attempt=?,error=? WHERE id=?',
                  ('HELD' if hold else 'PENDING',attempts,now+delay,type(error).__name__,row['id']))


class SyncWorker:
    def __init__(self, store, backend):
        self.store,self.backend = store,backend

    def _new_batch(self):
        self.store.check_space(ingestion=False)
        with self.store.transaction() as db:
            # Filter reservations in SQLite before limiting the batch, including held requests.
            rows = db.execute(f"""SELECT m.* FROM measurements m WHERE synced=0
                AND (mission_id IS NULL OR mission_id NOT IN ({_UNREPORTED_CANCELLATIONS})) AND NOT EXISTS (
                SELECT 1 FROM outbox o, json_each(o.members,'$.measurements') ref
                WHERE o.kind='sync' AND o.state<>'DONE' AND ref.value=m.record_id
                ) ORDER BY m.rowid LIMIT 100""").fetchall()
            attempts = db.execute(f"""SELECT a.* FROM collection_attempts a WHERE synced=0
                AND mission_id NOT IN ({_UNREPORTED_CANCELLATIONS}) AND NOT EXISTS (
                SELECT 1 FROM outbox o, json_each(o.members,'$.attempts') ref
                WHERE o.kind='sync' AND o.state<>'DONE'
                AND json_extract(ref.value,'$[0]')=a.mission_id
                AND json_extract(ref.value,'$[1]')=a.sensor_node_id
                AND json_extract(ref.value,'$[2]')=a.attempt_no
                ) ORDER BY mission_id,attempt_no LIMIT 100""").fetchall()
            if not rows and not attempts:
                return None
            mission = rows[0]['mission_id'] if rows else attempts[0]['mission_id']
            rows = [r for r in rows if r['mission_id'] == mission][:100]
            attempts = [r for r in attempts if r['mission_id'] == mission][:100]
            key = 'pi-'+secrets.token_hex(16)
            body = {'batchKey':key,'missionId':mission,'records':[],'unresolvedRecords':[],
                    'collectionResults':[json.loads(r['payload']) for r in attempts]}
            for row in rows:
                raw = json.loads(row['payload'])
                values = [(field,raw.get(field)) for field in FIELDS if raw.get(field) is not None]
                measured = raw.get('measuredAt')
                known = raw.get('timeSynced') is True and type(measured) is int and 1577836800 <= measured <= 4102444800
                collected = datetime.fromisoformat(row['collected_at'].replace('Z','+00:00'))
                if not known or not values or measured > collected.timestamp():
                    body['unresolvedRecords'].append({'sensorNodeCode':row['device_code'],
                        'sourceRecordKey':row['record_id'],'payload':raw})
                else:
                    stamp = datetime.fromtimestamp(measured,timezone.utc).isoformat().replace('+00:00','Z')
                    for field,value in values:
                        if type(value) not in (int,float) or not math.isfinite(value):
                            raise ValueError('invalid persisted sensor number')
                        body['records'].append({'sensorNodeCode':row['device_code'],'channelCode':field,
                            'sourceRecordKey':row['record_id'],'value':value,'measuredAt':stamp,
                            'collectedAt':row['collected_at'],'qualityStatus':'VALID'})
            members = {'measurements':[r['record_id'] for r in rows],
                       'attempts':[[r['mission_id'],r['sensor_node_id'],r['attempt_no']] for r in attempts]}
            db.execute("INSERT INTO outbox(kind,identity,mission_id,payload,members) VALUES ('sync',?,?,?,?)",
                       (key,mission,encode(body),encode(members)))
            return db.execute('SELECT * FROM outbox WHERE identity=?',(key,)).fetchone()

    @staticmethod
    def _validate(body, response):
        if not isinstance(response,dict) or response.get('batchKey') != body['batchKey']:
            raise ValueError('sync batch identity mismatch')
        verified = {}
        for group in ('records','unresolvedRecords','collectionResults'):
            outcomes = response.get(group)
            if outcomes is None and not body[group]:
                outcomes = []
            if not isinstance(outcomes,list) or len(outcomes) != len(body[group]):
                raise ValueError('missing sync outcomes')
            indexed = {}
            for item in outcomes:
                if not isinstance(item,dict) or type(item.get('index')) is not int:
                    raise ValueError('invalid sync outcome')
                index = item['index']
                if index in indexed or not 0 <= index < len(body[group]):
                    raise ValueError('duplicate or out-of-range sync index')
                expected = body[group][index]
                keys = ('sensorNodeId','attemptNo') if group == 'collectionResults' else ('sourceRecordKey',)
                if any(item.get(k) != expected[k] for k in keys) or item.get('status') not in ACKED | {'REJECTED'}:
                    raise ValueError('sync outcome identity/status mismatch')
                indexed[index] = item['status']
            verified[group] = indexed
        return verified

    def run_once(self, now=None):
        now = time.time() if now is None else now
        due = self.store.query(f"""SELECT * FROM outbox WHERE kind='sync' AND state='PENDING' AND next_attempt<=?
            AND (mission_id IS NULL OR mission_id NOT IN ({_UNREPORTED_CANCELLATIONS}))
            ORDER BY next_attempt,id LIMIT 1""",(now,))
        row = due[0] if due else self._new_batch()
        if row is None:
            return False
        body = json.loads(row['payload'])
        try:
            response = self.backend.request('POST',self.backend.root+'/sync',body)
            verified = self._validate(body,response)
            complete = all(status in ACKED for values in verified.values() for status in values.values())
            with self.store.transaction() as db:
                for record_id in json.loads(row['members'])['measurements']:
                    statuses = [verified[group][i] for group in ('records','unresolvedRecords')
                                for i,item in enumerate(body[group]) if item['sourceRecordKey'] == record_id]
                    if statuses and all(status in ACKED for status in statuses):
                        db.execute('UPDATE measurements SET synced=1 WHERE record_id=?',(record_id,))
                for i,item in enumerate(body['collectionResults']):
                    if verified['collectionResults'][i] in ACKED:
                        db.execute('UPDATE collection_attempts SET synced=1 WHERE mission_id=? AND sensor_node_id=? AND attempt_no=?',
                                   (body['missionId'],item['sensorNodeId'],item['attemptNo']))
                db.execute('UPDATE outbox SET state=?,response=?,error=NULL,attempts=attempts+1,next_attempt=? WHERE id=?',
                           ('DONE' if complete else 'PENDING',encode(response),now+min(300,5*2**min(row['attempts'],6)),row['id']))
            return complete
        except (OSError,ValueError,TypeError,KeyError) as error:
            _failure(self.store,row,error,now)
            return False


class ResultWorker:
    def __init__(self,store,backend):
        self.store,self.backend = store,backend

    def run_once(self,now=None):
        now = time.time() if now is None else now
        rows = self.store.query("SELECT * FROM outbox WHERE kind='result' AND state='PENDING' AND next_attempt<=? ORDER BY id LIMIT 1",(now,))
        if not rows:
            for mission in self.store.query("SELECT * FROM missions WHERE result IS NOT NULL AND state NOT IN ('REPORTED','RECOVERY_REQUIRED')"):
                identity = f"result-{mission['id']}"
                if self.store.query('SELECT 1 FROM outbox WHERE identity=?',(identity,)):
                    continue
                result = json.loads(mission['result'])
                status = result['status']
                if status not in ('COMPLETED','FAILED','CANCELLED'):
                    raise ValueError('invalid terminal status')
                if status != 'CANCELLED' and (self.store.query('SELECT 1 FROM measurements WHERE mission_id=? AND synced=0 LIMIT 1',(mission['id'],)) or self.store.query('SELECT 1 FROM collection_attempts WHERE mission_id=? AND synced=0 LIMIT 1',(mission['id'],))):
                    continue
                body = ({'failureReason':result.get('failureReason') or 'GATEWAY_MISSION_FAILED','note':None}
                        if status == 'FAILED' else {'reason':result.get('reason')} if status == 'CANCELLED' else None)
                payload = {'status':status,'body':body,'path':self.backend.root+f"/missions/{mission['id']}/"+{'COMPLETED':'complete','FAILED':'fail','CANCELLED':'cancel'}[status]}
                self.store.execute("INSERT INTO outbox(kind,identity,mission_id,payload,members) VALUES ('result',?,?,?,'{}')",(identity,mission['id'],encode(payload)))
                rows = self.store.query('SELECT * FROM outbox WHERE identity=?',(identity,))
                break
        if not rows:
            return False
        row = rows[0]
        body = json.loads(row['payload'])
        try:
            result = self.backend.request('POST',body['path'],body['body'])
            if not isinstance(result,dict) or result.get('id') != row['mission_id'] or result.get('status') != body['status']:
                raise ValueError('terminal acknowledgment mismatch')
            with self.store.transaction() as db:
                db.execute("UPDATE outbox SET state='DONE',response=? WHERE id=?",(encode(result),row['id']))
                db.execute("UPDATE missions SET state='REPORTED' WHERE id=?",(row['mission_id'],))
            return True
        except (OSError,ValueError,TypeError,KeyError) as error:
            _failure(self.store,row,error,now)
            return False


class TelemetryWorker:
    def __init__(self,store,backend):
        self.store,self.backend = store,backend
        # A process death while POST was in flight is not proof it failed at the server.
        store.execute("UPDATE telemetry SET state='UNCERTAIN' WHERE state='SENDING'")

    def run_once(self,now=None):
        now = time.time() if now is None else now
        rows = self.store.query("SELECT * FROM telemetry WHERE state='PENDING' AND next_attempt<=? ORDER BY id LIMIT 1",(now,))
        if not rows:
            return False
        row = rows[0]
        payload = json.loads(row['payload'])
        self.store.execute("UPDATE telemetry SET state='SENDING' WHERE id=?",(row['id'],))
        try:
            response = self.backend.request('POST','/api/device/telemetry',payload)
            if not isinstance(response,dict) or type(response.get('id')) is not int or response.get('missionId') != row['mission_id']:
                raise ValueError('invalid telemetry acknowledgment')
            self.store.execute("UPDATE telemetry SET state='SENT' WHERE id=?",(row['id'],))
            return True
        except (OSError,ValueError,TypeError,KeyError) as error:
            state = 'UNCERTAIN'
            if isinstance(error,UnsentError) or isinstance(error,HttpError) and error.status == 429:
                state = 'PENDING'
            elif isinstance(error,HttpError) and error.status in (400,401,403,404,409,413,422):
                state = 'HELD'
            attempts = row['attempts']+1
            delay = min(300,5*2**min(attempts-1,6))
            if isinstance(error,HttpError) and error.retry_after is not None:
                delay = max(delay,min(error.retry_after,3600))
            self.store.execute('UPDATE telemetry SET state=?,attempts=?,next_attempt=? WHERE id=?',
                               (state,attempts,now+delay,row['id']))
            return False
