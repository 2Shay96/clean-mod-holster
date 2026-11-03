using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace RDR2ModHolster;

internal static class NativeTextMetrics
{
    // RichEdit's document-to-pixel baseline is fixed when it first loads in the
    // process. Newly created controls do not inherit earlier DPI notifications.
    private static readonly int InitialDpi = InitialDesktopDpi();
    private static int InitialDesktopDpi()
    {
        using (Graphics graphics = Graphics.FromHwnd(System.IntPtr.Zero)) return (int)System.Math.Round(graphics.DpiY);
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(System.IntPtr window);
    private sealed class DocumentDpi { public int NativeDpi = InitialDpi; }
    private static readonly ConditionalWeakTable<RichTextBox, DocumentDpi> Documents = new ConditionalWeakTable<RichTextBox, DocumentDpi>();
    private static DocumentDpi Track(RichTextBox box)
    {
        return Documents.GetValue(box, delegate(RichTextBox control)
        {
            var state = new DocumentDpi();
            control.HandleCreated += delegate { state.NativeDpi = InitialDpi; };
            // Once RichEdit receives a real child DPI transition it updates its
            // internal pixel baseline. Fresh controls still start at InitialDpi.
            control.DpiChangedAfterParent += delegate { state.NativeDpi = control.DeviceDpi; };
            return state;
        });
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CharacterFormat
    {
        public uint Size, Mask, Effects;
        public int Height, Offset;
        public uint Color;
        public byte Charset, Pitch;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Face;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern System.IntPtr SendMessage(System.IntPtr window, int message, System.IntPtr flags, ref CharacterFormat format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern System.IntPtr SendMessage(System.IntPtr window, int message, System.IntPtr flags, System.IntPtr value);
    internal static void Apply(RichTextBox box, Font font)
    {
        DocumentDpi document = Track(box);
        int start = box.SelectionStart, length = box.SelectionLength;
        box.Font = font;
        // Font equality can make the property setter a no-op even when native DPI
        // processing changed RichEdit's document character sizes independently.
        box.SelectAll(); box.SelectionFont = font;
        // WinForms converts Font via a process-DPI HFONT. After a live DPI
        // change even SelectionFont can report the right points while the
        // native document has the wrong size. Set absolute twips directly.
        if (box.IsHandleCreated)
        {
            var format = new CharacterFormat { Size = (uint)Marshal.SizeOf(typeof(CharacterFormat)), Mask = 0x80000000, Height = (int)System.Math.Round(font.SizeInPoints * 20), Face = "" };
            SendMessage(box.Handle, 0x0444, new System.IntPtr(4), ref format); // EM_SETCHARFORMAT, SCF_ALL, CFM_SIZE
        }
        box.Select(start, length);
        if (box.IsHandleCreated)
        {
            int dpi = box.DeviceDpi;
            try { dpi = (int)GetDpiForWindow(box.Handle); } catch (System.EntryPointNotFoundException) { }
            box.ZoomFactor = (float)dpi / document.NativeDpi;
            // Force the native update even if WinForms' cached factor is equal.
            SendMessage(box.Handle, 0x04E1, new System.IntPtr(dpi), new System.IntPtr(document.NativeDpi)); // EM_SETZOOM
        }
    }
}
