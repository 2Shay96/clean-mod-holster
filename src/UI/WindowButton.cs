using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RDR2ModHolster;

internal sealed class WindowButton : Button
{
    public enum GlyphKind { Minimize, Maximize, Close }
    public GlyphKind Glyph;
    public bool RestoredGlyph;
    // Draw the same title-bar artwork underneath the chrome instead of opaque black button tiles.
    public Action<Graphics, Rectangle> PaintBackdrop;
    private bool _hover;
    public WindowButton(GlyphKind glyph)
    {
        Glyph = glyph;
        AccessibleName = glyph.ToString();
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Th.Bg; ForeColor = Th.T2; TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (PaintBackdrop != null) PaintBackdrop(g, Bounds); else g.Clear(Th.Bg);
        if (_hover) using (var b = new SolidBrush(Glyph == GlyphKind.Close ? Color.FromArgb(220, 185, 30, 43) : Color.FromArgb(65, Color.White))) g.FillRectangle(b, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = Height / 38f, half = 4.5f * s, x = Width / 2f, y = Height / 2f;
        using (var pen = new Pen(Th.T1, Math.Max(1f, s)))
        {
            if (Glyph == GlyphKind.Close) { g.DrawLine(pen, x-half, y-half, x+half, y+half); g.DrawLine(pen, x+half, y-half, x-half, y+half); }
            else if (Glyph == GlyphKind.Minimize) g.DrawLine(pen, x-half, y+half/2, x+half, y+half/2);
            else if (!RestoredGlyph) g.DrawRectangle(pen, x-half, y-half, half*2, half*2);
            else
            {
                // Outline-only stacked squares let the art remain continuous through the restore icon.
                g.DrawLine(pen, x-half+2*s, y-half-2*s, x+half+2*s, y-half-2*s);
                g.DrawLine(pen, x+half+2*s, y-half-2*s, x+half+2*s, y+half-2*s);
                g.DrawLine(pen, x+half+2*s, y+half-2*s, x+half, y+half-2*s);
                g.DrawRectangle(pen, x-half, y-half, half*2, half*2);
            }
        }
    }
}
