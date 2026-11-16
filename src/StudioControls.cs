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
                if (!Enabled) return Color.FromArgb(135, 152, 165);
                return BackColor.GetBrightness() > .62f ? Color.FromArgb(24, 30, 33) : ForeColor;
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
            using (var shape = Round(new RectangleF(1, 1, Width - 2, Height - 2), Math.Min(7, Height / 4)))
            using (var fill = new SolidBrush(Enabled && pressed ? ControlPaint.Dark(BackColor, .1f) :
                Enabled && hover ? ControlPaint.Light(BackColor, .12f) : BackColor))
            using (var border = new Pen(Focused && ShowFocusCues ? Color.FromArgb(232, 191, 130) :
                Color.FromArgb(86, 119, 135), Focused && ShowFocusCues ? 1.6f : .8f))
            {
                e.Graphics.FillPath(fill, shape);
                if (BackColor.GetBrightness() <= .62f || Focused) e.Graphics.DrawPath(border, shape);
            }
            if (Text.Length > 0)
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, GlyphColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static GraphicsPath Round(RectangleF r, float radius)
        {
            var shape = new GraphicsPath();
            float size = radius * 2;
            shape.AddArc(r.Left, r.Top, size, size, 180, 90);
            shape.AddArc(r.Right - size, r.Top, size, size, 270, 90);
            shape.AddArc(r.Right - size, r.Bottom - size, size, size, 0, 90);
            shape.AddArc(r.Left, r.Bottom - size, size, size, 90, 90);
            shape.CloseFigure();
            return shape;
        }
    }

    internal sealed class ScoreDesk : Panel
    {
        public ScoreDesk() { DoubleBuffered = true; ResizeRedraw = true; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            using (var wash = new LinearGradientBrush(ClientRectangle, Color.FromArgb(68, 88, 101),
                Color.FromArgb(32, 48, 58), LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(wash, ClientRectangle);

            if (Controls.Count == 0) return;
            var sheet = Controls[0].Bounds;
            for (int i = 11; i > 0; i--)
            {
                var shape = Rectangle.Inflate(sheet, i, i);
                shape.Offset(1, 4);
                using (var shadow = new SolidBrush(Color.FromArgb(7, 0, 0, 0)))
                    e.Graphics.FillRectangle(shadow, shape);
            }
        }
    }
}
