using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RDR2ModHolster.Safety;

namespace RDR2ModHolster;

internal sealed partial class MainForm
{
    private Panel _sideScroll, _sideMeta, _toolbar, _footer;
    private Label _version, _detailLabel;
    private ProgressBar _progress;
    private ActionButton _emptyScanButton;
    private Bitmap _sunrise;
    private WindowButton _closeButton, _maxButton, _minButton;
    private readonly ToolTip _tips = new ToolTip { AutoPopDelay = 12000, InitialDelay = 450, ReshowDelay = 150 };
    private bool _uiReady, _layoutBusy;
    private int _tableX, _tableWidth;
    protected override Size LogicalMinimum { get { return new Size(640, 420); } }

    private void BuildUi()
    {
        Text = "CLEAN - Mod Remover and Holster — v3.2";
        FormBorderStyle = FormBorderStyle.None;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        // Supplied artwork is separate from the code licence; see ARTWORK-NOTICE.txt.
        using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("RDR2ModHolster.sunrise.png"))
            if (stream != null) using (Image artwork = Image.FromStream(stream)) _sunrise = new Bitmap(artwork);
        _titleBar = new Panel { Dock = DockStyle.Top, AccessibleName = "CLEAN - Mod Remover and Holster" };
        _titleBar.SetStyle_Dbl(); _titleBar.Paint += PaintTitle;
        _titleBar.MouseDown += TitleMouseDown;
        _titleBar.MouseDoubleClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ToggleMaximize(); };
        _closeButton = new WindowButton(WindowButton.GlyphKind.Close);
        _maxButton = new WindowButton(WindowButton.GlyphKind.Maximize);
        _minButton = new WindowButton(WindowButton.GlyphKind.Minimize);
        foreach (WindowButton button in new[] { _closeButton, _maxButton, _minButton })
            button.PaintBackdrop = delegate(Graphics graphics, Rectangle bounds)
            {
                var saved = graphics.Save();
                graphics.TranslateTransform(-bounds.X, -bounds.Y);
                PaintHeaderBackground(graphics, _titleBar.ClientSize.Width, _titleBar.ClientSize.Height);
                graphics.Restore(saved);
            };
        _closeButton.Click += delegate { Close(); };
        _maxButton.Click += delegate { ToggleMaximize(); };
        _minButton.Click += delegate { WindowState = FormWindowState.Minimized; };
        _titleBar.Controls.AddRange(new Control[] { _closeButton, _maxButton, _minButton });
        _tips.SetToolTip(_closeButton, "Close (Alt+F4)"); _tips.SetToolTip(_maxButton, "Maximize / restore"); _tips.SetToolTip(_minButton, "Minimize");

        _footer = new Panel { Dock = DockStyle.Bottom, BackColor = Th.BgSide };
        _footer.Paint += delegate(object s, PaintEventArgs e) { using (var p = new Pen(Th.Border)) e.Graphics.DrawLine(p, 0, 0, _footer.Width, 0); };
        _status = new Label { Dock = DockStyle.Fill, ForeColor = Th.T3, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Text = "Ready", UseMnemonic = false, AccessibleName = "Operation status" };
        _status.TextChanged += delegate { _tips.SetToolTip(_status, _status.Text); };
        _footer.Controls.Add(_status);
        _progress = new ProgressBar { Dock = DockStyle.Right, Width = 120, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 35, Visible = false, AccessibleName = "Operation in progress" };
        _footer.Controls.Add(_progress);

        _sidebar = new Panel { Dock = DockStyle.Left, BackColor = Th.BgSide };
        _sidebar.SetStyle_Dbl();
        _sidebar.Paint += delegate(object s, PaintEventArgs e) { using (var b = new LinearGradientBrush(new Point(0, 0), new Point(0, Math.Max(1, _sidebar.Height)), Th.Gold, Th.DeepRed)) using (var pen = new Pen(b, Math.Max(1, I(2)))) e.Graphics.DrawLine(pen, _sidebar.Width - I(1), 0, _sidebar.Width - I(1), _sidebar.Height); };
        _sideScroll = new VerticalScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Th.BgSide };
        AddSideHeader("COMMANDS", Th.Gold);
        SideButton scan = AddButton("Scan Mods", Th.Gold, IconScan);
        SideButton holster = AddButton("Holster Mods", Color.FromArgb(255, 154, 0), IconHolster);
        SideButton restore = AddButton("Restore Mods", Th.Ok, IconRestore);
        SideButton delete = AddButton("Delete Mods", Th.Crimson, IconClean);
        AddSideHeader("GROUPS", Th.Gold);
        SideButton reshade = AddButton("ReShade", Color.FromArgb(255, 116, 0), null);
        SideButton enb = AddButton("ENB", Color.FromArgb(255, 77, 0), null);
        AddSideHeader("TOOLS", Th.Gold);
        SideButton open = AddButton("Open Folder", Th.T3, null);
        SideButton browse = AddButton("Browse…", Th.T3, null);
        SideButton about = AddButton("About / Support", Th.T2, null);
        SideButton exit = AddButton("Exit", Th.T3, null);
        scan.Click += async delegate { Activate(scan); await ScanAsync(); };
        holster.Click += async delegate { Activate(holster); await HolsterAsync(ModGroup.Mods); };
        restore.Click += async delegate { Activate(restore); await RestoreAsync(ModGroup.Mods); };
        delete.Click += async delegate { Activate(delete); await DeleteAsync(ModGroup.Mods); };
        reshade.Click += async delegate { Activate(reshade); await GroupMenuAsync(ModGroup.ReShade); };
        enb.Click += async delegate { Activate(enb); await GroupMenuAsync(ModGroup.ENB); };
        open.Click += delegate { Activate(null); OpenFolder(); };
        browse.Click += delegate { Activate(null); Browse(); };
        about.Click += delegate { ShowAbout(); };
        exit.Click += delegate { Close(); };
        _sideMeta = new Panel { Dock = DockStyle.Bottom, BackColor = Th.BgSide };
        _path = new Label { Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Th.T3, AutoEllipsis = true, UseMnemonic = false, Text = String.IsNullOrEmpty(_game) ? "No game folder" : _game };
        _path.TextChanged += delegate { _tips.SetToolTip(_path, _game); };
        _version = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Th.T4, Text = "v3.2  •  © 2Shay" };
        _sideMeta.Controls.Add(_path); _sideMeta.Controls.Add(_version);
        _sidebar.Controls.Add(_sideScroll); _sidebar.Controls.Add(_sideMeta);
        _tips.SetToolTip(_path, _game);

        var main = new Panel { Dock = DockStyle.Fill, BackColor = Th.Bg };
        _toolbar = new Panel { Dock = DockStyle.Top, BackColor = Th.Bg };
        _detailToggle = new ToggleSwitch { BackColor = Th.Bg };
        _detailLabel = new Label { Text = "Detailed log", ForeColor = Th.T3, TextAlign = ContentAlignment.MiddleRight };
        _toolbar.Controls.Add(_detailToggle); _toolbar.Controls.Add(_detailLabel);
        _dashPanel = new DashboardPanel { Dock = DockStyle.Fill, BackColor = Th.Bg, AutoScroll = true, AccessibleName = "Safety scan dashboard" };
        _emptyScanButton = new ActionButton { Text = "Scan Mods", AccessibleName = "Scan Mods", Font = Fonts.FBold, ForeColor = Th.Gold, BackColor = Th.BgSide, Cursor = Cursors.Hand, TabIndex = 0, Visible = false };
        _emptyScanButton.Click += delegate { scan.PerformClick(); };
        _dashPanel.Controls.Add(_emptyScanButton);
        _dashPanel.Paint += PaintDashboard; _dashPanel.MouseMove += DashboardMouseMove;
        _dashPanel.MouseLeave += delegate { _tableHover = -1; _tips.SetToolTip(_dashPanel, ""); _dashPanel.Invalidate(); };
        _dashPanel.Scroll += delegate { _tableHover = -1; _tips.SetToolTip(_dashPanel, ""); LayoutEmptyScanButton(); _dashPanel.Invalidate(); };
        _dashPanel.Resize += delegate { RefreshDashboard(); };
        _logPanel = new Panel { Dock = DockStyle.Fill, BackColor = Th.Bg, Visible = false };
        var logTitle = new Label { Dock = DockStyle.Top, Text = "ACTIVITY LOG", ForeColor = Th.T3, TextAlign = ContentAlignment.MiddleLeft, Name = "LogTitle" };
        _logBox = new RichTextBox { Dock = DockStyle.Fill, BackColor = Th.BgCard, ForeColor = Th.T1, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, WordWrap = true, AccessibleName = "Detailed activity log" };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy all", null, delegate { if (_logBox.TextLength > 0) { try { Clipboard.SetText(_logBox.Text); } catch (Exception ex) { ShowInfo("Copy failed", "The clipboard is busy or unavailable. Please try again.\n\n" + ex.Message); } } });
        menu.Items.Add("Clear view", null, delegate { _logBox.Clear(); }); _logBox.ContextMenuStrip = menu;
        _logPanel.Disposed += delegate { menu.Dispose(); };
        _logPanel.Controls.Add(_logBox); _logPanel.Controls.Add(logTitle);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Th.Bg };
        content.Controls.Add(_logPanel); content.Controls.Add(_dashPanel);
        _detailToggle.Toggled += delegate { _detailedLog = _detailToggle.On; _dashPanel.Visible = !_detailedLog; _logPanel.Visible = _detailedLog; };
        main.Controls.Add(content); main.Controls.Add(_toolbar);
        Controls.Add(main); Controls.Add(_sidebar); Controls.Add(_footer); Controls.Add(_titleBar);
        _uiReady = true; OnMetricsChanged();
        LogGold("  CLEAN - Mod Remover and Holster v3.2"); LogGold("  © 2Shay • Windows — see validation report"); Separator();
        LogDim("  Unknown files are preserved. Vanilla integrity/Online safety are not verified.");
        if (String.IsNullOrEmpty(_game)) { LogWarn("  RDR2 not found. Click Browse to locate RDR2.exe."); Activity("✘ Game path not set"); Status("No game folder — use Browse"); }
        else { LogOk("  Game path detected: " + _game); Activity("✔ Game path detected"); Status("Game folder: " + _game); }
    }

    protected override void OnLoad(EventArgs e) { FitInitial(new Size(1080, 760)); base.OnLoad(e); }
    protected override void OnMetricsChanged()
    {
        if (!_uiReady) return;
        _status.Font = _path.Font = _version.Font = _detailLabel.Font = Fonts.FSm;
        _status.Padding = new Padding(I(12), 0, I(12), 0);
        _sideMeta.Padding = new Padding(I(12), I(6), I(12), I(4));
        NativeTextMetrics.Apply(_logBox, Fonts.FNativeLog);
        _emptyScanButton.Font = Fonts.FBold;
        foreach (Control c in _sideScroll.Controls) c.Font = c is SideButton ? Fonts.FBody : Fonts.FCmdH;
        foreach (Control c in _logPanel.Controls) if (c is Label) { c.Font = Fonts.FSmB; c.Height = I(30); c.Padding = new Padding(I(12), 0, 0, 0); }
        LayoutChrome();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        // Set docked sizes before the framework positions their siblings. Doing this only
        // after base.OnLayout leaves the old header height reserved after a live DPI change.
        if (_uiReady && !_layoutBusy) LayoutChrome();
        base.OnLayout(e);
        if (_uiReady && !_layoutBusy) LayoutChrome();
    }
    private void LayoutChrome()
    {
        if (_layoutBusy || !_uiReady) return;
        _layoutBusy = true;
        try
        {
            Padding = new Padding(WindowState == FormWindowState.Maximized ? 0 : I(5));
            bool shortScreen = ClientSize.Height < I(560), veryShort = ClientSize.Height < I(360);
            // A slightly deeper banner on wide windows keeps the figures in frame rather than
            // turning the artwork into an excessively thin crop; cap it on short screens.
            int preferredHeader = Math.Max(I(veryShort ? 64 : shortScreen ? 80 : 104), (int)Math.Round(ClientSize.Width * .10));
            _titleBar.Height = Math.Min(preferredHeader, Math.Max(I(64), ClientSize.Height / 4));
            _footer.Height = I(shortScreen ? 26 : 30);
            _sidebar.Width = ClientSize.Width < I(860) ? I(174) : I(210);
            _sidebar.Padding = new Padding(0, 0, I(2), 0);
            _sideMeta.Height = I(veryShort ? 28 : 56); _path.Height = I(22); _version.Height = I(20);
            _path.Visible = !veryShort;
            _toolbar.Height = I(shortScreen ? 32 : 36);
            _detailToggle.Bounds = new Rectangle(_toolbar.ClientSize.Width - I(65), I(4), I(46), I(28));
            _detailLabel.Bounds = new Rectangle(_toolbar.ClientSize.Width - I(165), I(4), I(94), I(28));
            int bw = I(46);
            _closeButton.Bounds = new Rectangle(_titleBar.Width - bw, 0, bw, I(38));
            _maxButton.Bounds = new Rectangle(_titleBar.Width - bw * 2, 0, bw, I(38));
            _minButton.Bounds = new Rectangle(_titleBar.Width - bw * 3, 0, bw, I(38));
            _maxButton.RestoredGlyph = WindowState == FormWindowState.Maximized;
            _maxButton.AccessibleName = _maxButton.RestoredGlyph ? "Restore window" : "Maximize";
            _maxButton.Invalidate();
            // Each command remains reachable at high scaling or on short displays.
            int total = I(12) + _buttons.Count * I(38) + 3 * I(28) + 2 * I(16) + I(8);
            // A DPI/size transition can retain a vertical scrollbar for a layout pass.
            // Never place commands into that occupied part of the current client area.
            int viewportWidth = Math.Min(_sideScroll.ClientSize.Width,
                _sideScroll.Width - (total > _sideScroll.Height ? SystemInformation.VerticalScrollBarWidth : 0));
            int y = I(12);
            _version.Text = _sidebar.Width < I(190) ? "v3.2" : "v3.2  •  2Shay";
            foreach (Control c in _sideScroll.Controls)
            {
                bool button = c is SideButton;
                if (!button && c != _sideScroll.Controls[0]) y += I(16);
                int left = button ? I(8) : I(42);
                c.Bounds = new Rectangle(left, y + _sideScroll.AutoScrollPosition.Y,
                    Math.Max(I(80), viewportWidth - left - I(8)), I(button ? 36 : 20));
                y += I(button ? 38 : 28);
            }
            Size min = new Size(0, y + I(8));
            if (_sideScroll.AutoScrollMinSize != min) _sideScroll.AutoScrollMinSize = min;
            RefreshDashboard(); _titleBar.Invalidate(true); _sidebar.Invalidate();
        }
        finally { _layoutBusy = false; }
    }
    private void AddSideHeader(string text, Color color)
    {
        _sideScroll.Controls.Add(new Label { Text = text, ForeColor = color, BackColor = Th.BgSide, TabStop = false });
    }
    private SideButton AddButton(string text, Color color, IconPainter icon)
    {
        var button = new SideButton { Text = text, Accent = color, DrawIcon = icon, AccessibleName = text, TabIndex = _buttons.Count };
        _buttons.Add(button); _sideScroll.Controls.Add(button); return button;
    }

    private void PaintHeaderBackground(Graphics g, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        Rectangle target = new Rectangle(0, 0, w, h);
        using (var b = new SolidBrush(Th.Bg)) g.FillRectangle(b, target);
        if (_sunrise == null) return;
        // Cover the WHOLE title bar, preserving the source aspect ratio. Pan the vertical crop
        // toward the silhouettes rather than the empty red sky or the black foreground.
        float scale = Math.Max((float)w / _sunrise.Width, (float)h / _sunrise.Height);
        float sourceW = w / scale, sourceH = h / scale;
        float sourceX = (_sunrise.Width - sourceW) / 2f;
        float sourceY = Math.Max(0, Math.Min(_sunrise.Height - sourceH, _sunrise.Height * 0.585f - sourceH / 2f));
        var saved = g.Save();
        g.SetClip(target, CombineMode.Intersect);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var attrs = new ImageAttributes())
        {
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(_sunrise, target, sourceX, sourceY, sourceW, sourceH, GraphicsUnit.Pixel, attrs);
        }
        // Readability overlays, not opaque panels: artwork continues behind the title and controls.
        Rectangle left = new Rectangle(0, 0, Math.Max(1, Math.Min(w, Math.Max(I(340), w * 43 / 100))), h);
        using (var b = new LinearGradientBrush(left, Color.FromArgb(205, Color.Black), Color.FromArgb(0, Color.Black), LinearGradientMode.Horizontal)) g.FillRectangle(b, left);
        Rectangle right = new Rectangle(Math.Max(0, w - Math.Max(I(170), w / 5)), 0, Math.Min(w, Math.Max(I(170), w / 5)), h);
        using (var b = new LinearGradientBrush(right, Color.FromArgb(0, Color.Black), Color.FromArgb(145, Color.Black), LinearGradientMode.Horizontal)) g.FillRectangle(b, right);
        using (var b = new LinearGradientBrush(target, Color.FromArgb(5, Color.Black), Color.FromArgb(100, Color.Black), LinearGradientMode.Vertical)) g.FillRectangle(b, target);
        g.Restore(saved);
    }

    private void PaintTitle(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        int w = _titleBar.ClientSize.Width, h = _titleBar.ClientSize.Height;
        PaintHeaderBackground(g, w, h);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath title = HeaderTypography.Create("CLEAN", F(60)))
        using (GraphicsPath subtitle = HeaderTypography.Create("Mod Remover and Holster", F(18)))
        {
            RectangleF titleInk = title.GetBounds(), subtitleInk = subtitle.GetBounds();
            float gap = I(10), left = I(28);
            float fit = Math.Min(1f, Math.Max(1, h - I(20)) / (titleInk.Height + gap + subtitleInk.Height));
            if (fit < 1f)
            {
                using (var shrink = new Matrix()) { shrink.Scale(fit, fit); title.Transform(shrink); subtitle.Transform(shrink); }
                titleInk = title.GetBounds(); subtitleInk = subtitle.GetBounds(); gap *= fit;
            }
            float top = (h - titleInk.Height - gap - subtitleInk.Height) / 2f;
            HeaderTypography.DrawAligned(g, title, titleInk, left, top, Th.T1);
            HeaderTypography.DrawAligned(g, subtitle, subtitleInk, left, top + titleInk.Height + gap, Th.Gold);
        }
        if (w >= I(760)) DrawText(g, "v3.2 • 2Shay", Fonts.FVer, new Rectangle(w - I(236), Math.Max(I(42), (h - I(62)) / 2 + I(40)), I(210), I(20)), Th.T2, TextFormatFlags.Right);
        using (var b = new SolidBrush(Th.Gold)) g.FillRectangle(b, 0, h - I(2), w, I(2));
    }

    private const string Disclaimer = "Unknown files stay untouched unless explicitly selected. Vanilla integrity and Online safety are not verified. Changes inside .rpf archives are not checked.";
    private int DashboardWidth { get { return Math.Max(I(180), _dashPanel.ClientSize.Width - I(40)); } }
    private int NoticeHeight(int width) { return TextRenderer.MeasureText(Disclaimer, Fonts.FSm, new Size(Math.Max(I(80), width - I(24)), Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + I(24); }
    private void RefreshDashboard()
    {
        if (!_uiReady || _dashPanel == null || _dashPanel.IsDisposed) return;
        int activities = Math.Max(1, Math.Min(5, _activity.Count));
        int height = I(8 + 34 + 126 + 18 + 34 + 116 + 10 + 30 + Math.Max(1, _dashboardItems.Count) * 40 + 18 + 30 + activities * 24 + 18 + 20) + NoticeHeight(DashboardWidth);
        Size extent = new Size(0, height);
        if (_dashPanel.AutoScrollMinSize != extent) _dashPanel.AutoScrollMinSize = extent;
        LayoutEmptyScanButton();
        _dashPanel.Invalidate();
    }
    private void LayoutEmptyScanButton()
    {
        if (_emptyScanButton == null) return;
        _emptyScanButton.Visible = _lastScan == null && _dashboardItems.Count == 0;
        _emptyScanButton.Enabled = !_busy;
        // Match the table's logical row position, including per-element DPI rounding.
        int rowTop = I(8) + I(34) + I(126) + I(18) + I(34) + I(116) + I(10) + I(30);
        _emptyScanButton.Bounds = new Rectangle(I(30), rowTop + _dashPanel.AutoScrollPosition.Y + I(5), I(112), I(30));
    }
    private static void DrawText(Graphics g, string text, Font font, Rectangle rect, Color color, TextFormatFlags extra = 0)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var saved = g.Save();
        g.SetClip(rect, CombineMode.Intersect);
        TextRenderer.DrawText(g, text, font, rect, color, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping | extra);
        g.Restore(saved);
    }
    private void PaintDashboard(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.Clear(Th.Bg); g.SmoothingMode = SmoothingMode.AntiAlias;
        int x = I(20), y = _dashPanel.AutoScrollPosition.Y + I(8), w = DashboardWidth;
        DrawSection(g, "Game Installation", x, ref y, w); DrawGameCard(g, x, ref y, w);
        y += I(18); DrawSection(g, "Safety Scan Overview", x, ref y, w); DrawStats(g, x, ref y, w);
        y += I(10); DrawTable(g, x, ref y, w); y += I(18); DrawActivity(g, x, ref y, w); y += I(18);
        int nh = NoticeHeight(w);
        using (var b = new SolidBrush(Th.BgCard)) g.FillRectangle(b, x, y, w, nh);
        TextRenderer.DrawText(g, Disclaimer, Fonts.FSm, new Rectangle(x + I(12), y + I(12), w - I(24), nh - I(24)), Th.T2, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
    }
    private void DrawSection(Graphics g, string text, int x, ref int y, int w)
    {
        DrawText(g, text, Fonts.FHead, new Rectangle(x, y, w, I(28)), Th.T1);
        int start = x + TextRenderer.MeasureText(text, Fonts.FHead).Width + I(10);
        if (start < x + w) using (var p = new Pen(Th.Border)) g.DrawLine(p, start, y + I(14), x + w, y + I(14));
        y += I(34);
    }
    private void DrawGameCard(Graphics g, int x, ref int y, int w)
    {
        Rectangle rect = new Rectangle(x, y, w, I(126));
        using (var b = new SolidBrush(Th.BgCard)) g.FillRectangle(b, rect);
        using (var p = new Pen(Th.Border)) g.DrawRectangle(p, x, y, w - 1, I(126) - 1);
        DrawText(g, "RED DEAD REDEMPTION 2", Fonts.FGameTitle, new Rectangle(x + I(16), y + I(12), w - I(32), I(28)), Th.T1);
        using (var b = new LinearGradientBrush(new Point(x, y), new Point(x + Math.Max(1, w), y), Th.DeepRed, Th.Gold)) g.FillRectangle(b, x + I(16), y + I(45), w - I(32), I(2));
        bool set = !String.IsNullOrEmpty(_game);
        using (var b = new SolidBrush(set ? Th.Ok : Th.Crimson)) g.FillEllipse(b, x + I(17), y + I(64), I(7), I(7));
        DrawText(g, set ? "Game folder selected" : "No game folder selected", Fonts.FBody, new Rectangle(x + I(30), y + I(55), w - I(46), I(24)), set ? Th.Ok : Th.Crimson);
        DrawText(g, set ? _game : "Use Browse to locate RDR2.exe", Fonts.FSm, new Rectangle(x + I(16), y + I(83), w - I(32), I(20)), Th.T2, set ? TextFormatFlags.PathEllipsis : 0);
        DrawText(g, "Folder validated before every action", Fonts.FSm, new Rectangle(x + I(16), y + I(103), w - I(32), I(18)), Th.T3);
        y += I(126);
    }
    private void DrawStats(Graphics g, int x, ref int y, int w)
    {
        string[] labels = { "Recommended\ncandidates", "Unknown left\nuntouched", "Vanilla\nintegrity" };
        string[] values = { _lastScan == null ? "—" : _recommendedCount.ToString(), _lastScan == null ? "—" : _unknownCount.ToString(), "Not checked" };
        int part = w / 3;
        for (int n = 0; n < 3; n++)
        {
            DrawText(g, values[n], n == 2 ? Fonts.FBold : Fonts.FStat, new Rectangle(x + n * part, y, part, I(43)), Th.Gold, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, labels[n], Fonts.FStatLbl, new Rectangle(x + n * part + I(3), y + I(45), part - I(6), I(38)), Th.T2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
            if (n < 2) using (var p = new Pen(Th.Border)) g.DrawLine(p, x + (n + 1) * part, y + I(8), x + (n + 1) * part, y + I(80));
        }
        DrawText(g, "ScriptHook recommended: " + (_lastScan == null ? "—" : _scriptHookCount.ToString()), Fonts.FSm, new Rectangle(x, y + I(91), w, I(20)), Th.T3, TextFormatFlags.HorizontalCenter);
        y += I(116);
    }
    private void DrawTable(Graphics g, int x, ref int y, int w)
    {
        bool showReason = w >= I(640);
        int type = I(60), state = I(119), reason = showReason ? (int)(w * .30) : 0, name = w - type - state - reason - I(16);
        int head = I(30);
        using (var b = new SolidBrush(Color.FromArgb(32, 24, 11))) g.FillRectangle(b, x, y, w, head);
        int cx = x + I(8);
        DrawText(g, "TYPE", Fonts.FTblH, new Rectangle(cx, y, type, head), Th.T3); cx += type;
        DrawText(g, "NAME", Fonts.FTblH, new Rectangle(cx, y, name, head), Th.T3); cx += name;
        if (showReason) { DrawText(g, "REASON", Fonts.FTblH, new Rectangle(cx, y, reason, head), Th.T3); cx += reason; }
        DrawText(g, "STATE", Fonts.FTblH, new Rectangle(cx, y, state, head), Th.T3);
        y += head; _tableY = y; _tableRowH = I(40); _tableX = x; _tableWidth = w;
        if (_dashboardItems.Count == 0)
        {
            using (var b = new SolidBrush(Th.BgTable)) g.FillRectangle(b, x, y, w, _tableRowH);
            if (_lastScan == null)
            {
                if (w >= I(240)) DrawText(g, "to begin", Fonts.FTbl, new Rectangle(x + I(130), y, w - I(140), _tableRowH), Th.T3);
            }
            else DrawText(g, "No selectable candidates found", Fonts.FTbl, new Rectangle(x + I(10), y, w - I(20), _tableRowH), Th.T3);
        }
        else
        {
            // Clip-aware row iteration avoids redrawing hundreds of off-screen entries while scrolling.
            int first = Math.Max(0, -y / _tableRowH), last = Math.Min(_dashboardItems.Count, Math.Max(0, (_dashPanel.ClientSize.Height - y) / _tableRowH + 2));
            for (int i = first; i < last; i++)
            {
                ModEntry item = _dashboardItems[i]; int row = y + i * _tableRowH;
                using (var b = new SolidBrush(i == _tableHover ? Th.BgHover : i % 2 == 0 ? Th.BgTable : Th.BgTableAlt)) g.FillRectangle(b, x, row, w, _tableRowH);
                cx = x + I(8); Rectangle badge = new Rectangle(cx + I(2), row + I(10), I(42), I(20));
                using (var p = RoundRect(badge, I(4))) using (var b = new SolidBrush(ModEntry.TypeColor(item.Type))) g.FillPath(b, p);
                DrawText(g, item.Type, Fonts.FBadge, badge, item.Type == "ASI" || item.Type == "LOG" || item.Type == "DIR" ? Color.Black : Color.White, TextFormatFlags.HorizontalCenter);
                cx += type; DrawText(g, item.Name, Fonts.FTbl, new Rectangle(cx, row, name - I(5), _tableRowH), Th.T1); cx += name;
                if (showReason) { DrawText(g, item.Reason ?? "", Fonts.FSm, new Rectangle(cx, row, reason - I(5), _tableRowH), Th.T3); cx += reason; }
                DrawText(g, item.Status, Fonts.FSm, new Rectangle(cx, row, state, _tableRowH), item.Status == "Unknown" ? Th.Gold : Th.Ok);
            }
        }
        y += Math.Max(1, _dashboardItems.Count) * _tableRowH; _tableEndY = y;
    }
    private void DrawActivity(Graphics g, int x, ref int y, int w)
    {
        DrawText(g, "Activity Log", Fonts.FBold, new Rectangle(x, y, w, I(26)), Th.T1); y += I(30);
        foreach (string a in _activity.Skip(Math.Max(0, _activity.Count - 5))) { DrawText(g, a, Fonts.FSm, new Rectangle(x + I(8), y, w - I(16), I(22)), a.StartsWith("✔") ? Th.Ok : a.StartsWith("✘") ? Th.Crimson : Th.T2); y += I(24); }
        if (_activity.Count == 0) y += I(24);
    }
    private void DashboardMouseMove(object sender, MouseEventArgs e)
    {
        int row = _tableRowH == 0 ? -1 : (e.Y - _tableY) / _tableRowH;
        int next = e.X >= _tableX && e.X < _tableX + _tableWidth && e.Y >= _tableY && e.Y < _tableEndY && row >= 0 && row < _dashboardItems.Count ? row : -1;
        if (next == _tableHover) return;
        _tableHover = next; _dashPanel.Invalidate();
        _tips.SetToolTip(_dashPanel, next < 0 ? "" : _dashboardItems[next].Name + "\n" + _dashboardItems[next].Reason + "\n" + _dashboardItems[next].FullPath);
    }

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrack, MaxTrack; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; if (IsWindows) cp.Style |= 0x00040000 | 0x00020000 | 0x00010000; return cp; }
    }
    private void TitleMouseDown(object sender, MouseEventArgs e)
    {
        if (!IsWindows || e.Button != MouseButtons.Left || e.Clicks > 1) return;
        ReleaseCapture(); SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }
    private void ToggleMaximize() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; LayoutChrome(); }
    protected override void WndProc(ref Message m)
    {
        if (IsWindows && m.Msg == 0x0083 && m.WParam != IntPtr.Zero) { m.Result = IntPtr.Zero; return; } // retain custom non-client appearance
        // Default non-client activation painting can draw a system caption over our
        // full-client header. Activation itself is handled by WM_ACTIVATE separately.
        if (IsWindows && m.Msg == 0x0086) { Invalidate(true); m.Result = new IntPtr(1); return; }
        if (IsWindows && m.Msg == 0x0024)
        {
            base.WndProc(ref m);
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (GetMonitorInfo(MonitorFromWindow(Handle, 2), ref info))
            {
                var mm = (MinMaxInfo)Marshal.PtrToStructure(m.LParam, typeof(MinMaxInfo));
                mm.MaxPosition.X = info.Work.Left - info.Monitor.Left; mm.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
                mm.MaxSize.X = info.Work.Right - info.Work.Left; mm.MaxSize.Y = info.Work.Bottom - info.Work.Top;
                mm.MinTrack.X = Math.Min(mm.MinTrack.X, mm.MaxSize.X); mm.MinTrack.Y = Math.Min(mm.MinTrack.Y, mm.MaxSize.Y);
                Marshal.StructureToPtr(mm, m.LParam, false);
            }
            return;
        }
        if (IsWindows && m.Msg == 0x0084 && WindowState == FormWindowState.Normal)
        {
            long lp = m.LParam.ToInt64(); Point p = PointToClient(new Point((short)(lp & 0xffff), (short)((lp >> 16) & 0xffff)));
            int edge = I(6); bool left = p.X < edge, right = p.X >= ClientSize.Width - edge, top = p.Y < edge, bottom = p.Y >= ClientSize.Height - edge;
            int hit = top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0;
            if (hit != 0) { m.Result = new IntPtr(hit); return; }
        }
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tips.Dispose(); if (_sunrise != null) _sunrise.Dispose(); }
        base.Dispose(disposing);
    }
}

internal class VerticalScrollPanel : Panel
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern bool ShowScrollBar(IntPtr window, int bar, bool show);
    private bool _updatingNativeScroll;
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        // These panels always reflow to their viewport. A native DPI conversion
        // can leave WS_HSCROLL set even when WinForms reports Visible=false.
        if (!_updatingNativeScroll && IsHandleCreated && Environment.OSVersion.Platform == PlatformID.Win32NT && (GetWindowLong(Handle, -16) & 0x00100000) != 0)
        {
            _updatingNativeScroll = true;
            try { ShowScrollBar(Handle, 0, false); } // SB_HORZ; preserve vertical scrolling
            finally { _updatingNativeScroll = false; }
        }
    }
}

internal sealed class DashboardPanel : VerticalScrollPanel
{
    public DashboardPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); TabStop = true; }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
    protected override bool IsInputKey(Keys keyData) { Keys k = keyData & Keys.KeyCode; return k == Keys.PageDown || k == Keys.PageUp || k == Keys.Home || k == Keys.End || k == Keys.Up || k == Keys.Down || base.IsInputKey(keyData); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        int y = -AutoScrollPosition.Y, step = Math.Max(20, ClientSize.Height - 40);
        switch (e.KeyCode) { case Keys.PageDown: y += step; break; case Keys.PageUp: y -= step; break; case Keys.Home: y = 0; break; case Keys.End: y = AutoScrollMinSize.Height; break; case Keys.Down: y += 40; break; case Keys.Up: y -= 40; break; default: base.OnKeyDown(e); return; }
        AutoScrollPosition = new Point(0, Math.Max(0, y)); Invalidate(); e.Handled = true;
    }
}
