#!/bin/sh
# Uninstall FUNC (macOS). macOS has no native package uninstaller, so this
# script is bundled inside the app at /opt/func/uninstall.sh.
#
#   sudo /opt/func/uninstall.sh            # remove the app, keep node data + accounts
#   sudo /opt/func/uninstall.sh --purge    # also stop node/reti, delete data and accounts
set -u

PURGE=0
[ "${1:-}" = "--purge" ] && PURGE=1

if [ "$(id -u)" -ne 0 ]; then
    echo "Please run with sudo: sudo /opt/func/uninstall.sh${1:+ $1}" >&2
    exit 1
fi

echo "Stopping FUNC app..."
launchctl bootout system/func.api 2>/dev/null || true
rm -f /Library/LaunchDaemons/func.api.plist

if [ "$PURGE" -eq 1 ]; then
    echo "Purging node/reti services, data, and accounts..."
    # The node and reti daemons are independent, KeepAlive launchd jobs the app
    # creates at runtime with dynamic, per-network names (func.algorand, func.voi, ...),
    # plus a fixed func.reti. Boot out and remove every func.* daemon before deleting
    # data/accounts so nothing is left running against removed files.
    for plist in /Library/LaunchDaemons/func.*.plist; do
        [ -e "$plist" ] || continue
        label="$(basename "$plist" .plist)"   # e.g. func.algorand, func.reti
        launchctl bootout "system/$label" 2>/dev/null || true
        rm -f "$plist"
    done

    # Kill anything still running as the service accounts.
    pkill -KILL -u _func-node 2>/dev/null || true
    pkill -KILL -u _func-reti 2>/dev/null || true

    # Remove app files, accounts, homes, and all node/reti data.
    rm -rf /opt/func
    dscl /Local/Default -delete /Users/_func-node 2>/dev/null || true
    dscl /Local/Default -delete /Users/_func-reti 2>/dev/null || true
    rm -rf /usr/local/var/func-node /usr/local/var/func-reti
    rm -rf /usr/local/share/func
    echo "FUNC fully removed."
else
    # Preserve mode: leave node/reti daemons running and all data intact; just
    # remove the manager app. Stash this script so --purge stays available later
    # (removing /opt/func would otherwise delete it).
    cp "$0" /usr/local/share/func/uninstall.sh 2>/dev/null || true
    chmod 0755 /usr/local/share/func/uninstall.sh 2>/dev/null || true
    rm -rf /opt/func
    echo ""
    echo "FUNC app removed. Node/reti services and data were preserved and keep running."
    echo "To remove everything later, run:"
    echo "  sudo /usr/local/share/func/uninstall.sh --purge"
fi
