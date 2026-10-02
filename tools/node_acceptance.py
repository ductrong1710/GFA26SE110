"""Acceptance checks against a REAL node. Never run ACK mode on uncollected real data.

Keep the real ESP32 collector off for these assertions; use a temporary Wi-Fi AP
with the configured credentials. Supply NODE_DEVICE_SECRET in the environment or
enter it at the hidden prompt. The script never prints the token.
"""
import argparse
import getpass
import http.client
import json
import os
from pathlib import Path
import time
from urllib.parse import urlsplit

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', required=True, help='Example: http://192.168.4.2')
    parser.add_argument('--device-code', default='SENSOR-001')
    parser.add_argument('--exercise-ack', action='store_true', help='ACK one DEMO record; requires >=2 pending')
    parser.add_argument('--set-time', action='store_true', help='Set current Unix time on the node')
    parser.add_argument('--snapshot', type=Path, help='Write a batch for a later reboot comparison')
    parser.add_argument('--compare', type=Path, help='Verify snapshot record IDs survived reboot')
    args = parser.parse_args()
    address = urlsplit(args.url)
    if address.scheme != 'http' or not address.hostname:
        parser.error('Use an http:// node URL')
    secret = os.environ.get('NODE_DEVICE_SECRET') or getpass.getpass('Node device secret: ')

    def request(path, body=None, expected=200, token=secret, raw=None):
        connection = http.client.HTTPConnection(address.hostname, address.port or 80, timeout=5)
        headers = {'X-Device-Code': args.device_code, 'X-Device-Token': token}
        payload = raw if raw is not None else (json.dumps(body) if body is not None else None)
        if payload is not None: headers['Content-Type'] = 'application/json'
        try:
            connection.request('POST' if payload is not None else 'GET', path, payload, headers)
            response = connection.getresponse()
            data = response.read(12289)
            assert len(data) <= 12288, 'Oversized response'
            assert response.status == expected, f'{path}: HTTP {response.status}, expected {expected}'
            result = json.loads(data)
            assert result.get('success') is (expected == 200), f'{path}: unexpected success flag'
            return result
        finally:
            connection.close()

    health = request('/api/node/health')
    assert health['deviceCode'] == args.device_code and health['storageHealthy']
    info = request('/api/node/info')
    assert info['deviceCode'] == args.device_code and info['pendingRecords'] >= 1, 'Wait for at least one sample'
    first = request('/api/node/data?limit=10')
    again = request('/api/node/data?limit=10')
    ids = [r['recordId'] for r in first['records']]
    assert first['count'] == len(ids) <= 10
    assert ids == [r['recordId'] for r in again['records']], 'GET changed pending batch (disable gateway collector)'
    sequences = [r['sequence'] for r in first['records']]
    assert sequences == sorted(sequences) and len(set(ids)) == len(ids)
    for record in first['records']:
        assert record['recordId'] == f"{args.device_code}-{record['sequence']}"
        assert record['state'] == 'PENDING'
        assert record['timeSynced'] or record['measuredAt'] == 0
        assert all(key in record for key in ('temperature','humidity','soilMoisture','lightIntensity','ph','waterLevel','batteryVoltage','uptimeMs'))
    assert request('/api/node/data?limit=999')['count'] <= 10
    request('/api/node/data?limit=-1', expected=400)
    request('/api/node/info', expected=401, token='intentionally-wrong-test-token')
    request('/api/node/ack', expected=400, raw='{')
    request('/api/node/ack', expected=400, raw='{} trailing')
    request('/api/node/ack', {'recordIds':['valid',123]}, expected=400)
    request('/api/node/ack', {'recordIds':[]}, expected=400)
    request('/api/node/ack', {'recordIds':['UNKNOWN']*11}, expected=400)
    request('/api/node/ack', expected=413, raw='x'*1025)
    request('/api/node/time', {'unixTime':1}, expected=400)
    assert request('/api/node/ack', {'recordIds':['UNKNOWN-123']})['ackedCount'] == 0
    if args.snapshot:
        args.snapshot.write_text(json.dumps(first,indent=2),encoding='utf-8')
    if args.compare:
        previous = json.loads(args.compare.read_text(encoding='utf-8'))
        assert set(r['recordId'] for r in previous['records']).issubset(ids), 'Snapshot records missing after reboot'
    if args.set_time:
        assert request('/api/node/time', {'unixTime':int(time.time())})['timeSynced']
    if args.exercise_ack:
        assert health['demoMode'] and len(ids) >= 2, 'Use DEMO_MODE with at least two records'
        assert request('/api/node/ack', {'recordIds':[ids[0]]})['ackedCount'] == 1
        assert request('/api/node/ack', {'recordIds':[ids[0]]})['ackedCount'] == 0
        remaining = [r['recordId'] for r in request('/api/node/data?limit=10')['records']]
        assert ids[0] not in remaining and all(i in remaining for i in ids[1:])
    print('Node acceptance checks passed against the requested device.')

if __name__ == '__main__':
    main()
