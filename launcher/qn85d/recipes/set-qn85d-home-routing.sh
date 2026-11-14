#!/bin/sh
set -eu
test "$(id -u)" = 0
[ "$(sed -n 's/^BUILD_ID=//p' /etc/tizen-release)" = T-PTMDDEUC-0090-REL-202605141701 ]
D=/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024
R=$D/home-routing-01
P=$D/Probe.dll
test ! -e "$R"
sha256sum -c "$D/installed-adplayer.sha256"
/usr/bin/dotnet "$P" | head -1 | grep -Fx 'home_result=0 home=com.samsung.tv.csfs'
vconftool get db/appfw/home_appid | grep -Fx 'db/appfw/home_appid, value = com.samsung.tv.csfs (String)'
vconftool get db/appfw/startup_appid | grep -Fx 'db/appfw/startup_appid, value = org.tizen.tv-viewer (String)'
mkdir "$R"
/usr/bin/dotnet "$P" > "$R/operation-before.txt"
vconftool get db/appfw/home_appid > "$R/home-before.txt"
vconftool get db/appfw/startup_appid > "$R/startup-before.txt"
cat > "$R/restore-routing.sh" <<'RESTORE'
#!/bin/sh
set -eu
test "$(id -u)" = 0
P=/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024/Probe.dll
/usr/bin/dotnet "$P" | head -1 | grep -Fx 'home_result=0 home=com.samsung.tv.adplayer'
vconftool get db/appfw/home_appid | grep -Fx 'db/appfw/home_appid, value = com.samsung.tv.adplayer (String)'
vconftool get db/appfw/startup_appid | grep -Fx 'db/appfw/startup_appid, value = com.samsung.tv.adplayer (String)'
/usr/bin/dotnet "$P" set-home com.samsung.tv.csfs
vconftool set -f -t string db/appfw/home_appid com.samsung.tv.csfs
vconftool set -f -t string db/appfw/startup_appid org.tizen.tv-viewer
RESTORE
/usr/bin/dotnet "$P" set-home com.samsung.tv.adplayer
vconftool set -f -t string db/appfw/home_appid com.samsung.tv.adplayer
vconftool set -f -t string db/appfw/startup_appid com.samsung.tv.adplayer
/usr/bin/dotnet "$P" | head -1 | grep -Fx 'home_result=0 home=com.samsung.tv.adplayer'
vconftool get db/appfw/home_appid | grep -Fx 'db/appfw/home_appid, value = com.samsung.tv.adplayer (String)'
vconftool get db/appfw/startup_appid | grep -Fx 'db/appfw/startup_appid, value = com.samsung.tv.adplayer (String)'
echo 'Home and startup routing verified.'
