using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ValeScore
{
    public static class Output
    {
        public static void PagePng(Score score, ScoreLayout layout, int page, Stream target)
        {
            if (page < 0 || page >= layout.PageCount) throw new ArgumentOutOfRangeException("page");
            using (var image = new System.Drawing.Bitmap(ScorePage.PageWidth * 2, ScorePage.PageHeight * 2))
            {
                using (var graphics = System.Drawing.Graphics.FromImage(image))
                {
                    graphics.Clear(System.Drawing.Color.White);
                    graphics.ScaleTransform(2, 2);
                    ScorePage.Draw(graphics, score, layout, page, null, -1, null);
                }
                image.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public static void CompressedMusicXml(Score score, Stream target)
        {
            using (var archive = new System.IO.Compression.ZipArchive(target, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                using (var mime = new StreamWriter(archive.CreateEntry("mimetype", System.IO.Compression.CompressionLevel.NoCompression).Open(), new UTF8Encoding(false)))
                    mime.Write("application/vnd.recordare.musicxml");
                using (var manifest = new StreamWriter(archive.CreateEntry("META-INF/container.xml").Open(), new UTF8Encoding(false)))
                    manifest.Write("<container><rootfiles><rootfile full-path=\"score.musicxml\" media-type=\"application/vnd.recordare.musicxml+xml\"/></rootfiles></container>");
                using (var document = archive.CreateEntry("score.musicxml").Open()) MusicXml(score, document);
            }
        }

        public const int SampleRate = 22050;
        public static MemoryStream Wave(Score score)
        {
            return Wave(score, false);
        }

        public static MemoryStream Wave(Score score, bool metronome, double speed = 1)
        {
            score.Validate();
            if (double.IsNaN(speed) || speed < .5 || speed > 1.5) throw new ArgumentOutOfRangeException("speed");
            double tick = SampleRate * 15.0 / (score.Tempo * speed);
            int size = (int)Math.Ceiling(score.Bars * score.BarTicks * tick);
            var samples = new float[size];
            bool stereo = score.Parts.Any(p => p.Pan != 0);
            var right = stereo ? new float[size] : null;
            double gain = 0.68 / Math.Sqrt(score.Parts.Sum(p => p.Staves.Count));
            foreach (var part in score.Parts)
            {
                double partGain = score.PlaybackGain(part);
                if (partGain == 0) continue;
                var instrumentSamples = stereo ? new float[size] : samples;
                double decay = part.Instrument == "Voz" ? 0.8 : part.Instrument == "Violão" ? 5.0 : 2.8;
                foreach (var staff in part.Staves)
                {
                    var lengths = NoteTies.AttackLengths(staff);
                    var velocities = new[] { 88, 88 };
                    foreach (var onset in staff.Where(n => !n.IsRest).GroupBy(n => new { n.Tick, n.Voice }))
                    {
                        string marking = onset.Select(n => n.Dynamic).FirstOrDefault(value => value != null);
                        if (marking != null) velocities[onset.Key.Voice] = Score.Velocity(marking);
                        double headroom = Math.Sqrt(onset.Count());
                        foreach (var note in onset)
                        {
                            double length;
                            if (!lengths.TryGetValue(note.Id, out length)) continue;
                            int start = (int)Math.Round(note.Tick * tick);
                            int count = (int)Math.Round((note.Tick + length) * tick) - start;
                            AddTone(instrumentSamples, start, count, note.Midi, gain * partGain * Articulations.Attack(note.Articulation, velocities[note.Voice]) / (88 * headroom), decay);
                        }
                    }
                }
                if (stereo)
                {
                    double leftGain = Math.Min(1, 1 - part.Pan / 100.0);
                    double rightGain = Math.Min(1, 1 + part.Pan / 100.0);
                    for (int i = 0; i < size; i++)
                    {
                        samples[i] += (float)(instrumentSamples[i] * leftGain);
                        right[i] += (float)(instrumentSamples[i] * rightGain);
                    }
                }
            }
            var beforeClick = stereo && metronome ? (float[])samples.Clone() : null;
            if (metronome)
            {
                int total = score.Bars * score.BarTicks;
                for (int beat = 0; beat < total; beat += score.BeatTicks)
                {
                    int start = (int)Math.Round(beat * tick);
                    int count = Math.Min(samples.Length - start, SampleRate / 26);
                    bool downbeat = beat % score.BarTicks == 0;
                    double frequency = downbeat ? 1480 : 990;
                    for (int i = 0; i < count; i++)
                        samples[start + i] += (float)(.17 * Math.Sin(2 * Math.PI * frequency * i / SampleRate) *
                            Math.Min(1, i / 16.0) * Math.Pow(1 - i / (double)count, 2));
                }
            }
            if (beforeClick != null)
                for (int i = 0; i < size; i++) right[i] += samples[i] - beforeClick[i];
            if (!stereo) return WriteWave(samples);
            var interleaved = new float[size * 2];
            for (int i = 0; i < size; i++)
            {
                interleaved[i * 2] = samples[i];
                interleaved[i * 2 + 1] = right[i];
            }
            return WriteWave(interleaved, 2);
        }

        private sealed class Voice
        {
            public int Pitch, Velocity;
            public double Start;
        }

        public static MemoryStream MidiWave(MidiSequence sequence, CancellationToken cancellation)
        {
            if (sequence.Duration > 600)
                throw new InvalidOperationException("O sintetizador interno toca arquivos MIDI de até 10 minutos. Selecione uma saída MIDI para arquivos maiores.");
            var samples = new float[Math.Max(1, (int)Math.Ceiling(sequence.Duration * SampleRate))];
            var notes = new Dictionary<int, Voice>();
            var held = Enumerable.Range(0, 16).Select(i => new List<Voice>()).ToArray();
            var pedal = new bool[16];
            int work = 0;
            Action<Voice, double> render = (voice, end) =>
            {
                cancellation.ThrowIfCancellationRequested();
                int start = (int)Math.Round(voice.Start * SampleRate);
                int count = Math.Min(samples.Length, (int)Math.Round(end * SampleRate)) - start;
                if (count <= 0) return;
                if (count > 80000000 - work) throw new InvalidOperationException("O MIDI é denso demais para o sintetizador interno. Use uma saída MIDI.");
                work += count;
                AddTone(samples, start, count, voice.Pitch, .19 * voice.Velocity / 127, 1.7);
            };
            foreach (var item in sequence.Events)
            {
                cancellation.ThrowIfCancellationRequested();
                int channel = item.Status & 15;
                int kind = item.Status & 240;
                int key = channel * 128 + item.Data1;
                if (kind == 0xB0 && (item.Data1 == 64 || item.Data1 == 120 || item.Data1 == 123))
                {
                    bool sustain = item.Data1 == 64 && item.Data2 >= 64;
                    if (!sustain)
                    {
                        foreach (var voice in held[channel]) render(voice, item.Seconds);
                        held[channel].Clear();
                    }
                    if (item.Data1 != 64)
                    {
                        foreach (int active in notes.Keys.Where(k => k / 128 == channel).ToArray())
                        {
                            render(notes[active], item.Seconds);
                            notes.Remove(active);
                        }
                    }
                    pedal[channel] = sustain;
                    continue;
                }
                if (kind != 0x80 && kind != 0x90) continue;
                Voice current;
                if (notes.TryGetValue(key, out current))
                {
                    if (pedal[channel] && (kind == 0x80 || item.Data2 == 0)) held[channel].Add(current);
                    else render(current, item.Seconds);
                    notes.Remove(key);
                }
                if (kind == 0x90 && item.Data2 > 0)
                    notes[key] = new Voice { Pitch = item.Data1, Velocity = item.Data2, Start = item.Seconds };
            }
            foreach (var voice in notes.Values.Concat(held.SelectMany(list => list))) render(voice, sequence.Duration);
            return WriteWave(samples);
        }

        private static void AddTone(float[] samples, int start, int count, int midi, double gain, double decay)
        {
            double pitch = 2 * Math.PI * 440 * Math.Pow(2, (midi - 69) / 12.0) / SampleRate;
            for (int i = 0; i < count; i++)
            {
                double angle = pitch * i;
                double envelope = Math.Min(1, Math.Min(i / 165.0, (count - 1 - i) / 600.0));
                double tone = Math.Sin(angle) + 0.20 * Math.Sin(angle * 2) + 0.06 * Math.Sin(angle * 3);
                samples[start + i] += (float)(gain * envelope * tone * Math.Exp(-decay * i / SampleRate));
            }
        }

        private static MemoryStream WriteWave(float[] samples, int channels = 1)
        {
            int size = samples.Length;
            var stream = new MemoryStream(44 + size * 2);
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + size * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2 * channels);
                writer.Write((short)(2 * channels));
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(size * 2);
                foreach (float sample in samples)
                    writer.Write((short)(Math.Max(-1, Math.Min(1, sample)) * 32767));
            }
            stream.Position = 0;
            return stream;
        }

        public static void MusicXml(Score score, Stream stream)
        {
            score.Validate();
            using (var xml = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("score-partwise");
                xml.WriteAttributeString("version", "4.0");
                xml.WriteElementString("movement-title", score.Title);
                if (score.Author.Length > 0)
                {
                    xml.WriteStartElement("identification");
                    xml.WriteStartElement("creator");
                    xml.WriteAttributeString("type", "composer");
                    xml.WriteString(score.Author);
                    xml.WriteEndElement();
                    xml.WriteEndElement();
                }
                xml.WriteStartElement("part-list");
                for (int p = 0; p < score.Parts.Count; p++)
                {
                    xml.WriteStartElement("score-part");
                    xml.WriteAttributeString("id", "P" + (p + 1));
                    xml.WriteElementString("part-name", score.Parts[p].Name);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
                for (int p = 0; p < score.Parts.Count; p++)
                {
                    var part = score.Parts[p];
                    var slurs = NoteSlur.Numbered(part, score.BarTicks);
                    var slurStarts = slurs.ToLookup(s => s.Start.Id);
                    var slurEnds = slurs.ToLookup(s => s.End.Id);
                    xml.WriteStartElement("part");
                    xml.WriteAttributeString("id", "P" + (p + 1));
                    for (int bar = 0; bar < score.Bars; bar++)
                    {
                        xml.WriteStartElement("measure");
                        xml.WriteAttributeString("number", (bar + 1).ToString());
                        if (bar == 0)
                        {
                            xml.WriteStartElement("attributes");
                            xml.WriteElementString("divisions", "4");
                            xml.WriteStartElement("key");
                            xml.WriteElementString("fifths", score.Fifths.ToString());
                            xml.WriteElementString("mode", score.MinorKey ? "minor" : "major");
                            xml.WriteEndElement();
                            xml.WriteStartElement("time");
                            xml.WriteElementString("beats", score.Beats.ToString());
                            xml.WriteElementString("beat-type", score.BeatType.ToString());
                            xml.WriteEndElement();
                            if (part.Staves.Count > 1) xml.WriteElementString("staves", "2");
                            for (int staff = 0; staff < part.Staves.Count; staff++)
                            {
                                xml.WriteStartElement("clef");
                                if (part.Staves.Count > 1) xml.WriteAttributeString("number", (staff + 1).ToString());
                                xml.WriteElementString("sign", staff == 0 ? "G" : "F");
                                xml.WriteElementString("line", staff == 0 ? "2" : "4");
                                xml.WriteEndElement();
                            }
                            xml.WriteEndElement();
                            if (p == 0)
                            {
                                xml.WriteStartElement("sound");
                                xml.WriteAttributeString("tempo", score.Tempo.ToString());
                                xml.WriteEndElement();
                            }
                        }
                        if (p == 0)
                        {
                            var rehearsal = score.RehearsalMarks.Find(mark => mark.Bar == bar);
                            if (rehearsal != null) WriteRehearsal(xml, rehearsal.Text);
                        }
                        int start = bar * score.BarTicks;
                        int end = start + score.BarTicks;
                        for (int staff = 0; staff < part.Staves.Count; staff++)
                        {
                            var lane = part.Staves[staff];
                            var measureNotes = lane.Where(n => n.Tick >= start && n.Tick < end).ToArray();
                            var marks = score.PrintedAccidentals(measureNotes);
                            var tiedFrom = new HashSet<Guid>(lane.Where(n => n.TiedTo.HasValue && n.Tick < end).Select(n => n.TiedTo.Value));
                            int voices = measureNotes.Any(n => n.Voice == 1) ? 2 : 1;
                            for (int voice = 0; voice < voices; voice++)
                            {
                                if (staff > 0 || voice > 0)
                                {
                                    xml.WriteStartElement("backup");
                                    xml.WriteElementString("duration", score.BarTicks.ToString());
                                    xml.WriteEndElement();
                                }
                                WriteVoice(xml, score, lane, measureNotes, marks, tiedFrom, slurStarts, slurEnds, staff, part.Staves.Count > 1, voice, start, end);
                            }
                        }
                        xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
        }

        private static void WriteVoice(XmlWriter xml, Score score, List<Note> lane, Note[] measureNotes,
            HashSet<Guid> marks, HashSet<Guid> tiedFrom, ILookup<Guid, NoteSlur> slurStarts,
            ILookup<Guid, NoteSlur> slurEnds, int staff, bool piano, int voice, int start, int end)
        {
            string stem = measureNotes.Select(n => n.Voice).Distinct().Count() > 1 ? (voice == 0 ? "up" : "down") : null;
            int cursor = start;
            var previous = lane.LastOrDefault(n => n.Voice == voice && n.Tick < start && !n.IsRest);
            var onsets = measureNotes.Where(n => n.Voice == voice).GroupBy(n => n.Tick).ToArray();
            var events = onsets.Select(group => group.First()).ToArray();
            int groupStart = 0, groupEnd = 0;
            for (int i = 0; i < events.Length; i++)
            {
                var note = events[i];
                if (i >= groupEnd)
                {
                    groupStart = i;
                    groupEnd = Score.BeamGroupEnd(events, i, start, score.BeatTicks);
                    for (int j = i; j < groupEnd; j++)
                        if (onsets[j].Count() > 1) { groupEnd = Math.Max(i + 1, j); break; }
                }
                while (cursor < note.Tick)
                {
                    int rest = Score.RestLength(cursor - start, note.Tick - cursor, score.BeatTicks);
                    WriteNote(xml, null, rest, staff, piano, voice: voice);
                    cursor += rest;
                }
                string beam = groupEnd - groupStart < 2 ? null : i == groupStart ? "begin" : i == groupEnd - 1 ? "end" : "continue";
                var members = onsets[i];
                var chordMark = members.Select(n => n.Chord).FirstOrDefault(mark => mark != null);
                var dynamic = members.Select(n => n.Dynamic).FirstOrDefault(mark => mark != null);
                var expressionText = members.Select(n => n.ExpressionText).FirstOrDefault(text => text != null);
                ChordSymbol chord;
                if (ChordSymbol.TryParse(chordMark, out chord)) chord.Write(xml, staff, piano);
                if (dynamic != null) WriteDynamic(xml, dynamic, staff, piano, voice);
                if (expressionText != null) WriteExpressionText(xml, expressionText, staff, piano, voice);
                foreach (var member in members)
                {
                    bool extra = member != note;
                    WriteNote(xml, member, member.Length, staff, piano,
                        previous != null && previous.HyphenAfter, extra ? null : beam,
                        showAccidental: marks.Contains(member.Id), chordMember: extra, voice: voice, stem: stem, tieStop: tiedFrom.Contains(member.Id),
                        slurStarts: slurStarts[member.Id], slurEnds: slurEnds[member.Id]);
                }
                if (!note.IsRest) previous = members.FirstOrDefault(n => n.Lyric != null) ?? note;
                cursor += note.Length;
            }
            while (cursor < end)
            {
                bool entireBar = cursor == start;
                int rest = entireBar ? score.BarTicks : Score.RestLength(cursor - start, end - cursor, score.BeatTicks);
                WriteNote(xml, null, rest, staff, piano, fullMeasure: entireBar, voice: voice);
                cursor += rest;
            }
        }

        private static void WriteDynamic(XmlWriter xml, string marking, int staff, bool piano, int voice)
        {
            xml.WriteStartElement("direction");
            xml.WriteAttributeString("placement", "below");
            xml.WriteStartElement("direction-type");
            xml.WriteStartElement("dynamics");
            xml.WriteElementString(marking, "");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteElementString("voice", (staff + 1 + voice * 2).ToString());
            if (piano) xml.WriteElementString("staff", (staff + 1).ToString());
            xml.WriteStartElement("sound");
            xml.WriteAttributeString("dynamics", (Score.Velocity(marking) * 100.0 / 90).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        private static void WriteExpressionText(XmlWriter xml, string text, int staff, bool piano, int voice)
        {
            xml.WriteStartElement("direction");
            xml.WriteAttributeString("placement", "above");
            xml.WriteStartElement("direction-type");
            xml.WriteElementString("words", text);
            xml.WriteEndElement();
            xml.WriteElementString("voice", (staff + 1 + voice * 2).ToString());
            if (piano) xml.WriteElementString("staff", (staff + 1).ToString());
            xml.WriteEndElement();
        }

        private static void WriteRehearsal(XmlWriter xml, string text)
        {
            xml.WriteStartElement("direction");
            xml.WriteAttributeString("placement", "above");
            xml.WriteStartElement("direction-type");
            xml.WriteElementString("rehearsal", text);
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        private static void WriteTie(XmlWriter xml, string element, string type)
        {
            xml.WriteStartElement(element);
            xml.WriteAttributeString("type", type);
            xml.WriteEndElement();
        }

        private static void WriteSlur(XmlWriter xml, NoteSlur slur, bool start)
        {
            xml.WriteStartElement("slur");
            xml.WriteAttributeString("type", start ? "start" : "stop");
            xml.WriteAttributeString("number", slur.Number.ToString());
            if (start) xml.WriteAttributeString("placement", slur.Start.Voice == 0 ? "above" : "below");
            xml.WriteEndElement();
        }

        private static void WriteNote(XmlWriter xml, Note note, int length, int staff, bool piano,
            bool fromPrevious = false, string beam = null, bool fullMeasure = false, bool showAccidental = false, bool chordMember = false, int voice = 0, string stem = null, bool tieStop = false,
            IEnumerable<NoteSlur> slurStarts = null, IEnumerable<NoteSlur> slurEnds = null)
        {
            xml.WriteStartElement("note");
            if (chordMember) xml.WriteElementString("chord", "");
            if (note == null || note.IsRest)
            {
                xml.WriteStartElement("rest");
                if (fullMeasure) xml.WriteAttributeString("measure", "yes");
                xml.WriteEndElement();
            }
            else
            {
                xml.WriteStartElement("pitch");
                xml.WriteElementString("step", Score.Steps[note.Step % 7]);
                if (note.Alter != 0) xml.WriteElementString("alter", note.Alter.ToString());
                xml.WriteElementString("octave", (note.Step / 7).ToString());
                xml.WriteEndElement();
            }
            xml.WriteElementString("duration", length.ToString());
            bool tieStart = note != null && note.TiedTo.HasValue;
            if (tieStop) WriteTie(xml, "tie", "stop");
            if (tieStart) WriteTie(xml, "tie", "start");
            xml.WriteElementString("voice", (staff + 1 + voice * 2).ToString());
            if (!fullMeasure) xml.WriteElementString("type", new[] { "whole", "half", "quarter", "eighth", "16th" }[Array.IndexOf(Score.Durations, Score.BaseDuration(length))]);
            for (int i = 0; !fullMeasure && i < Score.DotCount(length); i++) xml.WriteElementString("dot", "");
            if (showAccidental)
            {
                xml.WriteStartElement("accidental");
                if (note.Courtesy)
                {
                    xml.WriteAttributeString("cautionary", "yes");
                    xml.WriteAttributeString("parentheses", "yes");
                }
                xml.WriteString(KeySignature.AccidentalName(note.Alter));
                xml.WriteEndElement();
            }
            if (stem != null && note != null && !note.IsRest && Score.BaseDuration(length) != 16)
                xml.WriteElementString("stem", stem);
            if (piano) xml.WriteElementString("staff", (staff + 1).ToString());
            if (beam != null)
            {
                int levels = length == 1 ? 2 : 1;
                for (int number = 1; number <= levels; number++)
                {
                    xml.WriteStartElement("beam");
                    xml.WriteAttributeString("number", number.ToString());
                    xml.WriteString(beam);
                    xml.WriteEndElement();
                }
            }
            bool fermata = note != null && note.PauseMark == "fermata";
            bool breath = note != null && note.PauseMark == "breath-mark";
            bool caesura = note != null && note.PauseMark == "caesura";
            if (tieStart || tieStop || (note != null && (note.Articulation != null || fermata || breath || caesura)) ||
                (slurStarts != null && slurStarts.Any()) || (slurEnds != null && slurEnds.Any()))
            {
                xml.WriteStartElement("notations");
                if (tieStop) WriteTie(xml, "tied", "stop");
                if (tieStart) WriteTie(xml, "tied", "start");
                if (slurEnds != null) foreach (var slur in slurEnds) WriteSlur(xml, slur, false);
                if (slurStarts != null) foreach (var slur in slurStarts) WriteSlur(xml, slur, true);
                if (fermata) xml.WriteElementString("fermata", "");
                if (note.Articulation != null || breath || caesura)
                {
                    xml.WriteStartElement("articulations");
                    if (note.Articulation != null) xml.WriteElementString(note.Articulation, "");
                    if (breath) xml.WriteElementString("breath-mark", "");
                    if (caesura) xml.WriteElementString("caesura", "");
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            if (note != null && !note.IsRest && note.Lyric != null)
            {
                xml.WriteStartElement("lyric");
                xml.WriteAttributeString("number", "1");
                string syllabic = fromPrevious
                    ? (note.HyphenAfter ? "middle" : "end")
                    : (note.HyphenAfter ? "begin" : "single");
                xml.WriteElementString("syllabic", syllabic);
                xml.WriteElementString("text", note.Lyric);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
    }
}

