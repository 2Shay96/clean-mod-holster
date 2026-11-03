using System;
using System.Collections.Generic;
using System.Drawing;

namespace RDR2ModHolster;

internal static class Th
{
    public static readonly Color Crimson = Color.FromArgb(226, 70, 77);
    public static readonly Color Gold = Color.FromArgb(254, 172, 1);
    public static readonly Color DeepRed = Color.FromArgb(185, 3, 3);
    public static readonly Color Bg = Color.FromArgb(8, 5, 7);
    public static readonly Color BgSide = Color.FromArgb(16, 9, 11);
    public static readonly Color BgCard = Color.FromArgb(20, 13, 11);
    public static readonly Color BgTable = Color.FromArgb(16, 11, 10);
    public static readonly Color BgTableAlt = Color.FromArgb(24, 16, 13);
    public static readonly Color BgHover = Color.FromArgb(42, 24, 17);
    public static readonly Color BgPress = Color.FromArgb(56, 28, 18);
    public static readonly Color T1 = Color.FromArgb(255, 245, 228);
    public static readonly Color T2 = Color.FromArgb(200, 183, 156);
    public static readonly Color T3 = Color.FromArgb(163, 148, 128);
    public static readonly Color T4 = Color.FromArgb(141, 127, 111);
    public static readonly Color Ok = Color.FromArgb(118, 216, 104);
    public static readonly Color Border = Color.FromArgb(61, 44, 28);
}

// Pixel fonts make owner-drawn text predictable at each window's DPI, including under mixed-monitor
// configurations. There is no mutable process-wide scaling factor and no log-font double scaling.
internal sealed class UiFonts : IDisposable
{
    private readonly Dictionary<string, Font> _current = new Dictionary<string, Font>();
    private readonly List<Font> _retired = new List<Font>();
    private int _dpi;
    public UiFonts() { Update(96); }
    public void Update(int dpi)
    {
        if (_dpi == dpi) return;
        _retired.AddRange(_current.Values); _current.Clear(); _dpi = dpi;
    }
    private Font Get(string key, float points, FontStyle style = FontStyle.Regular, string family = "Segoe UI")
    {
        Font font;
        if (!_current.TryGetValue(key, out font)) { font = new Font(family, points * _dpi / 72f, style, GraphicsUnit.Pixel); _current.Add(key, font); }
        return font;
    }
    public Font FTitle { get { return Get("title", 15, FontStyle.Bold); } }
    public Font FSub { get { return Get("sub", 9, FontStyle.Regular); } }
    public Font FHead { get { return Get("head", 12, FontStyle.Bold); } }
    public Font FBody { get { return Get("body", 9.5f); } }
    public Font FBold { get { return Get("bold", 9.5f, FontStyle.Bold); } }
    public Font FSm { get { return Get("small", 8.5f); } }
    public Font FSmB { get { return Get("smallbold", 8.5f, FontStyle.Bold); } }
    public Font FStat { get { return Get("stat", 24, FontStyle.Bold); } }
    public Font FStatLbl { get { return Get("statlabel", 9); } }
    public Font FTbl { get { return Get("table", 9); } }
    public Font FTblH { get { return Get("tablehead", 8, FontStyle.Bold); } }
    public Font FBadge { get { return Get("badge", 8, FontStyle.Bold); } }
    public Font FLog { get { return Get("log", 10, FontStyle.Regular, "Consolas"); } }
    // RichEdit stores character sizes in points/twips. Pixel fonts are converted using
    // the process startup DPI, which scales document text twice after a live DPI change.
    private Font Native(string key, float points, string family)
    {
        Font font;
        if (!_current.TryGetValue(key, out font)) { font = new Font(family, points, FontStyle.Regular, GraphicsUnit.Point); _current.Add(key, font); }
        return font;
    }
    public Font FNativeBody { get { return Native("nativebody", 9.5f, "Segoe UI"); } }
    public Font FNativeLog { get { return Native("nativelog", 10, "Consolas"); } }
    public Font FGameTitle { get { return Get("gametitle", 13, FontStyle.Bold); } }
    public Font FVer { get { return Get("version", 8); } }
    public Font FCmdH { get { return Get("command", 8, FontStyle.Bold); } }
    public void ReleaseRetired() { foreach (Font f in _retired) f.Dispose(); _retired.Clear(); }
    public void Dispose() { ReleaseRetired(); foreach (Font f in _current.Values) f.Dispose(); _current.Clear(); }
}
