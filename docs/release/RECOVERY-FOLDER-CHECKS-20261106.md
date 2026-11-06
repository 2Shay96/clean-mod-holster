# Versionless recovery folder — 6 November 2026

Unpublished local update by Codex for 2Shay. Base private commit: 0c85b74.
Production EXE SHA-256: `a99abf03bddb84f2e1de3b05969e4b996bc9cb2695d9efdc2515dcdf3c1d823d`.

New sessions and logs use `game folder\_RDR2_ModHolster\`.
Existing `_RDR2_ModHolster31` sessions stay in place and are discovered alongside
new sessions. No automatic migration, merge or deletion is performed. Both store
names are excluded from scans and protected from file operations. Existing stores
are locked together during operations. Do not run older CLEAN versions concurrently.
Duplicate IDs across stores fail closed. Result dialogs show the actual session path.

## Reproduced on this Windows PC

- Production build: zero warnings/errors; engine38 PASS/2 Linux-only skips;
  release21 PASS/2 Linux-only skips; UI224 assertions PASS.
- Extended suite:20 PASS,0 skips/failures, including a10,000-file SHA roundtrip
  (178.197 seconds), fault injection, restart, ACLs, locks and LML conflicts.
- Native .NET Framework production-EXE suite:16 PASS,0 skips/failures, including
  a10,000-file SHA roundtrip (186.079 seconds), repeated cycles and LML recovery.
- An additional final5-case focused run passed on both runtimes, covering previous
  schema3.1 sessions, old-store locks, dual-store discovery, conflicts/moved-aside
  recovery, duplicate IDs, interrupted old-store recovery, old-store purge on
  disposable fixtures, and junction refusal including the previous store root.
  The fourth versionless test was added after the full suites; no production code
  changed between these runs. Counts are distinct runs, not all unique tests.
- About was visually inspected in the real Windows application: `_RDR2_ModHolster`
  appears without31; text and all three buttons fit. App left open for author review.
- Automated screenshot output can include desktop occlusion; it is not the release
  gallery. The approved1920x1080 About release screenshot still needs replacement.

Native long paths over260 characters were safely refused on this host, not proven
to roundtrip. No real game files/saves were operated on for this update. Real Story
Mode, genuine disk-full and other earlier evidence retain their original build labels.
No new remote CI result, public release, commit or push is claimed.

Evidence: `docs/evidence/recovery-folder-20261106/` (full outputs and XML reports).
New ZIP/package and exact extracted-EXE checks are recorded separately after freezing.
Earlier packages remain preserved for rollback.
