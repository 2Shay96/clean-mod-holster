param([string]$BinaryDirectory = 'src/bin/Release/net48')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$binary = (Resolve-Path (Join-Path $project $BinaryDirectory)).Path
$sdk = Join-Path $project '.dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }
& $sdk build (Join-Path $project 'tests/portable/PortableTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Portable harness build failed.' }
$fixture = Join-Path $project ('scratch/portable-game-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
foreach ($name in @('PortableTests.exe', 'PortableTests.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $project "tests/portable/bin/Release/net48/$name") -Destination $fixture
}
foreach ($name in @('CLEAN_v3.2.exe', 'CLEAN_v3.2.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $binary $name) -Destination $fixture
}
Set-Content -LiteralPath (Join-Path $fixture 'RDR2.exe') -Value 'DISPOSABLE game marker; not a game executable'
Set-Content -LiteralPath (Join-Path $fixture 'DISPOSABLE-PORTABLE-TEST.txt') -Value 'Disposable fixture only'
& (Join-Path $fixture 'PortableTests.exe') | Tee-Object -FilePath (Join-Path $fixture 'results.txt')
$result = $LASTEXITCODE
Write-Host "Preserved fixture and results: $fixture"
if ($result -ne 0) { throw "Portable checks failed: $result" }
