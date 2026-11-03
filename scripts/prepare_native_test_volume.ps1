# Run manually from an elevated PowerShell after reviewing this file.
# Creates one new 128MiB VHD in this project's scratch folder; never formats an existing disk.
$ErrorActionPreference = 'Stop'
$cleanPrincipal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $cleanPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Administrator token required to mount an isolated VHD. No volume was created.'
}
$cleanProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
if ($cleanProject -ne 'C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster') { throw 'Unexpected project path.' }
if (Get-PSDrive -Name T -ErrorAction SilentlyContinue) { throw 'T: is already in use; refusing.' }
if (Test-Path -LiteralPath 'T:\') { throw 'T: is already mounted; refusing.' }
$cleanFixture = Join-Path $cleanProject ('scratch\managed-native-volume-' + [guid]::NewGuid().ToString('N'))
$cleanImage = [IO.Path]::GetFullPath((Join-Path $cleanFixture 'clean-disposable.vhd'))
if (-not $cleanImage.StartsWith($cleanProject + '\scratch\managed-native-volume-', [StringComparison]::OrdinalIgnoreCase)) { throw 'Image escaped the intended scratch folder.' }
if ($cleanImage -notmatch '^[A-Za-z]:\\[a-zA-Z0-9 _\\.\-]+$') { throw 'Unsafe diskpart file-path characters.' }
if (Test-Path -LiteralPath $cleanFixture) { throw 'Fixture must be new.' }
New-Item -ItemType Directory -Path $cleanFixture | Out-Null
$cleanDiskpart = Join-Path $cleanFixture 'create-image.txt'
@("create vdisk file=`"$cleanImage`" maximum=128 type=fixed", 'exit') | Set-Content -LiteralPath $cleanDiskpart -Encoding ascii
& diskpart.exe /s $cleanDiskpart | Tee-Object (Join-Path $cleanFixture 'diskpart-output.txt')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $cleanImage)) { throw 'VHD creation failed; nothing else will be formatted.' }
$cleanMounted = Mount-DiskImage -ImagePath $cleanImage -StorageType VHD -PassThru
$cleanDisk = $cleanMounted | Get-Disk
if (@($cleanDisk).Count -ne 1 -or $cleanDisk.BusType -ne 'File Backed Virtual' -and $cleanDisk.BusType -ne 'FileBackedVirtual') { throw 'Not exactly one file-backed virtual disk; stopped.' }
if ($cleanDisk.Size -gt 140MB -or $cleanDisk.Size -lt 120MB -or $cleanDisk.PartitionStyle -ne 'RAW' -or $cleanDisk.IsBoot -or $cleanDisk.IsSystem) { throw 'Disk is not the expected new small RAW virtual disk; stopped.' }
$cleanDiskId = $cleanDisk.UniqueId
$cleanRecheck = Get-DiskImage -ImagePath $cleanImage | Get-Disk
if ($cleanRecheck.Number -ne $cleanDisk.Number -or $cleanRecheck.UniqueId -ne $cleanDiskId) { throw 'Disk identity changed; stopped.' }
Initialize-Disk -Number $cleanDisk.Number -PartitionStyle MBR | Out-Null
$cleanInitialized = Get-DiskImage -ImagePath $cleanImage | Get-Disk
if ($cleanInitialized.Number -ne $cleanDisk.Number -or $cleanInitialized.IsBoot -or $cleanInitialized.IsSystem) { throw 'Unexpected initialized image disk; stopped.' }
$cleanDiskId = $cleanInitialized.UniqueId
$cleanPartition = New-Partition -DiskNumber $cleanDisk.Number -UseMaximumSize -DriveLetter T
if ($cleanPartition.DiskNumber -ne $cleanDisk.Number -or $cleanPartition.DriveLetter -ne 'T') { throw 'Unexpected new partition; stopped before formatting.' }
$cleanRecheck = Get-DiskImage -ImagePath $cleanImage | Get-Disk
if ($cleanRecheck.Number -ne $cleanPartition.DiskNumber -or $cleanRecheck.UniqueId -ne $cleanDiskId) { throw 'Image/partition identity mismatch; stopped before formatting.' }
$cleanVolume = $cleanPartition | Format-Volume -FileSystem NTFS -NewFileSystemLabel CLEAN_DISPOSABLE -Confirm:$false
if ($cleanVolume.DriveLetter -ne 'T' -or $cleanVolume.FileSystemLabel -ne 'CLEAN_DISPOSABLE') { throw 'Unexpected formatted volume; stopped.' }
[IO.File]::WriteAllText('T:\CLEAN-DISPOSABLE-TEST-ONLY.txt', 'CLEAN disposable volume: no user files')
@{ image=$cleanImage; drive='T:\'; diskNumber=$cleanDisk.Number; uniqueId=$cleanDiskId; purpose='CLEAN disposable native-volume tests only' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $cleanFixture 'volume.json')
Write-Host "Prepared isolated T: volume. Retained VHD: $cleanImage"
Write-Host 'No test was run. Preserve the VHD and evidence; dismount it only after tests finish.'
