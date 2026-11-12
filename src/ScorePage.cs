using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ValeScore
{
    public sealed class StaffPosition
    {
        public int Part, Staff, Tick, Step;
    }

    public sealed class ScorePage : Control
    {
        public const int PageWidth = 780, PageHeight = 1040;
        private const int StaffLeft = 132, StaffRight = 738, FirstStaffTop = 184, StaffGap = 90, BarWidth = (StaffRight - StaffLeft) / 3;
        private static readonly Color Ink = Color.FromArgb(34, 38, 44);
        private static readonly Color Accent = Color.FromArgb(20, 118, 105);
        private static readonly Color Guide = Color.FromArgb(232, 244, 240);
        public Score Music;
        public int Page;
        public int Playing = -1;
        public StaffPosition Selection;
        public Action<StaffPosition, bool> Pick;

        public ScorePage()
        {
            Size = new Size(PageWidth, PageHeight);
            DoubleBuffered = true;
            BackColor = Color.White;
            AccessibleName = "Página da partitura";
            TabStop = false;
        }

        private static int Rows(Score score) { return score.Parts.Sum(p => p.Staves.Count); }
        private static int SystemsPerPage(Score score) { return Math.Max(1, 826 / (Rows(score) * StaffGap + 38)); }
        public static int PageCount(Score score) { return (score.Bars + SystemsPerPage(score) * 3 - 1) / (SystemsPerPage(score) * 3); }
        public static int PageOf(Score score, int tick) { return tick / (48 * SystemsPerPage(score)); }
        private static int BottomStep(int staff) { return staff == 0 ? 30 : 18; }
        private static int StaffTop(Score score, int system, int row) { return FirstStaffTop + system * (Rows(score) * StaffGap + 38) + row * StaffGap; }
        private static float NoteX(int bar, int unit) { return StaffLeft + bar * BarWidth + 17 + unit * ((BarWidth - 26f) / 16); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Music != null) Draw(e.Graphics, Music, Page, Selection, Playing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Music == null || Pick == null || e.X < StaffLeft || e.X >= StaffRight) return;
            int bar = (e.X - StaffLeft) / BarWidth;
            int unit = Math.Max(0, Math.Min(15, (int)Math.Round((e.X - NoteX(bar, 0)) / ((BarWidth - 26f) / 16))));
            for (int system = 0; system < SystemsPerPage(Music); system++)
            {
                int tick = (Page * SystemsPerPage(Music) * 3 + system * 3 + bar) * 16 + unit;
                if (tick >= Music.Bars * 16) continue;
                int row = 0;
                for (int part = 0; part < Music.Parts.Count; part++)
                for (int staff = 0; staff < Music.Parts[part].Staves.Count; staff++, row++)
                {
                    int top = StaffTop(Music, system, row);
                    if (e.Y < top - 25 || e.Y >= top + 65) continue;
                    int step = BottomStep(staff) + (int)Math.Round((top + 40 - e.Y) / 5.0);
                    Pick(new StaffPosition { Part = part, Staff = staff, Tick = tick, Step = step }, e.Button == MouseButtons.Right);
                    return;
                }
            }
        }

        public static void Draw(Graphics g, Score score, int page, StaffPosition selected, int playing)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var ink = new SolidBrush(Ink))
            using (var line = new Pen(Ink, 1.05f))
            using (var accent = new SolidBrush(Accent))
            using (var guide = new SolidBrush(Guide))
            using (var small = new Font("Segoe UI", 8))
            using (var credits = new Font("Georgia", 11, FontStyle.Italic))
            using (var title = new Font("Georgia", 24))
            using (var meter = new Font("Georgia", 14, FontStyle.Bold))
            using (var center = new StringFormat { Alignment = StringAlignment.Center })
            using (var right = new StringFormat { Alignment = StringAlignment.Far })
            {
                if (page == 0)
                {
                    g.DrawString(score.Title, title, ink, new RectangleF(44, 51, PageWidth - 88, 86), center);
                    g.DrawString(score.Author, credits, ink, new RectangleF(48, 124, PageWidth - 96, 24), right);
                    g.DrawString("Andamento  " + score.Tempo + " bpm", small, ink, 51, 153);
                }
                else g.DrawString(score.Title, credits, ink, 51, 115);

                for (int system = 0; system < SystemsPerPage(score); system++)
                {
                    int first = (page * SystemsPerPage(score) + system) * 3;
                    if (first >= score.Bars) break;
                    int bars = Math.Min(3, score.Bars - first);
                    int row = 0;
                    int endX = StaffLeft + BarWidth * bars;
                    for (int part = 0; part < score.Parts.Count; part++)
                    {
                        var instrument = score.Parts[part];
                        int firstY = StaffTop(score, system, row);
                        g.DrawString(instrument.Name, small, ink, 22, firstY - 20);
                        if (instrument.Staves.Count == 2)
                        {
                            int endY = firstY + StaffGap + 40;
                            g.DrawLine(line, 47, firstY, 47, endY);
                            using (var brace = new GraphicsPath())
                            {
                                int middle = (firstY + endY) / 2;
                                brace.AddBezier(45, firstY, 30, firstY + 15, 47, middle - 9, 33, middle);
                                brace.AddBezier(33, middle, 47, middle + 9, 30, endY - 15, 45, endY);
                                g.DrawPath(line, brace);
                            }
                        }

                        for (int staff = 0; staff < instrument.Staves.Count; staff++, row++)
                        {
                            int y = StaffTop(score, system, row);
                            if (selected != null && selected.Part == part && selected.Staff == staff &&
                                selected.Tick >= first * 16 && selected.Tick < (first + bars) * 16)
                            {
                                float x = NoteX(selected.Tick / 16 - first, selected.Tick % 16);
                                g.FillRectangle(guide, x - 7, y - 23, 14, 88);
                            }

                            for (int i = 0; i < 5; i++) g.DrawLine(line, 47, y + 10 * i, endX, y + 10 * i);
                            DrawClef(g, ink, line, staff, y);
                            g.DrawString("4", meter, ink, 101, y - 4);
                            g.DrawString("4", meter, ink, 101, y + 16);
                            for (int bar = 0; bar <= bars; bar++)
                                g.DrawLine(line, StaffLeft + bar * BarWidth, y, StaffLeft + bar * BarWidth, y + 40);
                            if (first + bars == score.Bars) g.FillRectangle(ink, endX + 3, y, 3, 41);

                            for (int bar = 0; bar < bars; bar++)
                            {
                                int start = (first + bar) * 16;
                                if (row == 0) g.DrawString((first + bar + 1).ToString(), small, ink, StaffLeft + bar * BarWidth + 3, y - 20);
                                int cursor = start;
                                int previousAlter = 0;
                                var accidentals = new Dictionary<int, int>();
                                foreach (var note in instrument.Staves[staff].Where(n => n.Tick >= start && n.Tick < start + 16))
                                {
                                    while (cursor < note.Tick)
                                    {
                                        int rest = Score.RestLength(cursor, note.Tick - cursor);
                                        DrawRest(g, ink, line, NoteX(bar, cursor - start), y, rest);
                                        cursor += rest;
                                    }
                                    accidentals.TryGetValue(note.Step, out previousAlter);
                                    bool active = (playing >= note.Tick && playing < note.Tick + note.Length) ||
                                        (selected != null && selected.Part == part && selected.Staff == staff && selected.Tick == note.Tick);
                                    DrawNote(g, active ? accent : ink, line, NoteX(bar, note.Tick - start), y,
                                        note.Step - BottomStep(staff), note.Length, note.Alter, previousAlter != note.Alter);
                                    accidentals[note.Step] = note.Alter;
                                    cursor += note.Length;
                                }
                                while (cursor < start + 16)
                                {
                                    int rest = Score.RestLength(cursor, start + 16 - cursor);
                                    DrawRest(g, ink, line, rest == 16 ? StaffLeft + bar * BarWidth + BarWidth / 2 : NoteX(bar, cursor - start), y, rest);
                                    cursor += rest;
                                }
                            }
                        }
                    }
                }
                g.DrawString((page + 1).ToString(), small, ink, new RectangleF(0, 1002, PageWidth, 24), center);
            }
        }

        private static void DrawClef(Graphics g, Brush brush, Pen pen, int staff, int y)
        {
            using (var path = new GraphicsPath())
            using (var curve = new Pen(pen.Color, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (staff == 1)
                {
                    path.AddBezier(55, y + 33, 79, y + 22, 81, y - 5, 64, y + 2);
                    path.AddBezier(64, y + 2, 57, y + 4, 57, y + 14, 64, y + 15);
                    g.DrawPath(curve, path);
                    g.FillEllipse(brush, 59, y + 7, 8, 8);
                    g.FillEllipse(brush, 80, y + 5, 4, 4);
                    g.FillEllipse(brush, 80, y + 15, 4, 4);
                    return;
                }
                path.AddBezier(65, y + 38, 54, y + 26, 77, y + 20, 79, y + 32);
                path.AddBezier(79, y + 32, 83, y + 52, 51, y + 51, 52, y + 32);
                path.AddBezier(52, y + 32, 53, y + 15, 88, y + 8, 73, y - 19);
                path.AddBezier(73, y - 19, 53, y - 3, 72, y + 30, 72, y + 51);
                path.AddBezier(72, y + 51, 75, y + 65, 58, y + 65, 61, y + 55);
                g.DrawPath(curve, path);
                g.FillEllipse(brush, 58, y + 51, 8, 8);
            }
        }

        private static void DrawNote(Graphics g, Brush brush, Pen pen, float x, int top, int step, int length, int alter, bool accidental)
        {
            float y = top + 40 - step * 5;
            for (int ledger = -2; ledger >= step; ledger -= 2) g.DrawLine(pen, x - 9, top + 40 - ledger * 5, x + 9, top + 40 - ledger * 5);
            for (int ledger = 10; ledger <= step; ledger += 2) g.DrawLine(pen, x - 9, top + 40 - ledger * 5, x + 9, top + 40 - ledger * 5);
            if (accidental)
                using (var font = new Font("Segoe UI Symbol", 12))
                    g.DrawString(alter == 1 ? "♯" : alter == -1 ? "♭" : "♮", font, brush, x - 21, y - 13);
            var state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-19);
            using (var outline = new Pen(((SolidBrush)brush).Color, 1.5f))
            {
                if (length >= 8) g.DrawEllipse(outline, -5.8f, -3.4f, 11.6f, 6.8f);
                else g.FillEllipse(brush, -5.8f, -3.4f, 11.6f, 6.8f);
            }
            g.Restore(state);
            if (length == 16) return;
            int direction = step < 4 ? -1 : 1;
            float stem = x - direction * 5;
            float end = y + direction * 29;
            g.DrawLine(pen, stem, y, stem, end);
            int flags = length == 1 ? 2 : length == 2 ? 1 : 0;
            for (int flag = 0; flag < flags; flag++)
            {
                float start = end - direction * flag * 7;
                g.DrawBezier(pen, stem, start, stem + 1, start - direction * 9, stem + 12, start - direction * 8,
                    stem + 8, start - direction * 19);
            }
        }

        private static void DrawRest(Graphics g, Brush brush, Pen pen, float x, int y, int length)
        {
            if (length >= 8)
            {
                g.FillRectangle(brush, x - 5, y + (length == 16 ? 10 : 16), 10, 4);
                return;
            }
            using (var stroke = new Pen(pen.Color, 1.6f))
            {
                if (length == 4)
                {
                    g.DrawLines(stroke, new[] { new PointF(x - 2, y + 7), new PointF(x + 4, y + 16),
                        new PointF(x - 2, y + 22), new PointF(x + 3, y + 29) });
                    g.DrawArc(stroke, x - 3, y + 26, 8, 8, 80, 240);
                    return;
                }
                g.DrawLine(stroke, x + 5, y + 13, x - 1, y + (length == 1 ? 36 : 28));
                g.FillEllipse(brush, x - 5, y + 12, 5, 5);
                g.DrawArc(stroke, x - 3, y + 11, 8, 8, 5, 140);
                if (length == 1)
                {
                    g.FillEllipse(brush, x - 7, y + 21, 5, 5);
                    g.DrawArc(stroke, x - 5, y + 20, 8, 8, 5, 140);
                }
            }
        }
    }
}
