# Current-candidate Story Mode and ReShade cycle

6 November 2026. CLEAN EXE SHA-256: `856a3cbd38c4e33a4dbdea8d6829a31b2821fa71ac047d557a337ef1f913f315`.
Exact packaged EXE and matching config ran beside RDR2.exe. No code changes during this cycle.
Installation: Steam, RDR2 1.0.1491.50, Vulkan; other launcher editions untested in this cycle.

## Actual results

| Check | Result and evidence |
|---|---|
| Starting clean Story Mode | User confirmed world loaded normally through Steam. Earlier pre-menu FFFFFFF failure retained separately; cause unconfirmed. |
| ReShade 6.8.0 Vulkan | End opened overlay; Scroll Lock toggled SweetFX Monochrome by user report. Log confirmed shader compilation. |
| Combined modded Story Mode | User reported trainer/ReShade controls worked. Fresh logs confirmed NativeTrainer main(), LML example loading and Monochrome compilation. |
| Holster Mods and ReShade | Explicit packaged UI reviews; 10 Mods items / 13 files and 4 ReShade items / 21 files. All 34 backup hashes matched independently preserved pre-holster files; active paths absent. |
| Holstered Story Mode | User confirmed world loaded and F5, End and Scroll Lock had no mod effects. Active files remained absent after quit; retained loader/LML logs did not change. Live process module inventory was not captured. |
| Restore both groups | Explicit packaged UI confirmations; no conflicts or overwrites reported. Every restored payload matched the frozen hash and independent copy. All 37 frozen files, including three untouched auxiliary logs, matched before the final launch. |
| Restored Story Mode | User confirmed completion of trainer/overlay/effect checks and quit. Fresh ScriptHook/LML/ReShade logs confirmed initialization and Monochrome compilation. |

## Failures and corrections

Initial clean launch failed before the main menu, before ReShade installation. Three Vulkan caches were preserved outside Settings before retry; the subsequent Steam Story launch passed. This does not establish the crash cause.
Home conflicted with Rockstar's overlay, so ReShade was changed to End with a retained configuration backup.
The first shader test failed because both search paths had duplicated recursive suffixes. Files were present; ReShade logged invalid-path error123. Configuration/log/screenshot were preserved, paths corrected to the existing direct Shaders/Textures folders, and compilation plus visible effect checks passed on retry.

## Scope and remaining checks

This cycle passed for the controlled LML/ScriptHook/NativeTrainer/ReShade set on this Steam/Vulkan installation. It does not establish compatibility with every mod or prove vanilla integrity. No Red Dead Online test was performed, and no restriction/ban guarantee follows.
Shared ReShade Vulkan installation/registration remains; CLEAN managed the game-local configuration, preset, shader tree and log. The reported inactive/active behavior was tested in Story Mode.
The current-candidate LML reinstall/restore-conflict repeat subsequently passed, including recovery of moved-aside files after app restart; see LML-CONFLICT-CHECKS-20261106.md. Earlier evidence remains separately dated. Post-launch runtime logs/configuration may change, so restoration proof uses the hashes captured immediately before the final launch.

Evidence: `docs/evidence/game/current-cycle-20261106/`, especially `combined-modded-before-holster.json`, `combined-holster-verification.json`, `holstered-story-user-confirmed.json`, `combined-restore-verification.json` and `restored-story-user-confirmed.json`.
Independent saves, mod originals and post-game backups remain under ignored `scratch/real-game-20261106/`. No real user files were deleted or purged.

Post-final-launch comparison:31 of37 frozen files remained identical. Five runtime logs changed, and ReShade saved default shader sections/technique order in ReShadePreset.ini; Monochrome remained selected. All post-game files were independently copied/hash verified. This does not change the exact pre-launch restoration proof.
