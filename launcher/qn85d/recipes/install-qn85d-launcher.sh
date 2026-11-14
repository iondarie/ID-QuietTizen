#!/bin/sh
set -eu
test "$(id -u)" = 0
[ "$(sed -n 's/^BUILD_ID=//p' /etc/tizen-release)" = T-PTMDDEUC-0090-REL-202605141701 ]
D=/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024
A=/opt/usr/apps/com.samsung.tv.adplayer
S=/home/owner/share/tmp/sdk_tools/quiet-qn85d
B=$D/adplayer-original
test -s "$D/.pkgmgr_parser.db"
test -s "$D/restore-carrier.sh"
test -s "$B/bin/AdPlayer.dll"
test ! -e "$D/installed-adplayer.sha256"
test ! -e "$A/res/quiet-home"
test ! -e "$S/unpacked-home-01"
test "$(stat -c %u:%g "$A/bin/AdPlayer.dll")" = 201:0
sha256sum -c "$D/original-adplayer.sha256"
test "$(sha256sum "$A/bin/AdPlayer.dll" | cut -d ' ' -f 1)" = "$(sha256sum "$B/bin/AdPlayer.dll" | cut -d ' ' -f 1)"
mkdir "$S/unpacked-home-01"
tar -xzf "$S/qn85d-package.tar.gz" -C "$S/unpacked-home-01"
test -s "$S/unpacked-home-01/AdPlayer.dll"
test -s "$S/unpacked-home-01/quiet-home/catalog.json"
app_launcher -u 5001 -k com.samsung.tv.adplayer || true
if [ -d "$A/bin/.native_image" ]; then test ! -e "$D/removed-native-image"; mv "$A/bin/.native_image" "$D/removed-native-image"; fi
cp "$S/unpacked-home-01/AdPlayer.dll" "$A/bin/AdPlayer.dll"
cp -R "$S/unpacked-home-01/quiet-home" "$A/res/"
chown 201:0 "$A/bin/AdPlayer.dll"
chmod 644 "$A/bin/AdPlayer.dll"
chsmack -a User::Pkg::com.samsung.tv.adplayer::RO "$A/bin/AdPlayer.dll"
find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
find "$A/res/quiet-home" -exec chown 201:0 {} \;
find "$A/res/quiet-home" -exec chsmack -a User::Pkg::com.samsung.tv.adplayer::RO {} \;
sha256sum "$A/bin/AdPlayer.dll" > "$D/installed-adplayer.sha256"
app_launcher -u 5001 -s com.samsung.tv.adplayer
echo 'Quiet Home installed; manual launch requested. Home/startup selectors unchanged.'
