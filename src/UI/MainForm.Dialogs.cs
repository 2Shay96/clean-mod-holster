using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RDR2ModHolster.Safety;

namespace RDR2ModHolster;

internal sealed partial class MainForm
{
    private List<Candidate> ReviewCandidates(ModGroup group, ScanResult scan, string verb)
    {
        List<Candidate> rows = scan.Items.Where(c => c.Group == group).ToList();
        if (rows.Count == 0) { ShowInfo(verb + " " + GroupName(group), "No " + GroupName(group) + " candidates were found. Unknown files remain untouched unless explicitly selected in a future scan."); return null; }
        using (var dialog = new ThemedDialog(verb + " " + GroupName(group), "Review the freshly scanned items. Recommended items are preselected; unknown items are left untouched. Protected items cannot be selected.", new Size(820, 560)))
        {
            ListView list = dialog.AddList(new[] { "Name", "Reason", "State" }, new[] { 0.32, 0.48, 0.20 }, true);
            foreach (Candidate c in rows)
            {
                var item = new ListViewItem(c.Name) { Tag = c, Checked = c.Recommended && !c.Protected, ToolTipText = c.Name + "\n" + c.Reason + "\n" + c.FullPath };
                item.SubItems.Add(c.Reason ?? ""); item.SubItems.Add(c.Protected ? "Protected" : c.Recommended ? "Recommended" : "Unknown");
                if (c.Protected) { item.ForeColor = Th.T3; item.BackColor = Th.BgCard; }
                else if (!c.Recommended) item.ForeColor = Th.Gold;
                list.Items.Add(item);
            }
            list.ItemCheck += delegate(object sender, ItemCheckEventArgs e) { Candidate c = list.Items[e.Index].Tag as Candidate; if (c != null && c.Protected && e.NewValue == CheckState.Checked) e.NewValue = CheckState.Unchecked; };
            // Also enforce the displayed state after programmatic/assistive-technology changes.
            list.ItemChecked += delegate(object sender, ItemCheckedEventArgs e) { Candidate c = e.Item.Tag as Candidate; if (c != null && c.Protected && e.Item.Checked) e.Item.Checked = false; };
            dialog.Notice("Unknown files are not automatically safe. A selected unknown item may be a game or launcher file. Selections are not remembered. Hover a row to read its full name and reason.");
            Button proceed = dialog.AddAction("Review selection", Th.Gold);
            Button cancel = dialog.AddAction("Cancel", Th.T2, DialogResult.Cancel);
            proceed.Click += delegate
            {
                List<Candidate> unknown = list.CheckedItems.Cast<ListViewItem>().Select(i => i.Tag as Candidate).Where(c => c != null && !c.Recommended).ToList();
                if (unknown.Count > 0 && !Confirm("Unknown item warning", "You selected " + unknown.Count + " unknown item(s). Filename alone cannot establish safety; selected files may be game or launcher files.\n\nContinue to the final confirmation?")) return;
                dialog.DialogResult = DialogResult.OK;
            };
            dialog.FocusSafe(cancel);
            if (dialog.ShowDialog(this) != DialogResult.OK) return null;
            return list.CheckedItems.Cast<ListViewItem>().Select(i => i.Tag as Candidate).Where(c => c != null && !c.Protected).ToList();
        }
    }
    private bool ConfirmSelection(string title, IList<Candidate> selected, string note)
    {
        string names = String.Join("\n", selected.Select(c => "• " + c.Name));
        return Confirm(title, "Selected item(s) (" + selected.Count + "):\n" + names + "\n\n" + note + "\n\nThe operation creates a second verified copy temporarily. Free space is checked before anything moves, and checksumming large mod sets takes time — progress is shown while it runs.\n\nThe backup is kept inside the game folder, so launcher repair or game removal may affect it. Restore before doing either.\n\nThese checks do not verify that files are clean or safe for online play.\n\nContinue?");
    }
    private SessionInfo PickSession(ModGroup group, List<SessionInfo> sessions)
    {
        if (sessions.Count == 1) return sessions[0];
        using (var dialog = new ThemedDialog("Select " + GroupName(group) + " session", "Choose a backup session. No session is selected automatically when more than one is available.", new Size(820, 480)))
        {
            ListView list = dialog.AddList(new[] { "Session ID", "Group", "State", "Items", "Type" }, new[] { .30, .11, .17, .08, .34 }, false);
            foreach (SessionInfo session in sessions)
            {
                string type = session.IsLegacy ? "Legacy" : session.Kind == SessionKind.Quarantine ? "Files moved aside" : "Verified backup";
                if (session.NeedsRebind) type += " • made in another folder";
                var item = new ListViewItem(session.Id) { Tag = session, ToolTipText = session.Id + "\n" + session.State + " — " + type + (session.NeedsRebind ? "\nRecorded game folder: " + session.RecordedGame : "") };
                item.SubItems.Add(session.Group.ToString()); item.SubItems.Add(session.State); item.SubItems.Add(session.ItemCount.ToString()); item.SubItems.Add(type);
                if (session.Kind == SessionKind.Quarantine) item.ForeColor = Th.Gold;
                list.Items.Add(item);
            }
            Button select = dialog.AddAction("Select session", Th.Gold);
            Button cancel = dialog.AddAction("Cancel", Th.T2, DialogResult.Cancel);
            select.Click += delegate { if (list.SelectedItems.Count == 0) { ShowInfo("Choose a session", "Select one session from the list first."); return; } dialog.DialogResult = DialogResult.OK; };
            dialog.FocusSafe(cancel);
            return dialog.ShowDialog(this) == DialogResult.OK ? list.SelectedItems[0].Tag as SessionInfo : null;
        }
    }
    private GroupAction ShowGroupMenu(ModGroup group)
    {
        using (var dialog = new ThemedDialog(GroupName(group) + " actions", GroupName(group) + " is managed separately from Mods. All actions use the same safety checks.", new Size(450, 360)))
        {
            GroupAction result = GroupAction.None;
            Button holster = dialog.AddChoice("Holster " + GroupName(group), Color.FromArgb(255, 154, 0));
            Button restore = dialog.AddChoice("Restore " + GroupName(group), Th.Ok);
            Button delete = dialog.AddChoice("Delete " + GroupName(group), Th.Crimson);
            Button cancel = dialog.AddAction("Cancel", Th.T2, DialogResult.Cancel);
            holster.Click += delegate { result = GroupAction.Holster; dialog.DialogResult = DialogResult.OK; };
            restore.Click += delegate { result = GroupAction.Restore; dialog.DialogResult = DialogResult.OK; };
            delete.Click += delegate { result = GroupAction.Delete; dialog.DialogResult = DialogResult.OK; };
            dialog.FocusSafe(cancel); dialog.ShowDialog(this); return result;
        }
    }
    private bool RequireDeleteText()
    {
        using (var dialog = new ThemedDialog("Confirm permanent deletion", "This permanently deletes the verified backup payload and cannot be undone. Type DELETE to continue.", new Size(540, 320)))
        {
            TextBox input = dialog.AddInput();
            dialog.Notice("Cancelling keeps the remaining backup. Permanent deletion cannot be undone.");
            Button remove = dialog.AddAction("Permanently delete", Th.Crimson);
            Button cancel = dialog.AddAction("Cancel", Th.T2, DialogResult.Cancel);
            remove.Enabled = false;
            input.TextChanged += delegate { remove.Enabled = String.Equals(input.Text.Trim(), "DELETE", StringComparison.Ordinal); };
            remove.Click += delegate { if (String.Equals(input.Text.Trim(), "DELETE", StringComparison.Ordinal)) dialog.DialogResult = DialogResult.OK; };
            // Enter never defaults to a destructive action; the explicit button must be activated.
            dialog.Shown += delegate { input.Focus(); };
            return dialog.ShowDialog(this) == DialogResult.OK;
        }
    }
    private bool ShowTextDialog(string title, string text, bool question, bool error)
    {
        using (var dialog = new ThemedDialog(title, error ? "The operation did not complete. Review the details below." : question ? "Review before continuing" : title, new Size(700, 500)))
        {
            dialog.AddText(text);
            if (question) dialog.AddAction("Continue", Th.Gold, DialogResult.OK);
            Button safe = dialog.AddAction(question ? "Cancel" : "Close", Th.T2, DialogResult.Cancel);
            dialog.FocusSafe(safe);
            // Own nested messages with the active modal so the warning cannot hide behind it.
            Form owner = Form.ActiveForm;
            return dialog.ShowDialog(owner != null && owner != dialog ? owner : this) == DialogResult.OK;
        }
    }
}
