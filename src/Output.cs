using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ValeScore
{
    public static class Output
    {
        public const int SampleRate = 22050;
        public static MemoryStream Wave(Score score)
        {
            score.Validate();
            double tick = SampleRate * 15.0 / score.Tempo;
            int size = (int)Math.Ceiling(score.Bars * Score.BarTicks * tick);
            var samples = new float[size];
            double gain = 0.68 / Math.Sqrt(score.Parts.Sum(p => p.Staves.Count));
            foreach (var part in score.Parts)
            foreach (var staff in part.Staves)
            foreach (var note in staff)
            {
                int start = (int)Math.Round(note.Tick * tick);
                int count = (int)Math.Round((note.Tick + note.Length) * tick) - start;
                double pitch = 2 * Math.PI * 440 * Math.Pow(2, (note.Midi - 69) / 12.0) / SampleRate;
                double decay = part.Instrument == "Voz" ? 0.8 : part.Instrument == "Violão" ? 5.0 : 2.8;
                for (int i = 0; i < count; i++)
                {
                    double angle = pitch * i;
                    double envelope = Math.Min(1, Math.Min(i / 165.0, (count - 1 - i) / 600.0));
                    double tone = Math.Sin(angle) + 0.20 * Math.Sin(angle * 2) + 0.06 * Math.Sin(angle * 3);
                    samples[start + i] += (float)(gain * envelope * tone * Math.Exp(-decay * i / SampleRate));
                }
            }

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
                            foreach (var note in part.Staves[staff].Where(n => n.Tick >= start && n.Tick < end))
                            {
                                while (cursor < note.Tick)
                                {
                                    int rest = Score.RestLength(cursor, note.Tick - cursor);
                                    WriteNote(xml, null, rest, staff, part.Staves.Count > 1);
                                    cursor += rest;
                                }
                                WriteNote(xml, note, note.Length, staff, part.Staves.Count > 1);
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

        private static void WriteNote(XmlWriter xml, Note note, int length, int staff, bool piano)
        {
            xml.WriteStartElement("note");
            if (note == null) xml.WriteElementString("rest", "");
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
            xml.WriteElementString("type", new[] { "whole", "half", "quarter", "eighth", "16th" }[Array.IndexOf(Score.Durations, length)]);
            if (piano) xml.WriteElementString("staff", (staff + 1).ToString());
            xml.WriteEndElement();
        }
    }
}
