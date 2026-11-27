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
            var meters = root.Elements("part").Elements("measure").Elements("attributes").Elements("time").ToArray();
            if (meters.Length > 0)
            {
                int beats = Integer(meters[0].Element("beats"), -1);
                int beatType = Integer(meters[0].Element("beat-type"), -1);
                if (!Score.SupportsMeter(beats, beatType) || meters.Any(time => time.Elements("beats").Count() != 1 ||
                    time.Elements("beat-type").Count() != 1 || Integer(time.Element("beats"), -1) != beats ||
                    Integer(time.Element("beat-type"), -1) != beatType))
                    throw new InvalidDataException("Use uma fórmula fixa de 2/4, 3/4, 4/4, 6/8, 9/8 ou 12/8.");
                score.SetMeter(beats, beatType);
            }
            var initialKey = parts.Select(p => p.Elements("measure").First())
                .SelectMany(m => m.Elements("attributes")).SelectMany(a => a.Elements("key"))
                .FirstOrDefault(k => k.Element("fifths") != null);
            int fifths = initialKey == null ? 0 : Integer(initialKey.Element("fifths"), int.MaxValue);
            if (fifths >= -7 && fifths <= 7)
                score.SetKey(fifths, initialKey != null && (string)initialKey.Element("mode") == "minor");
            var result = new MusicXmlImport { Project = score };
            var names = root.Descendants("score-part").ToLookup(e => (string)e.Attribute("id") ?? "");
            foreach (var source in parts)
            {
                int staves = source.Descendants("staves").Select(e => Integer(e, 1)).DefaultIfEmpty(1).Max();
                staves = Math.Max(staves, source.Descendants("staff").Select(e => Integer(e, 1)).DefaultIfEmpty(1).Max());
                if (staves < 1 || staves > 2) throw new InvalidDataException("A prévia aceita até duas pautas por parte.");
                var metadata = names[(string)source.Attribute("id") ?? ""].FirstOrDefault();
                string namePart = metadata == null ? null : (string)metadata.Element("part-name");
                var staffDetails = source.Descendants("staff-details").FirstOrDefault();
                int strings = staffDetails == null ? 0 : Integer(staffDetails.Element("staff-lines"), 0, 0);
                string instrument = staves == 2 ? "Piano" :
                    GuitarTab.InstrumentFromMusicXml(namePart, strings);
                score.AddPart(instrument);
                int part = score.Parts.Count - 1;
                score.Parts[part].Name = Label(namePart, score.Parts[part].Name, 40);
                ReadTabSetup(staffDetails, score.Parts[part], result);
                ReadPart(source, result, part, staves);
            }
            if (result.Notes == 0 && score.Parts.All(p => p.Staves.All(s => s.Count == 0)))
                throw new InvalidDataException("A partitura não contém figuras compatíveis.");
            score.Validate();
            result.Report = result.Notes + " notas importadas em " + parts.Length + " parte(s)." + Environment.NewLine +
                result.Omitted + " figura(s) ou letra(s) omitidas por incompatibilidade." + Environment.NewLine +
                result.Normalized + " ajuste(s) de grade ou notação." + Environment.NewLine +
                "Pode haver perdas acima de duas vozes por pauta, em acordes com durações diferentes, timbres, mudanças de andamento, expressões e layout. " +
                "As pautas são exibidas em clave de sol ou no par sol/fá.";
            return result;
        }

        private static void ReadTabSetup(XElement details, Part part, MusicXmlImport result)
        {
            if (!GuitarTab.IsTabInstrument(part) || details == null) return;
            int capo = Integer(details.Element("capo"), -1, 0);
            if (capo < 0 || capo > 12 || !GuitarTab.TrySetTuning(part, part.TabTuning, capo))
            {
                result.Normalized++;
                return;
            }
            var pitches = new SortedDictionary<int, int>();
            foreach (var item in details.Elements("staff-tuning"))
            {
                int line;
                int degree = Array.IndexOf(Score.Steps, (string)item.Element("tuning-step"));
                int octave = Integer(item.Element("tuning-octave"), -1);
                int alter = Integer(item.Element("tuning-alter"), 9, 0);
                if (!int.TryParse((string)item.Attribute("line"), NumberStyles.Integer, CultureInfo.InvariantCulture, out line) ||
                    line < 1 || degree < 0 || octave < 0 || octave > 9 || alter < -2 || alter > 2 || pitches.ContainsKey(line))
                {
                    result.Normalized++;
                    return;
                }
                pitches.Add(line, (octave + 1) * 12 + Score.Semitones[degree] + alter);
            }
            if (pitches.Count > 0 && !GuitarTab.TryApplyImportedSetup(part, pitches.OrderBy(item => item.Key).Select(item => item.Value).ToArray(), capo))
                result.Normalized++;
        }

        private static void ReadPart(XElement source, MusicXmlImport result, int part, int staves)
        {
            int divisions = 1, bar = 0;
            int barTicks = result.Project.BarTicks;
            var voices = Enumerable.Range(0, staves).Select(i => new List<string>()).ToArray();
            var ties = new Dictionary<Tuple<int, int, int, int>, Note>();
            var slurs = new Dictionary<Tuple<int, int, int>, Note>();
            foreach (var measure in source.Elements("measure"))
            {
                double cursor = 0, previous = 0;
                var ends = new int[staves, Score.VoicesPerStaff];
                Note chordBase = null;
                int baseStaff = -1;
                string baseVoice = null;
                var chords = new Dictionary<int, string>();
                var dynamics = new Dictionary<Tuple<int, int, string>, string>();
                var texts = new Dictionary<Tuple<int, int, string>, string>();
                foreach (var child in measure.Elements())
                {
                    string kind = child.Name.LocalName;
                    if (kind == "attributes")
                    {
                        divisions = Integer(child.Element("divisions"), divisions);
                        if (divisions < 1 || divisions > 1000000) throw new InvalidDataException("Divisões MusicXML inválidas.");

                        if (child.Elements("key").Any(k => Integer(k.Element("fifths"), int.MaxValue) != result.Project.Fifths ||
                            (string)k.Element("mode") == (result.Project.MinorKey ? "major" : "minor") ||
                            (k.Element("mode") != null && (string)k.Element("mode") != "major" && (string)k.Element("mode") != "minor")) ||
                            child.Element("transpose") != null) result.Normalized++;
                        continue;
                    }
                    if (kind == "harmony" || kind == "direction")
                    {
                        var marks = child.Descendants("dynamics").SelectMany(e => e.Elements());
                        string dynamic = marks.Select(e => e.Name.LocalName).FirstOrDefault(n => Score.Dynamics.Contains(n));
                        string words = kind == "direction" ? child.Descendants("words").Select(e => e.Value).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) : null;
                        string rehearsal = kind == "direction" ? child.Descendants("rehearsal").Select(e => e.Value).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) : null;
                        if (rehearsal != null)
                        {
                            try { result.Project.SetRehearsalMark(bar, rehearsal); }
                            catch (InvalidOperationException) { result.Omitted++; }
                        }
                        if (kind == "direction" && dynamic == null && words == null) continue;
                        int staffNumber = Integer(child.Element("staff"), 1);
                        double atPosition = cursor + Integer(child.Element("offset"), 0) * 4.0 / divisions;
                        int tickOfMark = (int)Math.Round(atPosition, MidpointRounding.AwayFromZero);
                        string chordText = kind == "harmony" ? ChordSymbol.Read(child) : null;
                        if (staffNumber < 1 || staffNumber > staves || tickOfMark < 0 || tickOfMark >= barTicks ||
                            (kind == "harmony" && chordText == null))
                            result.Omitted++;
                        else
                        {
                            int key = (staffNumber - 1) * barTicks + tickOfMark;
                            if (kind == "harmony")
                            {
                                if (chords.ContainsKey(key)) result.Omitted++;
                                chords[key] = chordText;
                            }
                            else
                            {
                                var markKey = Tuple.Create(staffNumber - 1, tickOfMark, (string)child.Element("voice"));
                                if (dynamic != null)
                                {
                                    if (dynamics.ContainsKey(markKey)) result.Omitted++;
                                    dynamics[markKey] = dynamic;
                                }
                                if (words != null)
                                {
                                    if (texts.ContainsKey(markKey)) result.Omitted++;
                                    texts[markKey] = words;
                                }
                            }
                        }
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
                        chordBase = null;
                        cursor -= length;
                        if (cursor < -.00001) throw new InvalidDataException("O recuo MusicXML ultrapassa o início do compasso.");
                        continue;
                    }
                    if (!chord) { chordBase = null; previous = cursor; cursor += length; }
                    if (kind == "forward") continue;
                    int staff = Integer(child.Element("staff"), 1) - 1;
                    if (staff < 0 || staff >= staves || length <= 0 || start < -.00001 || start >= barTicks || start + length > barTicks + .00001)
                    {
                        result.Omitted++;
                        continue;
                    }
                    int tick = (int)Math.Round(start, MidpointRounding.AwayFromZero);
                    int end = Math.Max(tick + 1, (int)Math.Round(start + length, MidpointRounding.AwayFromZero));
                    int at = bar * barTicks + tick;
                    string voice = (string)child.Element("voice") ?? (chord ? baseVoice : null) ?? "1";
                    if (chord && (chordBase == null || baseStaff != staff || chordBase.Tick != at ||
                        baseVoice != voice || child.Element("rest") != null ||
                        chordBase.Length != end - tick))
                    {
                        result.Omitted++;
                        continue;
                    }
                    int voiceIndex = voices[staff].IndexOf(voice);
                    if (voiceIndex < 0)
                    {
                        if (voices[staff].Count == Score.VoicesPerStaff) { result.Omitted++; continue; }
                        voiceIndex = voices[staff].Count;
                        voices[staff].Add(voice);
                    }
                    if (start == 0 && end == barTicks && child.Element("rest") != null && (string)child.Element("rest").Attribute("measure") == "yes")
                    {
                        ends[staff, voiceIndex] = end;
                        continue;
                    }
                    if (!Score.IsSupportedDuration(end - tick) || end > barTicks || (!chord && tick < ends[staff, voiceIndex]))
                    {
                        result.Omitted++;
                        continue;
                    }
                    if (child.Element("rest") != null) result.Project.PutRest(part, staff, at, end - tick, voiceIndex);
                    else
                    {
                        var pitch = child.Element("pitch");
                        int degree = pitch == null ? -1 : Array.IndexOf(Score.Steps, (string)pitch.Element("step"));
                        int octave = pitch == null ? -1 : Integer(pitch.Element("octave"), -1);
                        int alteration = pitch == null ? 0 : Integer(pitch.Element("alter"), int.MaxValue, 0);
                        int step = octave * 7 + degree;
                        int min = GuitarTab.IsTabInstrument(result.Project.Parts[part]) ? 0 : staff == 0 ? 25 : 13;
                        int max = GuitarTab.IsTabInstrument(result.Project.Parts[part]) ? 69 : min + 18;
                        if (degree < 0 || octave < 0 || octave > 9 || step < min || step > max ||
                            alteration < -2 || alteration > 2) { result.Omitted++; continue; }
                        Note added;
                        var lane = result.Project.Parts[part].Staves[staff];
                        if (chord)
                        {
                            int count = lane.Count;
                            try { added = result.Project.AddChordNote(part, staff, at, end - tick, step, alteration, voiceIndex); }
                            catch (InvalidOperationException) { result.Omitted++; continue; }
                            if (lane.Count == count) { result.Omitted++; continue; }
                        }
                        else
                        {
                            result.Project.Put(part, staff, at, end - tick, step, alteration, voiceIndex);
                            added = chordBase = lane.Find(n => n.Tick == at && n.Voice == voiceIndex);
                            baseStaff = staff;
                            baseVoice = voice;
                        }
                        var technical = child.Elements("notations").Elements("technical").FirstOrDefault();
                        int tabString = technical == null ? -1 : Integer(technical.Element("string"), -1);
                        int tabFret = technical == null ? -1 : Integer(technical.Element("fret"), -1);
                        int tabPitch;
                        if (GuitarTab.IsTabInstrument(result.Project.Parts[part]) && GuitarTab.TryPitch(result.Project.Parts[part], tabString, tabFret, out tabPitch) && tabPitch == added.Midi)
                        {
                            added.TabString = tabString;
                            added.TabFret = tabFret;
                        }
                        var accidental = child.Element("accidental");
                        if (accidental != null && ((string)accidental.Attribute("cautionary") == "yes" ||
                            (string)accidental.Attribute("parentheses") == "yes"))
                            result.Project.SetCourtesy(part, staff, at, true, added.Id);
                        result.Notes++;
                        ReadTie(child, added, staff, ties, result);
                        ReadSlurs(child, added, staff, slurs, result);
                        var marksOnNote = child.Elements("notations").Elements("articulations").Elements().ToArray();
                        var articulation = marksOnNote.FirstOrDefault(e => Articulations.XmlNames.Contains(e.Name.LocalName));
                        if (articulation != null) result.Project.SetArticulation(part, staff, at, articulation.Name.LocalName, added.Id);
                        var pause = marksOnNote.FirstOrDefault(e => e.Name.LocalName == "breath-mark" || e.Name.LocalName == "caesura");
                        bool fermata = child.Elements("notations").Elements("fermata").Any();
                        if (fermata) result.Project.SetPauseMark(part, staff, at, "fermata", added.Id);
                        else if (pause != null) result.Project.SetPauseMark(part, staff, at, pause.Name.LocalName, added.Id);
                        result.Omitted += marksOnNote.Length - (articulation == null ? 0 : 1) - (!fermata && pause != null ? 1 : 0);
                        var lyric = child.Elements("lyric").FirstOrDefault();
                        if (lyric != null)
                        {
                            string syllabic = (string)lyric.Element("syllabic");
                            try { result.Project.SetLyric(part, staff, at, (string)lyric.Element("text"), syllabic == "begin" || syllabic == "middle", added.Id); }
                            catch (InvalidOperationException) { result.Omitted++; }
                        }
                    }
                    if (!chord) ends[staff, voiceIndex] = end;
                    if (Math.Abs(tick - start) > .00001 || Math.Abs(end - start - length) > .00001) result.Normalized++;
                }
                var usedChords = new HashSet<int>();
                var usedDynamics = new HashSet<Tuple<int, int, string>>();
                var usedTexts = new HashSet<Tuple<int, int, string>>();
                for (int staff = 0; staff < staves; staff++)
                foreach (var note in result.Project.Parts[part].Staves[staff].Where(n => !n.IsRest && n.Tick / barTicks == bar).GroupBy(n => new { n.Tick, n.Voice }).Select(group => group.First()).OrderBy(n => n.Voice))
                {
                    int key = staff * barTicks + note.Tick % barTicks;
                    string chord, marking, expressionText;
                    if (!chords.TryGetValue(key, out chord) || !usedChords.Add(key)) chord = null;
                    int local = note.Tick % barTicks;
                    var markKey = Tuple.Create(staff, local, voices[staff][note.Voice]);
                    if (!dynamics.TryGetValue(markKey, out marking))
                    {
                        markKey = Tuple.Create(staff, local, (string)null);
                        dynamics.TryGetValue(markKey, out marking);
                    }
                    if (marking != null && !usedDynamics.Add(markKey)) marking = null;
                    if (!texts.TryGetValue(markKey, out expressionText))
                    {
                        markKey = Tuple.Create(staff, local, (string)null);
                        texts.TryGetValue(markKey, out expressionText);
                    }
                    if (expressionText == null)
                    {
                        var fallback = texts.FirstOrDefault(item => item.Key.Item1 == staff && item.Key.Item2 == local &&
                            !usedTexts.Contains(item.Key));
                        if (fallback.Key != null)
                        {
                            markKey = fallback.Key;
                            expressionText = fallback.Value;
                        }
                    }
                    if (expressionText != null && !usedTexts.Add(markKey)) expressionText = null;
                    if (chord != null || marking != null)
                        result.Project.SetExpression(part, staff, note.Tick, chord, marking, note.Id);
                    if (expressionText != null)
                    {
                        try { result.Project.SetExpressionText(part, staff, note.Tick, expressionText, note.Id); }
                        catch (InvalidOperationException) { result.Omitted++; }
                    }
                }
                result.Omitted += chords.Count - usedChords.Count + dynamics.Count - usedDynamics.Count + texts.Count - usedTexts.Count;
                bar++;
            }
            result.Omitted += ties.Count + slurs.Count;
            foreach (var excess in NoteSlur.Numbered(result.Project.Parts[part], barTicks).Where(s => s.Number == 0))
            {
                excess.Start.SlursTo.Remove(excess.End.Id);
                if (excess.Start.SlursTo.Count == 0) excess.Start.SlursTo = null;
                result.Omitted++;
            }
        }

        private static void ReadSlurs(XElement element, Note note, int staff,
            Dictionary<Tuple<int, int, int>, Note> open, MusicXmlImport result)
        {
            foreach (var mark in element.Elements("notations").Elements("slur"))
            {
                var type = (string)mark.Attribute("type");
                if (type == "continue") continue;
                int number;
                if (!int.TryParse((string)mark.Attribute("number") ?? "1", out number) || number < 1 || number > 16)
                {
                    result.Omitted++;
                    continue;
                }
                var key = Tuple.Create(staff, note.Voice, number);
                if (type == "start")
                {
                    if (open.ContainsKey(key)) result.Omitted++;
                    open[key] = note;
                }
                else if (type == "stop")
                {
                    Note start;
                    if (!open.TryGetValue(key, out start) || !NoteSlur.Fits(start, note) ||
                        (start.SlursTo != null && start.SlursTo.Contains(note.Id)))
                        result.Omitted++;
                    else
                    {
                        if (start.SlursTo == null) start.SlursTo = new List<Guid>();
                        start.SlursTo.Add(note.Id);
                    }
                    open.Remove(key);
                }
            }
        }

        private static void ReadTie(XElement element, Note note, int staff,
            Dictionary<Tuple<int, int, int, int>, Note> open, MusicXmlImport result)
        {
            var types = element.Elements("tie").Concat(element.Elements("notations").Elements("tied"))
                .Select(tie => (string)tie.Attribute("type")).ToArray();
            var key = Tuple.Create(staff, note.Voice, note.Step, note.Alter);
            if (types.Contains("stop") || types.Contains("continue"))
            {
                Note source;
                if (open.TryGetValue(key, out source) && NoteTies.Fits(source, note)) source.TiedTo = note.Id;
                else result.Omitted++;
                open.Remove(key);
            }
            if (types.Contains("start") || types.Contains("continue"))
            {
                if (open.ContainsKey(key)) result.Omitted++;
                open[key] = note;
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

