# Final documentation-refresh packages — 7 November 2026

State: checked locally, unpublished. No production code or appearance changed.
Source baseline:45a3e2a53468a7460f4a10b5b4cc41294a682a7a plus the included
documentation/packaging refresh, awaiting its own commit/private push.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| CLEAN-v3.2-binary.zip |3183495|db4acf21e68f58e6722a6f6e789cd8307cfc40c0bb9b979f727e8ffdf55feaa3|
| CLEAN-v3.2-source.zip |3325142|574fc2fa40011c12709174566091b1a30ade61306917831287d8eabd722c46fe|

Folder:build/v32-final-review-20261107/. Separate SHA256SUMS.txt and per-member
package-manifest.json retained. Both ZIPs contain internal SHA256SUMS.txt.
The exact EXE is856a3cbd38c4e33a4dbdea8d6829a31b2821fa71ac047d557a337ef1f913f315;
config isf3f140a31d2664a30f1e42a9ac873e51e4d8e5f0af44f9a999554d71f2c7e4aa.

## Actual final checks

- Both whole-ZIP SHA-256 values, every member hash/size, internal checksums, CRCs,
  case-insensitive duplicate rejection and safe member paths PASS. Both archives
  extracted into new scratch folders; all source files match the working snapshot.
- All35 production inputs match the prior tested source snapshot; EXE/config remain
  byte-identical to the full Story/ReShade and LML conflict/recovery test executable.
  No production rebuild was performed. Original Git/path reproducibility limits
  remain documented in BUILD.md; CI produces its own executable.
- Native .NET Framework harness built against the extracted EXE:17 PASS,0 skips,
  0 failures; loaded assembly SHA verified in results.xml. Ten-thousand-file hash
  roundtrip134.667s. Covers old/new stores, locks, manifests, ACL/read-only failures,
  junctions, changing sources, fresh-process recovery, LML conflicts and repeated cycles.
- Native paths above260 characters safely refused on this host. That case's PASS
  does not establish a successful long-path-enabled roundtrip.
- Portable native GUI/engine harness using the extracted pair:17 PASS,0 failures.
  Adjacent game detection, actual message loop/open/normal close, protected runtime
  pair, three recovery cycles and fixture deletion/tampered-manifest safeguards passed.
- Harness builds:zero warnings/errors. All destructive/fault cases used disposable
  fixtures. No real-game operation occurred during final packaging checks.
- Ten approved PNG hashes and1920×1080 dimensions reverified; screenshot ZIP CRC PASS.
  No fresh screenshot or appearance change. Original4K capture provenance retained.
- Nexus offline helper verified both new ZIPs and emitted version3.2 metadata:
  binary main/update version, source optional, both manual-only. Archiving the intended
  v3.0 predecessor is prepared for publication, not executed.

## Evidence and rollback

- build/release-review-final-20261107/index.html: combined review and local receipts.
- scratch/extracted-v32-final-review-20261107/: extracted binary package.
- scratch/source-v32-final-review-20261107/: extracted source package.
- scratch/final-native-fixtures-20261107/results.xml:17 native test results and EXE hash.
- scratch/portable-game-c3ddbd866d0142a8bf3d273e939e2ba7/results.txt:17 portable checks.
- build/v32-portable-review-01/: preserved earlier tested rollback ZIPs, unchanged.
- docs/history/final-package-20261107/: superseded validation/build/changelog preserved.

Current Windows CI at45a3e2a passed38 engine/22 release/21 extended/17 native,
with four Linux-only skips. This documentation refresh has no new remote CI result yet.
Current game results and limitations are in VALIDATION.md and the two included game
reports. Official Rockstar artwork still has no written clearance; retained by author
choice, Donation Points OFF. Source-available licence adopted; older MIT rights survive.
No tag, release, repository visibility change, upload, site edit or message was sent.
