# Native Windows UI verification — 29 October 2026

Result: PASS for the checks below. Three UI defects were found and corrected.
Public publication, final ZIP verification and the remaining release checklist
are still pending. This is actual Windows evidence, separate from older Mono
results and from the simulated-DPI UI harness.

## Findings and fixes

- Fresh About/message dialogs opened after a Windows scaling change could show
  oversized or undersized rich text. Deferred refresh alone did not fix this.
  The app now sets document character sizes in twips and compensates for each
  RichEdit control's native DPI baseline, updating that baseline on real child
  DPI notifications. Both existing and newly opened dialogs were checked.
- Native horizontal scrollbars could appear after scaling even while WinForms
  reported them hidden. Sidebar and dashboard now share a vertical-scroll panel
  that clears the native horizontal-bar style while retaining vertical scrolling.
- The subtitle could clip in a short header. Both vector phrases and their gap
  now shrink together to fit the available height, preserving their shared left
  edge and title tracking. Normal-height lettering remains unchanged.

Production edits: `src/UI/NativeTextMetrics.cs`, `ThemedDialog.cs`,
`MainForm.Layout.cs`. SafetyEngine and operation/review handlers are unchanged.
The diagnostic probe now records dashboard bounds and native/control DPI too.

## Actual checks

| Check | Result and evidence |
|---|---|
| Windows scaling 100%, 125%, 150%, 200% | Final15 compact screenshots and `final15-metrics.xml`; no clipping or horizontal bars in inspected layouts |
| Native 1366×768 at 100% | `display-1366x768-100.png`, `dashboard-final15-1366x768-100.png` |
| Native 1920×1080 at 150% | `display-1920x1080-150.png`, `dashboard-final15-1920x1080-150.png` |
| Small dashboard/header and sidebar reachability | Compact screenshots; `sidebar-final15-scrolled-200.png` shows About and Exit reachable |
| Compact About, wrapped action buttons and text scrolling | `about-final13-compact-100.png` and `about-final13-compact-scrolled-100.png`; same dialog/text code as final15 |
| Existing About across DPI changes | Final13 native line advances 21/25/17 pixels at 125/150/100%; correct document character size retained |
| Fresh About after scaling | Final13 at 100%; final15 at 200%: 9.5-point document, 35-pixel line advance, compensation zoom 1.6 for a process started at 125% |
| Scan Mods keyboard input | Final14 compact100% and final15 normal executable200%: Tab exposed visible focus and Space completed read-only scan; `normal-final15-keyboard-scan.json` and focus/result screenshots retained |
| UI harness on final15 source | 190 assertions passed; zero build warnings/errors |
| Production assembly on final15 | 12 native safety/recovery checks passed, no skips/failures; long paths passed by safe refusal |

Evidence directory: `docs/evidence/final-ui-native-20261029/`.
All intermediate failures, screenshots and metrics remain there; files named
fix08 through fix12 are diagnostic attempts, not accepted release results.
Final14 standard regression also passed engine38/release21 with four explicit
Linux-specific skips. The last change, final15, shares the panel correction with
the sidebar; relevant UI190 and exact-assembly native12 checks were rerun.

The scan reported zero recommended and three unknown log files. A read-only
follow-up verified all ten controlled mod hashes in holstered session
`66f074d07525889a`, absent from live paths. Evidence:
`docs/evidence/game/ui-final15-current-holstered-20261029.json`.
The older statement that those files were active is superseded. No restore,
holster or deletion was performed on the real game in this session.

## Exact build and handoff

EXE SHA-256 `8431b008f7e6207d57c726c984dc12fe7d53b5f501652a032c76572103b2ea9e`.
Configuration SHA-256 `f3f140a31d2664a30f1e42a9ac873e51e4d8e5f0af44f9a999554d71f2c7e4aa`.
Current production, native-runner reference and retained preview copies match.
Source hashes: `final15-source-receipt.json`; regression transcript:
`final15-ui-native-regression.txt`; native report: `final15-native-results.xml`.
Retained EXE/config: `scratch/native-ui-final15/`. Candidate04 remains untouched.

The initial 2560×1600 at 200% display setting was restored and verified after
the common-resolution checks. Windows' timed resolution confirmation reverted
several attempts before restoration succeeded; these were automation timing
issues, not CLEAN test failures. Superseded preview windows were closed; their
files and evidence were preserved.

One physical display: mixed-monitor transitions remain untested. Prior human
About/review/delete keyboard confirmations remain separately recorded; they were
not newly reproduced by a human today. Final ZIP extraction/source correspondence
and packaged-build verification remain required after packaging.

Implementation references: Microsoft [EM_SETCHARFORMAT](https://learn.microsoft.com/en-us/windows/win32/controls/em-setcharformat),
[CHARFORMATW](https://learn.microsoft.com/en-us/windows/win32/api/richedit/ns-richedit-charformatw),
[EM_SETZOOM](https://learn.microsoft.com/en-us/windows/win32/controls/em-setzoom),
and [DynamicDPI RichEdit sample](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/DynamicDPI/cpp/SampleRichEditControl.cpp).
The baseline behaviour described above is a finding on this PC, not a claim
that all Windows/RichEdit versions have identical internals.

Next action: reconcile final release documentation and page copy before packaging.
