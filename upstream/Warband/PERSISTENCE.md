# Warband 1.3.1 — persistent parties (local revision)

Based on upstream v1.3.0 (Felihens/Warband). The upstream 1.4.0 update is not included in this focused change.

Every party member must use this version. Create/accept the party normally once after installation. The current leader distributes the remembered roster to its members. Disconnection removes the live HUD entry but keeps the saved membership. A returning member joins the saved party automatically after the player list is available; allow a few seconds. If everyone disconnected, a deterministic online member rebuilds the group. New connection IDs do not need to match the old ones.

Storage is local, under `BepInEx/config/Warband/parties/<hash>.xml`. The hash scopes each file to world UID, platform identity and character name. No matching by display name alone. Another world or character uses a separate file. Renaming a character requires forming the party again. Files contain platform identities and must not be shared in packs or committed.

Leave party and leader exclusion remove membership. When alone, use Shift+P > Party > Leave saved party to forget the remembered group. Offline membership is retained until explicitly removed; only ten saved members are supported. Leave the saved group to start another one if it is full.

The new RPCs only resume matching group tokens and remembered platform/character identities. Live leadership still governs roster updates. An established party takes priority over a lone restoring member, and a live party is never replaced automatically. Pending manual invitations take priority. World changes/disconnects clear session memory without erasing disk. Writes use a temporary file and atomic replacement; errors are logged.

No server plugin/restart needed. All clients need the same version for restoration; unmodified clients retain only normal session behavior. Updates/removals are exchanged between online members; an offline client cannot learn a roster change until it reconnects to an up-to-date member. This is not a server-authoritative guild database or a guarantee under network partition/crash during an exclusion.

Validation: pure C# tests cover disk restart, isolation, identity checks, leader election/offer policy, exclusions/leave and corrupt files. Unity menu probe loads the plugin and exercises native ZPackage serialization, suspension preserving the file and explicit forget deleting it, using synthetic identities only. Actual multi-client reconnect and party UI behavior remain to be confirmed in game.

Build from the monorepo root. Replace only `BepInEx/plugins/Warband/Warband.dll`, with the client closed. Keep the previous DLL to revert; saved XML files remain inactive with upstream 1.3.0.
