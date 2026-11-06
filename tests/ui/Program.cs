using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using RDR2ModHolster.Safety;

namespace RDR2ModHolster;

// Synthetic UI-only checks. No SafetyEngine instance, disk scan, holster, restore or purge is called.
internal static class UiChecks
{
    private static string _out;
    private static int _passed;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            _out = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "rdr2-ui-checks"); Directory.CreateDirectory(_out);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var f = new MainForm())
            {
                f.Show(); Application.DoEvents();
                Call(f, "SetGame", @"E:\Games\Red Dead Redemption 2");
                Button emptyScan = (Button)Get(f, "_emptyScanButton");
                Check(emptyScan.Visible && emptyScan.Text == "Scan Mods", "Unscanned dashboard exposes a real Scan Mods button");
                Check(emptyScan.TabStop && emptyScan.CanSelect, "Empty-state Scan Mods is keyboard reachable");
                Call(f, "SetBusy", true, "UI-only busy check");
                Check(!emptyScan.Enabled, "Empty-state Scan Mods cannot start overlapping work");
                Call(f, "SetBusy", false, null);
                Check(emptyScan.Enabled, "Empty-state Scan Mods re-enables after work");
                foreach (int dpi in new[] { 96, 120, 144, 192 })
                {
                    f.SimulateDpi(dpi); f.ClientSize = new Size(1080 * dpi / 96, 760 * dpi / 96); Application.DoEvents();
                    Panel emptyDashboard = (Panel)Get(f, "_dashPanel");
                    emptyDashboard.ScrollControlIntoView(emptyScan); Application.DoEvents();
                    Check(emptyScan.Left >= 0 && emptyScan.Right <= emptyDashboard.ClientSize.Width, "Empty-state scan fits horizontally at " + dpi + " DPI");
                    Check(emptyScan.Top >= 0 && emptyScan.Bottom <= emptyDashboard.ClientSize.Height, "Empty-state scan can be scrolled into view at " + dpi + " DPI");
                    emptyDashboard.AutoScrollPosition = Point.Empty;
                }
                f.SimulateDpi(96); f.ClientSize = new Size(1080, 760); Application.DoEvents(); Capture(f, "empty-state-scan-button");
                ScanResult scan = Fixture(); Call(f, "ApplyScan", scan); Call(f, "Activity", "✔ Scan complete — sample inventory for layout testing");
                Check(!emptyScan.Visible, "Empty-state scan button hides once results exist");
                Call(f, "Status", "Scan complete — sample inventory for layout testing");
                Check(f.Text.StartsWith("CLEAN - Mod Remover and Holster"), "CLEAN branding is consistent");
                Case(f, "dashboard-100", 96, 1080, 760);
                Case(f, "dashboard-125", 120, 1350, 950);
                Case(f, "dashboard-150", 144, 1620, 1140);
                Case(f, "dashboard-200", 192, 1920, 1080);
                Case(f, "dashboard-300", 288, 3000, 2100);
                Case(f, "dashboard-wide", 96, 1920, 1080);
                Case(f, "dashboard-compact", 96, 800, 600);
                Case(f, "dashboard-minimum", 96, 640, 420);
                // Full sidebar is independently scrollable on short screens; all commands remain present.
                Panel sidebar = (Panel)Get(f, "_sideScroll");
                Check(sidebar.AutoScrollMinSize.Height > sidebar.ClientSize.Height, "Short-screen sidebar has scrollable commands");
                sidebar.AutoScrollPosition = new Point(0, sidebar.AutoScrollMinSize.Height); Application.DoEvents();
                Capture(f, "dashboard-minimum-scrolled");
                Case(f, "dashboard-return-100", 96, 1080, 760);
                Panel dashboard = (Panel)Get(f, "_dashPanel");
                Size extent = dashboard.AutoScrollMinSize;
                dashboard.AutoScrollPosition = new Point(0, extent.Height); Application.DoEvents();
                Capture(f, "dashboard-bottom");
                Check(dashboard.AutoScrollMinSize == extent, "Paint/scroll does not change content extent");
                Call(f, "SetBusy", true, "Synthetic busy-state preview"); Capture(f, "dashboard-disabled");
                Check(All(f).OfType<SideButton>().All(b => !b.Enabled), "All command buttons visibly disabled during operations");
                Call(f, "SetBusy", false, null);
                ToggleSwitch toggle = (ToggleSwitch)Get(f, "_detailToggle");
                toggle.On = true; Application.DoEvents(); Capture(f, "detailed-log");
                Check(!dashboard.Visible && ((Panel)Get(f, "_logPanel")).Visible, "Log switch changes visible panel"); toggle.On = false;
                ReviewCheck(f, scan, 96, "review-100");
                ReviewCheck(f, scan, 192, "review-200");
                DeleteCheck(f, 96, "delete-100");
                DeleteCheck(f, 192, "delete-200");
                TextCheck(f); GroupCheck(f); SessionCheck(f);
                ConflictCheck(f, 96, "conflicts-100", true);
                ConflictCheck(f, 192, "conflicts-200", false);
                AboutCheck(f);
                foreach (int dpi in new[] { 96, 120, 144, 192 })
                {
                    f.SimulateDpi(dpi);
                    Modal(f, "ShowTextDialog", new object[] { "Native text DPI check", "Recovery instructions must keep the same logical text size.", false, false }, d =>
                    {
                        RichTextBox text = All(d).OfType<RichTextBox>().Single();
                        Check(Math.Abs(text.Font.SizeInPoints - 9.5f) < .1f, "Message text retains 9.5 point size at " + dpi + " DPI");
                        Check(text.SelectionFont != null && Math.Abs(text.SelectionFont.SizeInPoints - 9.5f) < .2f, "Message document retains 9.5 point character size at " + dpi + " DPI");
                        Check(Math.Abs(text.ZoomFactor - 1f) < .01f, "Message text has no extra zoom at " + dpi + " DPI");
                        d.DialogResult = DialogResult.Cancel;
                    });
                }
                ConstrainedChecks(f, scan);
                f.SimulateDpi(96); f.ClientSize = new Size(1080, 760); f.Location = new Point(1500, 200);
                Rectangle smallArea = new Rectangle(100, 100, 800, 600);
                f.SimulateWorkingArea(smallArea);
                Check(smallArea.Contains(f.Bounds), "Same-DPI smaller working area clamps size and position");
                f.SimulateDpi(288); f.SimulateWorkingArea(new Rectangle(0, 0, 1366, 768));
                Check(f.Width <= 1366 && f.Height <= 768, "High-DPI minimum cannot exceed a smaller physical working area");
                Capture(f, "dashboard-constrained-300");
                f.Close();
            }
            Console.WriteLine("PASS TOTAL: " + _passed); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static ScanResult Fixture()
    {
        var s = new ScanResult();
        string[] names = { "lml", "dinput8.dll", "ScriptHookRDR2.dll", "vfs.asi", "Trainer.asi", "asiloader.log", "My_custom_settings.ini", "nvngx_future_example.dll", ".egstore" };
        for (int i = 0; i < names.Length; i++) s.Items.Add(new Candidate { Name = names[i], FullPath = @"E:\Games\Red Dead Redemption 2\" + names[i], Group = ModGroup.Mods, Recommended = i < 6, Protected = i == 8, IsDirectory = i == 0 || i == 8, Reason = i == 8 ? "Protected launcher metadata" : i < 6 ? "Recognised mod component; review before changing" : "Unknown file — left untouched by default" });
        s.Items.Add(new Candidate { Name = "Long_custom_mod_configuration_Unicode_é_非常长的名称.ini", FullPath = @"E:\Games\Red Dead Redemption 2\Long custom file.ini", Group = ModGroup.Mods, Reason = "Long reason with an ampersand & and Unicode text; this row must not overlap State" });
        return s;
    }
    private static void Case(MainForm f, string name, int dpi, int w, int h)
    {
        f.SimulateDpi(dpi); f.ClientSize = new Size(w, h); f.Location = new Point(4, 4); f.PerformLayout(); Application.DoEvents();
        var dashboard = (Panel)Get(f, "_dashPanel"); dashboard.AutoScrollPosition = Point.Empty;
        Application.DoEvents(); Capture(f, name);
        Check(dashboard.ClientSize.Width > 0 && dashboard.ClientSize.Height > 0, name + " viewport is positive");
        var close = (WindowButton)Get(f, "_closeButton"); var max = (WindowButton)Get(f, "_maxButton");
        Check(close.Right <= close.Parent.ClientSize.Width && close.Left >= max.Right, name + " window controls do not overlap");
        Check(!dashboard.HorizontalScroll.Visible, name + " dashboard has no horizontal scrollbar");
        Check(!((Panel)Get(f, "_sideScroll")).HorizontalScroll.Visible, name + " sidebar has no horizontal scrollbar");
        Check(((Control)Get(f, "_sidebar")).Top == ((Control)Get(f, "_titleBar")).Bottom, name + " docked sidebar starts immediately below current header");
        Check(Math.Abs(((RichTextBox)Get(f, "_logBox")).Font.SizeInPoints - 10f) < .1f, name + " native log retains 10 point text size");
    }
    private static void ReviewCheck(MainForm f, ScanResult scan, int dpi, string name)
    {
        f.SimulateDpi(dpi);
        Modal(f, "ReviewCandidates", new object[] { ModGroup.Mods, scan, "Holster" }, d =>
        {
            ListView l = All(d).OfType<ListView>().Single();
            Check(l.CheckedItems.Count == 6, name + " recommended selection retained");
            foreach (ListViewItem i in l.Items) if (((Candidate)i.Tag).Protected) { i.Checked = true; Check(!i.Checked, name + " protected checkbox refuses selection"); }
            Check(l.Items.Cast<ListViewItem>().Where(i => !((Candidate)i.Tag).Recommended).All(i => !i.Checked), name + " unknowns stay unchecked");
            Check(All(d).OfType<Button>().All(b => TextRenderer.MeasureText(b.Text, b.Font).Width + 8 <= b.Width), name + " button captions fit");
            CheckDialog(d, name); Capture(d, name); d.DialogResult = DialogResult.Cancel;
        });
    }
    private static void DeleteCheck(MainForm f, int dpi, string name)
    {
        f.SimulateDpi(dpi);
        Modal(f, "RequireDeleteText", new object[0], d =>
        {
            TextBox t = All(d).OfType<TextBox>().Single(); Button remove = All(d).OfType<Button>().Single(b => b.Text == "Permanently delete");
            Check(!remove.Enabled, name + " deletion starts disabled"); t.Text = "delete"; Check(!remove.Enabled, name + " case-sensitive confirmation"); t.Text = "DELETE"; Check(remove.Enabled, name + " exact DELETE enables explicit button");
            Check(d.AcceptButton == null, name + " no destructive Enter default"); CheckDialog(d, name); Capture(d, name); d.DialogResult = DialogResult.Cancel;
        });
    }
    private static void TextCheck(MainForm f)
    {
        f.SimulateDpi(96);
        string text = String.Join("\n", Enumerable.Range(1, 200).Select(i => "Selected file " + i + " — synthetic long confirmation content"));
        Modal(f, "ShowTextDialog", new object[] { "Long selection confirmation", text, true, false }, d =>
        {
            RichTextBox box = All(d).OfType<RichTextBox>().Single(); Check(box.Text.Contains("file 200"), "Long confirmation preserves every entry"); Check(box.ScrollBars == RichTextBoxScrollBars.Vertical, "Long confirmation is scrollable"); CheckDialog(d, "long-confirmation"); Capture(d, "long-confirmation"); d.DialogResult = DialogResult.Cancel;
        });
    }
    private static void GroupCheck(MainForm f)
    {
        Modal(f, "ShowGroupMenu", new object[] { ModGroup.ReShade }, d => { CheckDialog(d, "group-actions"); Capture(d, "group-actions"); d.DialogResult = DialogResult.Cancel; });
    }
    private static void SessionCheck(MainForm f)
    {
        var sessions = Enumerable.Range(1, 3).Select(i => new SessionInfo { Id = "a1b2c3d4e5f6071" + i, Group = ModGroup.Mods, State = i == 1 ? "Restoring" : "Holstered", ItemCount = 70, Kind = i == 3 ? SessionKind.Quarantine : SessionKind.Session, NeedsRebind = i == 2, RecordedGame = @"D:\Games\Red Dead Redemption 2" }).ToList();
        Modal(f, "PickSession", new object[] { ModGroup.Mods, sessions }, d =>
        {
            CheckDialog(d, "session-picker");
            ListView l = All(d).OfType<ListView>().Single();
            Check(l.SelectedItems.Count == 0, "Multiple sessions start unselected");
            Check(l.Items.Cast<ListViewItem>().Any(i => i.SubItems[4].Text.Contains("moved aside")), "Moved-aside sets are labelled in the session picker");
            Check(l.Items.Cast<ListViewItem>().Any(i => i.SubItems[4].Text.Contains("another folder")), "Backups from a moved folder are labelled");
            Capture(d, "session-picker"); d.DialogResult = DialogResult.Cancel;
        });
    }
    // v3.2: a blocked restore must show every blocker, and offer to move files aside
    // only when doing so can actually unblock it.
    private static OperationResult Blocked(bool allMovable)
    {
        var r = new OperationResult { Success = false, Message = "Restore blocked" };
        r.Conflicts.Add(new RestoreConflict { Name = "lml", Path = @"E:\Games\Red Dead Redemption 2\lml", Reason = "A file or folder with this name already exists in the game folder.", CanMoveAside = true });
        r.Conflicts.Add(new RestoreConflict { Name = "vfs.asi", Path = @"E:\Games\Red Dead Redemption 2\vfs.asi", Reason = "Different content of this name is in the game folder.", CanMoveAside = true });
        if (!allMovable) r.Conflicts.Add(new RestoreConflict { Name = "ScriptHookRDR2.dll", Path = @"E:\Games\Red Dead Redemption 2\_RDR2_ModHolster", Reason = "The backup copy is incomplete.", CanMoveAside = false });
        return r;
    }
    private static void ConflictCheck(MainForm f, int dpi, string name, bool allMovable)
    {
        f.SimulateDpi(dpi);
        OperationResult blocked = Blocked(allMovable);
        Modal(f, "ShowConflicts", new object[] { ModGroup.Mods, blocked }, d =>
        {
            ListView l = All(d).OfType<ListView>().Single();
            Check(l.Items.Count == blocked.Conflicts.Count, name + " every blocker is listed");
            Check(blocked.Conflicts.All(c => l.Items.Cast<ListViewItem>().Any(i => i.Text == c.Name)), name + " each blocker is named");
            var buttons = All(d).OfType<Button>().ToList();
            Check(buttons.Any(b => b.Text.StartsWith("Move these aside")) == allMovable, name + " move-aside offered only when it can help");
            Check(buttons.Any(b => b.Text == "Cancel") && buttons.Any(b => b.Text == "Open game folder"), name + " safe exits are present");
            Check(buttons.All(b => TextRenderer.MeasureText(b.Text, b.Font).Width + 8 <= b.Width), name + " button captions fit");
            CheckDialog(d, name); Capture(d, name); d.DialogResult = DialogResult.Cancel;
        });
    }
    private static void ConstrainedChecks(MainForm f, ScanResult scan)
    {
        string[] methods = { "ReviewCandidates", "RequireDeleteText", "ShowGroupMenu", "ShowTextDialog", "ShowConflicts" };
        object[][] args = { new object[] { ModGroup.Mods, scan, "Holster" }, new object[0], new object[] { ModGroup.ENB }, new object[] { "Confirmation", "A long message for constrained-area scrolling.\n" + new string('x', 1200), true, false }, new object[] { ModGroup.Mods, Blocked(true) } };
        for (int n = 0; n < methods.Length; n++)
        {
            string name = "constrained-300-" + methods[n];
            f.SimulateDpi(288);
            Modal(f, methods[n], args[n], d =>
            {
                ((DpiWindow)d).SimulateDpi(288); d.ClientSize = new Size(1024, 640); d.Location = new Point(10, 10); d.PerformLayout(); Application.DoEvents();
                CheckDialog(d, name);
                Panel content = d.Controls.OfType<Panel>().Single();
                Check(content.AutoScrollMinSize.Height > content.ClientSize.Height, name + " entire content is scrollable");
                Check(!content.HorizontalScroll.Visible, name + " no horizontal content scrolling");
                Capture(d, name); content.AutoScrollPosition = new Point(0, content.AutoScrollMinSize.Height); Application.DoEvents();
                Capture(d, name + "-bottom");
                CheckDialog(d, name + "-scrolled");
                d.DialogResult = DialogResult.Cancel;
            });
        }
    }
    private static void AboutCheck(MainForm f)
    {
        foreach (int dpi in new[] { 96, 120, 144, 192 })
        {
            f.SimulateDpi(dpi);
            Modal(f, "ShowAbout", new object[0], d =>
            {
            Check(d.Text == "About CLEAN - Mod Remover and Holster", "About title uses CLEAN branding");
            string text = All(d).OfType<RichTextBox>().Single().Text;
            Check(text.Contains("CLEAN for GTA VI is planned, targeting day-one support for the PC release, subject to compatibility testing. Follow development on Ko-fi."), "About keeps conditional roadmap wording");
            Check(text.Contains("Support is voluntary"), "About keeps support separate from access");
            Check(All(d).OfType<Button>().Any(b => b.Text == "News & Support on Ko-fi"), "About labels the news and support destination");
            Check(All(d).OfType<Button>().Any(b => b.Text == "Source & Issues on GitHub"), "About labels the source and issue destination");
            CheckDialog(d,"about-" + dpi);
            Capture(d,"about-" + dpi);
            d.ClientSize = new Size(400 * dpi / 96, 320 * dpi / 96);
            Application.DoEvents();
            CheckDialog(d,"about-compact-" + dpi);
            Check(d.ActiveControl is Button && ((Button)d.ActiveControl).Text == "Close", "About initially focuses Close at " + dpi + " DPI");
            Capture(d,"about-compact-" + dpi);
            d.DialogResult = DialogResult.Cancel;
            });
        }
    }
    private static void CheckDialog(Form d, string name)
    {
        var actions = d.Controls.OfType<Button>().ToList();
        Check(actions.All(b => b.Left >= 0 && b.Top >= 0 && b.Right <= d.ClientSize.Width && b.Bottom <= d.ClientSize.Height), name + " actions inside client area");
        Check(actions.All(a => actions.Where(b => b != a).All(b => !a.Bounds.IntersectsWith(b.Bounds))), name + " actions do not overlap");
        Panel body = d.Controls.OfType<Panel>().Single();
        Check(body.Height > 20 && body.Bottom <= actions.Min(b => b.Top), name + " content does not cover actions");
    }
    private static void Modal(MainForm f, string method, object[] args, Action<Form> inspect)
    {
        Exception error = null;
        using (var timer = new Timer { Interval = 250 })
        {
            timer.Tick += delegate
            {
                Form d = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x is ThemedDialog);
                if (d == null) return;
                timer.Stop();
                try { inspect(d); } catch (Exception e) { error = e; d.DialogResult = DialogResult.Cancel; }
            };
            timer.Start(); Call(f, method, args); timer.Stop();
        }
        if (error != null) throw error;
    }
    private static IEnumerable<Control> All(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (Control nested in All(child)) yield return nested; } }
    private static object Get(object o, string field) { return o.GetType().GetField(field, Flags).GetValue(o); }
    private static object Call(object o, string method, params object[] args) { return o.GetType().GetMethod(method, Flags).Invoke(o, args); }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); _passed++; Console.WriteLine("PASS: " + name); }
    private static void Capture(Form f, string name)
    {
        f.Refresh(); Application.DoEvents();
        using (var image = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
        {
            // Capture real rendered controls, not a hand-built mockup. Mixed-monitor native Windows
            // DPI messages are still a separate test; this harness explicitly simulates UI metrics.
            using (Graphics g = Graphics.FromImage(image)) g.CopyFromScreen(f.PointToScreen(Point.Empty), Point.Empty, image.Size);
            image.Save(Path.Combine(_out, name + ".png"), ImageFormat.Png);
        }
    }
}
