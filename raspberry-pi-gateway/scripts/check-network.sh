#!/bin/bash
set -euo pipefail
[[ $# == 2 ]] || { echo 'Usage: bash check-network.sh SENSOR_INTERFACE TELLO_INTERFACE' >&2; exit 2; }
for iface in "$@"; do
    [[ $iface =~ ^[a-zA-Z0-9_.-]{1,15}$ ]] || exit 2
    ip -4 address show dev "$iface"
    iw dev "$iface" info
done
ip -4 route get 192.168.4.2 from 192.168.4.1
ip -4 route get 192.168.10.1
timedatectl show -p NTPSynchronized
df -h /var/lib/pi-gateway
