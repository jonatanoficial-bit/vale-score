using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ValeScore
{
    [DataContract]
    public sealed class Note
    {
        [DataMember] public Guid Id = Guid.NewGuid();
        [DataMember] public int Tick;
        [DataMember] public int Length;
        [DataMember] public int Step;
        [DataMember] public int Alter;
        [DataMember(EmitDefaultValue = false)] public string Lyric;
        [DataMember(EmitDefaultValue = false)] public bool HyphenAfter;
        [DataMember(EmitDefaultValue = false)] public bool IsRest;

        public int Midi
        {
            get { return 12 + (Step / 7) * 12 + Score.Semitones[Step % 7] + Alter; }
        }
    }

    [DataContract]
    public sealed class Part
    {
        [DataMember] public string Name;
        [DataMember] public string Instrument;
        [DataMember] public List<List<Note>> Staves;
    }

    [DataContract]
    public sealed class Score
    {
        public const int BarTicks = 16;
        public const int MaxBars = 64;
        public const int CurrentVersion = 4;
        public const int MaxLyricLength = 24;
        public static readonly int[] Durations = { 16, 8, 4, 2, 1 };
        public static readonly string[] Steps = { "C", "D", "E", "F", "G", "A", "B" };
        public static readonly string[] Names = { "Dó", "Ré", "Mi", "Fá", "Sol", "Lá", "Si" };
        public static readonly int[] Semitones = { 0, 2, 4, 5, 7, 9, 11 };
        [DataMember] public int Version = CurrentVersion;
        [DataMember] public string Title = "Partitura sem título";
        [DataMember] public string Author = "";
        [DataMember] public int Tempo = 96;
        [DataMember] public int Bars = 4;
        [DataMember] public List<Part> Parts = new List<Part>();

        public static Score New(bool example)
        {
            var score = new Score();
            score.AddPart("Piano");
            if (!example) return score;

            score.Title = "Prelúdio em dó";
            score.Author = "Partitura de exemplo";
            int[] melody = { 28, 30, 32, 34, 33, 32, 30, 29, 30, 32, 35, 34, 32, 29, 28 };
            for (int i = 0; i < melody.Length; i++)
                score.Put(0, 0, i * 4, i == 14 ? 8 : 4, melody[i], 0);
            int[] bass = { 21, 25, 24, 22, 23, 27, 25, 21 };
            for (int i = 0; i < bass.Length; i++)
                score.Put(0, 1, i * 8, 8, bass[i], 0);
            string[] lyrics = { "O", "di", "a", "nas", "ce", "so", "bre", "o", "mar", "e", "a", "luz", "vem", "bri", "lhar" };
            int[] joins = { 1, 3, 5, 13 };
            for (int i = 0; i < lyrics.Length; i++)
                score.SetLyric(0, 0, i * 4, lyrics[i], joins.Contains(i));
            score.Put(0, 0, 40, 2, melody[10], 0);
            score.Put(0, 0, 42, 2, 36, 0);
            score.Put(0, 1, 40, 4, bass[5], 0);
            score.PutRest(0, 1, 44, 4);
            return score;
        }

        public void AddPart(string instrument)
        {
            if (Parts.Count >= 4) throw new InvalidOperationException("A prévia suporta até quatro instrumentos.");
            var part = new Part { Instrument = instrument, Name = instrument, Staves = new List<List<Note>>() };
            int count = Parts.Count(p => p.Instrument == instrument);
            if (count > 0) part.Name += " " + (count + 1);
            part.Staves.Add(new List<Note>());
            if (instrument == "Piano") part.Staves.Add(new List<Note>());
            Parts.Add(part);
        }

        public void Put(int part, int staff, int tick, int length, int step, int alter)
        {
            PutEvent(part, staff, new Note { Tick = tick, Length = length, Step = step, Alter = alter });
        }

        public void PutRest(int part, int staff, int tick, int length)
        {
            PutEvent(part, staff, new Note { Tick = tick, Length = length, IsRest = true });
        }

        private void PutEvent(int part, int staff, Note note)
        {
            int tick = note.Tick;
            int length = note.Length;
            if (!ValidNote(note) || tick >= Bars * BarTicks)
                throw new InvalidOperationException("A figura precisa caber no compasso e na extensão da pauta.");
            var notes = Parts[part].Staves[staff];
            var previous = notes.FirstOrDefault(n => n.Tick == tick);
            if (notes.Any(n => n != previous && n.Tick < tick + length && tick < n.Tick + n.Length))
                throw new InvalidOperationException("A figura se sobrepõe a outra nesta pauta.");
            if (previous != null)
            {
                note.Id = previous.Id;
                if (!note.IsRest)
                {
                    note.Lyric = previous.Lyric;
                    note.HyphenAfter = previous.HyphenAfter;
                }
                notes.Remove(previous);
            }
            notes.Add(note);
            notes.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        }

        public void SetLyric(int part, int staff, int tick, string text, bool hyphenAfter)
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick);
            if (note == null || note.IsRest) throw new InvalidOperationException("Selecione uma nota para escrever a letra.");
            string lyric = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            if (!ValidLyric(lyric))
                throw new InvalidOperationException("Use até 24 caracteres por nota. Espaço e hífen avançam na partitura.");
            note.Lyric = lyric;
            note.HyphenAfter = lyric != null && hyphenAfter;
        }

        private static bool ValidLyric(string text)
        {
            if (text == null) return true;
            if (text.Length == 0 || text.Length > MaxLyricLength || text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                return false;
            try { System.Xml.XmlConvert.VerifyXmlChars(text); return true; }
            catch (System.Xml.XmlException) { return false; }
        }

        private static bool ValidNote(Note n)
        {
            if (n == null || n.Id == Guid.Empty || n.Tick < 0 ||
                !IsSupportedDuration(n.Length) || n.Tick % BarTicks + n.Length > BarTicks) return false;
            if (n.IsRest) return n.Step == 0 && n.Alter == 0 && n.Lyric == null && !n.HyphenAfter;
            return n.Step >= 0 && n.Step <= 69 && n.Alter >= -1 && n.Alter <= 1 && n.Midi >= 0 && n.Midi <= 127;
        }

        public static int RestLength(int tick, int remaining)
        {
            return Durations.First(d => d <= remaining && tick % d == 0);
        }

        public static int BaseDuration(int length)
        {
            if (length == 3 || length == 6 || length == 12) return length * 2 / 3;
            if (length == 7 || length == 14) return length * 4 / 7;
            return length;
        }

        public static int DotCount(int length)
        {
            int basis = BaseDuration(length);
            return length == basis ? 0 : length * 2 == basis * 3 ? 1 : 2;
        }

        public static bool IsSupportedDuration(int length)
        {
            return Durations.Contains(BaseDuration(length));
        }

        public void InsertBar(int bar)
        {
            if (bar < 0 || bar > Bars) throw new ArgumentOutOfRangeException("bar");
            if (Bars >= MaxBars) throw new InvalidOperationException("Limite de compassos atingido.");
            foreach (var note in Parts.SelectMany(p => p.Staves).SelectMany(staff => staff))
                if (note.Tick >= bar * BarTicks) note.Tick += BarTicks;
            Bars++;
        }

        public void RemoveBar(int bar)
        {
            if (bar < 0 || bar >= Bars) throw new ArgumentOutOfRangeException("bar");
            if (Bars == 1) throw new InvalidOperationException("A partitura precisa ter pelo menos um compasso.");
            int end = (bar + 1) * BarTicks;
            foreach (var staff in Parts.SelectMany(p => p.Staves))
            {
                staff.RemoveAll(note => note.Tick >= end - BarTicks && note.Tick < end);
                foreach (var note in staff)
                    if (note.Tick >= end) note.Tick -= BarTicks;
            }
            Bars--;
        }

        public int Transpose(int startBar, int endBar, int? partIndex, int amount, bool chromatic)
        {
            if (startBar < 0 || endBar < startBar || endBar >= Bars ||
                (partIndex.HasValue && (partIndex.Value < 0 || partIndex.Value >= Parts.Count)) ||
                amount < -24 || amount > 24)
                throw new ArgumentOutOfRangeException("Intervalo de transposição inválido.");
            var changes = new List<Tuple<Note, int, int>>();
            for (int p = 0; p < Parts.Count; p++)
            {
                if (partIndex.HasValue && p != partIndex.Value) continue;
                for (int staff = 0; staff < Parts[p].Staves.Count; staff++)
                foreach (var note in Parts[p].Staves[staff])
                {
                    if (note.IsRest || note.Tick < startBar * BarTicks || note.Tick >= (endBar + 1) * BarTicks) continue;
                    int step = note.Step + amount, alter = note.Alter;
                    if (chromatic && amount != 0)
                    {
                        int pitch = note.Midi + amount;
                        if (pitch < 12 || pitch > 127) throw new InvalidOperationException("A transposição excede os limites de altura.");
                        int tone = pitch % 12;
                        int degree = amount < 0
                            ? Array.FindIndex(Semitones, value => value >= tone)
                            : Array.FindLastIndex(Semitones, value => value <= tone);
                        if (degree < 0) { step = (pitch / 12) * 7; alter = -1; }
                        else
                        {
                            step = (pitch / 12 - 1) * 7 + degree;
                            alter = tone - Semitones[degree];
                        }
                    }
                    int min = staff == 0 ? 25 : 13;
                    if (step < min || step > min + 18)
                        throw new InvalidOperationException("Uma nota sairia da extensão editável da pauta. Escolha um intervalo menor.");
                    changes.Add(Tuple.Create(note, step, alter));
                }
            }
            foreach (var change in changes) { change.Item1.Step = change.Item2; change.Item1.Alter = change.Item3; }
            return changes.Count;
        }

        public void DuplicateBar(int bar)
        {
            if (bar < 0 || bar >= Bars) throw new ArgumentOutOfRangeException("bar");
            if (Bars == MaxBars) throw new InvalidOperationException("Limite de compassos atingido.");
            int start = bar * BarTicks;
            int end = start + BarTicks;
            foreach (var staff in Parts.SelectMany(p => p.Staves))
            {
                var copies = staff.Where(n => n.Tick >= start && n.Tick < end).Select(n => new Note
                {
                    Tick = n.Tick + BarTicks, Length = n.Length, Step = n.Step, Alter = n.Alter,
                    IsRest = n.IsRest, Lyric = n.Lyric, HyphenAfter = n.HyphenAfter
                }).ToArray();
                foreach (var note in staff.Where(n => n.Tick >= end)) note.Tick += BarTicks;
                staff.AddRange(copies);
                staff.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            }
            Bars++;
        }

        public Score CopyBar(int bar)
        {
            if (bar < 0 || bar >= Bars) throw new ArgumentOutOfRangeException("bar");
            var copy = Restore(Snapshot());
            int start = bar * BarTicks;
            foreach (var staff in copy.Parts.SelectMany(p => p.Staves))
            {
                staff.RemoveAll(n => n.Tick < start || n.Tick >= start + BarTicks);
                foreach (var note in staff) note.Tick -= start;
            }
            copy.Bars = 1;
            return copy;
        }

        public void PasteBar(int bar, Score copy)
        {
            if (bar < 0 || bar >= Bars) throw new ArgumentOutOfRangeException("bar");
            copy.Validate();
            if (copy.Bars != 1 || !Parts.Select(p => p.Staves.Count).SequenceEqual(copy.Parts.Select(p => p.Staves.Count)))
                throw new InvalidOperationException("O compasso copiado precisa ter o mesmo conjunto de pautas.");
            var sources = copy.Parts.SelectMany(p => p.Staves).ToArray();
            var targets = Parts.SelectMany(p => p.Staves).ToArray();
            int start = bar * BarTicks;
            for (int i = 0; i < targets.Length; i++)
            {
                var staff = targets[i];
                staff.RemoveAll(n => n.Tick >= start && n.Tick < start + BarTicks);
                staff.AddRange(sources[i].Select(n => new Note
                {
                    Tick = start + n.Tick, Length = n.Length, Step = n.Step, Alter = n.Alter,
                    IsRest = n.IsRest, Lyric = n.Lyric, HyphenAfter = n.HyphenAfter
                }));
                staff.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            }
        }

        public Score ExtractPart(int index)
        {
            if (index < 0 || index >= Parts.Count) throw new ArgumentOutOfRangeException("index");
            var copy = Restore(Snapshot());
            var part = copy.Parts[index];
            copy.Parts.Clear();
            copy.Parts.Add(part);
            return copy;
        }

        public static int BeamGroupEnd(IList<Note> events, int index)
        {
            var first = events[index];
            int end = index + 1;
            if (first.IsRest || first.Length > 2) return end;
            int beatEnd = (first.Tick / 4 + 1) * 4;
            while (end < events.Count)
            {
                var next = events[end];
                if (next.IsRest || next.Length != first.Length || next.Tick + next.Length > beatEnd ||
                    next.Tick != events[end - 1].Tick + first.Length) break;
                end++;
            }
            return end;
        }

        public void Validate()
        {
            if (Version != CurrentVersion || Bars < 1 || Bars > MaxBars || Tempo < 30 || Tempo > 240 ||
                string.IsNullOrWhiteSpace(Title) || Title.Length > 80 || Author == null || Author.Length > 80 ||
                Parts == null || Parts.Count < 1 || Parts.Count > 4)
                throw new InvalidDataException("Projeto inválido ou versão de arquivo incompatível.");

            var ids = new HashSet<Guid>();
            foreach (var p in Parts)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 40 ||
                    (p.Instrument != "Piano" && p.Instrument != "Violão" && p.Instrument != "Voz") ||
                    p.Staves == null || p.Staves.Count != (p.Instrument == "Piano" ? 2 : 1))
                    throw new InvalidDataException("Instrumento ou configuração de pautas inválida.");
                foreach (var lane in p.Staves)
                {
                    int end = 0;
                    if (lane == null || lane.Count > Bars * BarTicks) throw new InvalidDataException("Pauta inválida.");
                    foreach (var n in lane)
                    {
                        if (!ValidNote(n) || n.Tick < end || n.Tick >= Bars * BarTicks ||
                            !ids.Add(n.Id) || !ValidLyric(n.Lyric) || (n.Lyric == null && n.HyphenAfter))
                            throw new InvalidDataException("Nota ou ritmo inválido no projeto.");
                        end = n.Tick + n.Length;
                    }
                }
            }
        }

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(Score), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 70000 });

        public byte[] Snapshot()
        {
            using (var stream = new MemoryStream())
            {
                Serializer.WriteObject(stream, this);
                return stream.ToArray();
            }
        }

        public static Score Restore(byte[] data)
        {
            try
            {
                using (var stream = new MemoryStream(data))
                {
                    var score = (Score)Serializer.ReadObject(stream);
                    if (score == null) throw new InvalidDataException("Projeto vazio.");
                    if (score.Version >= 1 && score.Version <= 3) score.Version = CurrentVersion;
                    score.Validate();
                    return score;
                }
            }
            catch (SerializationException ex) { throw new InvalidDataException("O arquivo .valescore está corrompido.", ex); }
            catch (System.Xml.XmlException ex) { throw new InvalidDataException("O arquivo .valescore está corrompido.", ex); }
        }

        public static Score Open(string path)
        {
            using (var file = File.OpenRead(path))
            {
                if (file.Length > 2 * 1024 * 1024) throw new InvalidDataException("O projeto excede 2 MB.");
                var data = new byte[(int)file.Length];
                int read = 0;
                while (read < data.Length)
                {
                    int count = file.Read(data, read, data.Length - read);
                    if (count == 0) throw new EndOfStreamException();
                    read += count;
                }
                return Restore(data);
            }
        }

        public void Save(string path)
        {
            Validate();
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew))
                {
                    var data = Snapshot();
                    file.Write(data, 0, data.Length);
                    file.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
