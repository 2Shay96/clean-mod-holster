using System;
using System.Drawing;
using System.Windows.Forms;

namespace RDR2ModHolster;

// Keep real Button keyboard/accessibility semantics, but avoid unreadable embossed disabled text
// and legacy light backgrounds inside the dark confirmation dialogs.
internal sealed class ActionButton : Button
{
    private bool _hover, _pressed;
    public ActionButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _pressed = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _hover = _pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color background = Enabled && _pressed ? Th.BgPress : Enabled && _hover ? Th.BgHover : Th.BgSide;
        g.Clear(background);
        if (Width < 2 || Height < 2) return;
        using (var p = new Pen(Focused && Enabled ? Th.Gold : Th.Border, Math.Max(1, Height / 38f))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(g, Text, Font, Rectangle.Inflate(ClientRectangle, -4, -2), Enabled ? ForeColor : Th.T4, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -5, -5), Th.Gold, background);
    }
}
