"""Run the offline service, bench checks, or one explicitly confirmed mission."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import json
import logging
import os
from pathlib import Path
import signal
import sqlite3
import threading
import time

from .backend import BackendClient, ResultWorker, SyncWorker, TelemetryWorker
from .config import Settings
from .missions import MissionEngine, MissionError
from .network import RoutedHttp, check_route, clock_is_synced
from .sensors import Collector, RegistrationRegistry, make_server
from .storage import Store
from .video import VideoRecorder, VideoUploader, MediaClient

LOG = logging.getLogger("gateway")


@contextmanager
def exclusive(directory):
    directory.mkdir(parents=True,exist_ok=True)
    with (directory/"process.lock").open("a+b") as lock:
        try:
            if os.name == "nt":
                import msvcrt
                lock.seek(0)
                msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1)
            else:
                import fcntl
                fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except OSError as exc:
            raise RuntimeError("another gateway process owns this data directory") from exc
        yield


class Aircraft:
    """Permit service startup while the aircraft and its interface are offline."""
    def __init__(self,settings):
        self.settings = settings
        self.ground_test = settings.ground_test
        self._device = None
        self._recovery = False

    @property
    def airborne_possible(self):
        return self._recovery or bool(self._device and self._device.airborne_possible)

    def open(self):
        if self._device is None:
            from .tello import SafetyController
            cfg = self.settings
            check_route("192.168.10.1",cfg.tello_interface,cfg.tello_ip)
            self._device = SafetyController(cfg.tello_ip,ground_test=self.ground_test,
                                           min_battery=cfg.battery_min, interface=cfg.tello_interface)
            if self._recovery:
                self._device.recover()
        return self._device

    def recover(self):
        self._recovery = True
        if self._device:
            self._device.recover()

    def confirm_ground(self):
        self.open().confirm_ground()
        self._recovery = False

    def preflight(self):
        if not clock_is_synced():
            raise MissionError('synchronized system clock required for launch')
        self.open().preflight()

    def __getattr__(self,name):
        if name.startswith("_"):
            raise AttributeError(name)
        return getattr(self.open(),name)

    def close(self):
        if self._device:
            self._device.close()


class Runtime:
    def __init__(self,cfg,*,service=True):
        self.cfg = cfg
        self.store = Store(cfg.data_dir/"gateway.db",cfg.db_max_bytes,cfg.min_free_bytes)
        self.safety = Aircraft(cfg)
        if service:
            self.safety.ground_test = True
        self.registry = RegistrationRegistry(self.store,cfg.trusted_nodes,cfg.sensor_subnet)
        self.sensor_http = RoutedHttp(cfg.sensor_interface,cfg.sensor_ip,timeout=2,max_response=12288)
        self.collector = Collector(self.store,self.registry,self.sensor_http,clock_is_synced)
        wan = RoutedHttp(cfg.wan_interface,cfg.wan_ip,timeout=4)
        self.backend = BackendClient(cfg.backend_url,cfg.gateway_id,cfg.gateway_code,cfg.api_key,wan)
        self.recorder = VideoRecorder(self.store,cfg.data_dir/"video",self.safety,
            input_url=f"udp://0.0.0.0:11111?localaddr={cfg.tello_ip}&sources=192.168.10.1&fifo_size=4096&overrun_nonfatal=1",
            max_bytes=cfg.video_max_bytes,min_free_bytes=cfg.min_free_bytes)
        media_client = MediaClient(cfg.backend_url,cfg.gateway_code,cfg.api_key,wan)
        self.uploader = VideoUploader(self.store,media_client,contract_enabled=cfg.media_contract_enabled)
        self.engine = MissionEngine(self.store,self.backend,self.safety,self.collector,self.recorder,uav_id=cfg.uav_id)
        self.sync = SyncWorker(self.store,self.backend)
        self.telemetry = TelemetryWorker(self.store,self.backend)
        self.result = ResultWorker(self.store,self.backend)
        self.recorder.recover()
        self.engine.recover()
        self._schedule = {}
        self.server = None

    def listen(self):
        self.server = make_server(self.registry,self.cfg.sensor_ip,80)
        self._listener = threading.Thread(target=self.server.serve_forever,name="registration",daemon=True)
        self._listener.start()

    def _every(self,name,interval,fn):
        now = time.monotonic()
        due,failures = self._schedule.get(name,(0,0))
        if now < due:
            return
        try:
            fn()
        except (OSError,ValueError,RuntimeError,sqlite3.Error) as exc:
            failures = min(failures+1,6)
            LOG.warning("%s delayed (%s)",name,type(exc).__name__)
        else:
            failures = 0
        self._schedule[name] = (now+min(300,interval*2**failures), failures)

    def _collect(self):
        if self.store.query("SELECT 1 FROM missions WHERE state IN ('READY','DOWNLOADED') LIMIT 1"):
            return
        for node in self.registry.online():
            try:
                self.collector.collect(node["deviceCode"])
            except (ValueError,OSError) as exc:
                LOG.warning("node collection delayed (%s)",type(exc).__name__)

    def _download(self):
        if not self.store.query("SELECT 1 FROM missions WHERE state NOT IN ('REPORTED','RETIRED','CANCELLED','COMPLETED','FAILED')"):
            self.engine.download()
        elif self.store.query("SELECT 1 FROM missions WHERE state='DOWNLOADED'"):
            self.engine.download()
        elif self.store.query("SELECT 1 FROM missions WHERE state='READY'"):
            # Poll /next for cancellations or revised plans without producing readiness/flight permission.
            self.engine.download()

    def tick(self):
        if self.safety.airborne_possible:
            return
        self._every("heartbeat",30,self.backend.heartbeat)
        self._every("mission",30,self._download)
        self._every("collection",10,self._collect)
        self._every("sensor sync",5,self.sync.run_once)
        self._every("terminal result",5,self.result.run_once)
        self._every("telemetry",5,self.telemetry.run_once)
        self._every("video",30,self.uploader.upload_next)

    def close(self):
        if self.server:
            self.server.shutdown()
            self.server.server_close()
            self._listener.join()
        self.safety.close()
        self.store.close()


def status(store):
    report = {}
    for table,key in (("missions","state"),("telemetry","state"),("videos","state"),("outbox","state"),("measurements","synced")):
        report[table] = {row[0]:row[1] for row in store.query(f"SELECT {key},COUNT(*) FROM {table} GROUP BY {key}")}
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config",default="/etc/pi-gateway/gateway.json")
    subs = parser.add_subparsers(dest="action")
    for action in ("simulate","service","status"):
        subs.add_parser(action)
    bench = subs.add_parser('bench')
    bench.add_argument('--video-mission',type=int,help='persisted mission that explicitly requests recording')
    bench.add_argument('--seconds',type=int,default=10,help='ground capture duration, 1-30 seconds')
    backup = subs.add_parser("backup")
    backup.add_argument("destination",type=Path)
    for action in ("flight","reconcile"):
        sub = subs.add_parser(action)
        sub.add_argument("mission_id",type=int)
        sub.add_argument("--confirm",required=True,help="exact local confirmation phrase from README")
    retry = subs.add_parser("retry-held")
    retry.add_argument("outbox_id",type=int)
    retry.add_argument('--kind',choices=('outbox','telemetry','video'),default='outbox')
    cancel = subs.add_parser('cancel')
    cancel.add_argument('mission_id',type=int)
    cancel.add_argument('--reason',required=True)
    args = parser.parse_args(argv)
    if args.action in (None,"simulate"):
        from mocks.demo import run
        run()
        return 0
    logging.basicConfig(level=logging.INFO,format="%(asctime)s %(levelname)s %(message)s")
    cfg = Settings.load(args.config)
    if args.action in ("status","backup"):
        store = Store(cfg.data_dir/"gateway.db",cfg.db_max_bytes,cfg.min_free_bytes)
        try:
            if args.action == "backup":
                store.backup(args.destination)
            else:
                print(json.dumps(status(store),indent=2))
        finally:
            store.close()
        return 0
    with exclusive(cfg.data_dir):
        runtime = Runtime(cfg,service=args.action != "flight")
        try:
            if args.action == "retry-held":
                if args.kind == 'video':
                    runtime.store.execute("UPDATE videos SET state='UPLOADING' WHERE id=? AND state='HELD'",(args.outbox_id,))
                else:
                    table = 'telemetry' if args.kind == 'telemetry' else 'outbox'
                    runtime.store.execute(f"UPDATE {table} SET state='PENDING',next_attempt=0 WHERE id=? AND state='HELD'",(args.outbox_id,))
            elif args.action == 'cancel':
                runtime.engine.cancel(args.mission_id,args.reason)
            elif args.action == "reconcile":
                if args.confirm != f"LANDED {args.mission_id}":
                    parser.error("physical ground inspection confirmation required")
                runtime.engine.reconcile(args.mission_id,operator_confirmed=True)
            elif args.action == "bench":
                if not 1 <= args.seconds <= 30:
                    raise ValueError('ground capture duration must be 1-30 seconds')
                if args.video_mission is not None:
                    rows = runtime.store.query('SELECT plan FROM missions WHERE id=?',(args.video_mission,))
                    if not rows or json.loads(rows[0]['plan']).get('recordVideo') is not True:
                        raise ValueError('ground video requires a persisted mission with recordVideo=true')
                if runtime.safety.airborne_possible:
                    raise MissionError('resolve recovery before ground testing')
                runtime.safety.prepare()
                print(json.dumps(runtime.safety.snapshot()))
                if args.video_mission is not None:
                    try:
                        runtime.recorder.start(args.video_mission,requested=True)
                        time.sleep(args.seconds)
                    finally:
                        runtime.recorder.stop()
            elif args.action == "flight":
                if cfg.ground_test or args.confirm != f"FLY {args.mission_id}":
                    parser.error("flight requires ground test disabled and matching operator confirmation")
                runtime.listen()
                plan = runtime.engine.download()
                if not plan or plan["id"] != args.mission_id:
                    raise MissionError("selected mission does not match the downloaded plan")
                deadline = time.monotonic()+660
                while time.monotonic() < deadline:
                    response = runtime.engine.ready()
                    if response["state"] == "READY_TO_START":
                        print(json.dumps(runtime.engine.launch(operator_confirmed=True)))
                        break
                    if response["state"] not in ("AWAITING_CONFIRMATION","WAITING_FOR_TIME","WAITING_FOR_GATEWAY"):
                        raise MissionError("mission is no longer eligible")
                    time.sleep(5)
                else:
                    raise MissionError("no fresh launch approval in operator window")
            else:
                runtime.listen()
                stop = threading.Event()
                for sig in (signal.SIGTERM,signal.SIGINT):
                    signal.signal(sig,lambda *_:stop.set())
                while not stop.is_set():
                    runtime.tick()
                    stop.wait(1)
        finally:
            runtime.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
