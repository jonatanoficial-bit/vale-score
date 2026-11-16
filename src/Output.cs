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
            score.Validate();
            double tick = SampleRate * 15.0 / score.Tempo;
            int size = (int)Math.Ceiling(score.Bars * Score.BarTicks * tick);
            var samples = new float[size];
            double gain = 0.68 / Math.Sqrt(score.Parts.Sum(p => p.Staves.Count));
            foreach (var part in score.Parts)
            {
                double decay = part.Instrument == "Voz" ? 0.8 : part.Instrument == "Violão" ? 5.0 : 2.8;
                foreach (var staff in part.Staves)
                {
                    int velocity = 88;
                    foreach (var note in staff)
                    {
                        if (note.IsRest) continue;
                        if (note.Dynamic != null) velocity = Score.Velocity(note.Dynamic);
                        int start = (int)Math.Round(note.Tick * tick);
                        int count = (int)Math.Round((note.Tick + note.Length * Articulations.SoundLength(note.Articulation)) * tick) - start;
                        AddTone(samples, start, count, note.Midi, gain * Articulations.Attack(note.Articulation, velocity) / 88, decay);
                    }
                }
            }
            return WriteWave(samples);
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

        private static MemoryStream WriteWave(float[] samples)
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
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
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
                            xml.WriteElementString("fifths", "0");
                            xml.WriteEndElement();
                            xml.WriteStartElement("time");
                            xml.WriteElementString("beats", "4");
                            xml.WriteElementString("beat-type", "4");
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
                        for (int staff = 0; staff < part.Staves.Count; staff++)
                        {
                            if (staff > 0)
                            {
                                xml.WriteStartElement("backup");
                                xml.WriteElementString("duration", "16");
                                xml.WriteEndElement();
                            }
                            int start = bar * 16;
                            int end = start + 16;
                            int cursor = start;
                            var lane = part.Staves[staff];
                            var previous = lane.LastOrDefault(n => n.Tick < start && !n.IsRest);
                            var events = lane.Where(n => n.Tick >= start && n.Tick < end).ToArray();
                            int groupStart = 0, groupEnd = 0;
                            for (int i = 0; i < events.Length; i++)
                            {
                                var note = events[i];
                                if (i >= groupEnd)
                                {
                                    groupStart = i;
                                    groupEnd = Score.BeamGroupEnd(events, i);
                                }
                                while (cursor < note.Tick)
                                {
                                    int rest = Score.RestLength(cursor, note.Tick - cursor);
                                    WriteNote(xml, null, rest, staff, part.Staves.Count > 1);
                                    cursor += rest;
                                }
                                string beam = groupEnd - groupStart < 2 ? null : i == groupStart ? "begin" : i == groupEnd - 1 ? "end" : "continue";
                                ChordSymbol chord;
                                if (note.Chord != null && ChordSymbol.TryParse(note.Chord, out chord)) chord.Write(xml, staff, part.Staves.Count > 1);
                                if (note.Dynamic != null) WriteDynamic(xml, note.Dynamic, staff, part.Staves.Count > 1);
                                WriteNote(xml, note, note.Length, staff, part.Staves.Count > 1, previous != null && previous.HyphenAfter, beam);
                                if (!note.IsRest) previous = note;
                                cursor += note.Length;
                            }
                            while (cursor < end)
                            {
                                int rest = Score.RestLength(cursor, end - cursor);
                                WriteNote(xml, null, rest, staff, part.Staves.Count > 1);
                                cursor += rest;
                            }
                        }
                        xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
        }

        private static void WriteDynamic(XmlWriter xml, string marking, int staff, bool piano)
        {
            xml.WriteStartElement("direction");
            xml.WriteAttributeString("placement", "below");
            xml.WriteStartElement("direction-type");
            xml.WriteStartElement("dynamics");
            xml.WriteElementString(marking, "");
            xml.WriteEndElement();
            xml.WriteEndElement();
            if (piano) xml.WriteElementString("staff", (staff + 1).ToString());
            xml.WriteStartElement("sound");
            xml.WriteAttributeString("dynamics", (Score.Velocity(marking) * 100.0 / 90).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        private static void WriteNote(XmlWriter xml, Note note, int length, int staff, bool piano, bool fromPrevious = false, string beam = null)
        {
            xml.WriteStartElement("note");
            if (note == null || note.IsRest)
            {
                xml.WriteStartElement("rest");
                if (length == Score.BarTicks) xml.WriteAttributeString("measure", "yes");
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
            xml.WriteElementString("voice", (staff + 1).ToString());
            xml.WriteElementString("type", new[] { "whole", "half", "quarter", "eighth", "16th" }[Array.IndexOf(Score.Durations, Score.BaseDuration(length))]);
            for (int i = 0; i < Score.DotCount(length); i++) xml.WriteElementString("dot", "");
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
            if (note != null && note.Articulation != null)
            {
                xml.WriteStartElement("notations");
                xml.WriteStartElement("articulations");
                xml.WriteElementString(note.Articulation, "");
                xml.WriteEndElement();
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
