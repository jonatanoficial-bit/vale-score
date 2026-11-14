using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ValeScore
{
    public sealed class MidiImport
    {
        private sealed class Span
        {
            public MidiEvent Start, End;
        }

        public Score Project { get; private set; }
        public int Imported { get; private set; }
        public int Skipped { get; private set; }
        public int Quantized { get; private set; }
        public int Split { get; private set; }
        public string Report { get; private set; }

        public static MidiImport Convert(MidiSequence midi, string title)
        {
            var project = new Score { Title = string.IsNullOrWhiteSpace(title) ? "Partitura MIDI" : title.Trim(),
                Tempo = Math.Max(30, Math.Min(240, (int)Math.Round(60000000.0 / midi.InitialTempo))), Bars = Score.MaxBars };
            if (project.Title.Length > 80) project.Title = project.Title.Substring(0, 80).TrimEnd();
            var result = new MidiImport { Project = project };
            var spans = PairNotes(midi, result);
            int endOfPiece = 0;
            foreach (var group in spans.GroupBy(note => new { note.Start.Track, Channel = note.Start.Status & 15 }))
            {
                var parts = new List<int>();
                foreach (var span in group)
                {
                    double units = span.Start.Tick * 4.0 / midi.Division;
                    double endUnits = span.End.Tick * 4.0 / midi.Division;
                    int limit = Score.MaxBars * Score.BarTicks;
                    if (units >= limit || endUnits > limit + .5 || group.Key.Channel == 9)
                    {
                        result.Skipped++;
                        continue;
                    }
                    int start = (int)Math.Round(units, MidpointRounding.AwayFromZero);
                    int end = Math.Max(start + 1, (int)Math.Round(endUnits, MidpointRounding.AwayFromZero));
                    int octave = span.Start.Data1 / 12 - 1;
                    int tone = span.Start.Data1 % 12;
                    int degree = Array.FindLastIndex(Score.Semitones, n => n <= tone);
                    int step = octave * 7 + degree;
                    int alter = tone - Score.Semitones[degree];
                    if (end > limit || step < 13 || step > 43 ||
                        !Place(project, parts, start, end, step, alter, result))
                    {
                        result.Skipped++;
                        continue;
                    }
                    if (Math.Abs(units - start) > .0001 || Math.Abs(endUnits - end) > .0001) result.Quantized++;
                    result.Imported++;
                    endOfPiece = Math.Max(endOfPiece, end);
                }
            }
            if (result.Imported == 0)
                throw new InvalidDataException("O MIDI não contém notas importáveis nesta prévia. Use Abrir MIDI para ouvir o arquivo original.");
            project.Bars = (endOfPiece + Score.BarTicks - 1) / Score.BarTicks;
            project.Validate();
            var report = new List<string> {
                result.Imported + " notas convertidas em " + project.Parts.Count + " conjunto(s) de pautas.",
                result.Quantized + " notas ajustadas à grade de semicolcheias.",
                result.Split + " notas divididas em figuras. Os segmentos são rearticulados.",
                result.Skipped + " notas omitidas por limites de pauta, extensão, duração, percussão ou pares MIDI incompletos.",
                "As alturas são escritas com sustenidos, quando necessário. Timbres, dinâmica e pedal pertencem ao MIDI original."
            };
            if (midi.HasOtherMeter) report.Add("A métrica do arquivo foi convertida para 4/4.");
            if (midi.HasTempoChanges) report.Add("Foi usado o andamento inicial. Mudanças posteriores ficaram no MIDI original.");
            if (60000000.0 / midi.InitialTempo < 30 || 60000000.0 / midi.InitialTempo > 240)
                report.Add("O andamento inicial foi ajustado à faixa de 30 a 240 BPM.");
            result.Report = string.Join(Environment.NewLine, report);
            return result;
        }

        private static IEnumerable<Span> PairNotes(MidiSequence midi, MidiImport result)
        {
            var active = new Dictionary<int, Queue<MidiEvent>>();
            var spans = new List<Span>();
            foreach (var item in midi.Events)
            {
                int type = item.Status & 240;
                if (type != 0x90 && type != 0x80) continue;
                int key = (item.Track << 11) | ((item.Status & 15) << 7) | item.Data1;
                Queue<MidiEvent> queue;
                if (!active.TryGetValue(key, out queue))
                {
                    queue = new Queue<MidiEvent>();
                    active.Add(key, queue);
                }
                if (type == 0x90 && item.Data2 > 0) queue.Enqueue(item);
                else if (queue.Count > 0) spans.Add(new Span { Start = queue.Dequeue(), End = item });
            }
            result.Skipped += active.Values.Sum(queue => queue.Count);
            return spans.OrderBy(note => note.Start.Tick).ThenBy(note => note.Start.Data1);
        }

        private static bool Place(Score project, List<int> parts, int start, int end, int step, int alter, MidiImport result)
        {
            int[] order = step >= 28 ? new[] { 0, 1 } : new[] { 1, 0 };
            foreach (int part in parts)
            foreach (int staff in order)
            {
                if (!FitsStaff(staff, step)) continue;
                var lane = project.Parts[part].Staves[staff];
                if (lane.Count != 0 && lane[lane.Count - 1].Tick + lane[lane.Count - 1].Length > start) continue;
                Write(project, part, staff, start, end, step, alter, result);
                return true;
            }
            if (project.Parts.Count >= 4) return false;
            project.AddPart("Piano");
            int index = project.Parts.Count - 1;
            parts.Add(index);
            int target = FitsStaff(order[0], step) ? order[0] : order[1];
            Write(project, index, target, start, end, step, alter, result);
            return true;
        }

        private static bool FitsStaff(int staff, int step)
        {
            int min = staff == 0 ? 25 : 13;
            return step >= min && step <= min + 18;
        }

        private static void Write(Score score, int part, int staff, int start, int end, int step, int alter, MidiImport result)
        {
            int count = 0;
            for (int tick = start; tick < end; count++)
            {
                int available = Math.Min(end - tick, Score.BarTicks - tick % Score.BarTicks);
                int length = Score.RestLength(tick, available);
                score.Put(part, staff, tick, length, step, alter);
                tick += length;
            }
            if (count > 1) result.Split++;
        }
    }
}
