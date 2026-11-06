# Portable in-game-folder checks — 6 November 2026

Current production EXE SHA256: 856a3cbd38c4e33a4dbdea8d6829a31b2821fa71ac047d557a337ef1f913f315.
These are disposable Windows fixture checks, not another real-game launch cycle.

- Build: zero warnings/errors; engine38 PASS/2 Linux-only skips, release22 PASS/2 Linux-only skips; UI232 assertions PASS.
- Native production EXE:17 assertions PASS. Adjacent RDR2.exe detected by real MainForm constructor; production EXE opened from unrelated working directory and closed normally.
- EXE/config protected, forged selections refused, ordinary mod config selectable; three fresh-engine roundtrips preserve original bytes.
- Fixture permanent deletion leaves app pair byte-identical. Tampered manifests cannot restore, move conflicts aside or purge the runtime config.
- Eight focused native regressions PASS: both recovery stores, old/interrupted recovery, locks, duplicate IDs, junctions, LML reinstall/conflict recovery and ten repeated hash cycles.

First harness attempt checked the window handle before startup completed; corrected to bounded polling. Second attempt placed synthetic Documents inside the game root: engine correctly refused overlapping roots. Harness corrected to a separate sibling Documents folder. Both failed attempts are retained; no production safeguard was weakened.

Reproduce interactively after building: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test_portable.ps1
The script accepts -BinaryDirectory pointing to a verified extracted package, creates a fresh disposable fixture and preserves results. It does not touch the real game.

Installation: extract ZIP outside game to retain guides/notices; copy only CLEAN_v3.2.exe and its matching config beside RDR2.exe. Keep the real pair together. Alternatively leave them elsewhere and place a shortcut in the game folder; config stays beside the real EXE. Unknown documentation/source files copied into the game remain unchecked, not protected as app runtime files.

Limits: current checks establish portable placement and engine safety, not a new actual-game launch with this binary. Prior Steam Story Mode/LML tests remain separately dated. Real ReShade gameplay, other launchers, mixed monitors and native long-path success remain unverified. Public release, commit and private push remain pending author review. Artwork clearance unresolved; Nexus DP stays off; Ko-fi only.

Evidence: docs/evidence/portable-folder-20261106/. No visual design changed; existing screenshots retain their original provenance.
