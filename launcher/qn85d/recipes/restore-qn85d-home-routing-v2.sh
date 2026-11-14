#!/bin/sh
set -eu
test "$(id -u)" = 0
[ "$(sed -n 's/^BUILD_ID=//p' /etc/tizen-release)" = T-PTMDDEUC-0090-REL-202605141701 ]
P=/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024/Probe.dll
/usr/bin/dotnet "$P" | head -1 | grep -Fx 'home_result=0 home=com.samsung.tv.adplayer'
vconftool get db/appfw/home_appid | grep -Fx 'db/appfw/home_appid, value = com.samsung.tv.adplayer (String)'
startup="$(vconftool get db/appfw/startup_appid)"
case "$startup" in
 'db/appfw/startup_appid, value = com.samsung.tv.adplayer (String)'|'db/appfw/startup_appid, value = org.tizen.tv-viewer (String)') ;;
 *) echo 'Startup routing changed; inspect before restoring.' >&2; exit 1;;
esac
/usr/bin/dotnet "$P" set-home com.samsung.tv.csfs
vconftool set -f -t string db/appfw/home_appid com.samsung.tv.csfs
vconftool set -f -t string db/appfw/startup_appid org.tizen.tv-viewer
