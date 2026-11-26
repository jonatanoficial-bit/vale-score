using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ValeScore
{
    internal sealed class TransportRail : Control
    {
        private double progress;
        public TransportRail()
        {
            DoubleBuffered = true;
            AccessibleName = "Progresso da reprodução";
            Height = 7;
            TabStop = false;
        }

        public void UpdateProgress(double value)
        {
            double next = Math.Max(0, Math.Min(1, value));
            if (Math.Abs(progress - next) < .0001) return;
            progress = next;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            using (var track = new SolidBrush(Color.FromArgb(71, StudioTheme.Night)))
            using (var fill = new LinearGradientBrush(ClientRectangle, StudioTheme.Mint, StudioTheme.Gold, LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(track, 0, 2, Width, Height - 4);
                e.Graphics.FillRectangle(fill, 0, 2, (float)(Width * progress), Height - 4);
                if (progress > 0)
                using (var head = new SolidBrush(StudioTheme.Ivory))
                    e.Graphics.FillEllipse(head, (float)(Width * progress) - 2.5f, 0, 5, Height);
            }
        }
    }
}

