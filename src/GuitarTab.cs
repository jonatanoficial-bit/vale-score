using System;
using System.Linq;

namespace ValeScore
{
    internal sealed class TabTuning
    {
        internal readonly string Instrument, Code, Label, Detail;
        internal readonly int[] Pitches;

        internal TabTuning(string instrument, string code, string label, string detail, params int[] pitches)
        {
            Instrument = instrument;
            Code = code;
            Label = label;
            Detail = detail;
            Pitches = pitches;
        }
    }

    internal static class GuitarTab
    {
        // Mantido para projetos e integrações da primeira versão da TAB.
        internal const int Strings = 6;
        internal const int MaxFret = 24;
        private static readonly string[] PitchNames = { "Dó", "Dó♯", "Ré", "Ré♯", "Mi", "Fá", "Fá♯", "Sol", "Sol♯", "Lá", "Lá♯", "Si" };
        private static readonly TabTuning[] Tunings = {
            new TabTuning("Violão", "standard", "Padrão", "Mi Lá Ré Sol Si Mi", 64, 59, 55, 50, 45, 40),
            new TabTuning("Violão", "drop-d", "Drop D", "Ré Lá Ré Sol Si Mi", 64, 59, 55, 50, 45, 38),
            new TabTuning("Guitarra", "standard", "Padrão", "Mi Lá Ré Sol Si Mi", 64, 59, 55, 50, 45, 40),
            new TabTuning("Guitarra", "drop-d", "Drop D", "Ré Lá Ré Sol Si Mi", 64, 59, 55, 50, 45, 38),
            new TabTuning("Guitarra", "d-standard", "Ré padrão", "Ré Sol Dó Fá Lá Ré", 62, 57, 53, 48, 43, 38),
            new TabTuning("Guitarra", "open-g", "Aberta em Sol", "Ré Sol Ré Sol Si Ré", 62, 59, 55, 50, 43, 38),
            new TabTuning("Baixo", "standard", "Padrão", "Mi Lá Ré Sol", 43, 38, 33, 28),
            new TabTuning("Baixo", "drop-d", "Drop D", "Ré Lá Ré Sol", 43, 38, 33, 26),
            new TabTuning("Baixo", "low-b", "Si grave · 5 cordas", "Si Mi Lá Ré Sol", 43, 38, 33, 28, 23)
        };

        internal static bool IsTabInstrument(Part part)
        {
            return part != null && (part.Instrument == "Violão" || part.Instrument == "Guitarra" || part.Instrument == "Baixo");
        }

        internal static bool IsGuitar(Part part) { return IsTabInstrument(part); }

        internal static string DefaultTuning(string instrument)
        {
            var item = Tunings.FirstOrDefault(tuning => tuning.Instrument == instrument);
            return item == null ? null : item.Code;
        }

        internal static TabTuning[] Options(Part part)
        {
            return part == null ? new TabTuning[0] : Tunings.Where(tuning => tuning.Instrument == part.Instrument).ToArray();
        }

        internal static bool IsValidSetup(Part part)
        {
            if (!IsTabInstrument(part)) return part != null && part.TabTuning == null && part.Capo == 0;
            return Find(part) != null && part.Capo >= 0 && part.Capo <= 12;
        }

        internal static void Normalize(Part part)
        {
            if (part == null) return;
            if (!IsTabInstrument(part)) { part.TabTuning = null; part.Capo = 0; return; }
            if (Find(part) == null) part.TabTuning = DefaultTuning(part.Instrument);
            part.Capo = Math.Max(0, Math.Min(12, part.Capo));
        }

        internal static bool TrySetTuning(Part part, string code, int capo)
        {
            int[] ignored;
            if (part == null || capo < 0 || capo > 12 || !TryPitches(part.Instrument, code, capo, out ignored)) return false;
            part.TabTuning = code;
            part.Capo = capo;
            return true;
        }

        internal static int StringCount(Part part)
        {
            var tuning = Find(part);
            return tuning == null ? Strings : tuning.Pitches.Length;
        }

        internal static bool ValidPosition(Part part, int stringNumber, int fret)
        {
            return stringNumber >= 1 && stringNumber <= StringCount(part) && fret >= 0 && fret <= MaxFret;
        }

        internal static bool ValidPosition(int stringNumber, int fret)
        {
            return ValidPosition(null, stringNumber, fret);
        }

        internal static bool TryPitch(Part part, int stringNumber, int fret, out int midi)
        {
            midi = 0;
            int[] pitches;
            if (!ValidPosition(part, stringNumber, fret) || !TryPitches(part == null ? "Violão" : part.Instrument,
                part == null ? "standard" : part.TabTuning, part == null ? 0 : part.Capo, out pitches)) return false;
            midi = pitches[stringNumber - 1] + fret;
            return midi >= 0 && midi <= 127;
        }

        internal static bool TryPitch(int stringNumber, int fret, out int midi)
        {
            return TryPitch(null, stringNumber, fret, out midi);
        }

        internal static bool TryPosition(Part part, Note note, out int stringNumber, out int fret)
        {
            stringNumber = 0;
            fret = 0;
            if (note == null || note.IsRest) return false;
            if (note.TabString != 0)
            {
                stringNumber = note.TabString;
                fret = note.TabFret;
                return ValidPosition(part, stringNumber, fret);
            }
            int[] pitches;
            if (!TryPitches(part == null ? "Violão" : part.Instrument, part == null ? "standard" : part.TabTuning,
                part == null ? 0 : part.Capo, out pitches)) return false;
            for (int index = 0; index < pitches.Length; index++)
            {
                int candidate = note.Midi - pitches[index];
                if (candidate < 0 || candidate > MaxFret) continue;
                stringNumber = index + 1;
                fret = candidate;
                return true;
            }
            return false;
        }

        internal static bool TryPosition(Note note, out int stringNumber, out int fret)
        {
            return TryPosition(null, note, out stringNumber, out fret);
        }

        internal static bool TrySpell(Score score, int midi, out int step, out int alter)
        {
            return KeySignature.SpellMidi(score.Fifths, midi, 0, 69, out step, out alter);
        }

        internal static string StringLabel(Part part, int stringNumber)
        {
            int midi;
            if (!TryPitch(part, stringNumber, 0, out midi)) return "Corda " + stringNumber;
            return stringNumber + "ª · " + PitchNames[midi % 12];
        }

        internal static string StringLabel(int stringNumber)
        {
            return StringLabel(null, stringNumber);
        }

        internal static string SetupLabel(Part part)
        {
            var tuning = Find(part);
            if (tuning == null) return "";
            return tuning.Label + (part.Capo == 0 ? "" : " · capo " + part.Capo);
        }

        internal static string SetupDetail(Part part)
        {
            var tuning = Find(part);
            if (tuning == null) return "";
            return tuning.Detail + (part.Capo == 0 ? "" : " · capo " + part.Capo);
        }

        internal static bool TryApplyImportedSetup(Part part, int[] openPitches, int capo)
        {
            if (!IsTabInstrument(part) || capo < 0 || capo > 12) return false;
            var tuning = Tunings.FirstOrDefault(item => item.Instrument == part.Instrument &&
                item.Pitches.SequenceEqual(openPitches ?? new int[0]));
            if (tuning == null) return false;
            part.TabTuning = tuning.Code;
            part.Capo = capo;
            return true;
        }

        internal static string InstrumentFromMusicXml(string name, int strings)
        {
            string text = (name ?? "").ToLowerInvariant();
            if (text.Contains("violão") || text.Contains("acoustic")) return "Violão";
            if (text.Contains("baixo") || text.Contains("bass") || strings == 4 || strings == 5) return "Baixo";
            if (text.Contains("guitar") || text.Contains("guitarra")) return "Guitarra";
            if (strings == 6) return "Guitarra";
            return "Voz";
        }

        private static TabTuning Find(Part part)
        {
            string instrument = part == null ? "Violão" : part.Instrument;
            string code = part == null || string.IsNullOrEmpty(part.TabTuning) ? DefaultTuning(instrument) : part.TabTuning;
            return Tunings.FirstOrDefault(tuning => tuning.Instrument == instrument && tuning.Code == code);
        }

        private static bool TryPitches(string instrument, string code, int capo, out int[] pitches)
        {
            pitches = null;
            if (capo < 0 || capo > 12) return false;
            string actualCode = string.IsNullOrEmpty(code) ? DefaultTuning(instrument) : code;
            var tuning = Tunings.FirstOrDefault(item => item.Instrument == instrument && item.Code == actualCode);
            if (tuning == null) return false;
            pitches = tuning.Pitches.Select(pitch => pitch + capo).ToArray();
            return true;
        }
    }
}

