using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ValeScore
{
    internal class StudioButton : Button
    {
        private bool hover, pressed;

        public StudioButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            DoubleBuffered = true;
        }

        protected Color GlyphColor
        {
            get
            {
                if (!Enabled) return Color.FromArgb(118, 139, 160);
                return BackColor.GetBrightness() > .62f ? StudioTheme.Night : ForeColor;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); pressed = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            if (Width < 3 || Height < 3) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
            bool primary = BackColor.GetBrightness() > .62f;
            Color baseColor = !Enabled ? StudioTheme.Blend(BackColor, StudioTheme.Night, .52f) :
                pressed ? StudioTheme.Blend(BackColor, StudioTheme.Night, .24f) :
                hover ? StudioTheme.Blend(BackColor, Color.White, primary ? .15f : .09f) : BackColor;
            using (var shape = StudioTheme.Rounded(bounds, Math.Min(10, Height / 3.2f)))
            using (var fill = new LinearGradientBrush(bounds,
                StudioTheme.Blend(baseColor, Color.White, primary ? .17f : .10f),
                StudioTheme.Blend(baseColor, StudioTheme.Night, primary ? .12f : .20f), LinearGradientMode.Vertical))
            using (var border = new Pen(Focused && ShowFocusCues ? StudioTheme.Gold :
                hover ? StudioTheme.Blend(StudioTheme.Azure, StudioTheme.Ivory, .32f) :
                primary ? StudioTheme.Blend(StudioTheme.Gold, StudioTheme.Ivory, .26f) : StudioTheme.Line,
                Focused && ShowFocusCues ? 1.8f : hover ? 1.25f : .85f))
            {
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawPath(border, shape);
                var gloss = new RectangleF(bounds.Left + 2, bounds.Top + 2, bounds.Width - 4, Math.Max(2, bounds.Height * .34f));
                using (var sheen = new SolidBrush(Color.FromArgb(primary ? 46 : 28, Color.White)))
                using (var clip = StudioTheme.Rounded(bounds, Math.Min(10, Height / 3.2f)))
                {
                    e.Graphics.SetClip(clip);
                    e.Graphics.FillRectangle(sheen, gloss);
                    e.Graphics.ResetClip();
                }
                if (hover || Focused)
                {
                    using (var inner = new Pen(Color.FromArgb(80, StudioTheme.Mint), 1))
                    using (var inset = StudioTheme.Rounded(RectangleF.Inflate(bounds, -3, -3), Math.Min(7, Height / 4f)))
                        e.Graphics.DrawPath(inner, inset);
                }
            }
            if (Text.Length > 0)
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, GlyphColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class ScoreDesk : Panel
    {
        public ScoreDesk() { DoubleBuffered = true; ResizeRedraw = true; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            using (var wash = new LinearGradientBrush(ClientRectangle, StudioTheme.Chrome, StudioTheme.Night, LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(wash, ClientRectangle);
            using (var aurora = new SolidBrush(Color.FromArgb(28, StudioTheme.Azure)))
                e.Graphics.FillEllipse(aurora, ClientSize.Width * .50f, -ClientSize.Height * .35f, ClientSize.Width, ClientSize.Height);
            using (var ember = new SolidBrush(Color.FromArgb(18, StudioTheme.Gold)))
                e.Graphics.FillEllipse(ember, -ClientSize.Width * .30f, ClientSize.Height * .55f, ClientSize.Width, ClientSize.Height);

            if (Controls.Count == 0) return;
            var sheet = Controls[0].Bounds;
            for (int i = 16; i > 0; i--)
            {
                var shape = Rectangle.Inflate(sheet, i, i);
                shape.Offset(2, 6);
                using (var shadow = new SolidBrush(Color.FromArgb(8, 0, 0, 0)))
                    e.Graphics.FillRectangle(shadow, shape);
            }
            using (var halo = new Pen(Color.FromArgb(90, StudioTheme.Azure), 1.2f))
                e.Graphics.DrawRectangle(halo, sheet.X - 3, sheet.Y - 3, sheet.Width + 6, sheet.Height + 6);
        }
    }

    internal sealed class StudioGroup : Panel
    {
        public StudioGroup()
        {
            DoubleBuffered = true;
            BackColor = StudioTheme.Panel;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var shape = StudioTheme.Rounded(bounds, 9))
            using (var fill = new LinearGradientBrush(bounds, StudioTheme.Raised, StudioTheme.Panel, LinearGradientMode.Vertical))
            using (var border = new Pen(Color.FromArgb(100, StudioTheme.Line), .8f))
            {
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawPath(border, shape);
            }
        }
    }

    internal sealed class StudioSection : TableLayoutPanel
    {
        public StudioSection()
        {
            DoubleBuffered = true;
            BackColor = StudioTheme.Panel;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var shape = StudioTheme.Rounded(bounds, 15))
            using (var fill = new LinearGradientBrush(bounds, StudioTheme.Raised, StudioTheme.Panel, LinearGradientMode.Vertical))
            using (var border = new Pen(Color.FromArgb(120, StudioTheme.Line), 1))
            {
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawPath(border, shape);
            }
        }
    }

    internal sealed class CinematicFrame : TableLayoutPanel
    {
        public CinematicFrame()
        {
            DoubleBuffered = true;
            BackColor = StudioTheme.Night;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var wash = new LinearGradientBrush(ClientRectangle, StudioTheme.Midnight, StudioTheme.Night, LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(wash, ClientRectangle);
            using (var blue = new SolidBrush(Color.FromArgb(25, StudioTheme.Azure)))
                e.Graphics.FillEllipse(blue, Width * .51f, -Height * .25f, Width, Height);
            using (var gold = new SolidBrush(Color.FromArgb(17, StudioTheme.Gold)))
                e.Graphics.FillEllipse(gold, -Width * .38f, Height * .54f, Width, Height);
        }
    }
}

