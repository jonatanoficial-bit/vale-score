using System;
using System.Collections.Generic;

namespace ValeScore
{
    public static class KeySignature
    {
        private static readonly int[] Sharps = { 3, 0, 4, 1, 5, 2, 6 };
        private static readonly int[] Flats = { 6, 2, 5, 1, 4, 0, 3 };
        private static readonly string[] Major = { "Dó♭", "Sol♭", "Ré♭", "Lá♭", "Mi♭", "Si♭", "Fá", "Dó", "Sol", "Ré", "Lá", "Mi", "Si", "Fá♯", "Dó♯" };
        private static readonly string[] Minor = { "Lá♭", "Mi♭", "Si♭", "Fá", "Dó", "Sol", "Ré", "Lá", "Mi", "Si", "Fá♯", "Dó♯", "Sol♯", "Ré♯", "Lá♯" };

        public static string Name(int fifths, bool minor)
        {
            return (minor ? Minor : Major)[fifths + 7] + (minor ? " menor" : " maior");
        }

        public static int Effective(int fifths, int step)
        {
            int index = Array.IndexOf(fifths < 0 ? Flats : Sharps, step % 7);
            return index >= 0 && index < Math.Abs(fifths) ? Math.Sign(fifths) : 0;
        }

        public static int Prior(int fifths, IDictionary<int, int> state, int step)
        {
            int alter;
            return state.TryGetValue(step, out alter) ? alter : Effective(fifths, step);
        }

        public static string AccidentalName(int alter)
        {
            return new[] { "flat-flat", "flat", "natural", "sharp", "double-sharp" }[alter + 2];
        }

        public static bool SpellMidi(int fifths, int midi, int min, int max, out int step, out int alter)
        {
            int best = int.MaxValue;
            step = -1;
            alter = 0;
            for (int candidate = min; candidate <= max; candidate++)
            {
                int accidental = midi - (12 + candidate / 7 * 12 + Score.Semitones[candidate % 7]);
                if (Math.Abs(accidental) > 2) continue;
                int cost = accidental == Effective(fifths, candidate) ? 0 : 10;
                cost += Math.Abs(accidental) * 2;
                if (accidental != 0 && Math.Sign(accidental) != (fifths < 0 ? -1 : 1)) cost++;
                if (cost >= best) continue;
                best = cost;
                step = candidate;
                alter = accidental;
            }
            return step >= 0;
        }

        internal static int StaffPosition(int fifths, int staff, int index)
        {
            if (fifths > 0)
                return (staff == 0 ? new[] { 8, 5, 9, 6, 3, 7, 4 } : new[] { 6, 3, 7, 4, 1, 5, 2 })[index];
            return (staff == 0 ? new[] { 4, 7, 3, 6, 2, 5, 1 } : new[] { 2, 5, 1, 4, 0, 3, -1 })[index];
        }
    }
}
