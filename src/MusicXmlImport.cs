using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace ValeScore
{
    public sealed class MusicXmlImport
    {
        private const int MaxBytes = 8 * 1024 * 1024;
        public Score Project { get; private set; }
        public int Notes { get; private set; }
        public int Omitted { get; private set; }
        public int Normalized { get; private set; }
        public string Report { get; private set; }

        public static MusicXmlImport Open(string path)
        {
            using (var file = File.OpenRead(path))
            {
                if (file.Length > MaxBytes) throw new InvalidDataException("O arquivo excede 8 MB.");
                string name = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(Path.GetExtension(path), ".mxl", StringComparison.OrdinalIgnoreCase))
                    return Read(file, name);
                using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count > 128) throw new InvalidDataException("O MXL contém arquivos demais.");
                    var manifest = archive.GetEntry("META-INF/container.xml");
                    var container = ReadEntry(manifest);
                    var rootfile = container.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile");
                    var location = rootfile == null ? "" : (string)rootfile.Attribute("full-path") ?? "";
                    if (location.Length == 0 || location.Contains("\\") || location.Contains(":") ||
                        location.Split('/').Any(part => part == ".." || part == "." || part == ""))
                        throw new InvalidDataException("O MXL não indica um caminho de partitura válido.");
                    return Convert(ReadEntry(archive.GetEntry(location)), name);
                }
            }
        }

        private static XDocument ReadEntry(ZipArchiveEntry entry)
        {
            if (entry == null || entry.Length > MaxBytes) throw new InvalidDataException("Entrada MXL ausente ou grande demais.");
            using (var input = entry.Open())
            using (var bounded = new MemoryStream())
            {
                var buffer = new byte[16384];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (bounded.Length + read > MaxBytes) throw new InvalidDataException("Entrada MXL excede 8 MB.");
                    bounded.Write(buffer, 0, read);
                }
                bounded.Position = 0;
                return Parse(bounded);
            }
        }

        public static MusicXmlImport Read(Stream input, string name)
        {
            return Convert(Parse(input), name);
        }

        private static XDocument Parse(Stream input)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null,
                MaxCharactersInDocument = MaxBytes, IgnoreComments = true, IgnoreProcessingInstructions = true };
            using (var reader = XmlReader.Create(input, settings))
            {
                // Check nesting before materializing the tree.
                var document = new XDocument();
                var stack = new Stack<XElement>();
                while (reader.Read())
                {
                    if (reader.Depth > 64) throw new InvalidDataException("MusicXML com profundidade excessiva.");
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        var element = new XElement(reader.LocalName);
                        bool empty = reader.IsEmptyElement;
                        while (reader.MoveToNextAttribute())
                            if (reader.Prefix != "xmlns" && reader.Name != "xmlns" && element.Attribute(reader.LocalName) == null)
                                element.SetAttributeValue(reader.LocalName, reader.Value);
                        reader.MoveToElement();
                        if (stack.Count == 0) document.Add(element);
                        else stack.Peek().Add(element);
                        if (!empty) stack.Push(element);
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement) stack.Pop();
                    else if (stack.Count > 0 && (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA ||
                        reader.NodeType == XmlNodeType.SignificantWhitespace)) stack.Peek().Add(new XText(reader.Value));
                }
                return document;
            }
        }

        private static MusicXmlImport Convert(XDocument xml, string name)
        {
            var root = xml.Root;
            if (root == null || root.Name != "score-partwise")
                throw new InvalidDataException("Use um MusicXML no formato score-partwise.");
            var parts = root.Elements("part").ToArray();
            if (parts.Length < 1 || parts.Length > 4) throw new InvalidDataException("Use MusicXML com uma a quatro partes.");
            int bars = parts[0].Elements("measure").Count();
            if (bars < 1 || bars > Score.MaxBars || parts.Any(p => p.Elements("measure").Count() != bars))
                throw new InvalidDataException("As partes precisam ter a mesma quantidade de compassos, entre 1 e 64.");
            string title = (string)root.Element("movement-title") ?? (string)root.Descendants("work-title").FirstOrDefault() ?? name;
            string author = (string)root.Descendants("creator").FirstOrDefault(e => (string)e.Attribute("type") == "composer");
            var tempos = root.Descendants("sound").Select(e => (string)e.Attribute("tempo")).Where(v => v != null).ToArray();
            double tempo;
            if (!double.TryParse(tempos.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out tempo) ||
                double.IsNaN(tempo) || double.IsInfinity(tempo)) tempo = 120;
            var score = new Score { Title = Label(title, "Partitura importada", 80), Author = Label(author, "", 80),
                Bars = bars, Tempo = (int)Math.Round(Math.Max(30, Math.Min(240, tempo))) };
            var result = new MusicXmlImport { Project = score };
            var names = root.Descendants("score-part").ToLookup(e => (string)e.Attribute("id") ?? "");
            foreach (var source in parts)
            {
                int staves = source.Descendants("staves").Select(e => Integer(e, 1)).DefaultIfEmpty(1).Max();
                staves = Math.Max(staves, source.Descendants("staff").Select(e => Integer(e, 1)).DefaultIfEmpty(1).Max());
                if (staves < 1 || staves > 2) throw new InvalidDataException("A prévia aceita até duas pautas por parte.");
                var metadata = names[(string)source.Attribute("id") ?? ""].FirstOrDefault();
                string namePart = metadata == null ? null : (string)metadata.Element("part-name");
                string instrument = staves == 2 ? "Piano" :
                    (namePart ?? "").IndexOf("violão", StringComparison.OrdinalIgnoreCase) >= 0 ? "Violão" : "Voz";
                score.AddPart(instrument);
                int part = score.Parts.Count - 1;
                score.Parts[part].Name = Label(namePart, score.Parts[part].Name, 40);
                ReadPart(source, result, part, staves);
            }
            if (result.Notes == 0 && score.Parts.All(p => p.Staves.All(s => s.Count == 0)))
                throw new InvalidDataException("A partitura não contém figuras compatíveis.");
            score.Validate();
            result.Report = result.Notes + " notas importadas em " + parts.Length + " parte(s)." + Environment.NewLine +
                result.Omitted + " figura(s) ou letra(s) omitidas por incompatibilidade." + Environment.NewLine +
                result.Normalized + " ajuste(s) de grade ou notação." + Environment.NewLine +
                "Pode haver perdas de acordes e vozes sobrepostas, timbres, mudanças de andamento, expressões e layout. " +
                "As pautas são exibidas em clave de sol ou no par sol/fá.";
            return result;
        }

        private static void ReadPart(XElement source, MusicXmlImport result, int part, int staves)
        {
            int divisions = 1, bar = 0;
            foreach (var measure in source.Elements("measure"))
            {
                double cursor = 0, previous = 0;
                int[] ends = new int[staves];
                foreach (var child in measure.Elements())
                {
                    string kind = child.Name.LocalName;
                    if (kind == "attributes")
                    {
                        divisions = Integer(child.Element("divisions"), divisions);
                        if (divisions < 1 || divisions > 1000000) throw new InvalidDataException("Divisões MusicXML inválidas.");
                        foreach (var time in child.Elements("time"))
                            if ((string)time.Element("beats") != "4" || (string)time.Element("beat-type") != "4" ||
                                time.Elements("beats").Count() != 1)
                                throw new InvalidDataException("A importação MusicXML desta prévia aceita partituras em 4/4.");
                        if (child.Elements("key").Any(k => Integer(k.Element("fifths"), 0) != 0) ||
                            child.Element("transpose") != null) result.Normalized++;
                        continue;
                    }
                    if (kind != "note" && kind != "forward" && kind != "backup") continue;
                    int value = Integer(child.Element("duration"), 0);
                    if (value < 0) throw new InvalidDataException("Duração MusicXML negativa.");
                    double length = value * 4.0 / divisions;
                    bool chord = child.Element("chord") != null;
                    double start = chord ? previous : cursor;
                    if (kind == "backup")
                    {
                        cursor -= length;
                        if (cursor < -.00001) throw new InvalidDataException("O recuo MusicXML ultrapassa o início do compasso.");
                        continue;
                    }
                    if (!chord) { previous = cursor; cursor += length; }
                    if (kind == "forward") continue;
                    int staff = Integer(child.Element("staff"), 1) - 1;
                    if (staff < 0 || staff >= staves || length <= 0 || start < -.00001 || start >= 16 || start + length > 16.00001)
                    {
                        result.Omitted++;
                        continue;
                    }
                    int tick = (int)Math.Round(start, MidpointRounding.AwayFromZero);
                    int end = Math.Max(tick + 1, (int)Math.Round(start + length, MidpointRounding.AwayFromZero));
                    if (!Score.IsSupportedDuration(end - tick) || end > 16 || tick < ends[staff])
                    {
                        result.Omitted++;
                        continue;
                    }
                    int at = bar * Score.BarTicks + tick;
                    if (child.Element("rest") != null) result.Project.PutRest(part, staff, at, end - tick);
                    else
                    {
                        var pitch = child.Element("pitch");
                        int degree = pitch == null ? -1 : Array.IndexOf(Score.Steps, (string)pitch.Element("step"));
                        int octave = pitch == null ? -1 : Integer(pitch.Element("octave"), -1);
                        int alteration = pitch == null ? 0 : Integer(pitch.Element("alter"), int.MaxValue, 0);
                        int step = octave * 7 + degree;
                        int min = staff == 0 ? 25 : 13;
                        if (degree < 0 || octave < 0 || octave > 9 || step < min || step > min + 18 ||
                            alteration < -1 || alteration > 1) { result.Omitted++; continue; }
                        result.Project.Put(part, staff, at, end - tick, step, alteration);
                        result.Notes++;
                        var lyric = child.Elements("lyric").FirstOrDefault();
                        if (lyric != null)
                        {
                            string syllabic = (string)lyric.Element("syllabic");
                            try { result.Project.SetLyric(part, staff, at, (string)lyric.Element("text"), syllabic == "begin" || syllabic == "middle"); }
                            catch (InvalidOperationException) { result.Omitted++; }
                        }
                    }
                    ends[staff] = end;
                    if (Math.Abs(tick - start) > .00001 || Math.Abs(end - start - length) > .00001) result.Normalized++;
                }
                bar++;
            }
        }

        private static int Integer(XElement element, int invalid, int? absent = null)
        {
            if (element == null) return absent ?? invalid;
            int value;
            return int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : invalid;
        }

        private static string Label(string value, string fallback, int max)
        {
            string name = (value ?? "").Trim();
            if (name.Length == 0) return fallback;
            if (name.Length <= max) return name;
            return name.Substring(0, char.IsHighSurrogate(name[max - 1]) ? max - 1 : max).TrimEnd();
        }
    }
}
