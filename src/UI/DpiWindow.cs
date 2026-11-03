using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RDR2ModHolster;

// This UI uses explicit logical (96-DPI) metrics. WinForms automatic scaling is deliberately
// disabled: scaling fixed coordinates, point fonts and framework autoscaling together doubles sizes.
// Each top-level window owns its own scale/fonts; moving a dialog must not rescale its owner.
internal class DpiWindow : Form
{
    private int _dpi = 96;
    protected readonly UiFonts Fonts = new UiFonts();
    protected int I(int value) { return (int)Math.Round(value * _dpi / 96.0); }
    protected float F(float value) { return value * _dpi / 96f; }
    internal float UiScale { get { return _dpi / 96f; } }
    protected static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }

    public DpiWindow()
    {
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Th.Bg;
        ForeColor = Th.T1;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dpi = 96;
        if (IsWindows)
        {
            try { dpi = (int)GetDpiForWindow(Handle); }
            catch (EntryPointNotFoundException) { using (Graphics g = CreateGraphics()) dpi = (int)g.DpiX; }
        }
        ApplyDpi(dpi);
    }

    private void ApplyDpi(int dpi)
    {
        if (dpi < 48 || dpi > 768) dpi = 96;
        _dpi = dpi;
        // New fonts are assigned before old fonts are released; controls must never reference disposed fonts.
        Fonts.Update(dpi);
        Font = Fonts.FBody;
        OnMetricsChanged();
        PerformLayout();
        Invalidate(true);
        Fonts.ReleaseRetired();
    }
    protected virtual void OnMetricsChanged() { }

    protected void FitInitial(Size logicalSize, IWin32Window owner = null)
    {
        IntPtr unused = Handle; // Query the initial monitor DPI before calculating sizes.
        Rectangle area = owner != null ? Screen.FromHandle(owner.Handle).WorkingArea : Screen.FromHandle(Handle).WorkingArea;
        Control ownerControl = owner as Control;
        if (ownerControl is DpiWindow) ApplyDpi((int)Math.Round(((DpiWindow)ownerControl).UiScale * 96));
        Size desired = SizeFromClientSize(new Size(I(logicalSize.Width), I(logicalSize.Height)));
        MinimumSize = Size.Empty;
        Size = new Size(Math.Min(desired.Width, area.Width), Math.Min(desired.Height, area.Height));
        Rectangle centre = ownerControl != null ? ownerControl.Bounds : area;
        Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, centre.Left + (centre.Width - Width) / 2)), Math.Max(area.Top, Math.Min(area.Bottom - Height, centre.Top + (centre.Height - Height) / 2)));
        SetSafeMinimum(area);
        PerformLayout();
    }

    protected virtual Size LogicalMinimum { get { return new Size(480, 300); } }
    protected void SetSafeMinimum(Rectangle area)
    {
        Size min = SizeFromClientSize(new Size(I(LogicalMinimum.Width), I(LogicalMinimum.Height)));
        MinimumSize = new Size(Math.Min(area.Width, min.Width), Math.Min(area.Height, min.Height));
    }

    private bool _fittingArea;
    private bool _nativeDpiRefreshQueued;
    private void FitToWorkingArea(Rectangle area)
    {
        if (_fittingArea || WindowState != FormWindowState.Normal) return;
        _fittingArea = true;
        try
        {
            MinimumSize = Size.Empty;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
            SetSafeMinimum(area);
        }
        finally { _fittingArea = false; }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_DPICHANGED = 0x02E0;
        // A smaller monitor can have the same DPI, so WM_DPICHANGED is not sufficient.
        if (m.Msg == 0x0232 || m.Msg == 0x007E || m.Msg == 0x001A) // exit move/size, display/settings change
        {
            base.WndProc(ref m);
            if (IsHandleCreated && !IsDisposed && WindowState == FormWindowState.Normal)
                FitToWorkingArea(Screen.FromRectangle(Bounds).WorkingArea);
            return;
        }
        if (m.Msg == WM_DPICHANGED && m.LParam != IntPtr.Zero)
        {
            NativeRect r = (NativeRect)Marshal.PtrToStructure(m.LParam, typeof(NativeRect));
            SuspendLayout();
            try
            {
                MinimumSize = Size.Empty;
                ApplyDpi((int)(m.WParam.ToInt64() & 0xffff));
                Rectangle proposed = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                Rectangle area = Screen.FromRectangle(proposed).WorkingArea;
                proposed.Width = Math.Min(proposed.Width, area.Width);
                proposed.Height = Math.Min(proposed.Height, area.Height);
                proposed.X = Math.Max(area.Left, Math.Min(proposed.X, area.Right - proposed.Width));
                proposed.Y = Math.Max(area.Top, Math.Min(proposed.Y, area.Bottom - proposed.Height));
                Bounds = proposed;
                SetSafeMinimum(area);
            }
            finally { ResumeLayout(true); }
            // RichEdit processes its own child DPI notifications after this message.
            // Reapply document font sizes only after those native conversions finish.
            if (!_nativeDpiRefreshQueued && IsHandleCreated && !IsDisposed)
            {
                _nativeDpiRefreshQueued = true;
                BeginInvoke((MethodInvoker)delegate
                {
                    _nativeDpiRefreshQueued = false;
                    if (IsDisposed || Disposing) return;
                    OnMetricsChanged(); PerformLayout(); Invalidate(true);
                });
            }
            m.Result = IntPtr.Zero;
            return; // Handled explicitly; do not also invoke WinForms automatic rescaling.
        }
        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential)] protected struct NativeRect { public int Left, Top, Right, Bottom; }

#if UI_TESTS
    // Only present in the screenshot/test assembly, never in the shipped executable.
    internal void SimulateDpi(int dpi) { MinimumSize = Size.Empty; ApplyDpi(dpi); }
    internal void SimulateWorkingArea(Rectangle area) { FitToWorkingArea(area); }
#endif
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) Fonts.Dispose();
    }
}
