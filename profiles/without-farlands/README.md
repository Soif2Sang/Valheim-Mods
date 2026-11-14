# VEE far copies at 4 km, FarLands disabled

VEE 1.1.1: Far builds Distance=4000, Enabled=true, Foliage=true, Preset=Auto. Other settings remain at the existing configuration. The client already has BepInEx enabled; FarLands.dll is renamed with a .disabled-performance-test suffix.

VEE does not expose separate 4 km terrain and 2.5 km forest settings. This profile raises its shared far-copy distance from 2 to 4 km; it does not reproduce FarLands terrain generation or its 3D/impostor/grass distances. On a dedicated server, VEE needs the server's support for whole-world far records; otherwise VisitedOnServers=false keeps that fallback disabled. No server settings are changed by this profile.

Package after building with tools/package-client.ps1 -WithoutFarLands. When extracting onto an existing installation, rename/remove the old active FarLands.dll yourself before starting the client. The archive also contains an inactive copy for rollback.

Configuration and archive contents checked; actual rendering range, fog limits and FPS remain unmeasured. Restart the client to apply.
