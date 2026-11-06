# Current-candidate LML reinstall and restore-conflict check

Result: PASS on the real Steam RDR2 installation, 6 November 2026 (UTC; completion may cross midnight).
Exact packaged EXE SHA-256: 856a3cbd38c4e33a4dbdea8d6829a31b2821fa71ac047d557a337ef1f913f315.
Game remained closed; operations used the packaged native Windows UI and explicit review.

1. Independently backed up all34 controlled mod/ReShade files, froze protected EXE/config hashes and25 historical store-file hashes.
2. Holstered Mods10 items/13files into f29efb7808196ece; verified all13 payload hashes and absent active paths.
3. Reinstalled eight independently backed-up LML Beta11 fixture files into absent destinations. A harmless INI comment and separate marker distinguish the reinstall; no live overwrite.
4. Original restore blocked six occupied items. Both versions retained matching hashes; no partial restoration occurred.
5. Explicit Move these aside, then restore preserved eight reinstall files in quarantine322a6fe156a44cdc and restored all13 original files exactly.
6. Closed and reopened CLEAN normally. Selected the new moved-aside session, leaving historical quarantinee27be1751a829b84 untouched. Reverse restore correctly blocked original destinations, then explicitly moved them aside into b067009fca6f7301. All eight reinstall files were recovered with exact hashes; nine original conflict payloads remained recoverable.
7. Restored original moved-aside session through the same guarded workflow. Final original13 + ReShade21 + protected EXE/config hashes match the pretest state. All25 historical store files remain unchanged. Eight reinstall files remain hash-verified in final recoverable quarantine2e24fa0f353a2467 and independent backup.

No failure, unexplained loss, overwrite or Delete action occurred. No production code changed; no rebuild required for this test. This verifies controlled file conflict/recovery, not gameplay with every LML release, arbitrary mod set or launcher. The preceding Steam/Vulkan gameplay cycle is recorded separately in STORY-RESHADE-CYCLE-20261106.md.

Evidence: docs/evidence/game/current-cycle-20261106/lml-conflict/ (phase receipts and copied manifests).
Independent backup: scratch/real-game-20261106/lml-conflict-independent/ (before/ and reinstalled/).
Final game state: original tested modded setup restored; game and CLEAN closed. Do not purge the two retained reinstall quarantines or move either recovery store.
