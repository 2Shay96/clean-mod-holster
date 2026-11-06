# CLEAN - Mod Remover and Holster v3.2

Version 3.2. Prepared for author review on 3 November 2026; no previous v3.2 distribution.

Alpha 1 already included reviewed selections, unchecked unknowns, protected paths,
journalled SHA256 inventories, staging, resumable operations, no silent overwrites,
all-conflict reporting, moved-aside recovery, root-rebinding consent, manifest size
guards and conditional LML NLog recognition.

Windows preparation changes:

- Support the portable EXE/config pair beside RDR2.exe: detect the adjacent game and
  protect CLEAN's runtime config during scan, recovery and conflict resolution.
  Keep unrelated mod configs selectable. Document the alternative shortcut setup.
- Use the versionless `_RDR2_ModHolster` recovery folder for new sessions. Existing
  `_RDR2_ModHolster31` sessions remain recoverable in place; both stores are protected
  and locked during operations. Duplicate session IDs are refused without merging.
- Re-read/checksum staged bytes before commit; verify restored destinations and
  remaining backups before backup removal. Reject file/directory case aliases;
  use a path dictionary for large inventories.
- Give session paths and concrete recovery steps. Correct disk-full wording so
  partial staging is not described as a verified backup. Real error 112 recovery tested.
- Clear stale results when Delete is cancelled after holstering; retain separate
  typed DELETE confirmation.
- Use confirmed product name, supplied multi-size icon and app-only RDR2 artwork.
  Align fixed title/subtitle vectors, add 5% CLEAN tracking and fit short headers.
  No working font ships.
- Align sidebar rows/gold headings; use clearer supplied scalable action icons,
  explicit labels and distinct Holster/Restore/Delete colours.
- Make empty-dashboard Scan Mods a real keyboard-accessible action.
- Fix native DPI docking, caption painting, fresh-dialog text sizing and phantom
  horizontal scrollbars. Relevant final checks 190 UI/native 12; actual four scales
  and two common resolutions inspected.
- Add About/Support with the confirmed Ko-fi link and conditional GTA VI roadmap.
  Show activity/bytes without misleading whole-operation percentages or ETAs.
- Correct Windows readonly expectations and explicitly skip Linux-only fixtures.
  Add native safety/fault/recovery/load checks, build docs and local CI setup.
- Adopt author-approved source-available noncommercial code licence. Earlier MIT
  rights survive. Retain asset notices including Holster CC BY-SA 3.0 and separate art.

Actual Windows results, build identities and earlier Steam/LML/Script Hook game
evidence: VALIDATION.md. Historical crashes are not proven fixed. No Online or ban
safety claim. Exact-package checks and remaining limits are recorded in the accompanying validation report.

### Development links
- About now clearly labels News & Support on Ko-fi and Source & Issues on GitHub. GTA VI news points to Ko-fi; support remains voluntary.
