using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RDR2ModHolster;

internal sealed class ThemedDialog : DpiWindow
{
    private readonly Size _preferred;
    private readonly Label _intro = new Label { ForeColor = Th.T2, UseMnemonic = false };
    private readonly Label _notice = new Label { ForeColor = Th.Gold, UseMnemonic = false };
    private readonly Panel _content = new Panel { AutoScroll = true, AccessibleName = "Scrollable dialog content" };
    private readonly Panel _body = new Panel { AutoScroll = true };
    private readonly List<Button> _actions = new List<Button>();
    private readonly List<Button> _choices = new List<Button>();
    private ListView _list;
    private RichTextBox _text;
    private TextBox _input;
    private double[] _columnWeights;
    private bool _ready, _layingOut;
    public ThemedDialog(string title, string intro, Size preferred)
    {
        Text = title; _preferred = preferred;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        _intro.Text = intro;
        _content.Controls.Add(_intro); _content.Controls.Add(_notice); _content.Controls.Add(_body);
        Controls.Add(_content);
        _ready = true; OnMetricsChanged();
    }
    protected override Size LogicalMinimum { get { return new Size(400, 320); } }
    protected override void OnLoad(EventArgs e)
    {
        if (Owner != null) Icon = Owner.Icon;
        FitInitial(_preferred, Owner); base.OnLoad(e);
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // A dialog first opened after a system DPI change can receive RichEdit's
        // initial native font conversion after OnLoad. Set document sizes again
        // once its child handles and initial DPI messages have settled.
        BeginInvoke((MethodInvoker)delegate
        {
            if (!IsDisposed && !Disposing) OnMetricsChanged();
        });
    }
    public void Notice(string text) { _notice.Text = text; PerformLayout(); }
    public ListView AddList(string[] names, double[] weights, bool checks)
    {
        _columnWeights = weights;
        _list = new ListView { Dock = DockStyle.Fill, CheckBoxes = checks, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, BackColor = Th.BgTable, ForeColor = Th.T1, Font = Fonts.FSm, BorderStyle = BorderStyle.FixedSingle, ShowItemToolTips = true, AccessibleName = Text + " inventory" };
        foreach (string name in names) _list.Columns.Add(name);
        _body.Controls.Add(_list); _list.Resize += delegate { SizeColumns(); }; PerformLayout(); return _list;
    }
    public void AddText(string text)
    {
        _text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = text, BackColor = Th.BgCard, ForeColor = Th.T1, Font = Fonts.FNativeBody, BorderStyle = BorderStyle.None, WordWrap = true, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Vertical, AccessibleName = "Message details" };
        _body.Controls.Add(_text); PerformLayout();
    }
    public TextBox AddInput()
    {
        _input = new TextBox { Font = Fonts.FBody, BackColor = Th.BgTable, ForeColor = Th.T1, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Type DELETE to confirm permanent deletion" };
        _body.Controls.Add(_input); PerformLayout(); return _input;
    }
    public Button AddAction(string text, Color color, DialogResult result = DialogResult.None)
    {
        var b = MakeButton(text, color); b.DialogResult = result; _actions.Add(b); Controls.Add(b);
        if (result == DialogResult.Cancel) CancelButton = b;
        PerformLayout(); return b;
    }
    public Button AddChoice(string text, Color color)
    {
        var b = MakeButton(text, color); _choices.Add(b); _body.Controls.Add(b); PerformLayout(); return b;
    }
    private Button MakeButton(string text, Color color)
    {
        var b = new ActionButton { Text = text, ForeColor = color, Font = Fonts.FBold, BackColor = Th.BgSide, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseMnemonic = false, AccessibleName = text };
        b.FlatAppearance.BorderColor = Th.Border; b.FlatAppearance.MouseOverBackColor = Th.BgHover; b.FlatAppearance.MouseDownBackColor = Th.BgPress; return b;
    }
    protected override void OnMetricsChanged()
    {
        if (!_ready) return;
        _intro.Font = Fonts.FBody; _notice.Font = Fonts.FSm;
        foreach (Button b in _actions.Concat(_choices)) b.Font = Fonts.FBold;
        if (_list != null) _list.Font = Fonts.FSm;
        if (_text != null) NativeTextMetrics.Apply(_text, Fonts.FNativeBody);
        if (_input != null) _input.Font = Fonts.FBody;
        LayoutContent();
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if (_ready) LayoutContent(); }
    private int MeasureLabel(Label label, int width)
    {
        return String.IsNullOrEmpty(label.Text) ? 0 : TextRenderer.MeasureText(label.Text, label.Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + I(4);
    }
    private void LayoutContent()
    {
        if (!_ready || _layingOut) return;
        _layingOut = true;
        try
        {
            int pad = I(18), width = Math.Max(I(80), ClientSize.Width - pad * 2), gap = I(8), bh = I(38);
            int cursor = ClientSize.Width - pad, by = ClientSize.Height - pad - bh, rows = 1;
            // The last action (normally Cancel/Close) stays at the far right. Wide labels are measured,
            // not squeezed into the old 118px box; rows wrap on genuinely small windows.
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                Button b = _actions[i]; int bw = Math.Min(width, Math.Max(I(112), TextRenderer.MeasureText(b.Text, b.Font).Width + I(28)));
                if (cursor - bw < pad && cursor < ClientSize.Width - pad) { rows++; by -= bh + gap; cursor = ClientSize.Width - pad; }
                b.Bounds = new Rectangle(cursor - bw, by, bw, bh); cursor -= bw + gap;
            }
            int actionTop = _actions.Count == 0 ? ClientSize.Height - pad : by - I(12);
            // The entire explanatory/list region can scroll when a small working area caps the
            // dialog below its preferred size. The bottom action buttons never scroll out of reach.
            int viewportHeight = Math.Max(1, actionTop - pad);
            _content.Bounds = new Rectangle(pad, pad, width, viewportHeight);
            int bodyMinimum = _input != null ? I(48) : _choices.Count > 0 ? I(_choices.Count * 48) : I(150);
            int innerWidth = width, ih = MeasureLabel(_intro, innerWidth), nh = MeasureLabel(_notice, innerWidth);
            int gaps = I(12) + (nh > 0 ? I(12) : 0);
            int minimumContent = ih + bodyMinimum + nh + gaps;
            if (minimumContent > viewportHeight)
            {
                innerWidth = Math.Max(1, width - SystemInformation.VerticalScrollBarWidth);
                ih = MeasureLabel(_intro, innerWidth); nh = MeasureLabel(_notice, innerWidth);
                minimumContent = ih + bodyMinimum + nh + gaps;
            }
            int bodyHeight = Math.Max(bodyMinimum, viewportHeight - ih - nh - gaps);
            int sy = _content.AutoScrollPosition.Y;
            _intro.Bounds = new Rectangle(0, sy, innerWidth, ih);
            int bodyTop = ih + I(12);
            _body.Bounds = new Rectangle(0, bodyTop + sy, innerWidth, bodyHeight);
            _notice.Bounds = new Rectangle(0, bodyTop + bodyHeight + (nh > 0 ? I(12) : 0) + sy, innerWidth, nh);
            _content.AutoScrollMinSize = new Size(0, Math.Max(minimumContent, viewportHeight));
            if (_input != null) _input.Bounds = new Rectangle(0, 0, Math.Max(1, _body.ClientSize.Width), I(30));
            int y = 0;
            foreach (Button b in _choices) { b.Bounds = new Rectangle(0, y + _body.AutoScrollPosition.Y, Math.Max(I(80), _body.ClientSize.Width - I(4)), I(40)); y += I(48); }
            if (_choices.Count > 0) _body.AutoScrollMinSize = new Size(0, y);
            SizeColumns();
        }
        finally { _layingOut = false; }
    }
    private void SizeColumns()
    {
        if (_list == null || _columnWeights == null || _list.Columns.Count == 0) return;
        int remaining = Math.Max(1, _list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - I(4));
        double total = _columnWeights.Sum();
        for (int i = 0; i < _list.Columns.Count; i++)
        {
            int width = i == _list.Columns.Count - 1 ? remaining : (int)Math.Round((_list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - I(4)) * _columnWeights[i] / total);
            width = Math.Max(1, width); _list.Columns[i].Width = width; remaining -= width;
        }
    }
    public void FocusSafe(Button button) { Shown += delegate { ActiveControl = button; button.Focus(); }; }
}
