#!/bin/sh
set -eu
test "$(id -u)" = 0
[ "$(sed -n 's/^BUILD_ID=//p' /etc/tizen-release)" = T-PTMDDEUC-0090-REL-202605141701 ]
D=/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024
A=/opt/usr/apps/com.samsung.tv.adplayer
B=$D/adplayer-original
test -s "$B/bin/AdPlayer.dll"
sha256sum -c "$D/original-adplayer.sha256"
if [ -s "$D/installed-adplayer.sha256" ]; then sha256sum -c "$D/installed-adplayer.sha256"; fi
app_launcher -u 5001 -k com.samsung.tv.adplayer || true
cp -a "$B/bin/AdPlayer.dll" "$A/bin/AdPlayer.dll"
chsmack -a User::Pkg::com.samsung.tv.adplayer::RO "$A/bin/AdPlayer.dll"
if [ -d "$A/bin/.native_image" ]; then
    test ! -e "$D/custom-native-image-on-restore"
    mv "$A/bin/.native_image" "$D/custom-native-image-on-restore"
fi
if [ -d "$B/bin/.native_image" ]; then cp -a "$B/bin/.native_image" "$A/bin/"; fi
echo 'Original AdPlayer assembly and cache restored. Quiet Home resources remain inert.'
