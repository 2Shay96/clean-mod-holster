# Manual native keyboard exercise

Build with the .NET SDK, output to a new scratch directory, then launch
CleanKeyboardReview.exe. Keep its copied production executable/config beside it.
This harness invokes only ReviewCandidates, ShowTextDialog and RequireDeleteText on
the production form. Candidate rows are synthetic strings. It never constructs a
SafetyEngine or invokes scan/holster/restore/purge. Only manual-results.xml is written.
The owner dashboard is disabled while each modal is open and closed after the exercise.

1. In Review, try Tab/Shift+Tab, arrows and Space on a list row, then Escape.
2. In confirmation, press Enter with Cancel focused; it must cancel.
3. In Delete, enter DELETE then Enter while the text field remains focused; it must
   remain open. Escape must cancel.

The XML records return values, not proof of physical keys. Record the human's report
separately. Never claim a mouse-cancelled dialog proves keyboard routing. The runner
refuses to overwrite its result file; use a new output directory for another run.
Do not inject keys or invoke file-operation handlers to run this exercise.
Pass --delete-only to repeat just step3 in a fresh output directory.
