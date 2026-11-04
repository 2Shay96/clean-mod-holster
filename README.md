# CLEAN - Mod Remover and Holster

CLEAN - Mod Remover and Holster by **2Shay** reviews, holsters, restores and permanently
deletes selected Red Dead Redemption 2 mod files. Known game/launcher content is
protected, unknown files start unchecked, and transfers use journalled backups and
SHA-256 checks. ReShade and ENB have separate actions.

## Version 3.2
Portable Windows release.
See [VALIDATION.md](VALIDATION.md) for actual Windows checks and remaining limits.
The old Linux/Mono screenshots/results are historical and are not native evidence.

Requires Windows and .NET Framework 4.8. Extract the complete archive outside the game
folder and keep the executable and matching `.exe.config` together. The build is unsigned.
It does not install mods, modify Windows security settings or require elevation.

1. Keep independent backups of saves, mods and existing CLEAN sessions.
2. Close RDR2, LML's manager and other programs that may change game files.
3. Browse to `RDR2.exe`, then **Scan Mods**.
4. Choose **Holster**, review selected items, then confirm. Unknowns remain unchecked.
5. Use **Restore** to recover a session. Existing destinations cause a conflict prompt.
6. **Delete** first holsters the selection, then requires a separate typed `DELETE`.
   Cancelling permanent deletion leaves the selection holstered.

Backups, manifests and logs live in `game folder\_RDR2_ModHolster31\`. The name is
kept for compatibility. Failed staging data is retained and its location is logged.
Conflicting live files may be moved into a separate verified session only after review;
these appear as **Files moved aside** and are recoverable through Restore.

Holstered mods do not prove a vanilla game. Changes inside `.rpf` files are not checked.
Filename recognition does not establish ownership of a file. Do not use Red Dead Online
as a test; CLEAN provides no guarantee about online restrictions or bans.

Read [UPGRADE-RECOVERY.md](UPGRADE-RECOVERY.md) before upgrading, repairing the game,
moving its folder or resolving an interrupted operation. Long paths may be refused on
Windows configurations without native long-path support. ACLs, alternate data streams
and hard-link topology are not preserved as a full backup system would preserve them.

The author-approved v3.2 licence is source-available with commercial restrictions; see
[LICENSE.txt](LICENSE.txt). Do not call it open source. Earlier published v2.3 MIT permissions survive;
the notices are retained in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
Build instructions: [BUILD.md](BUILD.md). Changes: [CHANGELOG.md](CHANGELOG.md).
The application header uses the author-supplied RDR2 promotional artwork. This is
separate third-party material, excluded from the code licence; see
[ARTWORK-NOTICE.txt](ARTWORK-NOTICE.txt). Its redistribution clearance remains unresolved. The author chooses release with the
current artwork; that decision is not rights-holder permission. CLEAN is opted out of
Nexus Donation Points.

[Support CLEAN through PayPal](https://www.paypal.com/donate/?hosted_button_id=G677UQMTPA5EL).
[Support 2Shay on Ko-fi](https://ko-fi.com/2shay).
Support is voluntary and does not buy access, updates, assistance or promised features.

CLEAN for GTA VI is planned, targeting day-one support for the PC release, subject to compatibility testing. Follow development here.

[Follow development](https://www.nexusmods.com/reddeadredemption2/mods/8084?tab=articles).
