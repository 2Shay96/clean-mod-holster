using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using RDR2ModHolster.Safety;

namespace RDR2ModHolster;

// v3.2 recovery surface: a blocked restore explains every blocker, offers to move the
// files that are in the way into their own recoverable session, and asks before
// restoring a backup whose recorded folders have moved.
internal sealed partial class MainForm
{
    private enum ConflictChoice { Cancel, MoveAside, OpenFolder }

    private ConflictChoice ShowConflicts(ModGroup group, OperationResult result)
    {
        List<RestoreConflict> conflicts = result.Conflicts;
        bool movable = conflicts.Count > 0 && conflicts.All(c => c.CanMoveAside);
        string intro = movable
            ? "These files in the game folder are standing where the backup needs to go. Nothing has been changed."
            : "This restore is blocked. Some of these are problems with the backup itself, so they cannot be fixed by moving game files aside. Nothing has been changed.";
        using (var dialog = new ThemedDialog("Restore blocked — " + GroupName(group), intro, new Size(860, 560)))
        {
            ListView list = dialog.AddList(new[] { "Item", "Why it blocks the restore", "Can be moved aside" }, new[] { 0.26, 0.56, 0.18 }, false);
            foreach (RestoreConflict c in conflicts)
            {
                var item = new ListViewItem(c.Name) { ToolTipText = c.Name + "\n" + c.Path + "\n" + c.Reason };
                item.SubItems.Add(c.Reason ?? ""); item.SubItems.Add(c.CanMoveAside ? "Yes" : "No — backup problem");
                if (!c.CanMoveAside) item.ForeColor = Th.Crimson;
                list.Items.Add(item);
            }
            dialog.Notice(movable
                ? "Moving aside does not delete anything. The displaced files are placed in their own verified session that you can restore or delete later."
                : "A damaged or incomplete backup cannot be repaired from here. The backup folder is left exactly as it is so you can inspect it.");
            ConflictChoice choice = ConflictChoice.Cancel;
            if (movable)
            {
                Button aside = dialog.AddAction("Move these aside, then restore", Th.Gold);
                aside.Click += delegate { choice = ConflictChoice.MoveAside; dialog.DialogResult = DialogResult.OK; };
            }
            Button open = dialog.AddAction("Open game folder", Th.T2);
            open.Click += delegate { choice = ConflictChoice.OpenFolder; dialog.DialogResult = DialogResult.OK; };
            Button cancel = dialog.AddAction("Cancel", Th.T2, DialogResult.Cancel);
            dialog.FocusSafe(cancel);
            if (dialog.ShowDialog(this) != DialogResult.OK) return ConflictChoice.Cancel;
            return choice;
        }
    }

    // A backup whose recorded game/Documents folder has moved is never rebound silently.
    private bool ConfirmRebind(SessionInfo session, string recordedMessage)
    {
        string recorded = !String.IsNullOrEmpty(recordedMessage)
            ? recordedMessage
            : "This backup was made while the game folder was at:\n  " + (session == null ? "(unknown)" : session.RecordedGame);
        return Confirm("Backup was made in a different folder",
            recorded +
            "\n\nThe current folder is:\n  " + _game +
            "\n\nEvery file in the backup is checked against its recorded checksum before it is put back, so the contents are still verified. Only continue if this is the same installation — for example after moving the game to another drive, or after Windows moved your Documents folder.\n\nRestore into the current folders?");
    }

    private void ShowNotes(IEnumerable<string> notes)
    {
        if (notes == null) return;
        foreach (string note in notes.Where(n => !String.IsNullOrWhiteSpace(n))) LogWarn("  Note: " + note);
    }

    // Engine progress arrives on the worker thread.
    private void OnProgress(string phase, long done, long total)
    {
        if (String.IsNullOrEmpty(phase)) return;
        // Copying and verification can read the same bytes several times. Do not present
        // this work counter as a whole-operation completion percentage or an ETA.
        string text = done > 0 ? phase + " — " + Bytes(done) + " processed" : phase + "...";
        Status(text);
    }

    private static string Bytes(long bytes)
    {
        if (bytes >= 1073741824L) return (bytes / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        if (bytes >= 1048576L) return (bytes / 1048576.0).ToString("0", CultureInfo.InvariantCulture) + " MB";
        return (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
    }
}
