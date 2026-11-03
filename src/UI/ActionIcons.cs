using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RDR2ModHolster;

// Fixed, flattened vector assets. Original SVGs and individual rights: docs/ACTION-ICONS.md.
internal static class ActionIcons
{
    private static readonly GraphicsPath[] Paths = {
        HeaderTypography.Load("RDR2ModHolster.Icon.Anonymous_Magnify_1_icon.svg"),
        HeaderTypography.Load("RDR2ModHolster.Icon.joede-store-symbol.svg"),
        HeaderTypography.Load("RDR2ModHolster.Icon.reload-icon.svg"),
        HeaderTypography.Load("RDR2ModHolster.Icon.trash.svg")
    };
    static ActionIcons() { AppDomain.CurrentDomain.ProcessExit += delegate { foreach (var p in Paths) p.Dispose(); }; }
    public static void Draw(Graphics graphics, Rectangle target, Color colour, int index)
    {
        using (var path = (GraphicsPath)Paths[index].Clone())
        using (var matrix = new Matrix())
        using (var brush = new SolidBrush(colour))
        {
            RectangleF bounds = path.GetBounds();
            float scale = Math.Min(target.Width / bounds.Width, target.Height / bounds.Height);
            matrix.Translate(target.X + (target.Width - bounds.Width * scale) / 2f,
                target.Y + (target.Height - bounds.Height * scale) / 2f);
            matrix.Scale(scale, scale); matrix.Translate(-bounds.X, -bounds.Y);
            path.Transform(matrix); graphics.FillPath(brush, path);
        }
    }
}
