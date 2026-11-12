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
        private readonly int rows, systemsPerPage;

        public sealed class Measure
        {
            public readonly int Index;
            private readonly float[] positions;
            private readonly float naturalWidth;
            public float Left { get; private set; }
            public float Width { get; private set; }
            public float Right { get { return Left + Width; } }
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
                for (int unit = 0; unit < 15; unit++)
                    if (x < (At(unit) + At(unit + 1)) / 2) return unit;
                return 15;
            }
        }

        public ScoreLayout(Score score)
        {
            var lanes = score.Parts.SelectMany(p => p.Staves).ToArray();
            rows = lanes.Length;
            systemsPerPage = Math.Max(1, 864 / (rows * StaffGap + SystemGap));
            measureSystems = new int[score.Bars];
            var measures = new List<Measure>();
            float used = 0;
            for (int bar = 0; bar < score.Bars; bar++)
            {
                var measure = BuildMeasure(lanes, bar);
                if (measures.Count > 0 && (measures.Count == 3 || used + measure.MinimumWidth > StaffRight - StaffLeft))
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
        public int PageOf(int tick) { return measureSystems[tick / Score.BarTicks] / systemsPerPage; }
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
            int bar = position.Tick / Score.BarTicks;
            int row = position.Staff;
            for (int part = 0; part < position.Part; part++) row += score.Parts[part].Staves.Count;
            return new PointF(MeasureOf(bar).At(position.Tick % Score.BarTicks), StaffTop(measureSystems[bar] % systemsPerPage, row));
        }

        internal static Font TextFont { get { return LyricFont; } }

        private Measure BuildMeasure(List<Note>[] lanes, int bar)
        {
            int start = bar * Score.BarTicks;
            var halfWidths = new float[Score.BarTicks];
            var links = new List<Tuple<int, float>>[Score.BarTicks];
            foreach (var lane in lanes)
            {
                Note previous = null;
                float previousWidth = 0;
                foreach (var note in lane.Where(n => n.Tick >= start && n.Tick < start + Score.BarTicks))
                {
                    if (note.Lyric == null) continue;
                    int unit = note.Tick - start;
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

            var offsets = new float[Score.BarTicks];
            float end = 0;
            for (int unit = 0; unit < offsets.Length; unit++)
            {
                float left = unit == 0 ? 19 : offsets[unit - 1] + 10;
                left = Math.Max(left, halfWidths[unit] + 8);
                if (links[unit] != null)
                    foreach (var link in links[unit])
                        left = Math.Max(left, offsets[link.Item1] + link.Item2);
                offsets[unit] = left;
                end = Math.Max(end, left + Math.Max(13, halfWidths[unit] + 8));
            }
            return new Measure(bar, offsets, end);
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
            float capacity = StaffRight - StaffLeft;
            float target = last && measures.Count == 1 ? Math.Min(capacity, Math.Max(228, used)) : capacity;
            float left = StaffLeft;
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
