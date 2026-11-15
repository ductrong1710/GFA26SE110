"""Verify actual Linux routing before addressing a device or sending credentials."""
from ipaddress import IPv4Address
import json
import socket
import subprocess
from urllib.parse import urlsplit

from .http import HttpClient, UnsentError


def clock_is_synced():
    try:
        result = subprocess.run(['timedatectl','show','-p','NTPSynchronized','--value'],
                                capture_output=True,text=True,timeout=2,check=True)
        return result.stdout.strip() == 'yes'
    except (OSError,subprocess.SubprocessError):
        return False


def check_route(destination, interface, source, run=subprocess.run):
    result = run(["ip","-j","-4","route","get",str(IPv4Address(destination)),"from",str(IPv4Address(source))],
                 capture_output=True,text=True,timeout=3,check=True)
    routes = json.loads(result.stdout)
    if (len(routes) != 1 or routes[0].get("dev") != interface or
            (routes[0].get("prefsrc") or routes[0].get("from") or routes[0].get("src")) != source):
        raise OSError("kernel route does not match configured interface/source")


class RoutedHttp(HttpClient):
    def __init__(self,interface,source,**options):
        super().__init__(source_address=source,**options)
        self.interface = interface

    def request(self,method,url,headers=None,body=None):
        host = urlsplit(url).hostname
        try:
            destinations = {row[4][0] for row in socket.getaddrinfo(host,None,socket.AF_INET,socket.SOCK_STREAM)}
        except OSError as exc:
            raise UnsentError('endpoint resolution unavailable') from exc
        if not destinations:
            raise UnsentError("no IPv4 route to endpoint")
        for dest in destinations:
            try:
                check_route(dest,self.interface,self.source_address)
            except (OSError,subprocess.SubprocessError) as exc:
                raise UnsentError("configured network route unavailable") from exc
        return super().request(method,url,headers,body)
