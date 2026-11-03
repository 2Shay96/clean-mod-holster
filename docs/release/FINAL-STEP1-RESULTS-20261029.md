# Final-source regression — 29 October 2026

Result: PASS. Current icons07 source built on this Windows PC with zero build
warnings/errors. No production code or appearance changes were needed.

| Check | Actual result |
|---|---|
| Engine suite, .NET 8 | 38 passed; 2 explicit Linux-only fixture skips |
| Independent release suite, .NET 8 | 21 passed; 2 Linux-specific fixture skips |
| UI harness, .NET Framework 4.8 | 190 assertions passed |
| Production-EXE native suite, .NET Framework 4.8 | 12 passed, no skips/failures |
| Production-EXE abrupt worker termination/restart | Passed; original SHA-256 retained |
| Instrumented .NET 8 fault injection | 4 passed, no skips/failures |

Production checks covered read-only files, sharing locks/retry, denied read access,
unreadable/missing/oversized manifests, case aliases, junction rejection, files
changing type, separate-process locking, restart recovery, LML reinstall conflicts
and recovery of moved-aside content, long-path safe refusal, and ten repeated
hash-verified cycles. Abrupt termination killed only a disposable test worker
during staging; fresh-engine recovery preserved the original file hash.

Instrumented checks covered staged-byte corruption, source mutation during copy,
interrupted restore recovery and injected disk-space shortfall. Injection is not
a new genuine disk-full test; the earlier disposable-VHD evidence remains separate.

All operation fixtures were disposable. No real game files, saves or user mods
were operated on. UI checks compile the current UI sources into a harness;
they do not constitute a fresh native OS scaling/physical keyboard inspection.

## Exact build and retained evidence

EXE SHA-256:
`76c38fd1efd79c38c7e512c9693196eeb1a001f2227b0e10e0b88a9c6180b277`

Matching configuration SHA-256:
`f3f140a31d2664a30f1e42a9ac873e51e4d8e5f0af44f9a999554d71f2c7e4aa`

The native suite, abrupt-recovery runner, current production build and retained
payload copies all matched that EXE hash. Source/asset hashes are recorded in
`docs/evidence/final-step1-20261029-161534/source-payload-receipt.json`.
That directory also contains full build/native transcripts, release JSON, native,
abrupt-recovery and injection XML reports. Disposable fixtures and retained
EXE/config are in `scratch/final-step1-20261029-161534/`.

## Remaining boundary

The long-path test passed by safely refusing this host's unsupported path; a
successful over-260-character roundtrip is unverified. The earlier 10,000-file
load, genuine disk-full tests and Steam Story Mode cycle were not rerun for these
cosmetic changes. They retain their earlier build/evidence labels.

This completes the current-source part of checklist step 1. Final ZIPs have not
been created: verify the extracted package has this exact EXE/config and repeat
the relevant production checks when packaging in step 4. Native rendering checks
in step 2 remain next. No commit, push, tag or publication occurred.
