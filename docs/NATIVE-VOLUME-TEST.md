# Isolated native volume test — actual results recorded

This PC's agent token is not elevated. The user mounted the isolated T: VHD on 27 October.
Never fill C: to test disk exhaustion. The fixture requires a dedicated 96–256MiB NTFS
volume labelled `CLEAN_DISPOSABLE` with the exact marker checked by the executable.

`scripts/prepare_native_test_volume.ps1` prepares a new 128MiB VHD under this project's
scratch folder. It requires a manually elevated PowerShell. It refuses occupied T:,
checks the new image maps to one small non-system RAW file-backed virtual disk, and
rechecks image/partition identity before formatting only that new virtual partition.
It retains the VHD, setup log and identity file. It does not run tests or elevate itself.
The user executed the script successfully from elevated PowerShell. Image identity,
T: label, partition and sentinel were verified before running tests.

For a new test setup only, review the script and run it from elevated PowerShell
(the existing T: must not be replaced; setup refuses it). The user used a process-only
execution policy override because the default Windows PowerShell policy blocks scripts:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster\scripts\prepare_native_test_volume.ps1"
```

Historical first-run command below; its evidence folder already exists and is never
overwritten. A repeat requires a new folder and enough free room on the small volume:

```powershell
& 'C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster\scratch\native-volume-probe\NativeVolumeTests.exe' 'T:\' 'C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster\scratch\native-volume-results-01' 'C:\Users\Shadow\Desktop\VibeCoding\clean-mod-holster'
```

The first fixture references exact extracted review02; the second references the rebuilt
executable with corrected disk-full wording. Both PASS results are recorded in
`docs/evidence/native-volume-results-01.xml` and `native-volume-results-02.xml`.
The second run also requires exactly one readable interrupted journal and checks the
failure message does not promise a complete backup. The fixture creates only fake
game/config files on the marked volume, fills that small volume during an actual staged
copy, requires a native disk-full error and failure with the original hash intact, then
copies its filler to project `_to_delete` and verifies that copy before removing the
disposable volume's filler. It restarts recovery and verifies hashes. It also exercises
Documents configuration between two real volumes. It does not redirect the Windows
Known Folder or touch real saves. The two actual runs both observed native error112.

After tests, retain `results.xml` and the VHD before dismounting that exact image through
Disk Management or `Dismount-DiskImage -ImagePath` using its verified `volume.json` path.
No disk/image deletion is requested. A guard check against C: refused before creating
its evidence folder (`docs/evidence/native-volume-system-guard.txt`).
