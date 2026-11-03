# CLEAN - Mod Remover and Holster — Windows validation

Updated 3 November 2026. Author: 2Shay. Production .NET Framework 4.8; SDK 8.0.427.
Prepared release materials remain unpublished pending author review. Private repository
commit 6c30496f659c5c45efe94fea0c825af56de281d2 passed Windows CI run41675459572.
Documentation/packaging updates after that commit do not change production source/assets.

## Latest source and executable

Final15 EXE SHA256:
`8431b008f7e6207d57c726c984dc12fe7d53b5f501652a032c76572103b2ea9e`
Matching config SHA256:
`f3f140a31d2664a30f1e42a9ac873e51e4d8e5f0af44f9a999554d71f2c7e4aa`

| Check | Actual result and evidence |
|---|---|
| Final14 standard regression | Engine 38 pass/2 Linux-specific skips; release 21 pass/2 Linux-specific skips; UI 190 pass. final-ui-native-20261029/final14-regression.txt |
| Final15 relevant reruns | UI 190 and exact-assembly native 12 pass; no native skips/failures, 0 build warnings/errors. Same directory: final15-ui-native-regression.txt, final15-native-results.xml |
| Source correspondence | final15-source-receipt.json records sources/assets and EXE/config. Three production UI files changed since step 1; SafetyEngine unchanged |
| Actual Windows rendering | 100/125/150/200%, compact windows/scrolling, fresh/existing About, 1366×768/100%, 1920×1080/150%, Scan Mods visible focus/Space. FINAL-UI-CHECKS-20261029.md |
| Step 1 earlier source | Engine 38/release 21/UI 190/native 12, abrupt worker-stop recovery 1, injected faults 4 pass; four explicit Linux-specific skips across standard suites. FINAL-STEP1-RESULTS-20261029.md |
| Earlier human keyboard | About focus/Space; review arrows/Space/Escape; Cancel-focused Enter; DELETE text-field Enter leaves dialog open, Escape cancels. manual-keyboard-human-results.json |

Raw evidence is under docs/evidence/; named reports are under docs/release/.
The earlier final14/final15 PowerShell transcripts retain invocation/timing but omit
child stdout. A fresh UI harness confirmation passed 190 assertions; complete output
is retained in final-ui-native-20261029/step3-ui-output.txt. Native XML retains all
12 final15 results and the exact loaded EXE hash.
Candidate05 exact ZIP extraction/source/native checks passed on 29 October; see
FINAL-PACKAGE-CHECKS-20261029.md. The final distribution-format package receives its
own checksum/extraction/exact-assembly check recorded in the companion validation
report. Candidate04 and candidate05 are retained unchanged for rollback/history.

Native checks cover read-only files, sharing/retry, denied access, invalid/unreadable/
missing/oversized manifests, case aliases, junctions, changing file types, process locks,
fresh-process recovery, LML conflicts/moved-aside recovery and ten hash-verified cycles.
Long paths pass by safe refusal on this host (LongPathsEnabled=0), not a successful
native roundtrip above 260 characters. Destructive/fault fixtures are disposable.

Step 1 stopped a worker during staging; restart preserved the original 8 MiB SHA256.
Instrumented faults covered corrupt staging, source mutation, interrupted restore
and injected disk shortage. These belong to step 1 EXE
`76c38fd1efd79c38c7e512c9693196eeb1a001f2227b0e10e0b88a9c6180b277`.
They are not new genuine disk-full or physical power-loss results.

## Earlier native results retained

| Scenario | Recorded result |
|---|---|
| Genuine disk full | Windows error 112 twice on disposable 128 MiB VHD; original 16 MiB hash retained and interrupted journal recovered. native-volume-results-01.xml/-02.xml; second run verifies corrected recovery wording |
| Separate volumes | T:→C:→T: Unicode/Documents/modconfig fixture hashes matched in the volume reports. Real Known Folder was not redirected |
| Large inventory | 10,000 synthetic 64-byte file hashes retained. First net8/native 182.2/202.1s; heavier-I/O second 316.6/326.3s. native-windows-01/02.txt and related extended evidence; not real-mod throughput |
| ACL/manifest fixtures | Fixture privilege/restore defects corrected; latest extended-windows-03.txt 5/5 and native-windows-03.txt pass. Earlier failures retained |
| Supplied Alpha 1 baseline | Production 0 warnings/errors. Engine originally reported 40 but two Windows link cases were no-ops; corrected 38 passes/2 skips. Release 20 pass/2 skip/1 stale readonly expectation failure; later corrected to intended roundtrip |

Per-scenario earlier runs are not one complete run on final15. VHD/fixtures remain
retained; T: absent at last check. No real user files were deleted for testing.

## Actual Steam Story Mode cycle

Steam build 13773296, RDR2.exe 1.0.1491.50, Vulkan.
Exact earlier review01 EXE:
`ec149b742d80de281badcc47e8f96e93083ede2b886c735b87a27eb65823f9e2`.

Clean Story → modded Story (LML Beta 11, Script Hook/Native Trainer) → holstered
Story without trainer → restored modded Story with trainer was visually observed.
All 10 mod hashes matched independent originals after restore; all 123 pre-mod file
hashes matched. Evidence: game/live-cycle-01.json, game/controlled-mod-set.json,
game/test-mod-provenance.json.

Real LML reinstall reported conflicts before mutation; move-aside recovery preserved
both versions, with original 10 and second-version 8 hashes verified.
Evidence: game/lml-live-conflict-cycle.json. Historical LML crash cause is not proven.
Actual ReShade gameplay was deferred by the author.

Latest 29 Oct read-only check confirms 10 originals safely holstered in
session 66f074d07525889a, pristine hashes matched and live paths absent:
game/ui-final15-current-holstered-20261029.json. Second-version 8 files remain
recoverable in session e27be1751a829b84. Earlier statements that originals remain
installed are superseded. Independent save/mod backups and baseline are retained.
No real-game Delete or Red Dead Online test occurred.

The full game cycle belongs to review01, not final15 or an unbuilt final ZIP.
Later UI changes received relevant regressions; no new gameplay result is inferred.

## Remaining checks and limits

- Final distribution packaging results are recorded separately in the companion
  validation report; prior results are not relabelled as new tests.
- Untested Epic/standalone Rockstar, mixed monitors, real Known Folder redirection,
  native long-path-enabled host and actual ReShade gameplay.
- Every interruption boundary, physical power loss and exhaustive assistive technology
  unverified. ACLs, alternate data streams and hard-link topology are not preserved
  as a full backup system would preserve them.
- Historical crash cause/general compatibility unproven. Holstering does not verify
  vanilla archives or guarantee online access/ban safety.
- Author adopted the licence28 Oct and chooses current artwork for release.
  Take-Two request sent; only automatic reply recorded, no explicit permission.
  Artwork clearance remains unresolved; CLEAN was opted out of Donation Points3Nov.
- Private Windows CI passed3Nov: engine38/release21/extended17/native13, four
  Linux-specific skips. No CI UI/game/physical-disk-full coverage. CI executable
  differs from the local packaged binary. RDR2Mods author account2Shay verified;
  no listing submitted. GitHub funding-button eligibility remains unconfirmed.

No unexplained file loss, overwrite or unrecoverable operation was observed in passing
scenarios. This is limited evidence, not a guarantee.

## Historical claims

Supplied Alpha 1 reports Linux .NET/Mono/Xvfb 40 engine passes, 22 release passes/1
Windows skip and 141 UI assertions. These are prior reported results, not results
reproduced on this PC. Supplied v3.1 UI notes/counts are superseded by UI-CHANGES.md.
Imports and raw failures remain preserved.

## Screenshot walkthrough — 3 November 2026

Ten new native3840x2160 app-window captures downsampled to1920x1080 PNG were
visually checked and approved by the author. Dialogs use the actual app background;
no pointer capture, added labels or framing. Disposable fixture session03260daceff8568f
completed Holster/Restore; both mod hashes and unknown/protected fixture bytes matched.
The permanent Delete prompt was cancelled. This is a packaged-assembly fixture cycle,
not a new real-game compatibility result. No production changes were made for captures.
