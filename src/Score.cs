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
        [DataMember(EmitDefaultValue = false)] public int Voice;
        [DataMember] public int Length;
        [DataMember] public int Step;
        [DataMember] public int Alter;
        [DataMember(EmitDefaultValue = false)] public int TabString;
        [DataMember(EmitDefaultValue = false)] public int TabFret;
        [DataMember(EmitDefaultValue = false)] public bool Courtesy;
        [DataMember(EmitDefaultValue = false)] public string Lyric;
        [DataMember(EmitDefaultValue = false)] public bool HyphenAfter;
        [DataMember(EmitDefaultValue = false)] public string Chord;
        [DataMember(EmitDefaultValue = false)] public string Dynamic;
        [DataMember(EmitDefaultValue = false)] public string Articulation;
        [DataMember(EmitDefaultValue = false)] public string ExpressionText;
        [DataMember(EmitDefaultValue = false)] public string PauseMark;
        [DataMember(EmitDefaultValue = false)] public bool IsRest;
        [DataMember(EmitDefaultValue = false)] public Guid? TiedTo;
        [DataMember(EmitDefaultValue = false)] public List<Guid> SlursTo;

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
        [DataMember(EmitDefaultValue = false)] public int? Volume;
        [DataMember(EmitDefaultValue = false)] public bool Muted;
        [DataMember(EmitDefaultValue = false)] public bool Solo;
        [DataMember(EmitDefaultValue = false)] public int Pan;
        [DataMember] public List<List<Note>> Staves;
    }

    [DataContract]
    public sealed class RehearsalMark
    {
        [DataMember] public int Bar;
        [DataMember] public string Text;
    }

    [DataContract]
    public sealed class Score
    {
        public int BarTicks { get { return Beats * 16 / BeatType; } }
        public int BeatTicks { get { return BeatType == 8 ? 6 : 4; } }
        public static readonly string[] Meters = { "2/4", "3/4", "4/4", "6/8", "9/8", "12/8" };
        [DataMember] public int Beats = 4;
        [DataMember] public int BeatType = 4;

        public static bool SupportsMeter(int beats, int beatType)
        {
            return Meters.Contains(beats + "/" + beatType);
        }

        public void SetMeter(int beats, int beatType)
        {
            if (!SupportsMeter(beats, beatType)) throw new InvalidOperationException("Fórmula de compasso indisponível.");
            int oldSize = BarTicks;
            int newSize = beats * 16 / beatType;
            var notes = Parts.SelectMany(p => p.Staves).SelectMany(lane => lane).ToArray();
            if (notes.Any(note => note.Tick % oldSize + note.Length > newSize))
                throw new InvalidOperationException("Há figuras que ultrapassariam o novo compasso. Ajuste as durações antes de mudar a fórmula.");
            foreach (var note in notes) note.Tick = note.Tick / oldSize * newSize + note.Tick % oldSize;
            Beats = beats;
            BeatType = beatType;
            PruneNoteLinks();
        }
        public const int MaxBars = 64;
        public const int CurrentVersion = 16;
        public const int VoicesPerStaff = 2;
        public const int MaxChordNotes = 8;
        public const int MaxExpressionTextLength = 48;
        public const int MaxRehearsalTextLength = 16;
        private const int MaxProjectBytes = 48 * 1024 * 1024;
        [DataMember(EmitDefaultValue = false)] public int Fifths;
        [DataMember(EmitDefaultValue = false)] public bool MinorKey;

        public void SetKey(int fifths, bool minor)
        {
            if (fifths < -7 || fifths > 7) throw new ArgumentOutOfRangeException("fifths");
            Fifths = fifths;
            MinorKey = minor;
        }

        public int InputAccidental(int part, int staff, int tick, int step)
        {
            int barStart = tick / BarTicks * BarTicks;
            var previous = Parts[part].Staves[staff].FindLast(n =>
                n.Tick >= barStart && n.Tick <= tick && !n.IsRest && n.Step == step);
            return previous == null ? KeySignature.Effective(Fifths, step) : previous.Alter;
        }

        public bool Respelling(int part, int staff, int tick, int direction, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && !n.IsRest && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
            if (note == null || (direction != 1 && direction != -1)) return false;
            int step = note.Step + direction;
            int min = staff == 0 ? 25 : 13;
            if (step < min || step > min + 18) return false;
            int natural = 12 + (step / 7) * 12 + Semitones[step % 7];
            int alter = note.Midi - natural;
            if (alter < -2 || alter > 2 || Parts[part].Staves[staff].Any(n => n != note && n.Voice == note.Voice && n.Tick == tick && n.Step == step)) return false;
            note.Step = step;
            note.Alter = alter;
            PruneStaffLinks(Parts[part].Staves[staff]);
            return true;
        }

        public void SetCourtesy(int part, int staff, int tick, bool enabled, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && !n.IsRest && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
            if (note == null) throw new InvalidOperationException("Selecione uma nota para editar o acidente.");
            note.Courtesy = enabled;
        }
        public static readonly string[] Dynamics = { "ppp", "pp", "p", "mp", "mf", "f", "ff", "fff" };
        public static int Velocity(string dynamic)
        {
            int index = Array.IndexOf(Dynamics, dynamic);
            return index < 0 ? 88 : new[] { 24, 36, 49, 64, 80, 96, 112, 125 }[index];
        }
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
        [DataMember(EmitDefaultValue = false)] public List<RehearsalMark> RehearsalMarks = new List<RehearsalMark>();

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

        public double PlaybackGain(Part part)
        {
            return part.Muted || (Parts.Any(p => p.Solo) && !part.Solo) ? 0 : (part.Volume ?? 100) / 100.0;
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

        public void Put(int part, int staff, int tick, int length, int step, int alter, int voice = 0)
        {
            PutEvent(part, staff, new Note { Tick = tick, Length = length, Step = step, Alter = alter, Voice = voice });
        }

        public Note PutTab(int part, int staff, int tick, int length, int stringNumber, int fret, int voice = 0)
        {
            if (part < 0 || part >= Parts.Count || staff != 0 || !GuitarTab.IsGuitar(Parts[part]))
                throw new InvalidOperationException("A tablatura está disponível na pauta de Violão.");
            int midi, step, alter;
            if (!GuitarTab.TryPitch(stringNumber, fret, out midi) || !GuitarTab.TrySpell(this, midi, out step, out alter))
                throw new InvalidOperationException("Essa casa não cabe na extensão de tablatura suportada.");
            var lane = Parts[part].Staves[staff];
            var onset = lane.Where(note => note.Tick == tick && note.Voice == voice).ToArray();
            var sameString = onset.FirstOrDefault(note => note.TabString == stringNumber);
            if (sameString != null)
            {
                EditNote(part, staff, sameString, length, step, alter);
                sameString.TabString = stringNumber;
                sameString.TabFret = fret;
                return sameString;
            }
            var samePitch = onset.FirstOrDefault(note => !note.IsRest && note.Midi == midi);
            if (samePitch != null)
            {
                samePitch.TabString = stringNumber;
                samePitch.TabFret = fret;
                return samePitch;
            }
            if (onset.Length == 0 || onset.All(note => note.IsRest))
            {
                PutEvent(part, staff, new Note { Tick = tick, Length = length, Step = step, Alter = alter, Voice = voice,
                    TabString = stringNumber, TabFret = fret });
                return lane.Find(note => note.Tick == tick && note.Voice == voice);
            }
            var added = AddChordNote(part, staff, tick, length, step, alter, voice);
            added.TabString = stringNumber;
            added.TabFret = fret;
            return added;
        }

        public void PutRest(int part, int staff, int tick, int length, int voice = 0)
        {
            PutEvent(part, staff, new Note { Tick = tick, Length = length, IsRest = true, Voice = voice });
        }

        public Note AddChordNote(int part, int staff, int tick, int length, int step, int alter, int voice = 0)
        {
            var lane = Parts[part].Staves[staff];
            var onset = lane.FirstOrDefault(n => n.Tick == tick && n.Voice == voice);
            if (onset == null || onset.IsRest)
            {
                Put(part, staff, tick, length, step, alter, voice);
                return lane.First(n => n.Tick == tick && n.Voice == voice);
            }

            var note = new Note { Tick = tick, Length = onset.Length, Step = step, Alter = alter, Voice = voice };
            var cluster = lane.Where(n => n.Tick == tick && n.Voice == voice).ToArray();
            var same = cluster.FirstOrDefault(n => n.Step == step && n.Alter == alter);
            if (same != null) return same;
            if (!ValidNote(note) || cluster.Length >= MaxChordNotes ||
                cluster.Any(n => n.Step == step || n.Midi == note.Midi))
                throw new InvalidOperationException("O acorde aceita até oito alturas distintas, sem grafias duplicadas.");
            lane.Insert(lane.IndexOf(cluster[cluster.Length - 1]) + 1, note);
            return note;
        }

        public void EditNote(int part, int staff, Note note, int length, int step, int alter)
        {
            var lane = Parts[part].Staves[staff];
            var pitch = new Note { Tick = note.Tick, Length = length, Step = step, Alter = alter };
            var cluster = lane.Where(n => n.Tick == note.Tick && n.Voice == note.Voice).ToArray();
            if (note.IsRest || !lane.Contains(note) || !ValidNote(pitch) ||
                cluster.Any(n => n != note && (n.Step == step || n.Midi == pitch.Midi)) ||
                lane.Any(n => n.Voice == note.Voice && n.Tick > note.Tick && n.Tick < note.Tick + length))
                throw new InvalidOperationException("A alteração gera uma altura duplicada ou uma sobreposição rítmica.");
            int previousMidi = note.Midi;
            note.Step = step;
            note.Alter = alter;
            if (note.Midi != previousMidi) { note.TabString = 0; note.TabFret = 0; }
            foreach (var member in cluster) member.Length = length;
            PruneStaffLinks(lane);
        }

        private void PutEvent(int part, int staff, Note note)
        {
            int tick = note.Tick;
            int length = note.Length;
            if (!ValidNote(note) || tick >= Bars * BarTicks)
                throw new InvalidOperationException("A figura precisa caber no compasso e na extensão da pauta.");
            var notes = Parts[part].Staves[staff];
            var onset = notes.Where(n => n.Tick == tick && n.Voice == note.Voice).ToArray();
            var previous = onset.FirstOrDefault(n => !n.IsRest && n.Step == note.Step) ?? onset.FirstOrDefault();
            if (notes.Any(n => n.Voice == note.Voice && n.Tick != tick && n.Tick < tick + length && tick < n.Tick + n.Length))
                throw new InvalidOperationException("A figura se sobrepõe a outra na mesma voz.");
            if (previous != null)
            {
                note.Id = previous.Id;
                if (!note.IsRest)
                {
                    note.Lyric = previous.Lyric;
                    note.HyphenAfter = previous.HyphenAfter;
                    note.Chord = previous.Chord;
                    note.Dynamic = previous.Dynamic;
                    note.Articulation = previous.Articulation;
                    note.ExpressionText = previous.ExpressionText;
                    note.PauseMark = previous.PauseMark;
                    note.Courtesy = previous.Courtesy;
                    note.TiedTo = previous.TiedTo;
                    note.SlursTo = previous.SlursTo;
                    if (note.TabString == 0 && note.Midi == previous.Midi)
                    {
                        note.TabString = previous.TabString;
                        note.TabFret = previous.TabFret;
                    }
                }
                notes.RemoveAll(n => n.Tick == tick && n.Voice == note.Voice);
            }
            notes.Add(note);
            SortByTick(notes);
            PruneStaffLinks(notes);
        }

        public bool ToggleTie(int part, int staff, Guid id)
        {
            var lane = Parts[part].Staves[staff];
            var start = lane.Find(n => n.Id == id && !n.IsRest);
            if (start == null) throw new InvalidOperationException("Selecione a nota que inicia a ligadura.");
            if (start.TiedTo.HasValue)
            {
                start.TiedTo = null;
                return false;
            }
            var next = lane.Find(n => NoteTies.Fits(start, n));
            if (next == null)
                throw new InvalidOperationException("Escreva a continuação com a mesma altura na mesma voz, sem intervalo entre as figuras.");
            start.TiedTo = next.Id;
            return true;
        }

        public bool ToggleSlur(int part, int staff, Guid first, Guid last)
        {
            var lane = Parts[part].Staves[staff];
            var start = lane.Find(note => note.Id == first);
            var end = lane.Find(note => note.Id == last);
            if (start == null || end == null || !NoteSlur.Fits(start, end))
                throw new InvalidOperationException("Escolha uma nota posterior na mesma voz e pauta.");
            if (start.SlursTo != null && start.SlursTo.Remove(last))
            {
                if (start.SlursTo.Count == 0) start.SlursTo = null;
                return false;
            }
            if (start.SlursTo == null) start.SlursTo = new List<Guid>();
            start.SlursTo.Add(last);
            if (NoteSlur.Numbered(Parts[part], BarTicks).Any(slur => slur.Number == 0))
            {
                start.SlursTo.Remove(last);
                if (start.SlursTo.Count == 0) start.SlursTo = null;
                throw new InvalidOperationException("Este trecho já atingiu o limite de ligaduras de expressão simultâneas.");
            }
            return true;
        }

        private static void PruneStaffLinks(List<Note> staff)
        {
            NoteTies.Prune(staff);
            NoteSlur.Prune(staff);
        }

        internal void PruneNoteLinks()
        {
            foreach (var lane in Parts.SelectMany(p => p.Staves)) PruneStaffLinks(lane);
        }

        public void SetExpression(int part, int staff, int tick, string chord, string dynamic, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && !n.IsRest && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
            if (note == null) throw new InvalidOperationException("Selecione uma nota para adicionar cifra ou dinâmica.");
            ChordSymbol symbol = null;
            if (!string.IsNullOrWhiteSpace(chord) && !ChordSymbol.TryParse(chord, out symbol))
                throw new InvalidOperationException("Cifra inválida. Use, por exemplo, Cmaj7, F#m7, Bbadd9 ou G7/B.");
            if (dynamic != null && !Dynamics.Contains(dynamic)) throw new InvalidOperationException("Dinâmica inválida.");
            note.Chord = symbol == null ? null : symbol.Text;
            note.Dynamic = dynamic;
        }

        public void SetArticulation(int part, int staff, int tick, string articulation, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && !n.IsRest && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
            if (note == null) throw new InvalidOperationException("Selecione uma nota para editar a articulação.");
            if (articulation != null && !Articulations.XmlNames.Contains(articulation))
                throw new InvalidOperationException("Articulação inválida.");
            note.Articulation = articulation;
        }

        public void SetExpressionText(int part, int staff, int tick, string text, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && !n.IsRest && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
            if (note == null) throw new InvalidOperationException("Selecione uma nota para adicionar texto expressivo.");
            note.ExpressionText = NormalizeDisplayText(text, MaxExpressionTextLength, "O texto expressivo aceita até 48 caracteres.");
        }

        public void SetPauseMark(int part, int staff, int tick, string mark, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && !n.IsRest && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
            if (note == null) throw new InvalidOperationException("Selecione uma nota para adicionar um sinal de pausa.");
            if (mark != null && !PauseMarks.Names.Contains(mark)) throw new InvalidOperationException("Sinal de pausa inválido.");
            note.PauseMark = mark;
        }

        public void SetRehearsalMark(int bar, string text)
        {
            if (bar < 0 || bar >= Bars) throw new ArgumentOutOfRangeException("bar");
            string clean = NormalizeDisplayText(text, MaxRehearsalTextLength, "A marca de ensaio aceita até 16 caracteres.");
            var existing = RehearsalMarks.Find(mark => mark.Bar == bar);
            if (clean == null)
            {
                if (existing != null) RehearsalMarks.Remove(existing);
                return;
            }
            if (existing == null) RehearsalMarks.Add(new RehearsalMark { Bar = bar, Text = clean });
            else existing.Text = clean;
            RehearsalMarks.Sort((left, right) => left.Bar.CompareTo(right.Bar));
        }

        public void SetLyric(int part, int staff, int tick, string text, bool hyphenAfter, Guid id = default(Guid))
        {
            var note = Parts[part].Staves[staff].Find(n => n.Tick == tick && (id == Guid.Empty ? n.Voice == 0 : n.Id == id));
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

        private static string NormalizeDisplayText(string text, int maximum, string message)
        {
            string clean = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            if (clean == null) return null;
            if (!ValidDisplayText(clean, maximum)) throw new InvalidOperationException(message);
            return clean;
        }

        private static bool ValidDisplayText(string text, int maximum)
        {
            if (text == null) return true;
            if (text.Length == 0 || text.Length > maximum || text.Any(char.IsControl)) return false;
            try { System.Xml.XmlConvert.VerifyXmlChars(text); return true; }
            catch (System.Xml.XmlException) { return false; }
        }

        private bool ValidNote(Note n)
        {
            if (n == null || n.Id == Guid.Empty || n.Tick < 0 || n.Voice < 0 || n.Voice >= VoicesPerStaff ||
                !IsSupportedDuration(n.Length) || n.Tick % BarTicks + n.Length > BarTicks) return false;
            if (n.IsRest) return n.Step == 0 && n.Alter == 0 && n.TabString == 0 && n.TabFret == 0 && n.Lyric == null && !n.HyphenAfter &&
                n.Chord == null && n.Dynamic == null && n.Articulation == null && n.ExpressionText == null && n.PauseMark == null &&
                !n.Courtesy && !n.TiedTo.HasValue && n.SlursTo == null;
            ChordSymbol symbol;
            if ((n.Chord != null && !ChordSymbol.TryParse(n.Chord, out symbol)) ||
                (n.Dynamic != null && !Dynamics.Contains(n.Dynamic)) ||
                (n.Articulation != null && !Articulations.XmlNames.Contains(n.Articulation)) ||
                !ValidDisplayText(n.ExpressionText, MaxExpressionTextLength) ||
                (n.PauseMark != null && !PauseMarks.Names.Contains(n.PauseMark))) return false;
            return (n.TabString == 0 ? n.TabFret == 0 : GuitarTab.ValidPosition(n.TabString, n.TabFret)) &&
                n.Step >= 0 && n.Step <= 69 && n.Alter >= -2 && n.Alter <= 2 && n.Midi >= 0 && n.Midi <= 127;
        }

        public static int RestLength(int tick, int remaining)
        {
            return Durations.First(d => d <= remaining && tick % d == 0);
        }

        public static int RestLength(int localTick, int remaining, int beatSize)
        {
            if (beatSize != 6) return RestLength(localTick, remaining);
            int within = localTick % beatSize;
            int available = Math.Min(remaining, beatSize - within);
            return within == 0 && available == 6 ? 6 : RestLength(within, available);
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
            foreach (var mark in RehearsalMarks.Where(mark => mark.Bar >= bar)) mark.Bar++;
            Bars++;
            PruneNoteLinks();
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
            RehearsalMarks.RemoveAll(mark => mark.Bar == bar);
            foreach (var mark in RehearsalMarks.Where(mark => mark.Bar > bar)) mark.Bar--;
            Bars--;
            PruneNoteLinks();
        }

        public int Transpose(int startBar, int endBar, int? partIndex, int amount, bool chromatic)
        {
            if (startBar < 0 || endBar < startBar || endBar >= Bars ||
                (partIndex.HasValue && (partIndex.Value < 0 || partIndex.Value >= Parts.Count)) ||
                amount < -24 || amount > 24)
                throw new ArgumentOutOfRangeException("Intervalo de transposição inválido.");
            var changes = new List<Tuple<Note, int, int, string, List<Note>>>();
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
                    ChordSymbol chord;
                    string symbol = ChordSymbol.TryParse(note.Chord, out chord) ? chord.Transpose(amount, chromatic) : null;
                    changes.Add(Tuple.Create(note, step, alter, symbol, Parts[p].Staves[staff]));
                }
            }
            if (changes.GroupBy(c => new { c.Item1.Tick, c.Item1.Voice, Lane = c.Item5 })
                .Any(group => group.GroupBy(c => c.Item2).Any(spelling => spelling.Count() > 1)))
                throw new InvalidOperationException("A transposição reúne duas notas do acorde na mesma linha ou espaço.");
            foreach (var change in changes)
            {
                change.Item1.Step = change.Item2;
                change.Item1.Alter = change.Item3;
                change.Item1.Chord = change.Item4;
                change.Item1.TabString = 0;
                change.Item1.TabFret = 0;
            }
            PruneNoteLinks();
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
                var copies = CloneNotes(staff.Where(n => n.Tick >= start && n.Tick < end), BarTicks);
                foreach (var note in staff.Where(n => n.Tick >= end)) note.Tick += BarTicks;
                staff.AddRange(copies);
                SortByTick(staff);
            }
            foreach (var mark in RehearsalMarks.Where(mark => mark.Bar > bar)) mark.Bar++;
            var copiedMark = RehearsalMarks.Find(mark => mark.Bar == bar);
            if (copiedMark != null) RehearsalMarks.Add(new RehearsalMark { Bar = bar + 1, Text = copiedMark.Text });
            RehearsalMarks.Sort((left, right) => left.Bar.CompareTo(right.Bar));
            Bars++;
            PruneNoteLinks();
        }

        private static IEnumerable<Note> CloneNotes(IEnumerable<Note> notes, int delta)
        {
            var source = notes.ToArray();
            var copies = source.ToDictionary(n => n.Id, n => new Note
            {
                Tick = n.Tick + delta, Length = n.Length, Step = n.Step, Alter = n.Alter, TabString = n.TabString, TabFret = n.TabFret, Voice = n.Voice,
                IsRest = n.IsRest, Lyric = n.Lyric, HyphenAfter = n.HyphenAfter, Chord = n.Chord,
                Dynamic = n.Dynamic, Articulation = n.Articulation, ExpressionText = n.ExpressionText, PauseMark = n.PauseMark,
                Courtesy = n.Courtesy, TiedTo = n.TiedTo, SlursTo = n.SlursTo
            });
            foreach (var copy in copies.Values)
            {
                Note target;
                copy.TiedTo = copy.TiedTo.HasValue && copies.TryGetValue(copy.TiedTo.Value, out target)
                    ? target.Id : (Guid?)null;
                if (copy.SlursTo != null)
                {
                    var ends = copy.SlursTo.Where(copies.ContainsKey).Select(id => copies[id].Id).ToList();
                    copy.SlursTo = ends.Count == 0 ? null : ends;
                }
            }
            return source.Select(n => copies[n.Id]);
        }

        private static void SortByTick(List<Note> lane)
        {
            var ordered = lane.OrderBy(n => n.Tick).ToArray();
            lane.Clear();
            lane.AddRange(ordered);
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
            copy.RehearsalMarks.RemoveAll(mark => mark.Bar != bar);
            foreach (var mark in copy.RehearsalMarks) mark.Bar = 0;
            copy.PruneNoteLinks();
            copy.Bars = 1;
            return copy;
        }

        public void PasteBar(int bar, Score copy)
        {
            if (bar < 0 || bar >= Bars) throw new ArgumentOutOfRangeException("bar");
            copy.Validate();
            if (copy.Bars != 1 || copy.Beats != Beats || copy.BeatType != BeatType || !Parts.Select(p => p.Staves.Count).SequenceEqual(copy.Parts.Select(p => p.Staves.Count)))
                throw new InvalidOperationException("O compasso copiado precisa ter o mesmo conjunto de pautas.");
            var sources = copy.Parts.SelectMany(p => p.Staves).ToArray();
            var targets = Parts.SelectMany(p => p.Staves).ToArray();
            int start = bar * BarTicks;
            for (int i = 0; i < targets.Length; i++)
            {
                var staff = targets[i];
                staff.RemoveAll(n => n.Tick >= start && n.Tick < start + BarTicks);
                staff.AddRange(CloneNotes(sources[i], start));
                SortByTick(staff);
                PruneStaffLinks(staff);
            }
            RehearsalMarks.RemoveAll(mark => mark.Bar == bar);
            var copiedMark = copy.RehearsalMarks.Find(mark => mark.Bar == 0);
            if (copiedMark != null) RehearsalMarks.Add(new RehearsalMark { Bar = bar, Text = copiedMark.Text });
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

        public Score EmptyArrangement()
        {
            var copy = Restore(Snapshot());
            copy.Title = "Partitura sem título";
            copy.Author = "";
            foreach (var staff in copy.Parts.SelectMany(part => part.Staves)) staff.Clear();
            copy.RehearsalMarks.Clear();
            return copy;
        }

        public void RenamePart(int index, string name)
        {
            var clean = (name ?? "").Trim();
            if (clean.Length == 0 || clean.Length > 40 || clean.Any(char.IsControl))
                throw new InvalidOperationException("O nome deve conter de 1 a 40 caracteres.");
            Parts[index].Name = clean;
        }

        public Score ExtractBars(int first, int last)
        {
            if (first < 0 || last < first || last >= Bars) throw new ArgumentOutOfRangeException("first");
            var copy = Restore(Snapshot());
            int start = first * BarTicks;
            int end = (last + 1) * BarTicks;
            foreach (var staff in copy.Parts.SelectMany(p => p.Staves))
            {
                for (int voice = 0; voice < VoicesPerStaff; voice++)
                {
                    var prior = staff.LastOrDefault(n => n.Voice == voice && n.Tick < start && n.Dynamic != null);
                    var firstNote = staff.Find(n => n.Voice == voice && n.Tick >= start && n.Tick < end && !n.IsRest);
                    if (firstNote != null && prior != null &&
                        !staff.Any(n => n.Voice == voice && n.Tick == firstNote.Tick && n.Dynamic != null))
                        firstNote.Dynamic = prior.Dynamic;
                }
                staff.RemoveAll(n => n.Tick < start || n.Tick >= end);
                foreach (var note in staff) note.Tick -= start;
            }
            copy.RehearsalMarks.RemoveAll(mark => mark.Bar < first || mark.Bar > last);
            foreach (var mark in copy.RehearsalMarks) mark.Bar -= first;
            copy.PruneNoteLinks();
            copy.Bars = last - first + 1;
            return copy;
        }

        public HashSet<Guid> PrintedAccidentals(IEnumerable<Note> notes)
        {
            var marks = new HashSet<Guid>();
            var prior = new Dictionary<int, int?>();
            foreach (var onset in notes.Where(n => !n.IsRest).GroupBy(n => n.Tick).OrderBy(g => g.Key))
            foreach (var pitches in onset.GroupBy(n => n.Step))
            {
                int? previous;
                if (!prior.TryGetValue(pitches.Key, out previous)) previous = KeySignature.Effective(Fifths, pitches.Key);
                int alter = pitches.First().Alter;
                bool conflicts = pitches.Any(n => n.Alter != alter);
                foreach (var member in pitches)
                    if (conflicts || member.Alter != previous || member.Courtesy) marks.Add(member.Id);
                prior[pitches.Key] = conflicts ? (int?)null : alter;
            }
            return marks;
        }

        public static int BeamGroupEnd(IList<Note> events, int index, int barStart, int beatSize)
        {
            var first = events[index];
            int end = index + 1;
            if (first.IsRest || first.Length > 2) return end;
            int beatEnd = barStart + ((first.Tick - barStart) / beatSize + 1) * beatSize;
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
            if (Version != CurrentVersion || Fifths < -7 || Fifths > 7 || !SupportsMeter(Beats, BeatType) || Bars < 1 || Bars > MaxBars || Tempo < 30 || Tempo > 240 ||
                string.IsNullOrWhiteSpace(Title) || Title.Length > 80 || Author == null || Author.Length > 80 ||
                Parts == null || Parts.Count < 1 || Parts.Count > 4 || RehearsalMarks == null)
                throw new InvalidDataException("Projeto inválido ou versão de arquivo incompatível.");

            var ids = new HashSet<Guid>();
            if (RehearsalMarks.Count > Bars || RehearsalMarks.Any(mark => mark == null || mark.Bar < 0 || mark.Bar >= Bars ||
                !ValidDisplayText(mark.Text, MaxRehearsalTextLength)) || RehearsalMarks.GroupBy(mark => mark.Bar).Any(group => group.Count() > 1))
                throw new InvalidDataException("Marca de ensaio inválida.");
            foreach (var p in Parts)
            {
                if (p == null || p.Pan < -100 || p.Pan > 100 || (p.Volume.HasValue && (p.Volume.Value < 0 || p.Volume.Value > 100)) || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 40 ||
                    (p.Instrument != "Piano" && p.Instrument != "Violão" && p.Instrument != "Voz") ||
                    p.Staves == null || p.Staves.Count != (p.Instrument == "Piano" ? 2 : 1))
                    throw new InvalidDataException("Instrumento ou configuração de pautas inválida.");
                foreach (var lane in p.Staves)
                {
                    if (lane == null || lane.Count > Bars * BarTicks * MaxChordNotes * VoicesPerStaff)
                        throw new InvalidDataException("Pauta inválida.");
                    int lastTick = -1;
                    foreach (var n in lane)
                    {
                        if (!ValidNote(n) || n.Tick < lastTick || n.Tick >= Bars * BarTicks || !ids.Add(n.Id) ||
                            !ValidLyric(n.Lyric) || (n.Lyric == null && n.HyphenAfter))
                            throw new InvalidDataException("Nota ou ritmo inválido no projeto.");
                        lastTick = n.Tick;
                    }
                    if (p.Instrument != "Violão" && lane.Any(n => n.TabString != 0))
                        throw new InvalidDataException("Tablatura só é válida para Violão.");
                    if (lane.Any(n => n.TiedTo.HasValue))
                    {
                        var notes = lane.ToDictionary(n => n.Id);
                        foreach (var source in lane.Where(n => n.TiedTo.HasValue))
                        {
                            Note target;
                            if (!NoteTies.TryTarget(source, notes, out target))
                                throw new InvalidDataException("Ligadura de prolongamento inválida.");
                        }
                    }
                    if (lane.Any(n => n.SlursTo != null))
                    {
                        var notes = lane.ToDictionary(n => n.Id);
                        foreach (var start in lane.Where(n => n.SlursTo != null))
                        {
                            var unique = new HashSet<Guid>();
                            foreach (var id in start.SlursTo)
                            {
                                Note end;
                                if (!unique.Add(id) || !notes.TryGetValue(id, out end) || !NoteSlur.Fits(start, end))
                                    throw new InvalidDataException("Ligadura de expressão inválida.");
                            }
                        }
                    }
                    foreach (var voice in lane.GroupBy(n => n.Voice))
                    {
                        int end = 0;
                        foreach (var onset in voice.GroupBy(n => n.Tick))
                        {
                            var first = onset.First();
                            if (onset.Key < end) throw new InvalidDataException("Figuras sobrepostas na mesma voz.");
                            end = first.Tick + first.Length;
                            if (onset.Count() > 1 && (onset.Count() > MaxChordNotes ||
                                onset.Any(n => n.Length != first.Length || n.IsRest) ||
                                onset.Select(n => n.Step).Distinct().Count() != onset.Count() ||
                                onset.Select(n => n.Midi).Distinct().Count() != onset.Count() ||
                                onset.Where(n => n.TabString != 0).Select(n => n.TabString).Distinct().Count() != onset.Count(n => n.TabString != 0)))
                                throw new InvalidDataException("Acorde inválido no projeto.");
                        }
                    }
                }
                if (NoteSlur.Numbered(p, BarTicks).Any(slur => slur.Number == 0))
                    throw new InvalidDataException("Há ligaduras de expressão demais no mesmo trecho.");
            }
        }

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(Score), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 3600000 });

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
                    if (score.Version >= 1 && score.Version < CurrentVersion)
                    {
                        if (score.Version < 7) { score.Beats = 4; score.BeatType = 4; }
                        if (score.RehearsalMarks == null) score.RehearsalMarks = new List<RehearsalMark>();
                        score.Version = CurrentVersion;
                    }
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
                if (file.Length > MaxProjectBytes) throw new InvalidDataException("O projeto excede 48 MB.");
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

