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
Install the .NET 8 SDK from Microsoft if it is not already available; the SDK is not
bundled. The build-only reference assemblies restore through NuGet. No Git history is
required to build the source snapshot. Repository: https://github.com/2Shay96/clean-mod-holster.
The repository remains private until the author approves public source publication.

Output: `src/bin/Release/net48/CLEAN_v3.2.exe` and its matching `.exe.config`.
Keep them together. Version 3.2.0.0 / informational version 3.2.0 identifies this v3.2 build.

`HOLSTER_TESTS` enables fault hooks only in test assemblies. `UI_TESTS` enables metric
simulation only in the UI harness. NativeTests references the production executable;
its report records the loaded assembly hash. Source/packaging checks reject test hooks
in production. Windows CI passed on 3 November 2026 at source commit
6c30496f659c5c45efe94fea0c825af56de281d2: engine38/release21/extended17/native13
passes, four explicit Linux-specific skips. CI does not test native UI or launch RDR2.
It rebuilds its own executable; it does not validate identical local packaged bytes.

Fixtures live under private `scratch/` or the system temporary directory. Extended
fixtures are retained for inspection. They are disposable and never point at a real game.
The native long-path test distinguishes roundtrip support from safe refusal under the
host policy. It does not enable Windows long-path policy. UI metric simulation does not
substitute for changing display scaling or moving between physical monitors.

The original Alpha 1 build/results are preserved under `upstream/` and `docs/evidence/`.
Old `UI-CHANGES.md` assertions about v3.1 and byte-identical safety code are superseded.
The modified release test now checks the intended v3.2 read-only roundtrip rather than
the old expectation of failure. That is a test correction, not removal of protection.

## Reproducing the packaged executable

Ordinary builds include the source location in deterministic debug/compiler paths,
so building from a different directory can change the executable hash. This was
observed and documented, rather than treated as an unexplained binary difference.
To reproduce this package's exact bytes from a relocated source snapshot, build with:

```powershell
dotnet build src/RDR2ModHolster.csproj -c Release -t:Rebuild "-p:PathMap=$($PWD.Path)=C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster" -p:EnableSourceControlManagerQueries=false -p:EnableSourceLink=false
```

The release verification records whether this mapping reproduces both EXE and config.
The original packaged build predates the Git repository, so the reproduction command
also disables automatic SourceLink/Git metadata. Otherwise a build inside a repository
can embed its revision/remote and change the hash even with identical production code.
The forced rebuild avoids reuse of prior compiler outputs. These build settings do not
redirect application data or change runtime behaviour.
