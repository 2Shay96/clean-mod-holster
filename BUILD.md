# Build CLEAN - Mod Remover and Holster

Windows build tool: .NET 8 SDK. Production runtime: .NET Framework 4.8, WinForms.
The exact local SDK download and published SHA512 are recorded in `docs/evidence/sdk.json`.
The build-only reference package is Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3.
No third-party runtime packages or game/mod files are bundled.

```powershell
.\build.ps1                 # existing engine/release fixtures, then production build
.\build.ps1 -Extended       # adds fault probes and native .NET Framework checks
.\build.ps1 -UiChecks       # native WinForms harness; interactive desktop required
```

The script uses the private `.dotnet/` SDK if present, otherwise `dotnet` from PATH.
The authoring toolchain installer `scripts/install_sdk.py` downloads only from Microsoft,
checks its published SHA512 and extracts under `.dotnet/`; it makes no system install.
The local preparation checkout currently has no remote or commits. A published source
snapshot can be built without Git history; this does not imply remote CI has passed.

Output: `src/bin/Release/net48/CLEAN_v3.2.exe` and its matching `.exe.config`.
Keep them together. Version 3.2.0.0 / informational version 3.2.0 is a
private release candidate; public publication is not approved.

`HOLSTER_TESTS` enables fault hooks only in test assemblies. `UI_TESTS` enables metric
simulation only in the UI harness. NativeTests references the production executable;
its report records the loaded assembly hash. Source/packaging checks reject test hooks
in production. Windows CI is prepared but has not been run on GitHub.

Fixtures live under private `scratch/` or the system temporary directory. Extended
fixtures are retained for inspection. They are disposable and never point at a real game.
The native long-path test distinguishes roundtrip support from safe refusal under the
host policy. It does not enable Windows long-path policy. UI metric simulation does not
substitute for changing display scaling or moving between physical monitors.

The original Alpha 1 build/results are preserved under `upstream/` and `docs/evidence/`.
Old `UI-CHANGES.md` assertions about v3.1 and byte-identical safety code are superseded.
The modified release test now checks the intended v3.2 read-only roundtrip rather than
the old expectation of failure. That is a test correction, not removal of protection.
