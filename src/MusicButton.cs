using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ValeScore
{
    internal sealed class MusicButton : StudioButton
    {
        private readonly Action<Graphics, Brush> draw;

        public MusicButton(Action<Graphics, Brush> draw)
        {
            this.draw = draw;
            Size = new Size(34, 38);
            Margin = new Padding(1, 1, 1, 1);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var state = e.Graphics.Save();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TranslateTransform(ClientSize.Width / 2f, ClientSize.Height / 2f + .5f);
            e.Graphics.ScaleTransform(ClientSize.Height / 42f, ClientSize.Height / 42f);
            using (var ink = new SolidBrush(GlyphColor)) draw(e.Graphics, ink);
            e.Graphics.Restore(state);
        }

        public static MusicButton Figure(int length, bool rest)
        {
            return new MusicButton((g, ink) =>
            {
                if (rest)
                {
                    if (length >= 8)
                        using (var reference = new Pen(ink, .8f)) g.DrawLine(reference, -10, 0, 10, 0);
                    float top = length == 16 ? -10 : -20;
                    MusicGlyphs.Rest(g, ink, 0, top, length);
                }
                else
                {
                    float y = length == 16 ? 0 : 13;
                    float x = length <= 2 ? -4 : -1;
                    MusicGlyphs.Head(g, ink, x, y, length);
                    if (length == 16) return;
                    MusicGlyphs.Stem(g, ink, x, y, -1, -15);
                    if (length <= 2) MusicGlyphs.Flags(g, ink, MusicGlyphs.StemX(x, -1), -15, -1, length == 1 ? 2 : 1);
                }
            });
        }
    }
}

