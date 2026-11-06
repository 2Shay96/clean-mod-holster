using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using RDR2ModHolster.Safety;

namespace RDR2ModHolster
{
    // Drawing and chrome intentionally retain the recovered Desert Sunrise look.  All file decisions
    // and all mutations live in SafetyEngine; this form only presents its review/confirmation workflow.
    internal sealed partial class MainForm : DpiWindow
    {
        private static readonly string[] SearchPatterns =
        {
            "Program Files (x86)\\Steam\\steamapps\\common\\Red Dead Redemption 2",
            "Program Files\\Steam\\steamapps\\common\\Red Dead Redemption 2",
            "Steam\\steamapps\\common\\Red Dead Redemption 2",
            "SteamLibrary\\steamapps\\common\\Red Dead Redemption 2",
            "Games\\Steam\\steamapps\\common\\Red Dead Redemption 2",
            "Program Files\\Epic Games\\RedDeadRedemption2",
            "Epic Games\\RedDeadRedemption2",
            "Program Files\\Rockstar Games\\Red Dead Redemption 2",
            "Rockstar Games\\Red Dead Redemption 2",
            "Games\\Red Dead Redemption 2"
        };

        private string _game = "";
        private readonly string _documents;
        private bool _busy;
        private bool _detailedLog;
        private ScanResult _lastScan;
        private readonly List<ModEntry> _dashboardItems = new List<ModEntry>();
        private readonly List<string> _activity = new List<string>();
        private readonly List<SideButton> _buttons = new List<SideButton>();
        private int _recommendedCount;
        private int _unknownCount;
        private int _protectedCount;
        private int _scriptHookCount;
        private int _tableHover = -1, _tableY, _tableEndY, _tableRowH;

        private Panel _titleBar;
        private Panel _sidebar;
        private Panel _dashPanel;
        private Panel _logPanel;
        private RichTextBox _logBox;
        private Label _status;
        private Label _path;
        private ToggleSwitch _detailToggle;

        public MainForm()
        {
            _documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Rockstar Games", "Red Dead Redemption 2");
            FindGame();
            BuildUi();
            FormClosing += OnFormClosing;
        }


        private void FindGame()
        {
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (File.Exists(Path.Combine(appDirectory, "RDR2.exe"))) { SetGame(appDirectory); return; }
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    foreach (string relative in SearchPatterns)
                    {
                        string candidate = Path.Combine(drive.RootDirectory.FullName, relative);
                        if (File.Exists(Path.Combine(candidate, "RDR2.exe"))) { SetGame(candidate); return; }
                    }
                }
            }
            catch { /* Auto-detection is optional; actions validate through SafetyEngine. */ }
        }

        private void SetGame(string path)
        {
            _game = path ?? "";
            if (_path != null) _path.Text = String.IsNullOrEmpty(_game) ? "  No game folder" : "  " + _game;
            RefreshDashboard();
        }

        private SafetyEngine NewEngine()
        {
            if (String.IsNullOrWhiteSpace(_game)) throw new InvalidOperationException("No game folder is set. Use Browse to select RDR2.exe.");
            var engine = new SafetyEngine(_game, _documents, EngineLog);
        engine.Progress = OnProgress;
        return engine;
        }

        private async Task ScanAsync()
        {
            ScanResult scan = await Worker("Scanning", delegate { return NewEngine().Scan(); });
            if (scan == null) return;
            ApplyScan(scan);
            string warning = scan.Warnings != null && scan.Warnings.Count > 0 ? " Scan warnings require review before changes." : "";
            Status("Scan complete — " + _recommendedCount + " recommended, " + _unknownCount + " unknown" + warning);
            Activity(scan.Warnings != null && scan.Warnings.Count > 0 ? "✘ Scan completed with safety warnings" : "✔ Scan complete — " + _recommendedCount + " recommended candidate(s)");
            LogScan(scan); RefreshDashboard();
        }

        private async Task HolsterAsync(ModGroup group)
        {
            ScanResult scan = await FreshScanForReview(group, true);
            if (scan == null) return;
            List<Candidate> selected = ReviewCandidates(group, scan, "Holster");
            if (selected == null || selected.Count == 0) return;
            if (!ConfirmSelection("Holster " + GroupName(group), selected, "The selected items will be moved into a recoverable verified backup session. ReShade and ENB are separate groups.")) return;
            OperationResult result = await Worker("Holstering " + GroupName(group), delegate { return NewEngine().Holster(group, selected); });
            if (result != null && result.Success) InvalidateAfterMutation();
            HandleResult(result, "Holster");
        }

        private async Task RestoreAsync(ModGroup group)
        {
            RecoveryChoice choice = await Worker("Checking backups", delegate
            {
                SafetyEngine engine = NewEngine();
                return new RecoveryChoice { Sessions = engine.GetSessions(group), HasLegacy = engine.HasLegacy(group) };
            });
            if (choice == null) return;
            if (choice.Sessions.Count == 0 && !choice.HasLegacy) { ShowInfo("Restore " + GroupName(group), "No backup is available for " + GroupName(group) + "."); return; }
            if (choice.Sessions.Count > 0)
            {
                SessionInfo session = PickSession(group, choice.Sessions);
                if (session == null) return;
                if (String.Equals(session.State, "Purging", StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfo("Permanent deletion in progress", "This session is in Purging state and cannot be restored safely. Use Delete to finish the permanent deletion; no recovery promise is available.");
                    return;
                }
                if (!Confirm("Restore " + GroupName(group), "Restore " + (session.Kind == SessionKind.Quarantine ? "the set of files that were moved aside" : "session") + " " + session.Id + " (" + session.State + ", " + session.ItemCount + " item(s))?\n\nNothing already in the game folder is overwritten. If anything is in the way you will be shown exactly what, before any change is made.")) return;
                await RestoreSessionAsync(group, session);
                return;
            }
            if (!Confirm("Legacy backup", "A legacy " + GroupName(group) + " backup was found. It has no original integrity manifest. Existing changed files are not overwritten; verified interrupted transfers can resume, and conflicts may be reported.\n\nRestore it now?")) return;
            OperationResult legacy = await Worker("Restoring legacy " + GroupName(group), delegate { return NewEngine().RestoreLegacy(group); });
            if (legacy != null && legacy.Success) InvalidateAfterMutation();
            HandleResult(legacy, "Legacy restore");
        }

        // A restore is attempted, and if the game folder holds something in the way the user
    // is shown every blocker and may move those files into their own recoverable session
    // before the restore is retried.  Nothing is overwritten on either path.
    private async Task RestoreSessionAsync(ModGroup group, SessionInfo session)
    {
        bool rebind = false;
        if (session.NeedsRebind)
        {
            if (!ConfirmRebind(session, null)) return;
            rebind = true;
        }
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool allowRebind = rebind;
            OperationResult result = await Worker("Restoring " + GroupName(group), delegate { return NewEngine().Restore(session.Id, allowRebind); });
            if (result == null) return;
            if (result.Success) { InvalidateAfterMutation(); HandleResult(result, "Restore"); return; }
            if (result.NeedsRebind && !rebind)
            {
                if (!ConfirmRebind(session, result.Message)) { HandleResult(result, "Restore"); return; }
                rebind = true; attempt--; continue;
            }
            if (result.Conflicts.Count == 0 || attempt > 0) { HandleResult(result, "Restore"); return; }
            Status("Restore blocked — " + result.Conflicts.Count + " item(s) in the way; nothing was changed");
            Activity("✘ Restore blocked by " + result.Conflicts.Count + " item(s)");
            LogError("  Restore blocked; nothing was changed:");
            foreach (RestoreConflict c in result.Conflicts) LogError("    • " + c.Name + " — " + c.Reason);
            ConflictChoice choice = ShowConflicts(group, result);
            if (choice == ConflictChoice.OpenFolder) { OpenFolder(); HandleResult(result, "Restore"); return; }
            if (choice != ConflictChoice.MoveAside) { HandleResult(result, "Restore"); return; }
            bool asideRebind = rebind;
            OperationResult aside = await Worker("Moving conflicting files aside", delegate { return NewEngine().MoveConflictsAside(session.Id, asideRebind); });
            if (aside == null) return;
            if (!aside.Success) { HandleResult(aside, "Move aside"); return; }
            LogOk("  " + aside.Message); Activity("✔ Conflicting files moved aside");
            if (!String.IsNullOrEmpty(aside.SessionId)) LogDim("  Moved-aside session: " + aside.SessionId);
        }
    }

    private async Task DeleteAsync(ModGroup group)
        {
            RecoveryChoice choice = await Worker("Checking backup inventory", delegate
            {
                SafetyEngine engine = NewEngine();
                return new RecoveryChoice { Sessions = engine.GetSessions(group), HasLegacy = engine.HasLegacy(group) };
            });
            if (choice == null) return;
            if (choice.HasLegacy || choice.Sessions.Any(s => !String.Equals(s.State, "Holstered", StringComparison.OrdinalIgnoreCase) && !String.Equals(s.State, "Purging", StringComparison.OrdinalIgnoreCase)))
            {
                ShowInfo("Recovery required", "An unfinished session or legacy backup exists for " + GroupName(group) + ". It will not be purged. Use Restore/recovery first; no backup was removed.");
                return;
            }
            List<SessionInfo> deletableSessions = choice.Sessions.Where(s => String.Equals(s.State, "Holstered", StringComparison.OrdinalIgnoreCase) || String.Equals(s.State, "Purging", StringComparison.OrdinalIgnoreCase)).ToList();
            if (deletableSessions.Count > 0)
            {
                SessionInfo deletable = deletableSessions.Count == 1 ? deletableSessions[0] : PickSession(group, deletableSessions);
                if (deletable == null) return;
                bool interrupted = String.Equals(deletable.State, "Purging", StringComparison.OrdinalIgnoreCase);
                string prompt = interrupted ? "A permanent deletion is already in progress or was interrupted. Continue deleting its verified backup payload?" : "Permanently delete the holstered backup?";
                if (!Confirm("Permanently delete backup", prompt + "\n\nGroup: " + GroupName(group) + "\nSession: " + deletable.Id + "\nState: " + deletable.State + "\nItems: " + deletable.ItemCount + "\n\nThis permanently deletes only this backup payload and cannot be undone.")) return;
                if (!RequireDeleteText()) return;
                OperationResult purge = await Worker(interrupted ? "Continuing permanent deletion" : "Purging backup", delegate { return NewEngine().Purge(deletable.Id); });
                if (purge != null && purge.Success) InvalidateAfterMutation();
                HandleResult(purge, interrupted ? "Continue purge" : "Purge");
                return;
            }
            ScanResult scan = await FreshScanForReview(group, false);
            if (scan == null) return;
            List<Candidate> selected = ReviewCandidates(group, scan, "Holster before delete");
            if (selected == null || selected.Count == 0) return;
            if (!ConfirmSelection("Holster then delete " + GroupName(group), selected, "The selected items are first placed in a recoverable backup. You will then type DELETE before permanent purge.")) return;
            OperationResult holster = await Worker("Holstering selected items", delegate { return NewEngine().Holster(group, selected); });
            if (holster != null && holster.Success) InvalidateAfterMutation();
            HandleResult(holster, "Holster");
            if (holster == null || !holster.Success) return;
            if (!RequireDeleteText()) { ShowInfo("Items safely holstered", "Deletion was cancelled. The selected items remain safely holstered in session " + holster.SessionId + "."); return; }
            OperationResult purgeResult = await Worker("Permanently deleting backup", delegate { return NewEngine().Purge(holster.SessionId); });
            if (purgeResult != null && purgeResult.Success) InvalidateAfterMutation();
            HandleResult(purgeResult, "Purge");
        }

        private async Task GroupMenuAsync(ModGroup group)
        {
            GroupAction action = ShowGroupMenu(group);
            if (action == GroupAction.Holster) await HolsterAsync(group);
            else if (action == GroupAction.Restore) await RestoreAsync(group);
            else if (action == GroupAction.Delete) await DeleteAsync(group);
        }

        private async Task<ScanResult> FreshScanForReview(ModGroup group, bool rejectLegacy)
        {
            PreflightScan preflight = await Worker("Scanning " + GroupName(group), delegate
            {
                SafetyEngine engine = NewEngine();
                return new PreflightScan { Scan = engine.Scan(), HasLegacy = engine.HasLegacy(group) };
            });
            if (preflight == null) return null;
            ApplyScan(preflight.Scan); LogScan(preflight.Scan); RefreshDashboard();
            if (preflight.Scan.Warnings != null && preflight.Scan.Warnings.Count > 0)
            {
                ShowInfo("Scan needs attention", "The scan returned safety warnings. No changes will be made until the folder can be scanned safely:\n\n" + String.Join("\n", preflight.Scan.Warnings));
                return null;
            }
            if (rejectLegacy && preflight.HasLegacy)
            {
                ShowInfo("Legacy backup found", "A legacy " + GroupName(group) + " backup exists. Restore it first to avoid confusion. The legacy folder was not removed.");
                return null;
            }
            return preflight.Scan;
        }

        private async Task<T> Worker<T>(string operation, Func<T> work) where T : class
        {
            SetBusy(true, operation + "...");
            try { return await Task.Run(work); }
            catch (Exception ex) { ShowFailure(operation, ex.Message, new[] { SafetyEngine.ExplainError(ex) }); return null; }
            finally { SetBusy(false, null); }
        }

        private void SetBusy(bool busy, string text)
        {
            _busy = busy;
            if (_progress != null) _progress.Visible = busy;
            foreach (SideButton button in _buttons) button.Enabled = !busy;
            if (_detailToggle != null) _detailToggle.Enabled = !busy;
            if (_emptyScanButton != null) _emptyScanButton.Enabled = !busy;
            if (busy) Status(text); else if (!String.IsNullOrEmpty(text)) Status(text);
            UseWaitCursor = busy;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy) { e.Cancel = true; ShowInfo("Operation in progress", "Please wait for the safety operation to finish. The window cannot close while a backup or restore is running."); }
        }

        private void ClearScanState()
        {
            _lastScan = null; _dashboardItems.Clear(); _recommendedCount = _unknownCount = _protectedCount = _scriptHookCount = 0;
            RefreshDashboard();
        }

        private void InvalidateAfterMutation()
        {
            ClearScanState();
            Activity("↻ Scan results cleared after change");
            Status("Operation complete — scan again to review current files");
        }

        private void ApplyScan(ScanResult scan)
        {
            _lastScan = scan; _dashboardItems.Clear(); _recommendedCount = _unknownCount = _protectedCount = _scriptHookCount = 0;
            foreach (Candidate candidate in scan.Items)
            {
                if (candidate.Protected) _protectedCount++; else if (candidate.Recommended) _recommendedCount++; else _unknownCount++;
                if (candidate.Recommended && String.Equals(candidate.Name, "ScriptHookRDR2.dll", StringComparison.OrdinalIgnoreCase)) _scriptHookCount++;
                if (!candidate.Protected)
                {
                    _dashboardItems.Add(new ModEntry { Type = ModEntry.DetectType(candidate.Name, candidate.IsDirectory), Name = candidate.Name, Size = candidate.IsDirectory ? "folder" : "file", Reason = candidate.Reason, Status = candidate.Recommended ? "Recommended" : "Unknown", FullPath = candidate.FullPath, IsDir = candidate.IsDirectory });
                }
            }
        }

        private void LogScan(ScanResult scan)
        {
            Separator(); LogGold("  SCAN RESULTS");
            foreach (Candidate candidate in scan.Items)
            {
                string kind = candidate.Protected ? "PROTECTED" : candidate.Recommended ? "RECOMMENDED" : "UNKNOWN";
                Log(candidate.Protected ? "  Protected: " : candidate.Recommended ? "  Candidate: " : "  Unknown left untouched: ", candidate.Protected ? Th.T3 : candidate.Recommended ? Th.Gold : Th.T2);
                LogDim("    " + candidate.Name + " — " + kind + ". " + candidate.Reason);
            }
            if (scan.Warnings != null) foreach (string warning in scan.Warnings) LogError("  Scan warning: " + warning);
            ShowNotes(scan.Notes);
            LogDim("  Vanilla integrity is not checked; in-place .rpf changes are not checked.");
        }

        private void HandleResult(OperationResult result, string operation)
        {
            if (result == null) return;
            if (result.Success)
            {
                Status(operation + " complete — " + result.Message); Activity("✔ " + operation + " complete"); LogOk("  " + operation + " complete: " + result.Message); if (!String.IsNullOrEmpty(result.SessionId)) LogDim("  Session: " + result.SessionId); ShowNotes(result.Notes);
                string noteText = result.Notes.Count == 0 ? "" : "\n\nNotes:\n" + String.Join("\n", result.Notes.Select(n => "• " + n));
                ShowInfo(operation + " complete", result.Message + (String.IsNullOrEmpty(result.SessionPath) ? "" : "\n\nSession folder:\n" + result.SessionPath) + noteText);
            }
            else ShowFailure(operation, result.Message, result.Errors);
        }

        private void ShowFailure(string operation, string message, IEnumerable<string> errors)
        {
            string details = errors == null ? "" : String.Join("\n", errors.Where(e => !String.IsNullOrWhiteSpace(e) && e != message).Distinct());
            string backup = String.IsNullOrEmpty(_game) ? "" : "\n\nSafety log location: " + Path.Combine(_game, SafetyEngine.RecoveryFolderName);
            string advice = "\n\nKeep the session folder and independent backups. Close programs that may change these files, then use Restore to recover an interrupted transfer. If recovery reports a conflict, inspect it before moving anything aside.";
            Status(operation + " failed — " + message); Activity("✘ " + operation + " failed"); LogError("  " + operation + " failed: " + message); if (!String.IsNullOrEmpty(details)) LogError("  " + details);
            ShowTextDialog(operation + " failed", message + (String.IsNullOrEmpty(details) ? "" : "\n\n" + details) + backup + advice, false, true);
        }

        private void OpenFolder()
        {
            if (String.IsNullOrEmpty(_game) || !Directory.Exists(_game)) { ShowInfo("Game folder", "No valid game folder is set. Use Browse to select RDR2.exe."); return; }
            try { Process.Start("explorer.exe", "\"" + _game + "\""); } catch (Exception ex) { ShowFailure("Open folder", ex.Message, new[] { SafetyEngine.ExplainError(ex) }); }
        }

        private void Browse()
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "Select RDR2.exe in your game folder", Filter = "RDR2 Executable|RDR2.exe|All Files (*.*)|*.*", FileName = "RDR2.exe" })
            {
                if (!String.IsNullOrEmpty(_game) && Directory.Exists(_game)) dialog.InitialDirectory = _game;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!String.Equals(Path.GetFileName(dialog.FileName), "RDR2.exe", StringComparison.OrdinalIgnoreCase)) { ShowInfo("Select RDR2.exe", "Please select RDR2.exe, not another file."); return; }
                SetGame(Path.GetDirectoryName(dialog.FileName)); ClearScanState(); Status("Game folder selected — it will be validated before every action."); Activity("✔ Game path updated"); LogOk("  Game path selected: " + _game);
            }
        }

        private void Activate(SideButton active) { foreach (SideButton button in _buttons) button.Active = false; if (active != null) active.Active = true; }
        private void Activity(string entry) { _activity.Add(entry); RefreshDashboard(); }
        private void Status(string value) { if (_status == null) return; if (_status.InvokeRequired) _status.BeginInvoke((Action)delegate { _status.Text = value; }); else _status.Text = value; }
        private void EngineLog(string message) { LogDim("  [safety checks] " + message); }
        private void Log(string message, Color color) { if (_logBox == null) return; if (_logBox.InvokeRequired) { _logBox.BeginInvoke((Action)delegate { Log(message, color); }); return; } _logBox.SelectionStart = _logBox.TextLength; _logBox.SelectionColor = color; _logBox.AppendText(message + Environment.NewLine); _logBox.ScrollToCaret(); }
        private void LogOk(string m) { Log(m, Th.Ok); } private void LogWarn(string m) { Log(m, Th.Gold); } private void LogError(string m) { Log(m, Th.Crimson); } private void LogDim(string m) { Log(m, Th.T3); } private void LogGold(string m) { Log(m, Th.Gold); } private void Separator() { LogGold("  ────────────────────────────────────"); }
        private bool Confirm(string title, string text) { return ShowTextDialog(title, text, true, false); }
        private void ShowInfo(string title, string text) { ShowTextDialog(title, text, false, false); }
        private static string GroupName(ModGroup group) { return group == ModGroup.ReShade ? "ReShade" : group == ModGroup.ENB ? "ENB" : "Mods"; }

        private static void IconScan(Graphics g, Rectangle r, Color c) { ActionIcons.Draw(g, r, c, 0); }
        private static void IconHolster(Graphics g, Rectangle r, Color c) { ActionIcons.Draw(g, r, c, 1); }
        private static void IconRestore(Graphics g, Rectangle r, Color c) { ActionIcons.Draw(g, r, c, 2); }
        private static void IconClean(Graphics g, Rectangle r, Color c) { ActionIcons.Draw(g, r, c, 3); }
        private static string Trunc(string value, int maximum) { return value.Length <= maximum ? value : "..." + value.Substring(value.Length - maximum + 3); }
        private static GraphicsPath RoundRect(Rectangle r, int radius) { GraphicsPath path = new GraphicsPath(); int d = Math.Min(Math.Min(radius * 2, r.Width), r.Height); path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90); path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path; }

        private sealed class PreflightScan { public ScanResult Scan; public bool HasLegacy; }
        private sealed class RecoveryChoice { public List<SessionInfo> Sessions = new List<SessionInfo>(); public bool HasLegacy; }
        private enum GroupAction { None, Holster, Restore, Delete }
    }
}

