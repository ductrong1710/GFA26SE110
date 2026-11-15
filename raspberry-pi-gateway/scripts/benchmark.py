"""Local bounded-memory backlog check. Run with the installed gateway Python."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import time
import tracemalloc

from gateway.backend import SyncWorker
from gateway.storage import Store


class Receiver:
    root = '/api/device/gateways/1'

    def request(self,method,path,body=None):
        assert method == 'POST' and path.endswith('/sync')
        assert len(body['records']) <= 700
        return {'batchKey':body['batchKey'], 'records':[
            {'index':i,'sourceRecordKey':row['sourceRecordKey'],'status':'ACCEPTED'}
            for i,row in enumerate(body['records'])], 'unresolvedRecords':[], 'collectionResults':[]}


def run(count):
    with TemporaryDirectory(prefix='gateway-backlog-') as directory:
        store = Store(Path(directory)/'benchmark.db',min_free_bytes=0)
        try:
            now = int(time.time())
            stamp = datetime.now(timezone.utc).isoformat()
            store.execute("INSERT INTO nodes VALUES ('NODE',1,1,'192.168.4.2','{}',0)")
            with store.transaction() as db:
                for sequence in range(1,count+1):
                    payload = {'deviceCode':'NODE','recordId':f'NODE-{sequence}','sequence':sequence,
                               'measuredAt':now-1,'timeSynced':True,'temperature':25,'humidity':60}
                    db.execute('INSERT INTO measurements(record_id,device_code,sequence,payload,collected_at) VALUES (?,?,?,?,?)',
                               (payload['recordId'],'NODE',sequence,json.dumps(payload),stamp))
            started = time.monotonic()
            tracemalloc.start()
            worker = SyncWorker(store,Receiver())
            batches = 0
            while worker.run_once():
                batches += 1
            _,peak = tracemalloc.get_traced_memory()
            tracemalloc.stop()
            assert store.query('SELECT COUNT(*) FROM measurements WHERE synced=1')[0][0] == count
            assert store.query('PRAGMA integrity_check')[0][0] == 'ok'
            return {'records':count,'batches':batches,'seconds':round(time.monotonic()-started,3),
                    'pythonPeakMiB':round(peak/1024**2,3),
                    'databaseMiB':round(sum(p.stat().st_size for p in Path(directory).iterdir())/1024**2,3),
                    'scope':'host Python allocations during sync; physical Pi RSS/CPU unverified'}
        finally:
            store.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--records',type=int,default=1000)
    args = parser.parse_args()
    if not 1 <= args.records <= 100000:
        parser.error('records must be 1–100000')
    print(json.dumps(run(args.records)))
