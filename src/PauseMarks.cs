using System;

namespace ValeScore
{
    internal static class PauseMarks
    {
        internal static readonly string[] Names = { "fermata", "breath-mark", "caesura" };

        internal static double SoundLength(string mark)
        {
            return mark == "fermata" ? 1.35 : 1;
        }
    }
}

