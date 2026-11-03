param([switch]$SkipTests, [switch]$Extended, [switch]$UiChecks)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (Test-Path (Join-Path $PSScriptRoot '.dotnet\dotnet.exe')) {
        $env:DOTNET_ROOT = Join-Path $PSScriptRoot '.dotnet'
        $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    }
    if (-not $SkipTests) {
        & dotnet build tests/SafetyTests.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Engine test build failed.' }
        & dotnet tests/bin/Release/net8.0/SafetyTests.dll
        if ($LASTEXITCODE -ne 0) { throw 'Engine tests failed.' }
        & dotnet build tests/release/ReleaseTests.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Release test build failed.' }
        & dotnet tests/release/bin/Release/net8.0/ReleaseTests.dll
        if ($LASTEXITCODE -ne 0) { throw 'Release tests failed.' }
    }
    & dotnet build src/RDR2ModHolster.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows executable build failed.' }
    if ($Extended -and -not $SkipTests) {
        foreach ($suite in @('ExtendedTests','NativeTests')) {
            & dotnet build "tests/extended/$suite.csproj" -c Release
            if ($LASTEXITCODE -ne 0) { throw "$suite build failed." }
            $evidence = Join-Path $PSScriptRoot "scratch/$suite-$(Get-Date -Format yyyyMMdd-HHmmss)"
            if ($suite -eq 'ExtendedTests') { & dotnet tests/extended/bin/Release/net8.0/ExtendedTests.dll $evidence }
            else { & ./tests/extended/bin/Release/net48/NativeTests.exe $evidence }
            if ($LASTEXITCODE -ne 0) { throw "$suite failed." }
        }
    }
    if ($UiChecks) {
        & dotnet build tests/ui/UiChecks.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'UI checks build failed.' }
        & ./tests/ui/bin/Release/net48/UiChecks.exe (Join-Path $PSScriptRoot 'scratch/ui-checks')
        if ($LASTEXITCODE -ne 0) { throw 'UI checks failed.' }
    }
    Write-Host 'Built src/bin/Release/net48/CLEAN_v3.2.exe'
    Write-Host 'Distribute the matching .exe.config too. Test the GUI on Windows before public rollout.'
} finally { Pop-Location }
