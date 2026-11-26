using System;

namespace ValeScore
{
    internal static class GuitarTab
    {
        internal const int Strings = 6;
        internal const int MaxFret = 24;
        // Corda 1 (mi agudo) até a 6 (mi grave), em MIDI.
        private static readonly int[] Tuning = { 64, 59, 55, 50, 45, 40 };

        internal static bool IsGuitar(Part part) { return part != null && part.Instrument == "Violão"; }

        internal static bool ValidPosition(int stringNumber, int fret)
        {
            return stringNumber >= 1 && stringNumber <= Strings && fret >= 0 && fret <= MaxFret;
        }

        internal static bool TryPitch(int stringNumber, int fret, out int midi)
        {
            midi = 0;
            if (!ValidPosition(stringNumber, fret)) return false;
            midi = Tuning[stringNumber - 1] + fret;
            return true;
        }

        internal static bool TryPosition(Note note, out int stringNumber, out int fret)
        {
            stringNumber = 0;
            fret = 0;
            if (note == null || note.IsRest) return false;
            if (note.TabString != 0)
            {
                stringNumber = note.TabString;
                fret = note.TabFret;
                return ValidPosition(stringNumber, fret);
            }
            for (int index = 0; index < Tuning.Length; index++)
            {
                int candidate = note.Midi - Tuning[index];
                if (candidate < 0 || candidate > MaxFret) continue;
                stringNumber = index + 1;
                fret = candidate;
                return true;
            }
            return false;
        }

        internal static bool TrySpell(Score score, int midi, out int step, out int alter)
        {
            return KeySignature.SpellMidi(score.Fifths, midi, 0, 69, out step, out alter);
        }

        internal static string StringLabel(int stringNumber)
        {
            return new[] { "1ª · Mi", "2ª · Si", "3ª · Sol", "4ª · Ré", "5ª · Lá", "6ª · Mi" }[stringNumber - 1];
        }
    }
}

