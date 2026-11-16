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
        public bool HitNote;
    }

    public sealed class ScorePage : Control
    {
        public const int PageWidth = 780, PageHeight = 1100;
        private static readonly Color Ink = Color.FromArgb(31, 36, 42);
        private static readonly Color Accent = Color.FromArgb(101, 64, 175);
        private static readonly Color Guide = Color.FromArgb(237, 229, 254);
        public Score Music { get; private set; }
        public ScoreLayout Engraving { get; private set; }
        public int Page;
        public int Playing = -1;
        public StaffPosition Selection, Hover;
        public bool ShowEntryPreview;
        public bool PreviewRest;
        public int PreviewLength = 4, PreviewAlter;
        public Action<StaffPosition, MouseButtons> Pick;
        public float Zoom { get; private set; }

        public ScorePage()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(255, 254, 251);
            AccessibleName = "Página da partitura";
            TabStop = false;
            SetZoom(1);
        }

        public void LoadScore(Score score)
        {
            Music = score;
            Engraving = new ScoreLayout(score);
            Hover = null;
        }

        public void SetZoom(float scale)
        {
            Zoom = scale;
            Size = new Size((int)Math.Round(PageWidth * scale), (int)Math.Round(PageHeight * scale));
            Invalidate();
        }

        public Point OnSheet(StaffPosition position, int yOffset)
        {
            var point = Engraving.Position(Music, position);
            return new Point((int)Math.Round(point.X * Zoom), (int)Math.Round((point.Y + yOffset) * Zoom));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Engraving == null) return;
            e.Graphics.ScaleTransform(Zoom, Zoom);
            Draw(e.Graphics, Music, Engraving, Page, Selection, Playing, ShowEntryPreview ? Hover : null,
                PreviewLength, PreviewRest, PreviewAlter);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Pick == null || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right)) return;
            var hit = Hit(e.X / Zoom, e.Y / Zoom);
            if (hit != null) Pick(hit, e.Button);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!ShowEntryPreview) return;
            var hit = Hit(e.X / Zoom, e.Y / Zoom);
            if (hit == null && Hover == null) return;
            if (hit != null && Hover != null && hit.Part == Hover.Part && hit.Staff == Hover.Staff &&
                hit.Tick == Hover.Tick && hit.Step == Hover.Step) return;
            Hover = hit;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hover = null;
            Invalidate();
        }

        public StaffPosition Hit(float x, float y)
        {
            if (Engraving == null || x < ScoreLayout.StaffLeft || x >= ScoreLayout.StaffRight) return null;
            int lastRow = Music.Parts.Sum(p => p.Staves.Count) - 1;
            int system = 0;
            foreach (var measures in Engraving.OnPage(Page))
            {
                var measure = measures.FirstOrDefault(m => x >= m.Left && x < m.Right);
                if (measure == null) { system++; continue; }
                int row = 0;
                for (int part = 0; part < Music.Parts.Count; part++)
                for (int staff = 0; staff < Music.Parts[part].Staves.Count; staff++, row++)
                {
                    int top = Engraving.StaffTop(system, row);
                    if (y < top - 25 || y >= top + 85 + (row == lastRow ? 9 : 0)) continue;
                    int start = measure.Index * Score.BarTicks;
                    int tick = start + measure.Nearest(x);
                    int step = BottomStep(staff) + (int)Math.Round((top + 40 - y) / 5.0);
                    int min = staff == 0 ? 25 : 13;
                    step = Math.Max(min, Math.Min(min + 18, step));
                    var lane = Music.Parts[part].Staves[staff];
                    bool lyricRow = y >= top + ScoreLayout.LyricY - 2;
                    var note = lane.Where(n => n.Tick >= start && n.Tick < start + Score.BarTicks &&
                        (lyricRow ? n.Lyric != null : Math.Abs(y - (n.IsRest ? RestY(top, n.Length) : NoteY(top, staff, n))) < (n.IsRest ? 15 : 11)))
                        .OrderBy(n => Math.Abs(EventX(measure, n) - x)).FirstOrDefault();
                    if (note != null && Math.Abs(EventX(measure, note) - x) <= (lyricRow ? 24 : 8))
                        return new StaffPosition { Part = part, Staff = staff, Tick = note.Tick, Step = step, HitNote = true };
                    return new StaffPosition { Part = part, Staff = staff, Tick = tick, Step = step };
                }
                system++;
            }
            return null;
        }

        private static int BottomStep(int staff) { return staff == 0 ? 30 : 18; }
        private static float NoteY(int top, int staff, Note note) { return top + 40 - (note.Step - BottomStep(staff)) * 5; }
        private static float RestY(int top, int length) { return top + (length == 16 ? 12 : length == 8 ? 18 : 20); }
        private static float EventX(ScoreLayout.Measure measure, Note note)
        {
            return note.IsRest && note.Length == Score.BarTicks ? measure.Left + measure.Width / 2 : measure.At(note.Tick % Score.BarTicks);
        }

        public static void Draw(Graphics g, Score score, int page, StaffPosition selected, int playing)
        {
            Draw(g, score, new ScoreLayout(score), page, selected, playing, null);
        }

        public static void Draw(Graphics g, Score score, ScoreLayout layout, int page, StaffPosition selected, int playing, StaffPosition hover,
            int previewLength = 4, bool previewRest = false, int previewAlter = 0)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var ink = new SolidBrush(Ink))
            using (var line = new Pen(Ink, 1.02f))
            using (var staffLine = new Pen(Color.FromArgb(59, 65, 71), 0.86f))
            using (var accent = new SolidBrush(Accent))
            using (var ghost = new SolidBrush(Color.FromArgb(104, Accent)))
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
                    g.DrawString(score.Title, title, ink, new RectangleF(44, 52, PageWidth - 88, 86), center);
                    g.DrawString(score.Author, credits, ink, new RectangleF(48, 122, PageWidth - 96, 24), right);
                    g.DrawString("Andamento  " + score.Tempo + " bpm", small, ink, 47, 145);
                }
                else g.DrawString(score.Title, credits, ink, 47, 113);

                int system = 0;
                foreach (var measures in layout.OnPage(page))
                {
                    int first = measures[0].Index;
                    int after = first + measures.Length;
                    float endX = measures[measures.Length - 1].Right;
                    int row = 0;
                    for (int part = 0; part < score.Parts.Count; part++)
                    {
                        var instrument = score.Parts[part];
                        int firstY = layout.StaffTop(system, row);
                        g.DrawString(instrument.Name, small, ink, 22, firstY - 20);
                        if (instrument.Staves.Count == 2)
                        {
                            int endY = firstY + ScoreLayout.StaffGap + 40;
                            g.DrawLine(line, 47, firstY, 47, endY);
                            DrawBrace(g, ink, firstY, endY);
                            int lowerTop = endY - 40;
                            foreach (var measure in measures) g.DrawLine(line, measure.Left, firstY + 40, measure.Left, lowerTop);
                            g.DrawLine(line, endX, firstY + 40, endX, lowerTop);
                            if (after == score.Bars) g.FillRectangle(ink, endX + 3, firstY + 40, 3, lowerTop - firstY - 40);
                        }

                        for (int staff = 0; staff < instrument.Staves.Count; staff++, row++)
                        {
                            int y = layout.StaffTop(system, row);
                            bool laneSelected = selected != null && selected.Part == part && selected.Staff == staff;
                            var lane = instrument.Staves[staff];
                            if (laneSelected && selected.Tick >= first * 16 && selected.Tick < after * 16)
                            {
                                var measure = layout.MeasureOf(selected.Tick / 16);
                                var symbol = lane.Find(n => n.Tick == selected.Tick);
                                float x = symbol == null ? measure.At(selected.Tick % 16) : EventX(measure, symbol);
                                g.FillRectangle(guide, x - 8, y - 19, 16, 76);
                            }

                            for (int i = 0; i < 5; i++) g.DrawLine(staffLine, 47, y + 10 * i, endX, y + 10 * i);
                            DrawClef(g, ink, line, staff, y);
                            if (first == 0)
                            {
                                g.DrawString("4", meter, ink, 99, y - 4);
                                g.DrawString("4", meter, ink, 99, y + 16);
                            }
                            g.DrawLine(line, endX, y, endX, y + 40);
                            if (after == score.Bars) g.FillRectangle(ink, endX + 3, y, 3, 41);

                            foreach (var measure in measures)
                            {
                                g.DrawLine(line, measure.Left, y, measure.Left, y + 40);
                                if (row == 0) g.DrawString((measure.Index + 1).ToString(), small, ink, measure.Left + 4, y - 20);
                                DrawMeasure(g, ink, accent, line, measure, lane, staff, y, laneSelected ? selected.Tick : -1, playing);
                            }

                            DrawLyrics(g, layout, lane, first, after, y, ink, center);
                            foreach (var item in lane.Where(n => n.Tick >= first * Score.BarTicks && n.Tick < after * Score.BarTicks))
                            {
                                if (item.Chord == null && item.Dynamic == null) continue;
                                float x = EventX(layout.MeasureOf(item.Tick / Score.BarTicks), item);
                                if (item.Chord != null)
                                    g.DrawString(item.Chord, ScoreLayout.ChordFont, ink, new PointF(x, y - 37), center);
                                if (item.Dynamic != null)
                                    g.DrawString(item.Dynamic, ScoreLayout.DynamicFont, ink, new PointF(x, y + 54), center);
                            }
                            if (hover != null && hover.Part == part && hover.Staff == staff &&
                                hover.Tick >= first * 16 && hover.Tick < after * 16 && !hover.HitNote)
                            {
                                float x = layout.MeasureOf(hover.Tick / 16).At(hover.Tick % 16);
                                if (previewRest)
                                {
                                    if (previewLength == 16)
                                    {
                                        var measure = layout.MeasureOf(hover.Tick / 16);
                                        x = measure.Left + measure.Width / 2;
                                    }
                                    DrawRest(g, ghost, x, y, previewLength);
                                }
                                else
                                {
                                    using (var previewLine = new Pen(ghost, 1))
                                        DrawNote(g, ghost, previewLine, x, y, hover.Step - BottomStep(staff), previewLength, previewAlter, previewAlter != 0);
                                }
                            }
                        }
                    }
                    system++;
                }
                g.DrawString((page + 1).ToString(), small, ink, new RectangleF(0, PageHeight - 38, PageWidth, 24), center);
            }
        }

        private static void DrawLyrics(Graphics g, ScoreLayout layout, List<Note> lane, int first, int after, int y, Brush ink, StringFormat center)
        {
            using (var trimmed = new StringFormat(center) { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                for (int i = 0; i < lane.Count; i++)
                {
                    var note = lane[i];
                    int bar = note.Tick / Score.BarTicks;
                    if (note.Lyric == null || bar < first || bar >= after) continue;
                    var measure = layout.MeasureOf(bar);
                    float x = measure.At(note.Tick % Score.BarTicks);
                    float ratio = measure.Width / measure.MinimumWidth;
                    float width = g.MeasureString(note.Lyric, ScoreLayout.TextFont).Width;
                    if (ratio >= 1)
                        g.DrawString(note.Lyric, ScoreLayout.TextFont, ink, new PointF(x, y + ScoreLayout.LyricY), center);
                    else
                    {
                        width = Math.Max(8, width * ratio);
                        g.DrawString(note.Lyric, ScoreLayout.TextFont, ink,
                            new RectangleF(x - width / 2, y + ScoreLayout.LyricY, width, 20), trimmed);
                    }

                    if (!note.HyphenAfter) continue;
                    float leftEdge = x + width / 2 - 2;
                    float dash = Math.Min(measure.Right - 4, leftEdge + 6);
                    var next = lane.Find(n => !n.IsRest && n.Tick > note.Tick);
                    if (next != null)
                    {
                        int nextBar = next.Tick / Score.BarTicks;
                        if (next.Lyric != null && nextBar < after)
                        {
                            var nextMeasure = layout.MeasureOf(nextBar);
                            float nextX = nextMeasure.At(next.Tick % Score.BarTicks);
                            float nextWidth = g.MeasureString(next.Lyric, ScoreLayout.TextFont).Width * Math.Min(1, nextMeasure.Width / nextMeasure.MinimumWidth);
                            float rightEdge = nextX - nextWidth / 2 + 2;
                            if (rightEdge - leftEdge < 6) continue;
                            dash = (leftEdge + rightEdge) / 2;
                        }
                    }
                    g.DrawString("-", ScoreLayout.TextFont, ink, new PointF(dash, y + ScoreLayout.LyricY), center);
                }
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

        private static void DrawBrace(Graphics g, Brush ink, int top, int bottom)
        {
            float middle = (top + bottom) / 2f;
            using (var brace = new GraphicsPath())
            {
                brace.AddBezier(45, top, 26, top + 14, 45, middle - 12, 31, middle);
                brace.AddBezier(31, middle, 45, middle + 12, 26, bottom - 14, 45, bottom);
                brace.AddBezier(45, bottom, 33, bottom - 18, 50, middle + 15, 35, middle);
                brace.AddBezier(35, middle, 50, middle - 15, 33, top + 18, 45, top);
                brace.CloseFigure();
                g.FillPath(ink, brace);
            }
        }

        private static void DrawMeasure(Graphics g, Brush ink, Brush accent, Pen line, ScoreLayout.Measure measure,
            List<Note> lane, int staff, int top, int selected, int playing)
        {
            int start = measure.Index * Score.BarTicks;
            int end = start + Score.BarTicks;
            var events = lane.Where(n => n.Tick >= start && n.Tick < end).ToArray();
            var accidentals = new Dictionary<int, int>();
            int cursor = start;
            for (int i = 0; i < events.Length;)
            {
                var item = events[i];
                DrawGaps(g, ink, measure, top, ref cursor, item.Tick);
                int groupEnd = Score.BeamGroupEnd(events, i);
                bool beamed = groupEnd > i + 1;
                int direction = 0;
                float xStart = 0, xEnd = 0, beamY = 0, rise = 0;
                if (beamed)
                {
                    int sum = 0;
                    for (int j = i; j < groupEnd; j++) sum += events[j].Step;
                    direction = sum < (BottomStep(staff) + 4) * (groupEnd - i) ? -1 : 1;
                    var last = events[groupEnd - 1];
                    xStart = measure.At(item.Tick - start);
                    xEnd = measure.At(last.Tick - start);
                    rise = Math.Max(-10, Math.Min(10, NoteY(top, staff, last) - NoteY(top, staff, item)));
                    beamY = NoteY(top, staff, item) + direction * 29;
                    for (int j = i + 1; j < groupEnd; j++)
                    {
                        var n = events[j];
                        float fraction = (measure.At(n.Tick - start) - xStart) / (xEnd - xStart);
                        float limit = NoteY(top, staff, n) + direction * 29 - rise * fraction;
                        beamY = direction < 0 ? Math.Min(beamY, limit) : Math.Max(beamY, limit);
                    }
                }

                for (; i < groupEnd; i++)
                {
                    var n = events[i];
                    var color = n.Tick == selected || (!n.IsRest && playing >= n.Tick && playing < n.Tick + n.Length) ? accent : ink;
                    float x = EventX(measure, n);
                    if (n.IsRest) DrawRest(g, color, x, top, n.Length);
                    else
                    {
                        int prior;
                        accidentals.TryGetValue(n.Step, out prior);
                        float? stemEnd = beamed ? (float?)(beamY + rise * (x - xStart) / (xEnd - xStart)) : null;
                        DrawNote(g, color, line, x, top, n.Step - BottomStep(staff), n.Length, n.Alter, prior != n.Alter, direction, stemEnd);
                        if (n.Articulation != null)
                        {
                            bool above = n.Step >= BottomStep(staff) + 4 || n.Articulation == "strong-accent" ||
                                n.Dynamic != null || n.Lyric != null;
                            float pitchY = NoteY(top, staff, n);
                            float y = above ? Math.Min(top - 8, pitchY - 10) : Math.Max(top + 48, pitchY + 10);
                            Articulations.Draw(g, color, n.Articulation, x, y, above);
                        }
                        accidentals[n.Step] = n.Alter;
                    }
                    cursor = n.Tick + n.Length;
                }

                if (beamed)
                {
                    int levels = item.Length == 1 ? 2 : 1;
                    for (int level = 0; level < levels; level++)
                    {
                        float leftY = beamY - direction * level * 7.1f;
                        float rightY = leftY + rise;
                        float leftX = MusicGlyphs.StemX(xStart, direction) - .7f;
                        float rightX = MusicGlyphs.StemX(xEnd, direction) + .7f;
                        float thickness = -direction * 3.8f;
                        g.FillPolygon(ink, new[] { new PointF(leftX, leftY), new PointF(rightX, rightY),
                            new PointF(rightX, rightY + thickness), new PointF(leftX, leftY + thickness) });
                    }
                }
            }
            DrawGaps(g, ink, measure, top, ref cursor, end);
        }

        private static void DrawGaps(Graphics g, Brush ink, ScoreLayout.Measure measure, int top, ref int cursor, int until)
        {
            int start = measure.Index * Score.BarTicks;
            while (cursor < until)
            {
                int length = Score.RestLength(cursor, until - cursor);
                float x = length == Score.BarTicks ? measure.Left + measure.Width / 2 : measure.At(cursor - start);
                MusicGlyphs.Rest(g, ink, x, top, length);
                cursor += length;
            }
        }

        private static void DrawRest(Graphics g, Brush brush, float x, int top, int length)
        {
            MusicGlyphs.Rest(g, brush, x, top, Score.BaseDuration(length));
            DrawDots(g, brush, x, top + 15, length);
        }

        private static void DrawDots(Graphics g, Brush brush, float x, float y, int length)
        {
            for (int i = 0; i < Score.DotCount(length); i++) g.FillEllipse(brush, x + 8 + i * 4.7f, y - 1.65f, 3.3f, 3.3f);
        }

        private static void DrawNote(Graphics g, Brush brush, Pen pen, float x, int top, int step, int length, int alter, bool accidental,
            int beamDirection = 0, float? beamY = null)
        {
            float y = top + 40 - step * 5;
            for (int ledger = -2; ledger >= step; ledger -= 2) g.DrawLine(pen, x - 9, top + 40 - ledger * 5, x + 9, top + 40 - ledger * 5);
            for (int ledger = 10; ledger <= step; ledger += 2) g.DrawLine(pen, x - 9, top + 40 - ledger * 5, x + 9, top + 40 - ledger * 5);
            if (accidental) MusicGlyphs.Accidental(g, brush, x - 14, y, alter);
            DrawDots(g, brush, x, y - (step % 2 == 0 ? 5 : 0), length);
            length = Score.BaseDuration(length);
            MusicGlyphs.Head(g, brush, x, y, length);
            if (length == 16) return;
            int direction = beamDirection != 0 ? beamDirection : step < 4 ? -1 : 1;
            float end = beamY ?? y + direction * 29;
            MusicGlyphs.Stem(g, brush, x, y, direction, end);
            if (!beamY.HasValue && length <= 2)
                MusicGlyphs.Flags(g, brush, MusicGlyphs.StemX(x, direction), end, direction, length == 1 ? 2 : 1);
        }
    }
}
