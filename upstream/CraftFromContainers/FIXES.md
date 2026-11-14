# Shift-fill and container availability fixes

This branch makes two small changes to Craft From Containers 4.1.0:

- Holding the fill-all modifier no longer falls back to a single native ore insertion when the backpack contains an accepted input. Without the modifier, the native fallback is preserved.
- Ore, fuel and fireplace interactions invalidate the nearby-container cache before querying it. A chest excluded while another player had it open can therefore become available on the next interaction, even if the machine has not moved. The per-frame crafting UI cache remains in place.

Range, access checks, chest-in-use checks, resource filters and network transfer behavior are unchanged. This does not claim to fix the separately reported multiplayer item-loss/transfer races.

## Validation

The modified source compiles against Valheim 1.0.21 and BepInEx 5.4.2360 using Roslyn 4.8. No game assemblies or compiled plugins are distributed in this commit. Live multiplayer filling with this source build has not been tested.

In-game regression checks:

1. Put accepted ore/wood in both the backpack and an accessible nearby chest. Hold left Shift and interact with the machine: the batch-fill path should run. Repeat without Shift: native single insertion should remain available.
2. Have another player open the supply chest, then interact with the machine. Close the chest and retry without moving the machine: its stock should become available.
3. Repeat for fuel and fireplaces. Verify inaccessible, occupied and out-of-range chests remain excluded, and protected wood filters still apply.

Only the client needs to load these changes. Restarting a server is not required for this source change.
