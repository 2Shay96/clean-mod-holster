using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RDR2ModHolster;

internal class ToggleSwitch : CheckBox
{
    public bool On { get { return Checked; } set { Checked = value; } }
    public event EventHandler Toggled;
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        AccessibleName = "Detailed activity log";
        AccessibleRole = AccessibleRole.CheckButton;
        AutoSize = false;
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); if (Toggled != null) Toggled(this, EventArgs.Empty); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        int pad = Math.Max(2, Height / 8), trackH = Height - pad * 2;
        if (trackH < 2 || Width < trackH) return;
        Rectangle r = new Rectangle(pad, pad, Width - pad * 2, trackH);
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddArc(r.X, r.Y, trackH, trackH, 90, 180);
            p.AddArc(r.Right - trackH, r.Y, trackH, trackH, 270, 180); p.CloseFigure();
            using (var b = new SolidBrush(On && Enabled ? Color.FromArgb(137, 55, 19) : Color.FromArgb(62, 53, 45))) g.FillPath(b, p);
        }
        int inset = Math.Max(1, Height / 12), diameter = trackH - inset * 2;
        using (var b = new SolidBrush(!Enabled ? Th.T4 : On ? Th.Gold : Th.T2)) g.FillEllipse(b, On ? r.Right - inset - diameter : r.Left + inset, r.Top + inset, diameter, diameter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -1), Th.Gold, BackColor);
    }
}
