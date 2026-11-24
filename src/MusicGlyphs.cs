using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ValeScore
{
    internal static class MusicGlyphs
    {
        public static void Tie(Graphics g, Brush ink, float start, float end, float y, int side)
        {
            float width = end - start;
            float rise = side * Math.Min(11, 2.8f + width * .10f);
            Arc(g, ink, start, y, end, y, rise, 2);
        }

        public static void Arc(Graphics g, Brush ink, float start, float y1, float end, float y2, float rise, float weight)
        {
            float width = end - start;
            float inset = Math.Sign(rise) * weight;
            using (var path = new GraphicsPath())
            {
                path.AddBezier(start, y1, start + width * .25f, y1 + rise, end - width * .25f, y2 + rise, end, y2);
                path.AddBezier(end, y2, end - width * .25f, y2 + rise - inset, start + width * .25f, y1 + rise - inset, start, y1);
                path.CloseFigure();
                g.FillPath(ink, path);
            }
        }

        public static void PauseMark(Graphics g, Brush ink, string mark, float x, float y)
        {
            using (var pen = new Pen(ink, 1.45f))
            {
                if (mark == "fermata")
                {
                    g.DrawArc(pen, x - 8, y - 5, 16, 10, 194, 152);
                    g.FillEllipse(ink, x - 2.1f, y + 2.2f, 4.2f, 4.2f);
                }
                else if (mark == "breath-mark")
                {
                    g.DrawBezier(pen, x - 1.5f, y - 7, x + 4.2f, y - 5.5f, x + 3.2f, y + 2.8f, x - .5f, y + 6.3f);
                    g.DrawLine(pen, x - .5f, y + 6.3f, x - 2.2f, y + 4.2f);
                }
                else if (mark == "caesura")
                {
                    g.DrawLine(pen, x - 5.5f, y - 7, x - 1.2f, y + 7);
                    g.DrawLine(pen, x + 1.7f, y - 7, x + 6, y + 7);
                }
            }
        }

        public static void Head(Graphics g, Brush ink, float x, float y, int length)
        {
            using (var head = new GraphicsPath(FillMode.Alternate))
            using (var rotation = new Matrix())
            {
                if (length == 16)
                {
                    head.AddEllipse(-7.2f, -4.4f, 14.4f, 8.8f);
                    using (var opening = new GraphicsPath())
                    {
                        opening.AddEllipse(-3.3f, -2.8f, 6.6f, 5.6f);
                        rotation.Rotate(30);
                        opening.Transform(rotation);
                        head.AddPath(opening, false);
                    }
                    rotation.Reset();
                }
                else
                {
                    head.AddEllipse(-6, -3.8f, 12, 7.6f);
                    if (length == 8) head.AddEllipse(-4.6f, -2.25f, 9.2f, 4.5f);
                    rotation.Rotate(-20);
                }
                rotation.Translate(x, y, MatrixOrder.Append);
                head.Transform(rotation);
                g.FillPath(ink, head);
            }
        }

        public static float StemX(float x, int direction) { return x - direction * 5.25f; }

        public static void Stem(Graphics g, Brush ink, float x, float y, int direction, float endY)
        {
            using (var pen = new Pen(ink, 1.45f)) g.DrawLine(pen, StemX(x, direction), y, StemX(x, direction), endY);
        }

        public static void Flags(Graphics g, Brush ink, float x, float end, int direction, int count)
        {
            var state = g.Save();
            g.TranslateTransform(x, end);
            g.ScaleTransform(1, -direction);
            for (int i = 0; i < count; i++)
            {
                float y = i * 7.2f;
                using (var flag = new GraphicsPath())
                {
                    flag.AddBezier(0, y, 2, y + 5, 10, y + 8, 10, y + 14);
                    flag.AddBezier(10, y + 14, 11, y + 18, 9, y + 21, 7, y + 23);
                    flag.AddBezier(7, y + 23, 11, y + 14, 5, y + 12, 0, y + 8);
                    flag.CloseFigure();
                    g.FillPath(ink, flag);
                }
            }
            g.Restore(state);
        }

        public static void Rest(Graphics g, Brush ink, float x, float top, int length)
        {
            if (length >= 8)
            {
                g.FillRectangle(ink, x - 6.2f, top + (length == 16 ? 10f : 15.8f), 12.4f, 4.2f);
                return;
            }

            if (length == 4)
            {
                using (var rest = new GraphicsPath())
                {
                    rest.AddPolygon(new[] { new PointF(x - 4, top + 3), new PointF(x + 4.8f, top + 12),
                        new PointF(x + .4f, top + 20), new PointF(x + 5, top + 28),
                        new PointF(x - 3.4f, top + 22), new PointF(x + .5f, top + 14) });
                    rest.StartFigure();
                    rest.AddBezier(x + 4.5f, top + 28, x - 5, top + 24, x - 7, top + 34, x + 1.5f, top + 37);
                    rest.AddBezier(x + 1.5f, top + 37, x - 3, top + 32, x - .6f, top + 29, x + 4.5f, top + 30);
                    rest.CloseFigure();
                    g.FillPath(ink, rest);
                }
                return;
            }

            int count = length == 1 ? 2 : 1;
            using (var stem = new Pen(ink, 1.5f))
            {
                g.DrawLine(stem, x + 6.3f, top + 10, x - (count == 2 ? 2.7f : 0), top + (count == 2 ? 36 : 28));
                for (int i = 0; i < count; i++)
                {
                    float offset = i * 9.5f;
                    float left = x - 5.5f - i * 2.3f;
                    g.FillEllipse(ink, left, top + 9.4f + offset, 5.8f, 5.5f);
                    g.DrawBezier(stem, left + 1, top + 13 + offset, left + 4, top + 18 + offset,
                        x + 4.2f - i * 3, top + 15 + offset, x + 6.3f - i * 3, top + 10 + offset);
                }
            }
        }

        public static void Accidental(Graphics g, Brush ink, float x, float y, int alter)
        {
            if (alter == -2)
            {
                Accidental(g, ink, x - 3.7f, y, -1);
                Accidental(g, ink, x + 3.7f, y, -1);
                return;
            }
            if (alter == 2)
            {
                using (var pen = new Pen(ink, 2.1f))
                {
                    g.DrawLine(pen, x - 4.6f, y - 4.6f, x + 4.6f, y + 4.6f);
                    g.DrawLine(pen, x - 4.6f, y + 4.6f, x + 4.6f, y - 4.6f);
                }
                return;
            }
            using (var thin = new Pen(ink, 1.35f))
            using (var thick = new Pen(ink, 2.7f))
            {
                if (alter == -1)
                {
                    g.DrawLine(thin, x - 2.4f, y - 11, x - 2.4f, y + 7);
                    using (var bowl = new GraphicsPath(FillMode.Alternate))
                    {
                        bowl.AddBezier(x - 2.4f, y - 1.6f, x + 6, y - 6, x + 7.2f, y + 2, x - 2.4f, y + 7);
                        bowl.CloseFigure();
                        bowl.StartFigure();
                        bowl.AddBezier(x - 1.5f, y, x + 2.8f, y - 3, x + 3, y + 2.5f, x - 1.5f, y + 5);
                        bowl.CloseFigure();
                        g.FillPath(ink, bowl);
                    }
                }
                else if (alter == 1)
                {
                    g.DrawLine(thin, x - 2.7f, y - 8, x - 2.7f, y + 10);
                    g.DrawLine(thin, x + 2.7f, y - 10, x + 2.7f, y + 8);
                    g.DrawLine(thick, x - 5.4f, y - 2.2f, x + 5.4f, y - 5);
                    g.DrawLine(thick, x - 5.4f, y + 4, x + 5.4f, y + 1.2f);
                }
                else
                {
                    g.DrawLine(thin, x - 3, y - 10, x - 3, y + 5);
                    g.DrawLine(thin, x + 3, y - 5, x + 3, y + 10);
                    g.DrawLine(thick, x - 3, y - 1.5f, x + 3, y - 4);
                    g.DrawLine(thick, x - 3, y + 4.3f, x + 3, y + 1.8f);
                }
            }
        }
    }
}

