#!/bin/bash
# Run on the grounded Pi from a separate management connection.
set -euo pipefail
if [[ $# != 2 || $1 != --grounded ]]; then
    echo 'Usage: sudo bash setup-ap.sh --grounded USB_INTERFACE' >&2
    exit 2
fi
iface=$2
[[ $iface =~ ^[a-zA-Z0-9_.-]{1,15}$ ]] || exit 2
[[ $EUID == 0 ]] || { echo 'Root privileges required' >&2; exit 2; }
[[ -d /sys/class/net/$iface ]] || { echo 'Interface missing' >&2; exit 2; }
phy=$(basename "$(readlink -f "/sys/class/net/$iface/phy80211")")
iw phy "$phy" info | grep -Eq '^[[:space:]]+\* AP$' || { echo 'USB adapter lacks AP mode' >&2; exit 1; }
if nmcli -g NAME connection show | grep -Fxq pi-gateway-ap; then
    echo 'pi-gateway-ap already exists. Inspect it with nmcli before changing it.' >&2
    exit 1
fi
mac=$(cat "/sys/class/net/$iface/address")
nmcli connection add type wifi ifname "$iface" con-name pi-gateway-ap ssid UAV_GATEWAY \
    802-11-wireless.mode ap 802-11-wireless.band bg 802-11-wireless.mac-address "$mac" \
    ipv4.method shared ipv4.addresses 192.168.4.1/24 ipv4.never-default yes \
    ipv6.method disabled connection.autoconnect yes \
    wifi-sec.key-mgmt wpa-psk wifi-sec.psk-flags 2
echo 'Enter the AP password at the nmcli prompt. Configure the same password on sensor nodes.'
nmcli --ask connection up pi-gateway-ap
echo 'Password was not saved by this script. Use sudo nmtui to save it privately for boot.'
