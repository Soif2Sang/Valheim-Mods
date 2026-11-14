# Horizon Clarity 0.1.0

Client-side experiment for Valheim 1.0.21, BepInEx 5.4.2360. Use with the VEE 4 km / FarLands-disabled profile.

After EnvMan.SetEnv computes native fog, multiply RenderSettings.fogDensity by 0.25. After GameCamera.LateUpdate, keep its main camera far clip at least 4000 m, retaining a longer native range. Only active with a local player in a game; the menu is unchanged. Configuration: local.valheim.horizonclarity.cfg, Display.Enabled / FogMultiplier / ViewDistance.

No terrain generation, object scanning, additional rendering cameras or network changes. No change to weather selection, rain, wind, fog colours, water materials or minimap. Thinner global fog can change distant water appearance and cause more geometry to be visible. Volumetric mist (e.g. Mistlands) is not disabled. A longer camera clip does not create missing world geometry. More visibility can cost FPS; no performance gain claimed.

Fog is recalculated from each native SetEnv result, preventing cumulative thinning. Leaving the world, disabling the setting or destroying the plugin restores the last native fog/camera values only when they still match this plugin's applied values. Later native/other-mod changes are preserved. Avoid combining with other fog/range plugins without rechecking ordering.

Validation: pure policy checks and Unity synthetic probe 20261114-222228 passed: plugin load, fog scaling without compounding, main-camera 2000 -> 4000 -> 2000, fog colour unchanged, native density restored and later density changes preserved. The probe calls the same mutation/restore methods on synthetic values and a temporary camera in the menu. It does not verify the full real-world weather/camera loop, horizon screenshots or FPS.

Build from the monorepo root, or run this directory's build.ps1 with GamePath. Local install: BepInEx/plugins/HorizonClarity/HorizonClarity.dll and the generic configuration. Restart only the client. Rollback: close the client and remove this DLL; no world/save data is changed. Never restart a server for this plugin.
