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
The source is licensed under LICENSE.txt; public visibility does not make it open source.

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

## Source builds and exact executable identity

The matching source ZIP builds without Git history using the commands above. A normal
build from another directory or Git context can have a different SHA-256 because the
compiler embeds deterministic source paths and automatic Git/SourceLink metadata.
Use the release archive checksums to identify the distributed executable.

For the 6 November local candidate, the extracted source rebuild matched both EXE and
config with the same parent Git context (0c85b740214fa183bb4dcb69361b6a7a89edd557)
and this source-path mapping, without disabling automatic SourceLink metadata:

```powershell
dotnet build src/RDR2ModHolster.csproj -c Release -t:Rebuild "-p:PathMap=$($PWD.Path)=C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster"
```

That byte-for-byte result is specific to the recorded source-path/Git context, not a
promise that an independent checkout or standalone ZIP build has the same debug
identity. An earlier attempt that disabled Git/SourceLink queries produced different
bytes and is retained in the evidence. The versionless-folder and About updates use
the ordinary build policy. A new commit/rebuild receives fresh hashes and matching
package checks. Source-path mapping does not redirect application data.
