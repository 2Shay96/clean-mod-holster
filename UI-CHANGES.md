# CLEAN - Mod Remover and Holster — v3.2 interface

Updated 29 October 2026. Supersedes the supplied v3.1 Desert Sunrise notes.
SafetyEngine was modified during Windows preparation; it is not byte-identical
to Alpha 1. Imports and older findings remain preserved as historical evidence.

## Current appearance

- Author-confirmed name, supplied multi-size C/star app/dialog icon and supplied
  RDR2 artwork inside the app header. No new standalone page banner is requested.
- Fixed Chinese Rocks title/subtitle vector compositions with shared left alignment,
  5% CLEAN letter spacing and proportional fitting in short headers. No working
  font or reusable glyph library ships.
- Consistent sidebar rows/label alignment and gold Commands, Groups and Tools headings.
- Clear supplied scalable action icons, explicit labels and distinct colours:
  amber Scan/Holster, green Restore, red Delete.
- An actionable Scan Mods button in the empty dashboard using the existing handler.
- Resizable, scrollable dialogs, visible focus, progress activity and recovery
  messages identifying session locations and the next step. Repeated verification
  passes are not presented as a whole-operation percentage or reliable ETA.

Asset terms/provenance: ARTWORK-NOTICE.txt, THIRD-PARTY-NOTICES.txt and
docs/HEADER-LETTERING.md, ICON-PROVENANCE.md, ACTION-ICONS.md.
The Holster icon adaptation remains CC BY-SA 3.0, outside the code licence.
The author accepted the header before requesting subsequent spacing/icon changes;
final packaged screenshots remain step 4 work.

## Actual Windows verification

Earlier checks exposed docking order, caption painting and text scaling defects.
Final 29 October checks found three further issues: fresh RichEdit dialogs after a
DPI change could scale incorrectly; native horizontal scrollbars could remain;
and the subtitle could clip in a short header.

Final15 corrects native document sizing/zoom per control, defers initial dialog
refresh until handles settle, shares a vertical-scroll panel, and fits both header
phrases/gap together. SafetyEngine and operation/review handlers were unchanged.

Actual 100/125/150/200%, compact resizing/scrolling, fresh/existing About dialogs,
sidebar reachability and Scan Mods Tab/Space activation were inspected. Actual
1366×768/100% and 1920×1080/150% layouts were inspected. The original 2560×1600/200%
was restored. Final15 passed 190 UI assertions and 12 production-assembly native
checks. Exact identities, screenshots and retained intermediate failures:
docs/release/FINAL-UI-CHECKS-20261029.md and
docs/evidence/final-ui-native-20261029/.

Earlier human checks separately confirm About Tab/Shift+Tab/visible focus/Space,
review arrows/Space/Escape, Cancel-focused Enter, and DELETE text-field Enter
leaving the dialog open then Escape cancelling. UI-only fixtures, no file operation.
Evidence: docs/evidence/manual-keyboard-human-results.json.

One physical display: mixed-monitor testing remains unavailable. Exhaustive
assistive-technology coverage is unverified. The owner-drawn dashboard does not
expose every visual row as an accessibility element; review uses a native ListView
and full text is available in Detailed log. Harness DPI simulations and old
Linux/Mono results are distinct from actual Windows Settings scaling checks.
