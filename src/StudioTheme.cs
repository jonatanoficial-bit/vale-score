using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ValeScore
{
    internal static class StudioTheme
    {
        internal static readonly Color Night = Color.FromArgb(7, 12, 24);
        internal static readonly Color Midnight = Color.FromArgb(12, 21, 38);
        internal static readonly Color Chrome = Color.FromArgb(18, 31, 52);
        internal static readonly Color Panel = Color.FromArgb(23, 39, 63);
        internal static readonly Color Raised = Color.FromArgb(31, 52, 79);
        internal static readonly Color Azure = Color.FromArgb(96, 191, 224);
        internal static readonly Color Mint = Color.FromArgb(101, 223, 204);
        internal static readonly Color Gold = Color.FromArgb(238, 188, 104);
        internal static readonly Color Ivory = Color.FromArgb(244, 241, 232);
        internal static readonly Color Quiet = Color.FromArgb(157, 178, 197);
        internal static readonly Color Line = Color.FromArgb(77, 109, 140);

        internal static Color Blend(Color first, Color second, float amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)Math.Round(first.A + (second.A - first.A) * amount),
                (int)Math.Round(first.R + (second.R - first.R) * amount),
                (int)Math.Round(first.G + (second.G - first.G) * amount),
                (int)Math.Round(first.B + (second.B - first.B) * amount));
        }

        internal static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float size = Math.Max(2, radius * 2);
            path.AddArc(bounds.Left, bounds.Top, size, size, 180, 90);
            path.AddArc(bounds.Right - size, bounds.Top, size, size, 270, 90);
            path.AddArc(bounds.Right - size, bounds.Bottom - size, size, size, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - size, size, size, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

