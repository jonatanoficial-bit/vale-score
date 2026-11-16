using System;

namespace ValeScore
{
    public sealed class PlaybackRange
    {
        public int FirstTick { get; private set; }
        public int TickCount { get; private set; }
        public double TickSeconds { get; private set; }
        public bool Loop { get; private set; }
        public double Duration { get { return TickCount * TickSeconds; } }

        public PlaybackRange(Score score, int firstBar, int lastBar, double speed, bool loop)
        {
            if (firstBar < 0 || lastBar < firstBar || lastBar >= score.Bars)
                throw new ArgumentOutOfRangeException("firstBar");
            if (double.IsNaN(speed) || speed < .5 || speed > 1.5)
                throw new ArgumentOutOfRangeException("speed");
            FirstTick = firstBar * score.BarTicks;
            TickCount = (lastBar - firstBar + 1) * score.BarTicks;
            TickSeconds = 15.0 / (score.Tempo * speed);
            Loop = loop;
        }

        public double Position(double elapsed)
        {
            elapsed = Math.Max(0, elapsed);
            return Loop ? elapsed % Duration : Math.Min(elapsed, Duration);
        }

        public int TickAt(double elapsed)
        {
            return FirstTick + Math.Min(TickCount - 1, (int)(Position(elapsed) / TickSeconds));
        }
    }
}
