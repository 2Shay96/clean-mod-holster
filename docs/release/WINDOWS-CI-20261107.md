# Windows CI — 7 November 2026

Source commit:45a3e2a53468a7460f4a10b5b4cc41294a682a7a.
Workflow:.github/workflows/windows-ci.yml. Windows runner, .NET8 SDK, production net48.
Run:https://github.com/2Shay96/clean-mod-holster/actions/runs/42315170543
Job:128225883538. Completed SUCCESS; all build, test and artifact steps passed.

| Suite | Pass | Skip | Fail |
|---|---:|---:|---:|
| Engine |38|2 Linux-only|0|
| Release |22|2 Linux-only|0|
| Extended .NET8 |21|0|0|
| Native .NET Framework4.8 |17|0|0|

Builds reported zero warnings/errors. Ten-thousand-file hash roundtrips:
60.839s (.NET8),61.843s (native). A long-path case accepts either roundtrip or safe
policy refusal; this PASS does not establish native long-path-enabled support.
CI builds a separate executable. Native UI, portable placement, actual game cycles
and genuine disposable-volume disk-full tests are separate local evidence.
Repository was private when verified. No publication or tag was created.
The subsequent final documentation/packaging refresh changes no production input.
Local verification receipt:build/release-review-game-tested-20261106/PUSH-RECEIPT.json.
