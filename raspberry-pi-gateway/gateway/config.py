"""Read deployment settings and secrets without logging their values."""
from dataclasses import dataclass, field
from ipaddress import IPv4Address, IPv4Network
import json
import os
from pathlib import Path
import re
from urllib.parse import urlsplit


@dataclass(frozen=True)
class Settings:
    data_dir: Path
    backend_url: str
    gateway_code: str
    gateway_id: int
    uav_id: int
    sensor_interface: str
    sensor_ip: str
    sensor_subnet: str
    tello_interface: str
    tello_ip: str
    wan_interface: str
    wan_ip: str
    ground_test: bool
    battery_min: int
    media_contract_enabled: bool
    db_max_bytes: int
    video_max_bytes: int
    min_free_bytes: int
    api_key: str = field(repr=False)
    trusted_nodes: tuple = field(repr=False)

    @classmethod
    def load(cls,path):
        values = json.loads(Path(path).read_text(encoding="utf-8"))
        if not isinstance(values,dict):
            raise ValueError("configuration must be an object")
        root = urlsplit(values["backend_url"])
        if root.scheme != "https" or not root.hostname or root.username or root.password or root.query or root.fragment or root.path not in ("","/"):
            raise ValueError("use the HTTPS backend origin")
        for key in ("gateway_id","uav_id","battery_min","min_free_bytes","video_max_bytes","db_max_bytes"):
            if type(values[key]) is not int or values[key] < 1:
                raise ValueError("invalid configuration field " + key)
        if not 25 <= values["battery_min"] <= 100:
            raise ValueError("battery_min must be 25–100")
        if not isinstance(values["gateway_code"],str) or not re.fullmatch(r"[!-~]{1,100}",values["gateway_code"]):
            raise ValueError("invalid gateway code")
        interfaces = [values[k+"_interface"] for k in ("sensor","tello","wan")]
        if len(set(interfaces)) != 3 or not all(re.fullmatch(r"[a-zA-Z0-9_.-]{1,15}",i) for i in interfaces):
            raise ValueError("configure three distinct network interfaces")
        sensor_net = IPv4Network(values["sensor_subnet"])
        tello_net = IPv4Network("192.168.10.0/24")
        sensor,tello,wan = (IPv4Address(values[k+"_ip"]) for k in ("sensor","tello","wan"))
        if (sensor_net.overlaps(tello_net) or sensor not in sensor_net or tello not in tello_net or
                wan in sensor_net or wan in tello_net or sensor in (sensor_net.network_address,sensor_net.broadcast_address)
                or tello in (tello_net.network_address,tello_net.broadcast_address,IPv4Address("192.168.10.1"))):
            raise ValueError("network source address mismatch")
        for key in ("media_contract_enabled","ground_test"):
            if type(values[key]) is not bool:
                raise ValueError("invalid boolean " + key)
        override = os.environ.get("GROUND_TEST_MODE")
        if override is not None:
            if override.lower() not in ("true","false"):
                raise ValueError("GROUND_TEST_MODE must be true or false")
            values["ground_test"] = override.lower() == "true"
        def secret(key):
            value = os.environ.get(key)
            if not value or not value.isascii() or any(ord(c)<33 or ord(c)>126 for c in value):
                raise ValueError("missing or invalid secret environment variable " + key)
            return value
        trusted = []
        for node in values["trusted_nodes"]:
            name = node["token_env"]
            if not re.fullmatch(r"[A-Z][A-Z0-9_]*",name):
                raise ValueError("invalid token environment variable name")
            trusted.append({k:node[k] for k in ("deviceCode","farmId","zoneId")} | {"token":secret(name)})
        values["trusted_nodes"] = tuple(trusted)
        values["api_key"] = secret("GATEWAY_API_KEY")
        values["data_dir"] = Path(values["data_dir"]).resolve()
        return cls(**values)
