using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RDR2ModHolster;

// Button (rather than Control) preserves keyboard activation, accessibility and disabled semantics.
internal class SideButton : Button
{
    private bool _hover, _pressed, _active;
    private Font _bold;
    public IconPainter DrawIcon;
    public Color Accent { get; set; } = Th.Gold;
    public bool Active { get { return _active; } set { _active = value; Invalidate(); } }
    public SideButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        UseMnemonic = false;
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (_bold != null) _bold.Dispose(); _bold = new Font(Font, FontStyle.Bold); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _pressed = _hover = false; Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _pressed = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color background = !Enabled ? Th.BgSide : _pressed ? Th.BgPress : _hover ? Th.BgHover : _active ? Color.FromArgb(51, 18, 12) : Th.BgSide;
        g.Clear(background);
        if (_active && Enabled)
        {
            using (var b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(90, Th.DeepRed), Color.FromArgb(8, Th.Gold), LinearGradientMode.Horizontal)) g.FillRectangle(b, ClientRectangle);
        }
        if (Enabled && (_hover || _active)) using (var b = new SolidBrush(_active ? Th.Gold : Accent)) g.FillRectangle(b, 0, Height / 5, Math.Max(2, Height / 12), Height * 3 / 5);
        if (DrawIcon != null)
        {
            int size = Height * 56 / 100;
            DrawIcon(g, new Rectangle(Height * 22 / 100, (Height - size) / 2, size, size), Enabled ? Accent : Th.T4);
        }
        // Reserve one icon column on every row, including text-only tools and groups.
        // Stable text weight keeps labels from jumping when hovered or selected.
        int left = Height;
        Font f = Font;
        TextRenderer.DrawText(g, Text, f, new Rectangle(left, 0, Math.Max(1, Width - left - Height / 5), Height), !Enabled ? Th.T4 : _active ? Accent : _hover ? Th.T1 : Th.T2, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -4, -4), Th.Gold, background);
    }
    protected override void Dispose(bool disposing) { if (disposing && _bold != null) _bold.Dispose(); base.Dispose(disposing); }
}
