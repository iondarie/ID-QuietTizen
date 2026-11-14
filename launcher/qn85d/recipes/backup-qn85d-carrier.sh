#!/bin/sh
set -eu
test "$(id -u)" = 0
[ "$(sed -n 's/^BUILD_ID=//p' /etc/tizen-release)" = T-PTMDDEUC-0090-REL-202605141701 ]
D=/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024
A=/opt/usr/apps/com.samsung.tv.adplayer
S=/home/owner/share/tmp/sdk_tools/quiet-qn85d
test -s "$D/.pkgmgr_parser.db"
test ! -e "$D/adplayer-original"
test "$(stat -c %u:%g "$A/bin/AdPlayer.dll")" = 201:0
test -s "$S/restore-carrier.sh"
cp -a "$A" "$D/adplayer-original"
sha256sum "$D/adplayer-original/bin/AdPlayer.dll" > "$D/original-adplayer.sha256"
cp "$S/restore-carrier.sh" "$D/restore-carrier.sh"
cp "$S/Probe.dll" "$D/Probe.dll"
/usr/bin/dotnet "$D/Probe.dll" > "$D/operation-and-sources-before.txt"
tar -czf "$D/adplayer-original.tar.gz" -C "$D" adplayer-original
stat -c '%s %n' "$D/adplayer-original.tar.gz"
echo 'Carrier backup and restore script saved; original app unchanged.'
