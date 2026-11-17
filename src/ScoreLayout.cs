using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ValeScore
{
    public sealed class ScoreLayout
    {
        public const int StaffLeft = 130, StaffRight = 737, FirstStaffTop = 184, StaffGap = 110;
        public const int LyricY = 73;
        private const int SystemGap = 40;
        private static readonly Font LyricFont = new Font("Georgia", 14, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Dictionary<string, int> lyricWidths = new Dictionary<string, int>();
        private readonly List<Measure[]> systems = new List<Measure[]>();
        private readonly int[] measureSystems;
        private readonly int rows, systemsPerPage, barTicks;
        private readonly int fifths;
        private readonly int staffRight;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int MeasureLeft { get; private set; }

        public sealed class Measure
        {
            public readonly int Index;
            private readonly float[] positions;
            private readonly float naturalWidth;
            public float Left { get; private set; }
            public float Width { get; private set; }
            public float Right { get { return Left + Width; } }
            public int TickCount { get { return positions.Length; } }
            public float MinimumWidth { get { return naturalWidth; } }

            internal Measure(int index, float[] offsets, float width)
            {
                Index = index;
                positions = offsets;
                naturalWidth = width;
            }

            internal void Place(float left, float width) { Left = left; Width = width; }
            public float At(int unit) { return Left + positions[unit] * Width / naturalWidth; }

            public int Nearest(float x)
            {
                for (int unit = 0; unit < TickCount - 1; unit++)
                    if (x < (At(unit) + At(unit + 1)) / 2) return unit;
                return TickCount - 1;
            }
        }

        public ScoreLayout(Score score) : this(score, ScorePage.PageWidth, ScorePage.PageHeight)
        {
        }

        internal ScoreLayout(Score score, int width, int height)
        {
            Width = width;
            Height = height;
            staffRight = width - (ScorePage.PageWidth - StaffRight);
            barTicks = score.BarTicks;
            fifths = score.Fifths;
            MeasureLeft = StaffLeft + (fifths == 0 ? 0 : 10 + Math.Abs(fifths) * 11);
            var lanes = score.Parts.SelectMany(p => p.Staves).ToArray();
            rows = lanes.Length;
            systemsPerPage = Math.Max(1, (height - (ScorePage.PageHeight - 864)) / (rows * StaffGap + SystemGap));
            int barsPerSystem = Math.Max(1, (width - 170) / 203);
            measureSystems = new int[score.Bars];
            var measures = new List<Measure>();
            float used = 0;
            for (int bar = 0; bar < score.Bars; bar++)
            {
                var measure = BuildMeasure(lanes, bar);
                if (measures.Count > 0 && (measures.Count == barsPerSystem || used + measure.MinimumWidth > staffRight - MeasureLeft))
                {
                    AddSystem(measures, used, false);
                    measures.Clear();
                    used = 0;
                }
                measures.Add(measure);
                used += measure.MinimumWidth;
            }
            AddSystem(measures, used, true);
        }

        public int PageCount { get { return (systems.Count + systemsPerPage - 1) / systemsPerPage; } }
        public int PageOf(int tick) { return measureSystems[tick / barTicks] / systemsPerPage; }
        public int StaffTop(int system, int row)
        {
            return FirstStaffTop + system * (rows * StaffGap + SystemGap) + row * StaffGap;
        }

        public IEnumerable<Measure[]> OnPage(int page)
        {
            return systems.Skip(page * systemsPerPage).Take(systemsPerPage);
        }

        public Measure MeasureOf(int bar)
        {
            return systems[measureSystems[bar]].First(m => m.Index == bar);
        }

        public PointF Position(Score score, StaffPosition position)
        {
            int bar = position.Tick / barTicks;
            int row = position.Staff;
            for (int part = 0; part < position.Part; part++) row += score.Parts[part].Staves.Count;
            return new PointF(MeasureOf(bar).At(position.Tick % barTicks), StaffTop(measureSystems[bar] % systemsPerPage, row));
        }

        internal static Font TextFont { get { return LyricFont; } }
        internal static readonly Font ChordFont = new Font("Segoe UI Semibold", 14, FontStyle.Regular, GraphicsUnit.Pixel);
        internal static readonly Font DynamicFont = new Font("Georgia", 14, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);

        private Measure BuildMeasure(List<Note>[] lanes, int bar)
        {
            int start = bar * barTicks;
            var halfWidths = new float[barTicks];
            var alterations = new int[barTicks];
            var dots = new int[barTicks];
            var chordHeads = new int[barTicks];
            var links = new List<Tuple<int, float>>[barTicks];
            foreach (var lane in lanes)
            {
                Note previous = null;
                float previousWidth = 0;
                int previousChord = -1, previousDynamic = -1;
                float previousChordWidth = 0, previousDynamicWidth = 0;
                var accidentals = new Dictionary<int, int>();
                foreach (var onset in lane.Where(n => n.Tick >= start && n.Tick < start + barTicks).GroupBy(n => n.Tick))
                {
                    if (onset.Count() < 2) continue;
                    int unit = onset.Key - start;
                    int[] steps = onset.Select(n => n.Step).OrderBy(step => step).ToArray();
                    if (!steps.Zip(steps.Skip(1), (a, b) => b - a).Contains(1)) continue;
                    chordHeads[unit] = 11;
                    if (unit + 1 < barTicks) chordHeads[unit + 1] = 11;
                }
                foreach (var note in lane.Where(n => n.Tick >= start && n.Tick < start + barTicks))
                {
                    int origin = note.Tick - start;
                    int dotCount = Score.DotCount(note.Length);
                    if (dotCount > 0 && origin + 1 < barTicks)
                        dots[origin + 1] = Math.Max(dots[origin + 1], dotCount);
                    if (note.IsRest) continue;
                    int unit = note.Tick - start;
                    int alter = KeySignature.Prior(fifths, accidentals, note.Step);
                    if (note.Alter != alter || note.Courtesy)
                        alterations[unit] = Math.Max(alterations[unit], note.Courtesy || Math.Abs(note.Alter) == 2 ? 35 : 23);
                    accidentals[note.Step] = note.Alter;
                    ReserveMark(note.Chord, ChordFont, unit, halfWidths, links, ref previousChord, ref previousChordWidth);
                    ReserveMark(note.Dynamic, DynamicFont, unit, halfWidths, links, ref previousDynamic, ref previousDynamicWidth);
                    if (note.Lyric == null) continue;
                    float half = WidthOf(note.Lyric) / 2f;
                    halfWidths[unit] = Math.Max(halfWidths[unit], half);
                    if (previous != null)
                    {
                        if (links[unit] == null) links[unit] = new List<Tuple<int, float>>();
                        links[unit].Add(Tuple.Create(previous.Tick - start, previousWidth + half + (previous.HyphenAfter ? 19 : 10)));
                    }
                    previous = note;
                    previousWidth = half;
                }
            }

            var offsets = new float[barTicks];
            float end = 0;
            for (int unit = 0; unit < offsets.Length; unit++)
            {
                float gap = Math.Max(alterations[unit], 11.2f) + dots[unit] * 4.5f + chordHeads[unit];
                float left = unit == 0 ? Math.Max(19, gap) : offsets[unit - 1] + gap;
                left = Math.Max(left, halfWidths[unit] + 8);
                if (links[unit] != null)
                    foreach (var link in links[unit])
                        left = Math.Max(left, offsets[link.Item1] + link.Item2);
                offsets[unit] = left;
                end = Math.Max(end, left + Math.Max(13 + chordHeads[unit], halfWidths[unit] + 8));
            }
            return new Measure(bar, offsets, end);
        }

        private static void ReserveMark(string text, Font font, int unit, float[] widths, List<Tuple<int, float>>[] links,
            ref int previous, ref float previousWidth)
        {
            if (text == null) return;
            float half = TextRenderer.MeasureText(text, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width / 2f;
            widths[unit] = Math.Max(widths[unit], half);
            if (previous >= 0)
            {
                if (links[unit] == null) links[unit] = new List<Tuple<int, float>>();
                links[unit].Add(Tuple.Create(previous, previousWidth + half + 10));
            }
            previous = unit;
            previousWidth = half;
        }

        private int WidthOf(string text)
        {
            int width;
            if (!lyricWidths.TryGetValue(text, out width))
            {
                width = TextRenderer.MeasureText(text, LyricFont, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
                lyricWidths.Add(text, width);
            }
            return width;
        }

        private void AddSystem(List<Measure> measures, float used, bool last)
        {
            float capacity = staffRight - MeasureLeft;
            float target = last && measures.Count == 1 ? Math.Min(capacity, Math.Max(228, used)) : capacity;
            float left = MeasureLeft;
            foreach (var measure in measures)
            {
                float width = used > target ? measure.MinimumWidth * target / used
                    : measure.MinimumWidth + (target - used) / measures.Count;
                measure.Place(left, width);
                left += width;
                measureSystems[measure.Index] = systems.Count;
            }
            systems.Add(measures.ToArray());
        }
    }
}
