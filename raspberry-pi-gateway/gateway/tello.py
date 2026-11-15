"""Tello EDU safety controller. Only this module has access to command UDP."""

from collections import deque
import ipaddress
import socket
import threading
import time


class FlightError(RuntimeError):
    pass


def parse_telemetry(data):
    if not 0 < len(data) <= 511 or b"\0" in data:
        raise FlightError("invalid state datagram")
    bounds = {"h": (0, 3000), "tof": (0, 10000), "vgz": (-1000, 1000),
              "bat": (0, 100), "yaw": (-180, 180), "pitch": (-180, 180), "roll": (-180, 180)}
    sample = {}
    try:
        for item in data.decode("ascii").strip().split(";"):
            key, _, raw = item.partition(":")
            if key in bounds:
                if key in sample or not raw.removeprefix("-").isdigit():
                    raise ValueError()
                value = int(raw)
                if not bounds[key][0] <= value <= bounds[key][1]:
                    raise ValueError()
                sample[key] = value
    except (UnicodeError, ValueError) as exc:
        raise FlightError("invalid telemetry field") from exc
    if not sample:
        raise FlightError("no usable telemetry fields")
    return sample


class SafetyController:
    def __init__(self, local_ip, command_address=("192.168.10.1", 8889), *, command_port=8889,
                 state_port=8890, ground_test=True, interface=None, command_timeout=7.,
                 flight_timeout=20., telemetry_timeout=2., settle_seconds=5., stable_samples=10,
                 command_gap=.3, min_battery=25):
        address = ipaddress.IPv4Address(local_ip)
        if address.is_unspecified or address.is_multicast:
            raise ValueError("specific Tello interface IPv4 address required")
        if type(min_battery) is not int or not 25 <= min_battery <= 100 or type(ground_test) is not bool:
            raise ValueError("invalid flight safety configuration")
        self.local_ip = str(address)
        self.ground_test = ground_test
        self._min_battery = min_battery
        self._remote = (str(ipaddress.IPv4Address(command_address[0])), command_address[1])
        self._timeout, self._flight_timeout = command_timeout, flight_timeout
        self._fresh, self._settle, self._samples = telemetry_timeout, settle_seconds, stable_samples
        self._gap = command_gap
        self._cmd = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._state = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            if interface:
                for sock in (self._cmd, self._state):
                    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BINDTODEVICE, interface.encode()+b"\0")
            self._cmd.bind((local_ip, command_port))
            self._cmd.connect(self._remote)  # Kernel filters reply address and port.
            self._state.bind(("", state_port) if interface else (local_ip, state_port))
            self._state.settimeout(.05)
        except BaseException:
            self._cmd.close()
            self._state.close()
            raise
        self.state_address = (local_ip, self._state.getsockname()[1])
        self._latest = deque(maxlen=256)
        self._changed = threading.Condition()
        self._io = threading.RLock()
        self._stopped = threading.Event()
        self._prepared = self._poisoned = self._land_attempted = self._in_flight = False
        self.airborne_possible = False
        self.owner = None
        self._last_io = 0.
        self._land_at = None
        self._receiver = threading.Thread(target=self._receive, daemon=True)
        self._watcher = threading.Thread(target=self._watch, daemon=True)
        self._receiver.start()
        self._watcher.start()

    def _receive(self):
        while not self._stopped.is_set():
            try:
                packet, peer = self._state.recvfrom(512)
            except (TimeoutError, ConnectionResetError):
                continue
            except OSError:
                return
            if peer[0] != self._remote[0]:
                continue
            try:
                sample = parse_telemetry(packet)
            except FlightError:
                sample = {}
            with self._changed:
                self._latest.append((time.monotonic(), sample))
                self._changed.notify_all()

    def snapshot(self):
        with self._changed:
            if not self._latest or time.monotonic() - self._latest[-1][0] > self._fresh:
                raise FlightError("telemetry stale")
            return dict(self._latest[-1][1])

    def _checked(self):
        sample = self.snapshot()
        if not {"bat", "h", "tof", "vgz"} <= sample.keys():
            raise FlightError("complete altitude, velocity and battery telemetry required")
        if abs(sample["h"] - sample["tof"]) > 25 or sample["bat"] < self._min_battery:
            raise FlightError("altitude sensors disagree or battery is low")
        return sample

    def _command(self, text, timeout, *, battery=False, landing=False):
        with self._io:
            if self._stopped.is_set() or self._poisoned and not landing:
                raise FlightError("command stream unavailable or uncertain")
            time.sleep(max(0., self._last_io + self._gap - time.monotonic()))
            self._cmd.setblocking(False)
            try:
                for _ in range(16):
                    self._cmd.recv(128)
                raise FlightError("command reply flood")
            except BlockingIOError:
                pass
            self._cmd.send(text.encode("ascii"))
            deadline = time.monotonic() + timeout
            try:
                while time.monotonic() < deadline:
                    if self._in_flight and not landing:
                        try:
                            self._checked()
                        except FlightError:
                            self._poisoned = True
                            raise
                    self._cmd.settimeout(max(.001, min(.05, deadline - time.monotonic())))
                    try:
                        reply = self._cmd.recv(96).rstrip()
                    except TimeoutError:
                        continue
                    if battery and reply.isdigit() and len(reply) <= 3 and 0 <= int(reply) <= 100:
                        return int(reply)
                    if not battery and reply in (b"ok", b"OK"):
                        return
                    if reply.startswith(b"error"):
                        raise FlightError("SDK rejected command")
                raise TimeoutError()
            except (TimeoutError, OSError) as exc:
                self._poisoned = True
                raise FlightError("uncertain SDK response") from exc
            finally:
                self._last_io = time.monotonic()

    @staticmethod
    def _ground(sample):
        return ({"h", "tof", "vgz"} <= sample.keys() and max(sample["h"], sample["tof"]) <= 15
                and abs(sample["vgz"]) <= 5)

    def prepare(self):
        with self._io:
            if self.airborne_possible:
                raise FlightError("unresolved flight")
            self._command("command", self._timeout)
            self._battery = self._command("battery?", self._timeout, battery=True)
            deadline = time.monotonic() + self._timeout
            with self._changed:
                while not self._latest or time.monotonic() - self._latest[-1][0] > self._fresh:
                    remain = deadline - time.monotonic()
                    if remain <= 0:
                        raise FlightError("ground telemetry timeout")
                    self._changed.wait(remain)
            self._prepared = True
            return self.snapshot()

    def preflight(self):
        with self._io:
            if not self._prepared or self.airborne_possible or self._poisoned or self._battery < self._min_battery:
                raise FlightError("ground preflight unavailable or battery is low")
            if not self._ground(self._checked()):
                raise FlightError("fresh ground evidence required")

    def takeoff(self, owner, operator_confirmed=False):
        with self._io:
            if self.ground_test or operator_confirmed is not True or not owner or self.owner is not None:
                raise FlightError("takeoff denied")
            self.preflight()
            self.owner, self.airborne_possible, self._land_attempted = owner, True, False
            self._land_at = None
            self._command("takeoff", self._flight_timeout)
            self._in_flight = True

    def _flying(self, owner):
        if not owner or owner != self.owner or not self._in_flight or self._land_attempted or self._poisoned:
            raise FlightError("flight ownership or command state invalid")
        return self._checked()

    def move(self, owner, direction, cm):
        with self._io:
            sample = self._flying(owner)
            if direction not in ("forward", "back", "left", "right") or type(cm) is not int or not 20 <= cm <= 100:
                raise FlightError("invalid horizontal route movement")
            if sample["h"] < 80 or max(sample["h"], sample["tof"]) > 120:
                raise FlightError("horizontal movement outside altitude limits")
            self._command(f"{direction} {cm}", self._flight_timeout)

    def altitude(self, owner, target_cm):
        if type(target_cm) is not int or not 80 <= target_cm <= 120:
            raise FlightError("target altitude outside limits")
        deadline = time.monotonic() + self._flight_timeout
        earliest = time.monotonic()
        while time.monotonic() < deadline:
            with self._changed:
                if not self._latest or self._latest[-1][0] <= earliest:
                    self._changed.wait(.025)
                    continue
            with self._io:
                sample = self._flying(owner)
                height, upper = sample["h"], max(sample["h"], sample["tof"])
                if height >= 80 and upper <= 120 and abs(target_cm - height) <= 10:
                    good = lambda s: ({"h", "tof", "vgz", "bat"} <= s.keys() and s["bat"] >= self._min_battery and
                                      80 <= s["h"] <= 120 and max(s["h"], s["tof"]) <= 120 and
                                      abs(s["h"] - s["tof"]) <= 25 and abs(s["h"] - target_cm) <= 10 and abs(s["vgz"]) <= 5)
                    if self._stable(earliest, min(2., self._settle), good):
                        return
                elif target_cm - height > 10 and upper + 20 <= 120 and height + 20 <= target_cm + 10:
                    self._command("up 20", self._flight_timeout)
                    earliest = time.monotonic() + .5
                elif height - target_cm > 10 and height - 20 >= 80:
                    self._command("down 20", self._flight_timeout)
                    earliest = time.monotonic() + .5
                else:
                    raise FlightError("unable to correct altitude within safety limits")
            self._stopped.wait(max(.02, earliest - time.monotonic()))
        raise FlightError("altitude stabilization timeout")

    def rotate(self, owner, direction, degrees):
        with self._io:
            sample = self._flying(owner)
            if direction not in ('cw','ccw') or type(degrees) is not int or not 1 <= degrees <= 360:
                raise FlightError('invalid rotation')
            if sample['h'] < 80 or max(sample['h'],sample['tof']) > 120:
                raise FlightError('rotation outside altitude limits')
            self._command(f'{direction} {degrees}',self._flight_timeout)

    def abort(self, owner):
        self.land(owner)

    def hold(self, owner, seconds):
        if type(seconds) is not int or not 0 <= seconds <= 30:
            raise FlightError("invalid waypoint hold")
        deadline = time.monotonic() + seconds
        while True:
            with self._io:
                self._flying(owner)
            if time.monotonic() >= deadline:
                return
            self._stopped.wait(min(.1, max(0, deadline - time.monotonic())))

    def set_stream(self, enabled):
        with self._io:
            if not self._prepared or self._land_attempted and self.airborne_possible:
                raise FlightError("stream control unavailable")
            self._command("streamon" if enabled else "streamoff", self._timeout)

    def _stable(self, after, duration, predicate):
        with self._changed:
            first = last = count = 0
            prior = None
            for at, sample in self._latest:
                if at <= after:
                    continue
                if not predicate(sample):
                    first = last = count = 0
                    prior = None
                    continue
                if at - last > self._fresh or prior is not None and abs(prior - sample["h"]) > 3:
                    first = count = 0
                if not count:
                    first = at
                last, prior, count = at, sample["h"], count + 1
            return count >= self._samples and last - first >= duration and time.monotonic() - last <= self._fresh

    def _send_land(self):
        with self._io:
            if self._land_attempted:
                return
            self._land_attempted = True
            self._in_flight = False
            try:
                self._cmd.send(b"rc 0 0 0 0")
            finally:
                try:
                    self._command("land", self._flight_timeout, landing=True)
                except (OSError, FlightError):
                    self._poisoned = True
                self._land_at = time.monotonic()

    def land(self, owner):
        with self._io:
            if not self.airborne_possible or self.owner not in (owner, "recovery"):
                raise FlightError("landing ownership invalid")
            self._send_land()
        deadline = time.monotonic() + max(self._flight_timeout * 3, self._settle * 2)
        # A poisoned/rejected command demands the stronger two-sensor ground signature.
        def ground(sample):
            return self._ground(sample) and (not self._poisoned or sample["h"] == 0 and sample["vgz"] == 0)
        while time.monotonic() < deadline:
            if self._stable(self._land_at, self._settle, ground):
                with self._io:
                    self.airborne_possible, self.owner = False, None
                return
            self._stopped.wait(.025)
        raise FlightError("landing evidence insufficient")

    def _watch(self):
        while not self._stopped.wait(.05):
            with self._io:
                if not self._in_flight or self._land_attempted:
                    continue
                try:
                    sample = self._checked()
                    if max(sample["h"], sample["tof"]) > 120:
                        raise FlightError("altitude ceiling exceeded")
                except FlightError:
                    self._send_land()
                    continue
                # Prevent aircraft idle auto-land during collection or long holds.
                if time.monotonic() - self._last_io >= 2:
                    try:
                        self._cmd.send(b"rc 0 0 0 0")
                        self._last_io = time.monotonic()
                    except OSError:
                        self._send_land()

    def recover(self):
        with self._io:
            self.owner = self.owner or "recovery"
            self.airborne_possible, self._in_flight, self._poisoned = True, False, True

    def confirm_ground(self):
        # Called only after independent physical inspection by an operator.
        self._command("command", self._timeout, landing=True)
        after = time.monotonic()
        deadline = after + self._settle + self._timeout
        while not self._stable(after, self._settle, self._ground):
            if time.monotonic() >= deadline:
                raise FlightError("fresh, stable ground telemetry required for reconciliation")
            self._stopped.wait(.05)
        with self._io:
            self.airborne_possible, self.owner, self._prepared = False, None, False

    def close(self):
        with self._io:
            if self._in_flight:
                self._send_land()
            self._stopped.set()
        self._receiver.join(timeout=1)
        self._watcher.join(timeout=1)
        self._state.close()
        self._cmd.close()
