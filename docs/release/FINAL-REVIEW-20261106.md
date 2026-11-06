> Superseded by the portable-installation candidate. Current review: `build/release-review-portable-20261106/index.html`; current checks: [PORTABLE-FOLDER-CHECKS-20261106.md](PORTABLE-FOLDER-CHECKS-20261106.md). The results below describe the earlier review02 package.

# CLEAN v3.2 validation briefing — 6 November 2026

Candidate: v32-release-tonight-review-02. Unpublished, local source changes uncommitted.
EXE SHA-256: 750ecb56586451e5dbc824d3e55b51983195484cbb763d4678d1e0fe1d1625c8
Config SHA-256: f3f140a31d2664a30f1e42a9ac873e51e4d8e5f0af44f9a999554d71f2c7e4aa

Current build: zero warnings/errors; engine 38 PASS / 2 Linux-only skips; release 21
PASS / 2 Linux-only skips; UI 232 assertions PASS. About action labels, layout and
Close focus checked at simulated 100%, 125%, 150%, 200% and compact windows.

Exact packaged production EXE: 8 native checks PASS, no skips/failures:
old schema3.1 sessions/locks/conflicts, dual recovery stores, duplicate IDs fail closed,
interrupted old-store recovery/purge, readonly/sharing locks, junction refusal,
LML reinstall plus moved-aside recovery, and ten hash-verified repeated cycles.

Source snapshot rebuild: EXE and config hashes match using PathMap and the same
parent Git/SourceLink context (base0c85b74). A future commit/rebuild may change debug
identity and executable hash; it must receive matching fresh packages/checks.
ZIP CRC, paths, every member hash, embedded checksums and source correspondence PASS.
Exact ZIP identities:
c4e9eb091d1dec37fbab1c4cdbf888193e46f717b7e82ccabb405c7e36cb96ab  CLEAN-v3.2-binary.zip
ccac78d56323f955e23d8e50593db2a4bbf07bc2f823896a44bbad07d34a3e8d  CLEAN-v3.2-source.zip

Native packaged UI fixture: two example files holstered, app restarted, restored,
both original SHA-256 values equal the saved manifest. No real game files touched.
Fresh native4K/cursor-free captures downsampled to1920x1080: Holster result, Restore
confirmation/result and About. Six previously approved unchanged screens retained,
with provenance in gallery verification.json. Fixture closed normally.

Recorded preparation issues: first build could not replace the open preview EXE;
closed normally and rebuilt successfully. First screenshot byte expectation used LF;
original fixture was created with Windows CRLF. Corrected expected bytes and independent
manifest hashes both match. No safeguard weakened and no unexplained file loss.

The preceding versionless-folder candidate passed full extended20/native16, including
10,000-file hash cycles, plus final5 focused checks on both runtimes. SafetyEngine has
not changed since those results; current About changes received the checks above.
Earlier Steam Story testing with LML Beta11/ScriptHook preserved10 mod hashes and123
baseline hashes. Earlier genuine disk-full recovery passed twice. Those are earlier
build results, not a current final-build gameplay/disk-full claim.

Remote CI: base0c85b740214fa183bb4dcb69361b6a7a89edd557 SUCCESS, run42277857425,
job128098791307, verified6Nov. Current uncommitted update has no remote CI result.

Known limits: actual ReShade gameplay deferred; Steam only; other launchers, mixed
monitors, actual redirected Documents and physical power-loss durability unverified.
Native paths above260 characters were safely refused on this host, not roundtripped.
Historical crash causes unverified. No vanilla-integrity or online/ban guarantee.

Licence: adopted source-available noncommercial terms, older MIT rights survive.
Official RDR2 header art remains uncleared in writing; author chose to retain it.
Nexus Donation Points stays OFF. Ko-fi support voluntary; no promised features/access.

Evidence: docs/evidence/news-links-20261106/ and recovery-folder-20261106/.
Older build evidence stays dated and labelled. Previous packages retained for rollback.

Documentation-only repack02 corrects the stale private-preview notice and build reproduction instructions. EXE/config are byte-identical to tested package01; exact EXE native8 results still apply. Source02 rebuild also matches EXE/config. No runtime source change.
RDR2Mods verified signed in as2Shay; Tools submissions require moderator approval. Ko-fi public destination verified as2Shay without payment.
