# CLEAN - Mod Remover and Holster — validation

Updated 7 November 2026. Author: 2Shay. Production: .NET Framework 4.8.
Prepared packages remain unpublished pending author review.

## Executable and corresponding source

EXE SHA-256: `856a3cbd38c4e33a4dbdea8d6829a31b2821fa71ac047d557a337ef1f913f315`.
Config SHA-256: `f3f140a31d2664a30f1e42a9ac873e51e4d8e5f0af44f9a999554d71f2c7e4aa`.
These are the exact files used for the current Windows fixture and game checks.
The final documentation refresh retains those bytes; it does not rebuild the app.
All production source/assets/project/config files match the tested source snapshot.
Source baseline: Git commit45a3e2a53468a7460f4a10b5b4cc41294a682a7a plus the
included documentation/packaging refresh. Original local binary build context was
parent0c85b740214fa183bb4dcb69361b6a7a89edd557. See BUILD.md for reproducibility limits.

## Current evidence

| Check | Actual result |
|---|---|
| Windows CI, source45a3e2a | Engine38, release22, extended21 and native17 PASS; four Linux-only skips, zero failures. Run42315170543. CI builds its own executable. |
| Local Windows build | Engine38/release22 PASS; four Linux-only skips, zero warnings/errors. |
| Local UI harness | 232 assertions PASS, including About sizing and initial focus. |
| Portable placement | Production EXE17 checks and focused native8 PASS; exact extracted prior ZIP17 PASS. Adjacent RDR2.exe detection, protected EXE/config and normal close tested. |
| Actual Story Mode | Exact EXE/config beside RDR2.exe; clean → modded → holster → clean → restore → modded PASS on Steam/Vulkan. |
| Restoration hashes | All34 mod/ReShade payloads matched independent pre-holster hashes before the final launch; all37 frozen files matched at that point. |
| Actual LML reinstall conflict | Six occupied items blocked before changes. Both versions preserved and moved-aside files recovered after restarting CLEAN. Original13 + ReShade21 + protected pair matched pretest hashes. |

Reports: [Story/ReShade](docs/release/STORY-RESHADE-CYCLE-20261106.md),
[LML conflicts](docs/release/LML-CONFLICT-CHECKS-20261106.md),
[portable placement](docs/release/PORTABLE-FOLDER-CHECKS-20261106.md),
[recovery stores](docs/release/RECOVERY-FOLDER-CHECKS-20261106.md), and
[Windows CI](docs/release/WINDOWS-CI-20261107.md).
The companion final-package report records extraction/checksum and exact-assembly reruns.
The reports identify local raw evidence paths; independent saves, game/mod backups
and raw runtime logs are retained privately and are not bundled for distribution.

Game: Steam build13773296, RDR2 1.0.1491.50, Vulkan. Controlled set: LML Beta11,
Script Hook/Native Trainer and ReShade6.8.0 with SweetFX Monochrome. User loaded and
quit Story Mode and checked F5/End/Scroll Lock; fresh logs corroborated initialization.
Shared ReShade Vulkan registration remains installed. CLEAN managed game-local files;
inactive/active behavior was tested in Story Mode, without a live module inventory.
After the final launch five runtime logs and ReShadePreset.ini changed normally;
31 other frozen files remained identical. Restoration proof uses pre-launch hashes.
No Red Dead Online test occurred; holstering does not prove vanilla integrity or
guarantee online access or protection from restrictions/bans.

## Failures retained and corrected

- An initial clean launch failed before the menu with FFFFFFF, before ReShade was
  installed. Three Vulkan caches were preserved outside Settings; retry passed.
  The crash cause remains unconfirmed; no general crash fix is claimed.
- Home conflicted with Rockstar's overlay; ReShade uses End. Duplicated recursive
  shader search suffixes caused invalid-path123. Original config/logs were preserved,
  direct existing paths substituted and visible effect/compilation checks passed.
- Portable harness startup timing and overlapping synthetic Documents caused initial
  harness failures. Bounded startup polling and separate fixture roots corrected
  those harness defects; production protection was not weakened.
- An open preview locked a build output. Normal close and rebuild passed.

## Earlier tests and their limits

Native fixtures cover read-only/sharing/ACL failures; unreadable, missing, oversized
and corrupt manifests; process locks; changing sources; traversal/case aliases;
junction refusal; fresh-process recovery; older sessions; conflict recovery and
repeated hash cycles. CI includes10,000 synthetic-file roundtrips,60.839s on .NET8
and61.843s on .NET Framework. These are fixture timings, not real-mod throughput.
Current local UI checks supplement earlier actual100/125/150/200% rendering and
1366×768/100%,1920×1080/150% checks. Approved screenshots are plain1920×1080 PNGs
from native cursor-free4K captures; some unchanged screens retain earlier provenance.
Human keyboard review, About, confirmation and DELETE input checks passed.

Genuine disk-full recovery twice passed on a disposable128MiB VHD on earlier builds:
Windows error112, original16MiB hash retained, journal recovered. The final executable
did not repeat that physical volume test. Earlier abrupt-worker and injected
corruption/source-mutation/interruption/space tests retain their own build identities.
Historical reports and original failures remain preserved; they are not relabelled
as new final-build results. See included FINAL-STEP1 and FINAL-UI reports.

Supplied Alpha1 documentation reported synthetic Linux/Mono/Xvfb results. Those are
prior reported results, not results reproduced on this PC. Supplied v3.1 UI claims
are reconciled in UI-CHANGES.md. Earlier Windows/game results remain separately dated.

## Remaining limitations

- Epic/standalone Rockstar editions, mixed monitors and real Known Folder redirection
  are untested. Native paths above260 characters safely refused on this host;
  successful roundtrips with long-path policy enabled remain unverified.
- Every interruption boundary, physical power loss, exhaustive accessibility and
  historical crash causes remain unverified. ACLs, alternate data streams and
  hard-link topology are not preserved as a full backup system would preserve them.
- Controlled gameplay does not establish compatibility with every mod or ENB.
- Official Rockstar artwork has no written redistribution clearance. Author chose
  to retain it; Take-Two's automatic reply is not permission. Nexus Donation Points
  remains OFF. Ko-fi is the only support link; support is voluntary.

No unexplained file loss, silent overwrite or unrecoverable operation was observed
in the passing scenarios. This is bounded test evidence, not a guarantee.
