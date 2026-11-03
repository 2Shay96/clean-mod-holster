# Upgrade and recovery — CLEAN - Mod Remover and Holster

Keep an independent copy of mods, saves and every existing backup/session folder before
upgrading. A verified transfer is not a substitute for an independent backup.

Extract CLEAN outside the game installation. Keep the executable and its matching config
together. Do not run old BAT/v2.3/v3.0 tools against an installation holding new sessions:
they may treat `_RDR2_ModHolster31` as disposable content. v3.1 understands its own schema,
but does not understand v3.2 manifests. Finish recovery using the version that supports it.

## Supported backup formats

The engine reads schema 3.1/GUID sessions and schema 3.2/short-ID sessions under
`_RDR2_ModHolster31`. It also recognises `_RDR2_Mods_Disabled`,
`_RDR2_ReShade_Disabled` and `_RDR2_ENB_Disabled`, including their old
`Documents\modconfig` layout. Legacy backups have no original integrity record: checks
can verify the bytes present now, not prove that those bytes once launched successfully.
The historical v2.3 README mentions ModBackup, while its BAT uses the disabled-folder
names. Inspect the actual backup format; do not rename an unknown folder to force import.

## Interrupted holster or restore

1. Stop programs that may change these files. Keep the session store and independent backup.
2. Open Restore and choose the unfinished session. The engine checks recorded hashes/state.
3. If blocked, inspect every named conflict. Do not edit the XML to bypass a warning.
4. Keep any failed stage named in the log. It may help diagnose an incomplete transfer.
5. If a manifest is missing/corrupt/unreadable/oversized, retain the entire session and log.
   The UI will not guess a destructive repair or automatically replace the record with
   `manifest.xml.previous`. Seek a reviewed recovery using independent copies.

A `Purging` session means irreversible deletion began. Restore refuses it. Delete may
continue verified removal, but cannot recover bytes already purged. A success dialog for
file restoration does not prove game/mod version compatibility.

## LML reinstalled while mods were holstered

Restore lists all occupied destinations. **Move these aside, then restore** creates a
separate recoverable session for the reinstalled files, then retries the original restore.
Neither side is silently overwritten. Check the final result; a second failure can still
require attention. The displaced set appears as **Files moved aside** in Restore.

To recover the displaced set later, first independently back up the active files or
holster them into their own verified session. Restore the moved-aside set only into clear
destinations. Never delete the active files merely to dismiss a conflict.

## Moved installations and Documents

Select the current `RDR2.exe`. A session whose recorded game/Documents roots differ asks
for explicit consent before rebinding. Continue only for the same installation. Documents
is resolved through Windows' known-folder API, which can reflect a redirected location.
Junctions/symlinks in the selected roots/managed trees are refused.

## Repair, reinstall and disk errors

Copy the complete session store and saves elsewhere before launcher repair, uninstall,
drive migration or reinstall. Those operations may modify game files or affect files
inside the installation. CLEAN does not verify or restore modified base-game archives.
Do not claim launcher repair always deletes the store; its behaviour is launcher-specific.

For a sharing error, close the game/launcher/mod manager and retry. For denied access,
inspect the exact path and its permissions; do not disable antivirus or add blanket
exclusions. Read-only files are supported by v3.2. A free-space check reserves copy space,
but another program can consume it afterwards. Keep failed stages and retry recovery only
after space is available. Long-path policy is not changed by CLEAN; a shorter game folder
can reduce path pressure, and moving the store with it will require rebinding consent.
