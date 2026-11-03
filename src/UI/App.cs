using System;
using System.Windows.Forms;

namespace RDR2ModHolster;

internal static class App
{
    [STAThread]
    private static void Main()
    {
        // DPI awareness is configured in App.config before any HWND is created. The old
        // SetProcessDPIAware call forced system-only awareness and prevented monitor transitions.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
