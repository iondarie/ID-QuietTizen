# Quiet Home on QE65QN85DBTXXH

This is the launcher variant running on firmware 2130.0, build
`T-PTMDDEUC-0090-REL-202605141701`. Its entry assembly is `AdPlayer.dll`
and its carrier is `com.samsung.tv.adplayer`. Follow this guide for that
variant. The original launcher and its documentation remain in the repository.

## Build

Install the .NET 6 SDK and obtain matching TV assemblies as described in
[Build and install Quiet Home](04-launcher.md). Place the four assemblies under
`.local/tv-framework/`. Use an already authorized TV administration connection.

```sh
dotnet build launcher/qn85d/QuietQn85d.csproj -c Release -o .local/build-qn85d
dotnet build launcher/probe/Probe.csproj -c Release -o .local/build-qn85d-probe
python3 tools/package_qn85d.py .local/build-qn85d/AdPlayer.dll
tar -czf .local/qn85d-package.tar.gz -C .local/package-qn85d .
```

The example catalog omits household channel records. After tuning the TV, the
channel picker loads channels from the device. Change `AppleSource` in a local
catalog to match the HDMI source reported by the probe. Weather defaults to
Otopeni. The location and Open-Meteo request are defined in the launcher source.

To reproduce publisher artwork, place your own `icons/` and optional
`catalog.json` in an ignored local asset directory and pass it to
`--local-assets`. See [artwork](06-artwork.md) and [attribution](../NOTICE.md).
Use the relative icon paths listed in the example catalog. The launcher can use
art from installed applications and fetch channel artwork listed in
`logo-sources.json`. Results depend on what the applications and publishers expose.

## Install and verify

1. Confirm the model and build. Inspect the carrier and probe the current Home
   handler, startup selectors, HDMI sources and app IDs.
2. The recipes expect a temporary root TV session. Create a consistent SQLite
   backup of `/opt/dbspace/.pkgmgr_parser.db` using `.backup` and place it in
   `/opt/usr/share/quiet-tizen-backups/qn85d-original-20261024` under its original
   basename. Copy recovery files off the TV for private safekeeping.
3. Stage the carrier restore recipe as `restore-carrier.sh` and the built
   `Probe.dll` in `/home/owner/share/tmp/sdk_tools/quiet-qn85d`. Run
   `backup-qn85d-carrier.sh` and verify the saved archive and checksum.
4. Stage the package tarball in the same directory as
   `qn85d-package.tar.gz`. Run `install-qn85d-launcher.sh` for a manual launch.
5. Check both horizontal favorite rows, their **+** buttons, the focus fill,
   HDMI and broadcast playback with sound, and the **Settings** link to Samsung.
   See `/tmp/quiet-home-qn85d-state.txt` for runtime status.
6. If the initial routing matches the values checked by
   `set-qn85d-home-routing.sh`, that recipe can assign Home and startup. It
   points both to Quiet Home. To boot to live TV while retaining Quiet Home on
   the Home button, set the startup selector back to the captured
   `org.tizen.tv-viewer` value after checking it. Test reboot and standby separately.

Recipes are under [launcher/qn85d/recipes](../launcher/qn85d/recipes/).
If the model, build, checksums or routing differ, inspect the device before
adapting a recipe.

## Use and recover

Use Left and Right to scroll each row. Select **+** to add or remove shortcuts.
Use **Edit favorites** in the picker to reorder them. The launcher keeps those
choices, cached channel art and the last successful weather result in its data
directory.

Restore Home routing first using the captured inverse, or the documented
`restore-qn85d-home-routing-v2.sh` if its checks match. Then run
`restore-qn85d-carrier.sh` to restore the original assembly and native cache.
Confirm that Samsung Home and live TV launch normally. See [recovery](09-recovery.md)
for the broader recovery workflow.
