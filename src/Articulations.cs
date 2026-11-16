using System;
using System.Drawing;

namespace ValeScore
{
    public static class Articulations
    {
        public static readonly string[] Names = { "Staccato", "Staccatissimo", "Tenuto", "Acento", "Marcato" };
        public static readonly string[] XmlNames = { "staccato", "staccatissimo", "tenuto", "accent", "strong-accent" };

        public static double SoundLength(string mark)
        {
            switch (mark)
            {
                case "staccato": return .55;
                case "staccatissimo": return .30;
                case "tenuto": return 1;
                case "accent": return .92;
                case "strong-accent": return .75;
                default: return 1;
            }
        }

        public static int Attack(string mark, int velocity)
        {
            int boost = mark == "strong-accent" ? 24 : mark == "accent" ? 16 : 0;
            return Math.Min(127, velocity + boost);
        }

        internal static void Draw(Graphics g, Brush ink, string mark, float x, float y, bool above)
        {
            using (var pen = new Pen(ink, 1.55f))
            {
                switch (mark)
                {
                    case "staccato":
                        g.FillEllipse(ink, x - 2, y - 2, 4, 4);
                        break;
                    case "staccatissimo":
                        int direction = above ? 1 : -1;
                        g.FillPolygon(ink, new[] { new PointF(x - 2.6f, y - direction * 4),
                            new PointF(x + 2.6f, y - direction * 4), new PointF(x, y + direction * 4) });
                        break;
                    case "tenuto":
                        g.DrawLine(pen, x - 5, y, x + 5, y);
                        break;
                    case "accent":
                        g.DrawLines(pen, new[] { new PointF(x - 5, y - 3), new PointF(x + 5, y), new PointF(x - 5, y + 3) });
                        break;
                    case "strong-accent":
                        g.DrawLines(pen, new[] { new PointF(x - 4, y + 4), new PointF(x, y - 4), new PointF(x + 4, y + 4) });
                        break;
                }
            }
        }
    }
}
