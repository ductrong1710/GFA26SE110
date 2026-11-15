"""Loopback-only Tello SDK simulator, with controllable battery and lost replies."""

import socket
import threading
import time


class TelloSimulator:
    def __init__(self, *, battery=90, drop=(), ignore_land=False):
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._socket.bind(("127.0.0.1", 0))
        self._socket.settimeout(.01)
        self.address = self._socket.getsockname()
        self.telemetry_address = None
        self.telemetry_enabled = True
        self.battery = battery
        self.height = 0
        self.tof_offset = 0
        self.commands = []
        self.drop = set(drop)
        self.ignore_land = ignore_land
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, daemon=True)

    def __enter__(self):
        self._thread.start()
        return self

    def __exit__(self, *_):
        self._stop.set()
        self._thread.join()
        self._socket.close()

    def _run(self):
        sent = 0
        while not self._stop.is_set():
            if self.telemetry_enabled and self.telemetry_address and time.monotonic() - sent >= .02:
                if self.telemetry_address[0] != "127.0.0.1":
                    raise ValueError("simulator only supports loopback")
                msg = f"h:{self.height};tof:{self.height+self.tof_offset};vgz:0;bat:{self.battery};"
                self._socket.sendto(msg.encode(), self.telemetry_address)
                sent = time.monotonic()
            try:
                data, sender = self._socket.recvfrom(128)
            except (TimeoutError, ConnectionResetError):
                continue
            if sender[0] != "127.0.0.1":
                continue
            cmd = data.decode("ascii")
            self.commands.append(cmd)
            if cmd == "takeoff":
                self.height = 100
            elif cmd == "land" and not self.ignore_land:
                self.height = 0
            elif cmd.startswith("up "):
                self.height += int(cmd.split()[1])
            elif cmd.startswith("down "):
                self.height -= int(cmd.split()[1])
            if cmd not in self.drop and not cmd.startswith("rc "):
                self._socket.sendto(str(self.battery).encode() if cmd == "battery?" else b"OK", sender)
