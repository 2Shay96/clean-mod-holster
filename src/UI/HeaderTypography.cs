using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Xml;

namespace RDR2ModHolster;

// Two complete fixed vector compositions, not an embedded font or reusable glyph library.
// See docs/HEADER-LETTERING.md for the designer's finished-artwork permission and provenance.
internal static class HeaderTypography
{
    private static readonly GraphicsPath Title = Load("RDR2ModHolster.clean-title.svg");
    private static readonly GraphicsPath Subtitle = Load("RDR2ModHolster.clean-subtitle.svg");

    static HeaderTypography()
    {
        AppDomain.CurrentDomain.ProcessExit += delegate { Title.Dispose(); Subtitle.Dispose(); };
    }

    internal static GraphicsPath Load(string name)
    {
        var document = new XmlDocument { XmlResolver = null };
        using (Stream stream = typeof(HeaderTypography).Assembly.GetManifestResourceStream(name))
        {
            if (stream == null) throw new InvalidOperationException("The fixed CLEAN header lettering is missing.");
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
        }
        var result = new GraphicsPath();
        foreach (XmlElement outline in document.GetElementsByTagName("path", "http://www.w3.org/2000/svg"))
        {
            result.FillMode = outline.GetAttribute("fill-rule") == "nonzero" ? FillMode.Winding : FillMode.Alternate;
            string[] tokens = outline.GetAttribute("d").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var points = new List<PointF>();
            for (int i = 0; i < tokens.Length;)
            {
                string command = tokens[i++];
                if (command == "M" || command == "L")
                {
                    float x = Single.Parse(tokens[i++], CultureInfo.InvariantCulture);
                    float y = Single.Parse(tokens[i++], CultureInfo.InvariantCulture);
                    points.Add(new PointF(x, y));
                }
                else if (command == "Z") { if (points.Count >= 3) result.AddPolygon(points.ToArray()); points.Clear(); }
                else throw new InvalidOperationException("Unexpected fixed header outline command.");
            }
        }
        return result;
    }

    public static GraphicsPath Create(string text, float emPixels)
    {
        GraphicsPath original;
        if (text == "CLEAN") original = Title;
        else if (text == "Mod Remover and Holster") original = Subtitle;
        else throw new ArgumentException("Only the two complete fixed header compositions are available.", "text");
        var result = (GraphicsPath)original.Clone();
        using (var scale = new Matrix()) { scale.Scale(emPixels / 100f, emPixels / 100f); result.Transform(scale); }
        return result;
    }

    public static void DrawAligned(Graphics graphics, GraphicsPath path, RectangleF ink, float left, float top, Color colour)
    {
        using (var transform = new Matrix())
        using (var fill = new SolidBrush(colour))
        {
            transform.Translate(left - ink.Left, top - ink.Top); path.Transform(transform); graphics.FillPath(fill, path);
        }
    }
}
