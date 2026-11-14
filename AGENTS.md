# Working on Valheim Mods

- This monorepo is the source of truth for changes, builds and tests. Read README.md and docs/VALIDATION.md first.
- Edit source and test code here, not in historical copies outside this repository. Build from this repository and keep generated DLLs, logs and test worlds under the ignored dist directory.
- Use tools/MapValidation/run.ps1 for isolated Unity checks. Supply GamePath explicitly. LivingMap and RememberServerPassword are external installed dependencies; the runner copies their DLLs into its isolated client and records their hashes. Never copy password stores or personal configurations.
- For real-world scene checks, use tools/WorldBenchmark/run.ps1 with a stable private snapshot and an approved full client ZIP. Keep all world data/reports in ignored dist. Hidden local-host timing is provisional, not representative dedicated-server client FPS.
- Always retain a FarLands reference profile when evaluating long-distance rendering; enable it only in the isolated client unless the owner requests a normal-game installation change.
- Never restart a server. Never stop a normal player client. Preserve the runner's save isolation and display-preference restoration.
- Update README.md / validation documentation after changes. Record actual checks and limits; never label an unperformed test as passed. Keep secrets, game assemblies and personal data out of commits.
- Source changes intended for players require updating the complete client ZIP, INSTALLATION.txt and MODS.json SHA-256 inventory in the deployment workspace. Documentation/test-tool changes alone do not change the installed client pack.
