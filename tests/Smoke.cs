using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using ValeScore;

internal static class Smoke
{
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args[0]); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }

    private static void SlurChecks(string directory)
    {
        var score = Score.New(false);
        var first = score.AddChordNote(0, 0, 0, 4, 32, 0);
        var next = score.AddChordNote(0, 0, 4, 4, 34, 0);
        var otherVoice = score.AddChordNote(0, 0, 8, 4, 28, 0, voice: 1);
        var stop = score.AddChordNote(0, 0, 12, 4, 35, 0);
        var distant = score.AddChordNote(0, 0, 52, 4, 37, 0);
        Assert(score.ToggleSlur(0, 0, first.Id, stop.Id) && score.ToggleSlur(0, 0, first.Id, distant.Id) &&
            score.ToggleSlur(0, 0, next.Id, stop.Id), "Frases aceitam alturas diferentes, pontos comuns e arcos aninhados");
        bool invalid = false;
        try { score.ToggleSlur(0, 0, first.Id, otherVoice.Id); }
        catch (InvalidOperationException) { invalid = true; }
        Assert(invalid && first.SlursTo.Count == 2, "Ligadura de expressão exige a mesma voz e preserva arcos anteriores");
        var original = score.Snapshot();
        var clone = Score.Restore(original);
        Assert(clone.Parts[0].Staves[0].First().SlursTo.SequenceEqual(first.SlursTo),
            "Arquivo nativo conserva os destinos da frase");

        var copied = clone.CopyBar(0);
        clone.PasteBar(1, copied);
        clone.DuplicateBar(1);
        var lane = clone.Parts[0].Staves[0];
        var pasted = lane.Single(n => n.Tick == 16 && n.Voice == 0);
        var duplicated = lane.Single(n => n.Tick == 32 && n.Voice == 0);
        Assert(pasted.SlursTo.Single() == lane.Single(n => n.Tick == 28 && n.Voice == 0).Id &&
            duplicated.SlursTo.Single() == lane.Single(n => n.Tick == 44 && n.Voice == 0).Id,
            "Cópia e duplicação remapeiam os arcos internos para as notas novas");

        using (var stream = new MemoryStream())
        {
            Output.MusicXml(score, stream);
            stream.Position = 0;
            var marks = XDocument.Load(stream).Descendants("slur").ToArray();
            Assert(marks.Length == 6 && marks.Where(e => (string)e.Attribute("type") == "start")
                .Select(e => (string)e.Attribute("number")).Distinct().Count() == 3,
                "MusicXML distingue ligaduras sobrepostas com números separados");
            stream.Position = 0;
            var parsed = MusicXmlImport.Read(stream, "Frases");
            var imported = parsed.Project.Parts[0].Staves[0];
            Assert(parsed.Omitted == 0 && imported.Where(n => n.SlursTo != null)
                .SelectMany(n => n.SlursTo.Select(id => imported.Single(target => target.Id == id).Tick))
                .OrderBy(tick => tick).SequenceEqual(new[] { 12, 12, 52 }),
                "Reimportação MusicXML mantém os três finais de frase");
        }
        string mxl = Path.Combine(directory, "frases.mxl");
        using (var target = File.Create(mxl)) Output.CompressedMusicXml(score, target);
        Assert(MusicXmlImport.Open(mxl).Project.Parts[0].Staves[0].Where(n => n.SlursTo != null)
            .Sum(n => n.SlursTo.Count) == 3, "MXL preserva as ligaduras de expressão");

        var without = Score.Restore(original);
        Assert(!without.ToggleSlur(0, 0, first.Id, distant.Id), "Selecionar os mesmos extremos remove apenas o arco correspondente");
        var layout = new ScoreLayout(score);
        using (var withArc = new Bitmap(layout.Width, layout.Height))
        using (var withoutArc = new Bitmap(layout.Width, layout.Height))
        using (var a = Graphics.FromImage(withArc))
        using (var b = Graphics.FromImage(withoutArc))
        {
            ScorePage.Draw(a, score, 0, null, -1);
            ScorePage.Draw(b, without, 0, null, -1);
            var systems = layout.OnPage(0).ToArray();
            int endSystem = Array.FindIndex(systems, line => line.Any(bar => bar.Index == distant.Tick / score.BarTicks));
            Func<int, bool> hasArc = top => Enumerable.Range(top - 40, 39).Any(y =>
                Enumerable.Range(0, withArc.Width).Any(x => withArc.GetPixel(x, y) != withoutArc.GetPixel(x, y)));
            Assert(endSystem > 0 && hasArc(layout.StaffTop(0, 0)) && hasArc(layout.StaffTop(endSystem, 0)),
                "Arco de expressão aparece nos dois sistemas na quebra de linha");
        }
    }

    private static void TieChecks(string directory)
    {
        var score = Score.New(false);
        var start = score.AddChordNote(0, 0, 12, 4, 32, 0, voice: 1);
        score.Put(0, 0, 16, 8, 32, 0);
        var middle = score.AddChordNote(0, 0, 16, 8, 32, 0, voice: 1);
        var end = score.AddChordNote(0, 0, 24, 4, 32, 0, voice: 1);
        score.Put(0, 0, 28, 4, 32, 0, voice: 1);
        Assert(score.ToggleTie(0, 0, start.Id) && start.TiedTo == middle.Id &&
            score.ToggleTie(0, 0, middle.Id) && middle.TiedTo == end.Id,
            "Ligaduras atravessam a barra e permanecem na voz e altura selecionadas");
        string saved = Path.Combine(directory, "ligaduras.valescore");
        score.Save(saved);
        var reopened = Score.Open(saved);
        Assert(reopened.Parts[0].Staves[0].Count(n => n.TiedTo.HasValue) == 2,
            "Salvamento e reabertura preservam a cadeia de ligaduras");

        var before = score.ExtractBars(0, 0);
        var after = score.ExtractBars(1, 1);
        Assert(!before.Parts[0].Staves[0].Any(n => n.TiedTo.HasValue) &&
            after.Parts[0].Staves[0].Count(n => n.TiedTo.HasValue) == 1,
            "Recorte descarta ligaduras cortadas na borda e conserva as internas");
        reopened.PasteBar(2, score.CopyBar(1));
        reopened.DuplicateBar(1);
        var lane = reopened.Parts[0].Staves[0];
        Assert(lane.Where(n => n.TiedTo.HasValue).All(n => lane.Any(t => t.Id == n.TiedTo &&
            n.Tick + n.Length == t.Tick)) && lane.Count(n => n.TiedTo.HasValue) == 4,
            "Cópia e duplicação ligam as novas identidades no compasso de destino");
        var edited = Score.Restore(score.Snapshot());
        var note = edited.Parts[0].Staves[0].Single(n => n.Id == middle.Id);
        edited.EditNote(0, 0, note, 8, 33, 0);
        Assert(!edited.Parts[0].Staves[0].Any(n => n.TiedTo.HasValue), "Mudar a altura de uma continuação solta as ligaduras incompatíveis");

        using (var xml = new MemoryStream())
        {
            Output.MusicXml(score, xml);
            xml.Position = 0;
            var doc = XDocument.Load(xml);
            Assert(doc.Descendants("tie").Count() == 4 && doc.Descendants("tied").Count() == 4,
                "MusicXML exporta ligaduras para som e gravação musical");
            xml.Position = 0;
            var parsed = MusicXmlImport.Read(xml, "Ligaduras");
            Assert(parsed.Omitted == 0 && parsed.Project.Parts[0].Staves[0].Count(n => n.TiedTo.HasValue) == 2,
                "MusicXML reimporta a cadeia e preserva a distinção entre vozes");
            doc.Descendants("tie").Remove();
            using (var engraving = new MemoryStream())
            {
                doc.Save(engraving);
                engraving.Position = 0;
                Assert(MusicXmlImport.Read(engraving, "Ligaduras").Project.Parts[0].Staves[0].Count(n => n.TiedTo.HasValue) == 2,
                    "Importação aceita ligaduras MusicXML marcadas em notations");
            }
        }
        var playback = Score.Restore(score.Snapshot());
        playback.Parts[0].Staves[0].RemoveAll(n => n.Voice == 0);
        using (var midi = new MemoryStream())
        {
            MidiSequence.Export(playback, midi);
            midi.Position = 0;
            var sequence = MidiSequence.Read(midi);
            var attacks = sequence.Events.Where(e => (e.Status & 0xF0) == 0x90 && e.Data2 != 0).Select(e => e.Tick);
            Assert(attacks.SequenceEqual(new long[] { 1440, 3360 }) &&
                sequence.Events.Any(e => (e.Status & 0xF0) == 0x80 && e.Tick == 3360),
                "MIDI toca a cadeia como uma única nota e rearticula a próxima figura independente");
            var converted = MidiImport.Convert(sequence, "Ligaduras");
            Assert(converted.Project.Parts[0].Staves[0].Any(n => n.TiedTo.HasValue),
                "MIDI recria ligaduras quando uma nota sustentada precisa atravessar a barra");
        }
        var untied = Score.Restore(playback.Snapshot());
        foreach (var n in untied.Parts[0].Staves[0]) n.TiedTo = null;
        using (var sustained = Output.Wave(playback))
        using (var repeated = Output.Wave(untied))
        {
            int boundary = 44 + 2 * (int)Math.Round(16 * Output.SampleRate * 15.0 / score.Tempo);
            var tail = sustained.ToArray().Skip(boundary).Take(128);
            Assert(!tail.SequenceEqual(repeated.ToArray().Skip(boundary).Take(128)),
                "WAV preserva a fase e a sustentação na passagem entre notas ligadas");
        }

        var drawing = Score.New(false);
        var layout = new ScoreLayout(drawing);
        int breakTick = (layout.OnPage(0).First().Last().Index + 1) * drawing.BarTicks;
        var first = drawing.AddChordNote(0, 0, breakTick - 4, 4, 32, 0);
        drawing.Put(0, 0, breakTick, 4, 32, 0);
        drawing.ToggleTie(0, 0, first.Id);
        using (var linked = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var plain = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var a = Graphics.FromImage(linked))
        using (var b = Graphics.FromImage(plain))
        {
            ScorePage.Draw(a, drawing, 0, null, -1);
            drawing.ToggleTie(0, 0, first.Id);
            ScorePage.Draw(b, drawing, 0, null, -1);
            Func<int, bool> containsArc = top => Enumerable.Range(top + 33, 14).Any(y =>
                Enumerable.Range(0, linked.Width).Any(x => linked.GetPixel(x, y) != plain.GetPixel(x, y)));
            Assert(containsArc(layout.StaffTop(0, 0)) && containsArc(layout.StaffTop(1, 0)), "Gravação desenha a ligadura nos dois sistemas na quebra de linha");
        }
    }

    private static void TablatureChecks(string directory)
    {
        var score = new Score { Title = "Estudo para violão", Author = "Teste" };
        score.AddPart("Violão");
        var open = score.PutTab(0, 0, 0, 4, 1, 0);
        var low = score.PutTab(0, 0, 4, 4, 6, 3);
        var chordTop = score.PutTab(0, 0, 8, 4, 1, 3);
        var chordBottom = score.PutTab(0, 0, 8, 4, 2, 3);
        score.Validate();
        Assert(open.Midi == 64 && low.Midi == 43 && chordTop.TabString == 1 && chordBottom.TabString == 2,
            "Tablatura converte corda e casa em alturas tocáveis e aceita acordes");
        var restored = Score.Restore(score.Snapshot());
        Assert(restored.Parts[0].Staves[0].Select(note => note.TabString).SequenceEqual(new[] { 1, 6, 1, 2 }),
            "Projeto preserva corda e casa da tablatura");
        restored.PasteBar(1, restored.CopyBar(0));
        Assert(restored.Parts[0].Staves[0].Where(note => note.Tick >= 16 && note.Tick < 32).Select(note => note.TabFret)
            .SequenceEqual(new[] { 0, 3, 3, 3 }), "Cópia de compasso conserva as casas da tablatura");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(score, xml);
            xml.Position = 0;
            var document = XDocument.Load(xml);
            Assert(document.Descendants("technical").Elements("string").Select(item => item.Value).SequenceEqual(new[] { "1", "6", "1", "2" }) &&
                document.Descendants("technical").Elements("fret").Select(item => item.Value).SequenceEqual(new[] { "0", "3", "3", "3" }),
                "MusicXML exporta cordas e casas estruturadas");
            xml.Position = 0;
            var imported = MusicXmlImport.Read(xml, "Tablatura").Project.Parts[0].Staves[0];
            var importedNotes = imported.Where(note => !note.IsRest).ToArray();
            Assert(importedNotes.Select(note => note.TabString).SequenceEqual(new[] { 1, 6, 1, 2 }) && importedNotes[1].Midi == 43,
                "MusicXML reimporta tablatura e notas graves de violão");
        }
        using (var image = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
            ScorePage.Draw(graphics, score, 0, null, -1);
            Assert(Enumerable.Range(260, 220).Any(x => image.GetPixel(x, ScoreLayout.FirstStaffTop + 55).R < 150),
                "Página desenha as seis linhas da tablatura");
            image.Save(Path.Combine(directory, "tablatura.png"));
        }

        var guitar = new Score { Title = "Guitarra Drop D", Author = "Teste" };
        guitar.AddPart("Guitarra");
        guitar.ConfigureTablature(0, "drop-d", 2);
        var guitarLow = guitar.PutTab(0, 0, 0, 4, 6, 0);
        guitar.Validate();
        Assert(guitarLow.Midi == 40 && guitar.Parts[0].Capo == 2 && guitar.Parts[0].TabTuning == "drop-d",
            "Guitarra usa Drop D e capo para calcular a altura soante");
        var restoredGuitar = Score.Restore(guitar.Snapshot());
        Assert(restoredGuitar.Parts[0].Instrument == "Guitarra" && restoredGuitar.Parts[0].TabTuning == "drop-d" &&
            restoredGuitar.Parts[0].Capo == 2 && restoredGuitar.Parts[0].Staves[0][0].Midi == 40,
            "Projeto preserva instrumento, afinação e capo da guitarra");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(guitar, xml);
            xml.Position = 0;
            var document = XDocument.Load(xml);
            Assert(document.Descendants("staff-details").Elements("staff-lines").Single().Value == "6" &&
                document.Descendants("staff-details").Elements("capo").Single().Value == "2" &&
                document.Descendants("staff-tuning").Count() == 6,
                "MusicXML descreve linhas, afinação e capo da guitarra");
            xml.Position = 0;
            var importedGuitar = MusicXmlImport.Read(xml, "Guitarra").Project;
            Assert(importedGuitar.Parts[0].Instrument == "Guitarra" && importedGuitar.Parts[0].TabTuning == "drop-d" &&
                importedGuitar.Parts[0].Capo == 2 && importedGuitar.Parts[0].Staves[0][0].Midi == 40,
                "MusicXML reimporta guitarra, Drop D e capo");
        }

        var bass = new Score { Title = "Baixo Si", Author = "Teste" };
        bass.AddPart("Baixo");
        bass.ConfigureTablature(0, "low-b", 0);
        var lowB = bass.PutTab(0, 0, 0, 4, 5, 0);
        bass.Validate();
        Assert(bass.Parts[0].TabTuning == "low-b" && lowB.TabString == 5 && lowB.Midi == 23,
            "Baixo de cinco cordas escreve o Si grave na quinta corda");
        using (var image = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
            ScorePage.Draw(graphics, bass, 0, null, -1);
            Assert(Enumerable.Range(260, 220).Any(x => image.GetPixel(x, ScoreLayout.FirstStaffTop + 87).R < 150),
                "Página adapta a quantidade de linhas ao baixo de cinco cordas");
        }

        var techniques = new Score { Title = "Técnicas de guitarra", Author = "Teste" };
        techniques.AddPart("Guitarra");
        var bend = techniques.PutTab(0, 0, 0, 4, 1, 3);
        var slide = techniques.PutTab(0, 0, 4, 4, 2, 5);
        var harmonic = techniques.PutTab(0, 0, 8, 4, 3, 12);
        var mute = techniques.PutTab(0, 0, 12, 4, 4, 2);
        techniques.SetStringTechnique(0, 0, 0, "bend", bend.Id);
        techniques.SetStringTechnique(0, 0, 4, "slide-down", slide.Id);
        techniques.SetStringTechnique(0, 0, 8, "natural-harmonic", harmonic.Id);
        techniques.SetStringTechnique(0, 0, 12, "palm-mute", mute.Id);
        techniques.Validate();
        var restoredTechniques = Score.Restore(techniques.Snapshot());
        Assert(restoredTechniques.Parts[0].Staves[0].Where(note => !note.IsRest).Select(note => note.StringTechnique)
            .SequenceEqual(new[] { "bend", "slide-down", "natural-harmonic", "palm-mute" }),
            "Projeto preserva as técnicas de corda da tablatura");
        techniques.PasteBar(1, techniques.CopyBar(0));
        Assert(techniques.Parts[0].Staves[0].Where(note => note.Tick >= 16 && !note.IsRest).Select(note => note.StringTechnique)
            .SequenceEqual(new[] { "bend", "slide-down", "natural-harmonic", "palm-mute" }),
            "Cópia de compasso conserva as técnicas de corda");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(restoredTechniques, xml);
            xml.Position = 0;
            var document = XDocument.Load(xml);
            Assert(document.Descendants("other-technical").Select(item => item.Value)
                .SequenceEqual(new[] { "bend", "slide-down", "natural-harmonic", "palm-mute" }) &&
                document.Descendants("bend").Any() && document.Descendants("harmonic").Elements("natural").Any() && document.Descendants("slide").Any(),
                "MusicXML exporta técnicas de corda estruturadas");
            xml.Position = 0;
            var importedTechniques = MusicXmlImport.Read(xml, "Técnicas").Project.Parts[0].Staves[0].Where(note => !note.IsRest).ToArray();
            Assert(importedTechniques.Select(note => note.StringTechnique).SequenceEqual(new[] { "bend", "slide-down", "natural-harmonic", "palm-mute" }),
                "MusicXML reimporta as técnicas de corda");
        }
        var plainTechniques = Score.Restore(restoredTechniques.Snapshot());
        foreach (var note in plainTechniques.Parts[0].Staves[0]) note.StringTechnique = null;
        using (var withTechniques = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var withoutTechniques = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var withGraphics = Graphics.FromImage(withTechniques))
        using (var withoutGraphics = Graphics.FromImage(withoutTechniques))
        {
            withGraphics.Clear(Color.White);
            withoutGraphics.Clear(Color.White);
            ScorePage.Draw(withGraphics, restoredTechniques, 0, null, -1);
            ScorePage.Draw(withoutGraphics, plainTechniques, 0, null, -1);
            Assert(Enumerable.Range(120, 300).Any(x => Enumerable.Range(ScoreLayout.FirstStaffTop + 20, 90)
                .Any(y => withTechniques.GetPixel(x, y) != withoutTechniques.GetPixel(x, y))),
                "Página desenha sinais gráficos para técnicas de corda");
        }
    }

    private static void VoiceChecks(string directory)
    {
        var legacy = Score.New(false);
        legacy.Version = 9;
        legacy.AddChordNote(0, 0, 0, 4, 28, 0);
        legacy.AddChordNote(0, 0, 0, 4, 30, 0);
        var migrated = Score.Restore(legacy.Snapshot());
        Assert(migrated.Version == Score.CurrentVersion && migrated.Parts[0].Staves[0].Count == 2 &&
            migrated.Parts[0].Staves[0].All(n => n.Voice == 0), "Projetos 0.16 mantêm os acordes na primeira voz");
        var score = Score.New(false);
        score.Put(0, 0, 0, 4, 36, 0);
        score.Put(0, 0, 4, 4, 37, 0);
        score.Put(0, 0, 0, 8, 34, 0, voice: 1);
        score.AddChordNote(0, 0, 0, 8, 32, 0, voice: 1);
        score.PutRest(0, 0, 8, 8, voice: 1);
        score.Put(0, 1, 0, 16, 22, 0);
        var lane = score.Parts[0].Staves[0];
        var lower = lane.First(n => n.Voice == 1);
        score.SetExpression(0, 0, 0, null, "p", lower.Id);
        bool blocked = false;
        try { score.Put(0, 0, 2, 4, 35, 0, voice: 1); }
        catch (InvalidOperationException) { blocked = true; }
        Assert(blocked && lane.Count == 5 && lane.Count(n => n.Tick == 0 && n.Length == 8) == 2,
            "Vozes sobrepõem durações e rejeitam conflito dentro da mesma voz");
        score.EditNote(0, 0, lower, 6, 34, 0);
        Assert(lane.Count(n => n.Voice == 1 && n.Length == 6) == 2 && lane[0].Length == 4,
            "Editar a duração do acorde da segunda voz preserva a primeira");
        string path = Path.Combine(directory, "duas-vozes.valescore");
        score.Save(path);
        Assert(Score.Open(path).Snapshot().SequenceEqual(score.Snapshot()), "Projeto salva e reabre duas vozes independentes");
        var bars = Score.Restore(score.Snapshot());
        bars.PasteBar(1, bars.CopyBar(0));
        bars.DuplicateBar(1);
        Assert(bars.Parts[0].Staves[0].Count(n => n.Voice == 1 && n.Tick >= 16 && n.Tick < 48) == 6,
            "Copiar e duplicar compassos preserva a voz de cada figura");
        using (var sheet = new ScorePage())
        {
            sheet.LoadScore(score);
            float x = sheet.Engraving.MeasureOf(0).At(0);
            int y = sheet.Engraving.StaffTop(0, 0);
            var highHit = sheet.Hit(x - 7, y + 10);
            var lowHit = sheet.Hit(x + 7, y + 20);
            Assert(highHit != null && lowHit != null && highHit.HitNote && lowHit.HitNote && highHit.Voice == 0 && lowHit.Voice == 1,
                "Clique distingue as cabeças das duas vozes no mesmo instante");
        }
        var melody = Score.Restore(score.Snapshot());
        melody.Parts[0].Staves[0].RemoveAll(n => n.Voice == 1);
        using (var combined = Output.Wave(score, false))
        using (var alone = Output.Wave(melody, false))
            Assert(combined.Length == alone.Length && !combined.ToArray().SequenceEqual(alone.ToArray()),
                "O som interno e o WAV incluem o acompanhamento da segunda voz");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(score, xml);
            xml.Position = 0;
            var measure = XDocument.Load(xml).Descendants("measure").First();
            Assert(measure.Elements("backup").Count() == 2 &&
                measure.Elements("note").Any(n => (string)n.Element("voice") == "3" && (int)n.Element("duration") == 6 &&
                    (string)n.Element("stem") == "down") &&
                measure.Elements("note").Any(n => (string)n.Element("voice") == "1" && (string)n.Element("stem") == "up"),
                "MusicXML usa recuos e identificadores separados para duas vozes e pauta inferior");
            xml.Position = 0;
            var parsed = MusicXmlImport.Read(xml, "Duas vozes");
            var restored = parsed.Project.Parts[0].Staves[0];
            Assert(parsed.Omitted == 0 && restored.Count(n => !n.IsRest && n.Voice == 1 && n.Length == 6) == 2 &&
                restored.Any(n => n.Voice == 1 && n.Tick == 8 && n.IsRest) &&
                restored.Any(n => n.Voice == 1 && n.Dynamic == "p") && restored.Any(n => n.Voice == 0 && n.Tick == 4 && !n.IsRest),
                "Reimportação MusicXML mantém duração, pausa e dinâmica da segunda voz");
        }
        using (var file = new MemoryStream())
        {
            MidiSequence.Export(score, file);
            file.Position = 0;
            var sequence = MidiSequence.Read(file);
            Assert(sequence.Events.Any(n => (n.Status & 240) == 0x80 && n.Data1 == lower.Midi && n.Tick == 720),
                "MIDI mantém o fim da nota longa da segunda voz");
            var converted = MidiImport.Convert(sequence, "Vozes sobrepostas");
            Assert(converted.Skipped == 0 && converted.Project.Parts[0].Staves[0].Any(n => n.Voice == 1) &&
                converted.Project.Parts[0].Staves[0].Any(n => n.Tick == 0 && n.Midi == lower.Midi && n.Length == 6),
                "Importação MIDI usa a segunda voz para sobreposições que exigem ritmos distintos");
        }
    }

    private static void ChordChecks(string directory)
    {
        var score = Score.New(false);
        score.Put(0, 0, 0, 4, 28, 0);
        var middle = score.AddChordNote(0, 0, 0, 8, 30, 0);
        var top = score.AddChordNote(0, 0, 0, 2, 31, 0);
        score.Put(0, 0, 8, 4, 32, 0);
        var lane = score.Parts[0].Staves[0];
        Assert(lane.Take(3).All(n => n.Tick == 0 && n.Length == 4) && score.AddChordNote(0, 0, 0, 4, 30, 0) == middle,
            "Acorde herda a duração e evita altura duplicada");
        score.EditNote(0, 0, middle, 6, 30, -1);
        bool blocked = false;
        try { score.EditNote(0, 0, middle, 12, 29, 0); }
        catch (InvalidOperationException) { blocked = true; }
        Assert(blocked && lane.Take(3).All(n => n.Length == 6) && middle.Alter == -1,
            "Duração comum e acidente individual são editáveis sem sobreposição");
        score.SetCourtesy(0, 0, 0, true, middle.Id);
        score.SetExpression(0, 0, 0, null, "mf", top.Id);
        string projectPath = Path.Combine(directory, "acorde.valescore");
        score.Save(projectPath);
        Assert(Score.Open(projectPath).Snapshot().SequenceEqual(score.Snapshot()), "Projeto reabre acorde com IDs, alturas e duração");
        score.PasteBar(1, score.CopyBar(0));
        Assert(lane.Count(n => n.Tick == score.BarTicks) == 3 && lane.Select(n => n.Id).Distinct().Count() == lane.Count,
            "Copiar compasso conserva o acorde e gera IDs distintos");
        using (var sheet = new ScorePage())
        {
            sheet.LoadScore(score);
            int topY = sheet.Engraving.StaffTop(0, 0);
            float x = sheet.Engraving.MeasureOf(0).At(0);
            var bottomHit = sheet.Hit(x, topY + 50);
            var topHit = sheet.Hit(x + 10.5f, topY + 35);
            Assert(bottomHit != null && topHit != null && bottomHit.NoteId == lane[0].Id && topHit.NoteId == top.Id,
                "Cabeças deslocadas de um acorde podem ser selecionadas separadamente");
        }
        using (var file = new MemoryStream())
        {
            Output.MusicXml(score, file);
            file.Position = 0;
            var doc = XDocument.Load(file);
            Assert(doc.Descendants("chord").Count() == 4, "MusicXML usa membros de acorde sem avançar o cursor");
            file.Position = 0;
            var converted = MusicXmlImport.Read(file, "Acorde").Project.Parts[0].Staves[0].Where(n => !n.IsRest).ToList();
            Assert(converted.Count == lane.Count && converted.Select(n => n.Tick).SequenceEqual(lane.Select(n => n.Tick)) &&
                converted.Select(n => n.Midi).SequenceEqual(lane.Select(n => n.Midi)) && converted[1].Courtesy &&
                converted[3].Tick == 8, "MusicXML preserva acorde, acidente de cortesia e nota seguinte");
        }
        using (var file = new MemoryStream())
        {
            MidiSequence.Export(score, file);
            file.Position = 0;
            var midi = MidiSequence.Read(file);
            var attacks = midi.Events.Where(e => (e.Status & 240) == 0x90 && e.Data2 != 0 && e.Tick == 0).ToArray();
            Assert(attacks.Length == 3 && attacks.All(e => e.Data2 == Score.Velocity("mf")),
                "MIDI toca as três alturas simultâneas com a mesma dinâmica");
            var imported = MidiImport.Convert(midi, "Acorde");
            Assert(imported.Skipped == 0 && imported.Project.Parts[0].Staves[0].Count(n => n.Tick == 0) == 3,
                "Importação MIDI conserva notas simultâneas de igual duração na mesma pauta");
        }
        using (var wave = Output.Wave(score, false))
            Assert(wave.ToArray().Skip(44).Any(b => b != 0), "Acorde produz sinal no sintetizador interno");
    }

    private static void ExpressionMarkChecks(string directory)
    {
        var score = Score.New(false);
        score.Put(0, 0, 0, 4, 28, 0);
        score.Put(0, 0, 4, 4, 30, 0);
        score.Put(0, 0, 8, 4, 32, 0);
        score.SetPauseMark(0, 0, 0, "fermata");
        score.SetPauseMark(0, 0, 4, "breath-mark");
        score.SetPauseMark(0, 0, 8, "caesura");
        score.SetExpressionText(0, 0, 4, "dolce, legato");
        score.SetRehearsalMark(0, "A");
        score.SetRehearsalMark(1, "B");
        score.Validate();
        var native = Score.Restore(score.Snapshot());
        Assert(native.RehearsalMarks.Select(mark => mark.Text).SequenceEqual(new[] { "A", "B" }) &&
            native.Parts[0].Staves[0].Select(note => note.PauseMark).SequenceEqual(new[] { "fermata", "breath-mark", "caesura" }) &&
            native.Parts[0].Staves[0][1].ExpressionText == "dolce, legato",
            "Projeto preserva marcas de ensaio, textos expressivos e sinais de pausa");

        using (var midi = new MemoryStream())
        {
            MidiSequence.Export(score, midi);
            midi.Position = 0;
            var releases = MidiSequence.Read(midi).Events.Where(item => (item.Status & 240) == 0x80).ToArray();
            Assert(releases.Any(item => item.Data1 == native.Parts[0].Staves[0][0].Midi && item.Tick == 648),
                "Fermata prolonga a nota na reprodução MIDI");
        }
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(score, xml);
            xml.Position = 0;
            var written = XDocument.Load(xml);
            Assert(written.Descendants("rehearsal").Select(mark => mark.Value).SequenceEqual(new[] { "A", "B" }) &&
                written.Descendants("words").Any(mark => mark.Value == "dolce, legato") &&
                written.Descendants("fermata").Any() && written.Descendants("breath-mark").Any() && written.Descendants("caesura").Any(),
                "MusicXML exporta marcas, textos e sinais de pausa estruturados");
            xml.Position = 0;
            var imported = MusicXmlImport.Read(xml, "Expressões");
            var lane = imported.Project.Parts[0].Staves[0];
            Assert(imported.Omitted == 0 && imported.Project.RehearsalMarks.Select(mark => mark.Text).SequenceEqual(new[] { "A", "B" }) &&
                lane.Where(note => !note.IsRest).Select(note => note.PauseMark).SequenceEqual(new[] { "fermata", "breath-mark", "caesura" }) &&
                lane.First(note => !note.IsRest && note.Tick == 4).ExpressionText == "dolce, legato", "MusicXML reimporta marcas, textos e sinais de pausa");
        }
        var duplicate = Score.Restore(score.Snapshot());
        duplicate.DuplicateBar(0);
        Assert(duplicate.RehearsalMarks.Select(mark => mark.Bar + mark.Text).SequenceEqual(new[] { "0A", "1A", "2B" }),
            "Duplicar compasso reposiciona e replica a marca de ensaio");
        using (var image = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var canvas = Graphics.FromImage(image))
        {
            ScorePage.Draw(canvas, score, 0, null, -1);
            Assert(Enumerable.Range(ScoreLayout.FirstStaffTop - 50, 22).Any(y =>
                Enumerable.Range(50, 42).Any(x => image.GetPixel(x, y).R < 180)),
                "Gravação desenha marca de ensaio e símbolos expressivos");
            image.Save(Path.Combine(directory, "marcas-expressoes.png"));
        }
    }

    private static void Run(string directory)
    {
        var mix = Score.New(true);
        mix.Parts[0].Muted = true;
        using (var silent = Output.Wave(mix))
            Assert(silent.ToArray().Skip(44).All(b => b == 0), "Mixer silencia o instrumento no WAV");
        using (var file = new MemoryStream())
        {
            MidiSequence.Export(mix, file);
            file.Position = 0;
            Assert(!MidiSequence.Read(file).Events.Any(e => (e.Status & 240) == 0x90 && e.Data2 > 0),
                "Mixer silencia os ataques na exportação MIDI");
        }
        mix.Parts[0].Muted = false;
        mix.Parts[0].Volume = 35;
        mix.AddPart("Voz");
        mix.Parts[1].Solo = true;
        Assert(mix.PlaybackGain(mix.Parts[0]) == 0 && mix.PlaybackGain(mix.Parts[1]) == 1,
            "Solo isola o instrumento escolhido");
        var mixCopy = Score.Restore(mix.Snapshot());
        Assert(mixCopy.Parts[0].Volume == 35 && mixCopy.Parts[1].Solo, "Projeto reabre os controles do mixer");
        var stereoScore = Score.New(true);
        foreach (int pan in new[] { -100, 100 })
        {
            stereoScore.Parts[0].Pan = pan;
            using (var wave = Output.Wave(stereoScore))
            {
                var bytes = wave.ToArray();
                Assert(BitConverter.ToInt16(bytes, 22) == 2, "Pan gera WAV estéreo");
                int silentOffset = pan < 0 ? 2 : 0;
                Assert(Enumerable.Range(0, (bytes.Length - 44) / 4).All(i =>
                    BitConverter.ToInt16(bytes, 44 + i * 4 + silentOffset) == 0),
                    "Pan extremo silencia o canal oposto");
            }
        }
        Assert(Score.Restore(stereoScore.Snapshot()).Parts[0].Pan == 100, "Projeto conserva panorâmica");
        using (var file = new MemoryStream())
        {
            MidiSequence.Export(stereoScore, file);
            file.Position = 0;
            Assert(MidiSequence.Read(file).Events.Any(e => (e.Status & 240) == 0xB0 && e.Data1 == 10 && e.Data2 == 127),
                "Exportação MIDI registra panorâmica por instrumento");
        }
        SlurChecks(directory);
        TieChecks(directory);
        TablatureChecks(directory);
        ExpressionMarkChecks(directory);
        VoiceChecks(directory);
        ChordChecks(directory);
        var score = Score.New(true);
        score.Validate();
        Assert(ScoreTemplate.Library.Length == 8 && ScoreTemplate.Library.All(template => template.Create().Parts.Count >= 1),
            "Oito modelos geram projetos válidos");
        var ensemble = ScoreTemplate.Library.Last().Create();
        Assert(ensemble.Parts.Select(part => part.Staves.Count).SequenceEqual(new[] { 2, 2, 2, 1 }),
            "Modelo para três pianos e violão abre sete pautas");
        ensemble.RenamePart(0, "Piano principal");
        ensemble.SetMeter(6, 8);
        ensemble.SetKey(2, false);
        ensemble.Put(0, 0, 0, 4, 28, 0);
        var blankArrangement = ensemble.EmptyArrangement();
        Assert(blankArrangement.Parts[0].Name == "Piano principal" && blankArrangement.BarTicks == 12 &&
            blankArrangement.Fifths == 2 && blankArrangement.Parts.All(part => part.Staves.All(lane => lane.Count == 0)) &&
            ensemble.Parts[0].Staves[0].Count == 1, "Formação reutilizada abre vazia e preserva a original");
        var publicationScore = Score.New(true);
        publicationScore.Bars = 8;
        publicationScore.AddPart("Piano");
        publicationScore.AddPart("Piano");
        publicationScore.AddPart("Piano");
        int margin = Publication.Margin(12);
        var fullPrint = new Publication(publicationScore, null, 0, false, margin);
        var partPrint = new Publication(publicationScore, 0, 0, true, margin);
        Assert(fullPrint.PageCount > partPrint.PageCount && partPrint.Title.EndsWith(" - Piano"),
            "Prévia de parte recalcula as páginas e identifica o instrumento");
        var physical = Publication.Paper(0, true);
        var printArea = new RectangleF(margin, margin, physical.Width - 2 * margin, physical.Height - 2 * margin);
        var fit = partPrint.Fit(printArea);
        Assert(physical.Width == 1169 && physical.Height == 827 && partPrint.LayoutWidth > partPrint.LayoutHeight &&
            fit.Left >= printArea.Left - .01f && fit.Right <= printArea.Right + .01f && fit.Bottom <= printArea.Bottom + .01f,
            "Layout em paisagem respeita a proporção do papel e as margens");
        using (var pngPages = new MemoryStream())
        {
            partPrint.ExportPngPages(pngPages);
            pngPages.Position = 0;
            using (var archive = new System.IO.Compression.ZipArchive(pngPages, System.IO.Compression.ZipArchiveMode.Read))
            {
                Assert(archive.Entries.Count == partPrint.PageCount && archive.Entries[0].Name == "pagina-01.png",
                    "Exportação PNG inclui todas as páginas do instrumento");
                using (var imageBytes = new MemoryStream())
                {
                    using (var entry = archive.Entries[0].Open()) entry.CopyTo(imageBytes);
                    imageBytes.Position = 0;
                    using (var image = new Bitmap(imageBytes))
                        Assert(image.Width == physical.Width * 2 && image.Height == physical.Height * 2 && image.GetPixel(5, 5).R == 255,
                            "Imagem exportada usa papel escolhido e fundo branco");
                }
            }
        }
        var practice = Score.New(false);
        practice.SetMeter(6, 8);
        practice.SetKey(-2, false);
        practice.Put(0, 0, 0, 4, 28, 0);
        practice.SetExpression(0, 0, 0, null, "p");
        practice.Put(0, 0, 14, 2, 29, 0);
        practice.SetArticulation(0, 0, 14, "staccato");
        practice.Put(0, 0, 24, 4, 31, 0);
        practice.SetExpression(0, 0, 24, null, "f");
        var uncropped = practice.Snapshot();
        var passage = practice.ExtractBars(1, 2);
        var practiceNotes = passage.Parts[0].Staves[0];
        Assert(passage.Bars == 2 && passage.BarTicks == 12 && passage.Fifths == -2 &&
            practiceNotes.Select(n => n.Tick).SequenceEqual(new[] { 2, 12 }) && uncropped.SequenceEqual(practice.Snapshot()),
            "Trecho preserva pulso, armadura e posições relativas sem alterar o original");
        Assert(practiceNotes[0].Dynamic == "p" && practiceNotes[0].Articulation == "staccato" &&
            practiceNotes[1].Dynamic == "f", "Trecho conserva dinâmica anterior e marcas internas");
        var loop = new PlaybackRange(practice, 1, 2, .75, true);
        var once = new PlaybackRange(practice, 1, 2, .75, false);
        Assert(loop.TickAt(0) == 12 && loop.TickAt(loop.Duration + 3.25 * loop.TickSeconds) == 15 &&
            once.TickAt(10 * once.Duration) == 35, "Cursor repete no compasso de início e para no fim sem loop");
        var practiceLayout = new ScoreLayout(practice);
        var cursorMeasure = practiceLayout.MeasureOf(1);
        var onPage = new StaffPosition { Tick = 12 };
        var cursorPoint = practiceLayout.Position(practice, onPage);
        using (var frame = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.White);
            ScorePage.Draw(graphics, practice, practiceLayout, practiceLayout.PageOf(12), null, 12, null);
            Assert(Enumerable.Range((int)cursorMeasure.At(0) - 1, 4)
                .Any(x => { var pixel = frame.GetPixel(x, (int)cursorPoint.Y + 43); return pixel.B > pixel.R + 8; }),
                "Cursor de reprodução aparece também em posição silenciosa");
        }
        using (var wave = Output.Wave(passage, false, .75))
        using (var midi = new MemoryStream())
        {
            MidiSequence.Export(passage, midi);
            midi.Position = 0;
            var sequence = MidiSequence.Read(midi);
            var firstAttack = sequence.Events.First(e => (e.Status & 240) == 0x90);
            Assert(Math.Abs((wave.Length - 44) / 2.0 / Output.SampleRate - loop.Duration) < .001 &&
                Math.Abs(sequence.Duration / .75 - loop.Duration) < .001 && firstAttack.Tick == 240 && firstAttack.Data2 == 49,
                "Áudio e MIDI do trecho respeitam duração, pausa inicial e dinâmica");
        }
        var tonal = Score.New(false);
        tonal.SetMeter(6, 8);
        tonal.SetKey(-2, true);
        Assert(tonal.InputAccidental(0, 0, 0, 34) == -1 && tonal.InputAccidental(0, 0, 0, 30) == -1,
            "Armadura de dois bemóis afeta Si e Mi");
        tonal.Put(0, 0, 0, 4, 34, 0);
        Assert(tonal.InputAccidental(0, 0, 4, 34) == 0 && tonal.InputAccidental(0, 0, 12, 34) == -1,
            "Bequadro prevalece na oitava até a barra seguinte");
        tonal.Put(0, 0, 4, 4, 28, 2);
        tonal.SetCourtesy(0, 0, 4, true);
        var enharmonic = tonal.Parts[0].Staves[0][1];
        Assert(tonal.Respelling(0, 0, 4, 1) && enharmonic.Step == 29 && enharmonic.Alter == 0 && enharmonic.Midi == 62 &&
            tonal.Respelling(0, 0, 4, -1) && enharmonic.Alter == 2 && enharmonic.Courtesy,
            "Enarmonia preserva altura soante e acidente de cortesia");
        var tonalLayout = new ScoreLayout(tonal);
        Assert(tonalLayout.MeasureLeft > ScoreLayout.StaffLeft, "Pauta reserva espaço para a armadura");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(tonal, xml);
            xml.Position = 0;
            var read = MusicXmlImport.Read(xml, "Tonal");
            var figure = read.Project.Parts[0].Staves[0][1];
            Assert(read.Project.Fifths == -2 && read.Project.MinorKey && figure.Alter == 2 && figure.Courtesy &&
                read.Project.BarTicks == 12, "MusicXML preserva armadura, modo e dobrado sustenido de cortesia");
        }
        using (var midi = new MemoryStream())
        {
            MidiSequence.Export(tonal, midi);
            midi.Position = 0;
            var read = MidiSequence.Read(midi);
            Assert(read.InitialFifths == -2 && read.InitialMinorKey &&
                read.Events.Any(e => (e.Status & 240) == 0x90 && e.Data1 == 62) &&
                MidiImport.Convert(read, "Tonal").Project.Fifths == -2, "MIDI preserva armadura inicial e alturas");
        }
        var olderMeter = Score.New(false);
        olderMeter.SetMeter(9, 8);
        olderMeter.Version = 7;
        Assert(Score.Restore(olderMeter.Snapshot()).BarTicks == 18, "Migração conserva a fórmula de projetos 0.10");
        var meterScore = Score.New(false);
        meterScore.Put(0, 0, 16, 2, 29, 0);
        meterScore.SetMeter(6, 8);
        var moved = meterScore.Parts[0].Staves[0][0];
        Assert(meterScore.BarTicks == 12 && meterScore.BeatTicks == 6 && moved.Tick == 12,
            "Mudança para 6/8 preserva a posição dentro do compasso");
        meterScore.Put(0, 0, 14, 2, 30, 0);
        meterScore.Put(0, 0, 16, 2, 31, 0);
        meterScore.Put(0, 0, 18, 2, 32, 0);
        Assert(Score.BeamGroupEnd(meterScore.Parts[0].Staves[0], 0, 12, 6) == 3,
            "6/8 agrupa três colcheias sem atravessar o tempo");
        var beforeMeter = meterScore.Snapshot();
        meterScore.Put(0, 0, 20, 2, 33, 0);
        var beforeRejected = meterScore.Snapshot();
        bool rejectedMeter = false;
        try { meterScore.SetMeter(2, 4); }
        catch (InvalidOperationException) { rejectedMeter = true; }
        Assert(rejectedMeter && beforeRejected.SequenceEqual(meterScore.Snapshot()),
            "Encurtamento de compasso incompatível é atômico");
        meterScore = Score.Restore(beforeMeter);
        meterScore.SetMeter(12, 8);
        Assert(meterScore.BarTicks == 24 && meterScore.Parts[0].Staves[0][0].Tick == 24 &&
            new ScoreLayout(meterScore).MeasureOf(0).Nearest(10000) == 23,
            "12/8 remapeia notas e expõe todas as posições na pauta");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(meterScore, xml);
            xml.Position = 0;
            var written = XDocument.Load(xml);
            var time = written.Descendants("time").Single();
            Assert((int)time.Element("beats") == 12 && (int)time.Element("beat-type") == 8 &&
                written.Descendants("backup").All(e => (int)e.Element("duration") == 24) &&
                written.Descendants("rest").Any(e => (string)e.Attribute("measure") == "yes"),
                "MusicXML registra 12/8 e pausas de compasso corretamente");
            xml.Position = 0;
            var roundTrip = MusicXmlImport.Read(xml, "Composto");
            Assert(roundTrip.Omitted == 0 && roundTrip.Project.BarTicks == 24 &&
                roundTrip.Project.Parts[0].Staves[0].First(n => !n.IsRest).Tick == 24,
                "Importação MusicXML preserva métrica composta e posição");
        }
        using (var midi = new MemoryStream())
        {
            MidiSequence.Export(meterScore, midi);
            midi.Position = 0;
            var sequence = MidiSequence.Read(midi);
            Assert(sequence.InitialBeats == 12 && sequence.InitialBeatType == 8 &&
                Math.Abs(sequence.Duration - 15) < .001 && MidiImport.Convert(sequence, "Composto").Project.BarTicks == 24,
                "MIDI conserva fórmula e duração da partitura em 12/8");
        }
        var emptyClick = Score.New(false);
        emptyClick.SetMeter(6, 8);
        using (var wave = Output.Wave(emptyClick, true))
        using (var silent = Output.Wave(emptyClick))
        using (var slower = Output.Wave(emptyClick, true, .5))
        {
            var samples = wave.ToArray();
            int pulse = (int)Math.Round(Output.SampleRate * 90.0 / emptyClick.Tempo);
            Assert(samples.Skip(44 + pulse * 2).Take(200).Any(value => value != 0) &&
                samples.Skip(44 + pulse).Take(200).All(value => value == 0) &&
                silent.ToArray().Skip(44).All(value => value == 0),
                "Metrônomo de 6/8 marca semínimas pontuadas e fica fora da exportação WAV");
            Assert(Math.Abs((slower.Length - 44) - (wave.Length - 44) * 2) <= 2,
                "Velocidade de 50% dobra a duração da reprodução");
        }
        var expression = Score.New(false);
        expression.Put(0, 0, 0, 4, 28, 0);
        expression.Put(0, 0, 4, 4, 29, 0);
        expression.Put(0, 0, 8, 4, 30, 0);
        expression.SetExpression(0, 0, 0, "G7/B", "ppp");
        expression.SetExpression(0, 0, 8, "Bbadd9", "fff");
        expression.SetArticulation(0, 0, 0, "staccato");
        expression.SetArticulation(0, 0, 4, "tenuto");
        expression.SetArticulation(0, 0, 8, "strong-accent");
        expression.Validate();
        var markedLayout = new ScoreLayout(expression);
        using (var image = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var canvas = Graphics.FromImage(image))
        {
            canvas.Clear(Color.White);
            ScorePage.Draw(canvas, expression, markedLayout, 0, null, -1, null);
            int x = (int)markedLayout.MeasureOf(0).At(0);
            Func<int, bool> hasInk = top => Enumerable.Range(x - 18, 37)
                .Any(px => Enumerable.Range(top, 15).Any(py => image.GetPixel(px, py).R < 110));
            Assert(hasInk(ScoreLayout.FirstStaffTop - 37) && hasInk(ScoreLayout.FirstStaffTop + 54),
                "Cifra acima e dinâmica abaixo da pauta são desenhadas");
            Assert(image.GetPixel(x, ScoreLayout.FirstStaffTop - 8).R < 110,
                "Staccato fica separado da dinâmica sob a nota");
            image.Save(Path.Combine(directory, "expressao.png"));
        }
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(expression, xml);
            xml.Position = 0;
            var converted = MusicXmlImport.Read(xml, "Expressão");
            var figures = converted.Project.Parts[0].Staves[0];
            Assert(converted.Omitted == 0 && figures[0].Chord == "G7/B" && figures[0].Dynamic == "ppp" &&
                figures[2].Chord == "Bbadd9" && figures[2].Dynamic == "fff",
                "MusicXML preserva cifras estruturadas, baixo alternativo e dinâmicas");
            Assert(figures.Take(3).Select(n => n.Articulation).SequenceEqual(new[] { "staccato", "tenuto", "strong-accent" }),
                "MusicXML preserva articulações");
        }
        using (var midi = new MemoryStream())
        {
            MidiSequence.Export(expression, midi);
            midi.Position = 0;
            var events = MidiSequence.Read(midi).Events;
            var attacks = events.Where(e => (e.Status & 240) == 0x90);
            Assert(attacks.Select(e => e.Data2).SequenceEqual(new[] { 24, 24, 127 }),
                "Velocidade MIDI acompanha a dinâmica até a próxima marca");
            Assert(events.Where(e => (e.Status & 240) == 0x80).Select(e => e.Tick).SequenceEqual(new long[] { 264, 960, 1320 }),
                "MIDI encurta staccato e marcato e conserva o tenuto");
        }
        using (var wave = Output.Wave(expression))
        {
            byte[] data = wave.ToArray();
            int beat = (int)Math.Round(Output.SampleRate * 60.0 / expression.Tempo);
            Func<int, int> peak = beatIndex => Enumerable.Range(0, beat - 1)
                .Max(i => Math.Abs((int)BitConverter.ToInt16(data, 44 + ((beatIndex * beat) + i) * 2)));
            Assert(peak(2) > peak(0) * 3, "Dinâmica altera o volume do WAV");
            Assert(Enumerable.Range(beat * 3 / 4, beat / 8).All(i => BitConverter.ToInt16(data, 44 + i * 2) == 0),
                "WAV deixa silêncio após o staccato");
        }
        var copyExpression = Score.Restore(expression.Snapshot());
        copyExpression.PasteBar(1, expression.CopyBar(0));
        Assert(copyExpression.Parts[0].Staves[0].First(n => n.Tick == 16).Articulation == "staccato",
            "Projeto e cópia de compasso preservam a articulação");
        expression.Transpose(0, 0, null, 2, true);
        Assert(expression.Parts[0].Staves[0][0].Chord == "A7/C#" &&
            expression.Parts[0].Staves[0][2].Chord == "Cadd9", "Transposição move cifra e baixo alternativo");
        bool rejectedChord = false;
        try { expression.SetExpression(0, 0, 0, "acorde inválido", "mf"); }
        catch (InvalidOperationException) { rejectedChord = true; }
        Assert(rejectedChord && expression.Parts[0].Staves[0][0].Chord == "A7/C#", "Cifra inválida não altera a nota");
        var interchange = Score.New(false);
        interchange.Put(0, 0, 0, 7, 28, 1);
        interchange.SetLyric(0, 0, 0, "céu", false);
        interchange.PutRest(0, 0, 7, 3);
        interchange.Put(0, 1, 16, 12, 21, 0);
        var preserved = interchange.Snapshot();
        interchange.InsertBar(1);
        Assert(interchange.Bars == 5 && interchange.Parts[0].Staves[1][0].Tick == 32, "Inserção desloca o compasso posterior");
        interchange.RemoveBar(1);
        Assert(interchange.Snapshot().SequenceEqual(preserved), "Remover o compasso inserido restaura o projeto");
        var copied = interchange.CopyBar(0);
        var destination = Score.Restore(preserved);
        destination.PasteBar(2, copied);
        destination.Validate();
        var pasted = destination.Parts[0].Staves[0].First(n => n.Tick == 32);
        Assert(pasted.Length == 7 && pasted.Lyric == "céu" && pasted.Id != interchange.Parts[0].Staves[0][0].Id,
            "Copiar e colar compasso preserva pontos e letras com novos IDs");
        using (var xml = new MemoryStream())
        {
            Output.MusicXml(interchange, xml);
            xml.Position = 0;
            var written = XDocument.Load(xml);
            var notes = written.Descendants("note").ToArray();
            Assert(notes[0].Elements("dot").Count() == 2 && notes[1].Elements("dot").Count() == 1,
                "MusicXML distingue nota duplamente pontuada e pausa pontuada");
            xml.Position = 0;
            var result = MusicXmlImport.Read(xml, "Teste");
            var lanes = result.Project.Parts[0].Staves;
            Assert(result.Notes == 2 && result.Omitted == 0 && lanes[0][0].Length == 7 &&
                lanes[0][0].Midi == 61 && lanes[0][0].Lyric == "céu" && lanes[0][1].Length == 3 &&
                lanes[1].Any(n => !n.IsRest && n.Tick == 16 && n.Length == 12),
                "MusicXML reabre notas, letras, alturas e duas pautas");
        }
        string mxlPath = Path.Combine(directory, "intercambio.mxl");
        using (var mxl = File.Create(mxlPath)) Output.CompressedMusicXml(interchange, mxl);
        Assert(MusicXmlImport.Open(mxlPath).Notes == 2, "MXL exportado reabre pelo manifesto");
        bool unsafeXml = false;
        using (var external = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "<!DOCTYPE score-partwise [<!ENTITY externo SYSTEM 'file:///C:/Windows/win.ini'>]><score-partwise>&externo;</score-partwise>")))
        {
            try { MusicXmlImport.Read(external, "Entidade"); }
            catch (System.Xml.XmlException) { unsafeXml = true; }
        }
        Assert(unsafeXml, "Importação bloqueia resolução de entidades externas");
        var revised = Score.New(true);
        var firstNote = revised.Parts[0].Staves[0][0];
        var originalId = firstNote.Id;
        Assert(revised.Transpose(0, 0, 0, 1, true) > 0 && firstNote.Midi == 61 && firstNote.Id == originalId &&
            firstNote.Lyric == "O" && revised.Parts[0].Staves[0].First(n => n.Tick == 16).Midi == 69,
            "Transposição cromática preserva letra, identidade e compassos fora do intervalo");
        revised.Transpose(0, 0, 0, 1, false);
        Assert(firstNote.Step == 29 && firstNote.Alter == 1, "Transposição diatônica desloca o grau e conserva a alteração");
        var initial = revised.Snapshot();
        bool rejectedTranspose = false;
        try { revised.Transpose(0, 0, null, 24, true); }
        catch (InvalidOperationException) { rejectedTranspose = true; }
        Assert(rejectedTranspose && initial.SequenceEqual(revised.Snapshot()), "Transposição fora da extensão não altera o projeto");
        revised.DuplicateBar(0);
        revised.Validate();
        var duplicate = revised.Parts[0].Staves[0].First(n => n.Tick == 16);
        Assert(revised.Bars == 5 && duplicate.Id != originalId && duplicate.Midi == firstNote.Midi && duplicate.Lyric == "O",
            "Duplicar compasso desloca o material posterior e gera novas identidades");
        revised.AddPart("Violão");
        var extracted = revised.ExtractPart(0);
        Assert(extracted.Parts.Count == 1 && extracted.Parts[0].Staves.Count == 2 &&
            extracted.Parts[0].Staves[0][0].Id == originalId && revised.Parts.Count == 2,
            "Extração mantém as pautas e o conteúdo do instrumento em uma cópia independente");
        var service = new Recovery(Path.Combine(directory, "recovery-" + Guid.NewGuid().ToString("N")));
        service.Save(revised);
        string copy = Directory.GetFiles(service.Folder).Single();
        Assert(service.HasCopies() && Score.Open(copy).Snapshot().SequenceEqual(revised.Snapshot()), "Cópia de recuperação reabre integralmente");
        revised.Title = "Segunda revisão";
        service.Save(revised);
        Assert(Directory.GetFiles(service.Folder).Length == 1 && Score.Open(copy).Title == revised.Title, "Cópia da sessão é atualizada");
        service.Clear();
        Assert(!service.HasCopies(), "Salvamento normal remove a cópia da sessão");
        Directory.Delete(service.Folder);
        using (var png = new MemoryStream())
        {
            Output.PagePng(revised, new ScoreLayout(revised), 0, png);
            png.Position = 0;
            using (var image = new Bitmap(png))
                Assert(image.Width == 1560 && image.Height == 2200 && image.GetPixel(800, 368).R < 180,
                    "PNG exporta fundo, pauta e dimensões da página");
        }
        using (var buffer = new MemoryStream())
        {
            MidiSequence.Export(score, buffer);
            buffer.Position = 0;
            var midi = MidiSequence.Read(buffer);
            Assert(midi.Tracks == 2 && Math.Abs(midi.Duration - 10) < .001, "MIDI tipo 1 exporta pista, andamento e duração");
            var pitches = midi.Events.Where(e => (e.Status & 240) == 0x90 && e.Data2 != 0).ToArray();
            Assert(pitches.Length == score.Parts[0].Staves.Sum(lane => lane.Count(n => !n.IsRest)) &&
                pitches.Any(e => e.Data1 == 60 && e.Seconds == 0), "MIDI exporta alturas e preserva pausas");
            var imported = MidiImport.Convert(midi, "Prelúdio importado");
            Assert(imported.Imported == pitches.Length && imported.Skipped == 0 && imported.Quantized == 0 &&
                imported.Project.Bars == score.Bars && imported.Project.Tempo == score.Tempo,
                "MIDI exportado reabre como partitura editável");
            var converted = imported.Project.Parts.SelectMany(p => p.Staves).SelectMany(lane => lane).ToArray();
            Assert(pitches.All(e => converted.Any(n => n.Midi == e.Data1 && n.Tick * 120 == e.Tick)),
                "Conversão preserva alturas e posições na grade");
            string importedPath = Path.Combine(directory, "importado.valescore");
            imported.Project.Save(importedPath);
            Assert(Score.Open(importedPath).Snapshot().SequenceEqual(imported.Project.Snapshot()),
                "Partitura convertida pode ser salva e reaberta");
            using (var wave = Output.MidiWave(midi, System.Threading.CancellationToken.None))
                Assert(wave.ToArray().Skip(44).Any(b => b != 0), "MIDI produz sinal no sintetizador interno");
            var track = new byte[] { 0, 0x90, 60, 80, 96, 60, 0, 0, 0xFF, 0x51, 3, 0x0F, 0x42, 0x40,
                0, 0x90, 64, 80, 96, 64, 0, 0, 0xFF, 0x2F, 0 };
            var header = new byte[] { 77, 84, 104, 100, 0, 0, 0, 6, 0, 0, 0, 1, 0, 96, 77, 84, 114, 107, 0, 0, 0, (byte)track.Length };
            using (var formatZero = new MemoryStream(header.Concat(track).ToArray()))
            {
                var parsed = MidiSequence.Read(formatZero);
                Assert(parsed.Tracks == 1 && parsed.Events.Length == 4 && Math.Abs(parsed.Duration - 1.5) < .001 &&
                    Math.Abs(parsed.Events[2].Seconds - .5) < .001, "MIDI tipo 0 interpreta status corrente e mudanças de andamento");
            }
            bool broken = false;
            try { using (var invalid = new MemoryStream(header.Concat(track.Take(track.Length - 1)).ToArray())) MidiSequence.Read(invalid); }
            catch (InvalidDataException) { broken = true; }
            Assert(broken, "MIDI truncado é recusado");
        }
        Assert(score.Parts[0].Staves.Count == 2, "Piano com duas pautas");
        score.AddPart("Piano");
        score.AddPart("Piano");
        score.AddPart("Violão");
        Assert(score.Parts.Select(p => p.Staves.Count).SequenceEqual(new[] { 2, 2, 2, 1 }), "Três pianos e violão");
        var before = score.Snapshot();
        bool rejected = false;
        try { score.Put(0, 0, 14, 4, 30, 0); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && score.Snapshot().SequenceEqual(before), "Duração não atravessa a barra");
        rejected = false;
        try { score.Put(0, 0, 1, 1, 30, 0); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && score.Snapshot().SequenceEqual(before), "Sobreposição recusada sem alterar projeto");
        Guid stable = score.Parts[0].Staves[0][0].Id;
        score.Put(0, 0, 0, 2, 30, 1);
        Assert(score.Parts[0].Staves[0][0].Id == stable, "Edição preserva identidade");
        Assert(score.Parts[0].Staves[0][0].Lyric == "O", "Mudar altura, acidente e duração preserva letra");
        Assert(Score.Restore(before).Parts[0].Staves[0][0].Length == 4, "Snapshot recupera estado anterior");
        var legacy = Score.New(false);
        legacy.Version = 1;
        legacy.Put(0, 1, 0, 4, 21, 0);
        var migrated = Score.Restore(legacy.Snapshot());
        Assert(migrated.Version == Score.CurrentVersion && migrated.Parts[0].Staves[1][0].Id == legacy.Parts[0].Staves[1][0].Id &&
            migrated.Parts[0].Staves[1][0].Lyric == null, "Projetos v1 abrem no novo esquema");
        legacy.SetLyric(0, 1, 0, "céu", false);
        legacy.Version = 2;
        Assert(Score.Restore(legacy.Snapshot()).Parts[0].Staves[1][0].Lyric == "céu", "Projetos v2 preservam a letra na migração");
        var rests = Score.New(false);
        rests.PutRest(0, 0, 0, 4);
        rests.PutRest(0, 0, 4, 4);
        var restoredRests = Score.Restore(rests.Snapshot()).Parts[0].Staves[0];
        Assert(restoredRests.Count == 2 && restoredRests.All(n => n.IsRest && n.Length == 4), "Pausas explícitas conservam figuras separadas no projeto");
        using (var silent = Output.Wave(rests))
            Assert(silent.ToArray().Skip(44).All(b => b == 0), "Pausas explícitas geram silêncio");
        score.SetLyric(0, 0, 0, "céu&mar", false);
        score.SetLyric(0, 0, 12, "nas", true);
        score.SetLyric(0, 0, 16, "céu", false);
        string path = Path.Combine(directory, "Composição – teste.valescore");
        score.Save(path);
        score.Title = "Revisão";
        score.Save(path);
        Assert(Score.Open(path).Snapshot().SequenceEqual(score.Snapshot()), "Salvamento atômico e reabertura em caminho Unicode");
        rejected = false;
        try { Score.Restore(new byte[] { 0, 1, 2 }); }
        catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Projeto corrompido é recusado");
        using (var buffer = new MemoryStream())
        {
            Output.MusicXml(score, buffer);
            buffer.Position = 0;
            var xml = XDocument.Load(buffer);
            var measures = xml.Descendants("measure").ToArray();
            Assert(measures.Length == 16 && measures.All(m => m.Elements("note").Sum(n => (int)n.Element("duration")) -
                m.Elements("backup").Sum(b => (int)b.Element("duration")) == 16), "MusicXML mantém 4/4 em todas as partes");
            Assert(xml.Descendants("clef").Any(c => (string)c.Element("sign") == "F"), "MusicXML inclui clave de fá");
            var lyrics = xml.Descendants("lyric").Take(5).ToArray();
            Assert(lyrics.Select(l => (string)l.Element("text")).SequenceEqual(new[] { "céu&mar", "di", "a", "nas", "céu" }),
                "MusicXML preserva texto, acentos e caracteres XML");
            Assert(lyrics.Select(l => (string)l.Element("syllabic")).SequenceEqual(new[] { "single", "begin", "end", "begin", "end" }),
                "MusicXML preserva palavras e sílabas inclusive entre compassos");
            Assert(xml.Descendants("beam").Select(b => b.Value).SequenceEqual(new[] { "begin", "end" }),
                "Colcheias no mesmo tempo recebem barra de união no MusicXML");
            xml.Save(Path.Combine(directory, "exemplo.musicxml"));
        }
        using (var buffer = new MemoryStream())
        {
            Output.MusicXml(rests, buffer);
            buffer.Position = 0;
            var written = XDocument.Load(buffer).Descendants("measure").First().Elements("note").Take(3).ToArray();
            Assert(written.All(n => n.Element("rest") != null) &&
                written.Select(n => (int)n.Element("duration")).SequenceEqual(new[] { 4, 4, 8 }),
                "MusicXML distingue pausas escolhidas e completa a duração do compasso");
        }
        using (var wave = Output.Wave(score))
        {
            var bytes = wave.ToArray();
            Assert(System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && bytes.Skip(44).Any(b => b != 0),
                "WAV PCM contém amostras de áudio");
            Assert(Math.Abs((bytes.Length - 44) / 2.0 / Output.SampleRate - score.Bars * 240.0 / score.Tempo) < 0.001,
                "Duração do áudio segue o andamento");
            File.WriteAllBytes(Path.Combine(directory, "exemplo.wav"), bytes);
        }
        using (var bitmap = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            ScorePage.Draw(graphics, score, 0, null, -1);
            Assert(bitmap.GetPixel(400, 184).R < 140, "Pauta desenhada na página");
            Assert(Enumerable.Range(0, 150).Any(x => Enumerable.Range(0, 19).Any(y => bitmap.GetPixel(133 + x, 257 + y).R < 110)),
                "Letra desenhada sob a pauta na página");
            bitmap.Save(Path.Combine(directory, "partitura.png"));
        }
        var dense = Score.New(false);
        dense.Put(0, 0, 0, 1, 28, 0);
        dense.Put(0, 0, 1, 1, 29, 0);
        dense.SetLyric(0, 0, 0, "palavralonga", true);
        dense.SetLyric(0, 0, 1, "musical", false);
        var denseLayout = new ScoreLayout(dense);
        var measure = denseLayout.MeasureOf(0);
        Assert(measure.At(1) - measure.At(0) > 70 && denseLayout.PageOf(0) == 0, "O espaçamento da partitura se adapta às sílabas próximas");
        Application.EnableVisualStyles();
        var previewType = typeof(Editor).Assembly.GetType("ValeScore.PublicationDialog", true);
        using (var preview = (Form)Activator.CreateInstance(previewType, new object[] { publicationScore, 0, true }))
        {
            preview.Show();
            Application.DoEvents();
            Assert(preview.Visible && preview.Text == "Preparar publicação", "Janela de prévia paginada abre no Windows");
            using (var image = new Bitmap(preview.Width, preview.Height))
            {
                preview.DrawToBitmap(image, new Rectangle(Point.Empty, preview.Size));
                image.Save(Path.Combine(directory, "publicacao.png"));
            }
            preview.Close();
        }
        string homeProfile = Path.Combine(directory, "perfil-central");
        var recentStore = new RecentProjects(homeProfile);
        string projectOne = Path.Combine(directory, "Quarteto.valescore");
        string projectTwo = Path.Combine(directory, "Voz e piano.valescore");
        var homeScore = Score.New(true);
        homeScore.Title = "Quarteto";
        homeScore.Save(projectOne);
        homeScore.Title = "Voz e piano";
        homeScore.Save(projectTwo);
        recentStore.Record(projectOne);
        recentStore.Record(projectTwo);
        recentStore.Record(projectOne);
        Assert(new RecentProjects(homeProfile).Read().SequenceEqual(new[] { projectOne, projectTwo }),
            "Recentes persistem em ordem sem duplicatas");
        recentStore.Remove(projectTwo);
        Assert(File.Exists(projectTwo) && recentStore.Read().SequenceEqual(new[] { projectOne }),
            "Remover recente conserva o projeto original");
        recentStore.Remove(projectOne);
        var setupType = typeof(Editor).Assembly.GetType("ValeScore.NewProjectDialog", true);
        var newDraft = ScoreTemplate.Library[1].Create();
        using (var setup = (Form)Activator.CreateInstance(setupType, new object[] { newDraft, "Voz e piano" }))
        {
            setup.Show();
            Application.DoEvents();
            setup.Controls.OfType<TextBox>().Single(input => input.AccessibleName == "Título").Text = "Ensaio";
            setup.Controls.OfType<ComboBox>().Single(input => input.AccessibleName == "Fórmula de compasso").SelectedItem = "6/8";
            setup.Controls.OfType<NumericUpDown>().Single(input => input.AccessibleName == "Quantidade de compassos").Value = 12;
            setup.Controls.OfType<Button>().Single(button => button.Text == "Criar partitura").PerformClick();
            Assert(setup.DialogResult == DialogResult.OK && newDraft.Title == "Ensaio" && newDraft.BarTicks == 12 &&
                newDraft.Bars == 12 && newDraft.Parts.Count == 2, "Configuração inicial aplica título, métrica e quantidade ao modelo");
        }
        using (var editor = (Editor)Activator.CreateInstance(typeof(Editor),
            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { homeProfile }, null))
        {
            editor.Show();
            Application.DoEvents();
            var type = typeof(Editor);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var start = (Control)type.GetField("home", flags).GetValue(editor);
            var workspace = (Control[])type.GetField("workspace", flags).GetValue(editor);
            var recentList = (ListBox)start.GetType().GetField("recentList", flags).GetValue(start);
            Assert(start.Visible && workspace.All(control => !control.Visible) && editor.Text.Contains("Central de projetos"),
                "Inicialização mostra a central sem abrir uma partitura");
            using (var homeImage = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(homeImage, new Rectangle(Point.Empty, editor.Size));
                homeImage.Save(Path.Combine(directory, "central-projetos.png"));
            }
            Assert(recentList.Items.Count == 0, "Primeira abertura mostra uma lista de recentes vazia");
            recentStore.Record(projectOne);
            type.GetMethod("RefreshHome", flags).Invoke(editor, null);
            Assert(recentList.Items.Count == 1 && (string)recentList.Items[0] == projectOne,
                "Central lê projetos recentes do perfil");
            var openRecentButton = (Button)start.GetType().GetField("openRecent", flags).GetValue(start);
            openRecentButton.PerformClick();
            Assert(!start.Visible && editor.Text.Contains("Quarteto"), "Abrir recente leva ao editor do projeto");
            type.GetMethod("ShowHome", flags).Invoke(editor, null);
            Assert(start.Visible && ((Button)start.GetType().GetField("resume", flags).GetValue(start)).Visible,
                "Voltar à central mantém a edição disponível");
            var example = Descendants(start).OfType<Button>().Single(button => button.AccessibleName == "Abrir exemplo: Prelúdio em dó");
            example.PerformClick();
            Assert(editor.Visible && editor.Text.Contains("Prelúdio"), "Janela nativa abre com partitura");
            using (var image = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(image, new Rectangle(Point.Empty, editor.Size));
                image.Save(Path.Combine(directory, "editor.png"));
            }
            var catalog = ((System.Collections.IEnumerable)type.GetField("commands", flags).GetValue(editor)).Cast<object>().ToArray();
            Assert(catalog.Length > 30 && catalog.Any(command => command.ToString().Contains("Buscar comandos")) == false,
                "Catálogo registra ações executáveis e evita buscar a si mesmo");
            object[] searchMatches = catalog.Where(command =>
                (bool)command.GetType().GetMethod("Matches").Invoke(command, new object[] { "formula compasso" })).ToArray();
            Assert(searchMatches.Length == 1 && searchMatches[0].ToString().Contains("Fórmula de compasso"),
                "Busca encontra comandos por palavras sem acentos");
            var pickerType = type.Assembly.GetType("ValeScore.QuickPicker", true);
            using (var picker = (Form)Activator.CreateInstance(pickerType, new object[] {
                "Comandos do Vale Score", "Qual ação você quer executar?", type.GetField("commands", flags).GetValue(editor), "Executar" }))
            {
                picker.Show();
                Application.DoEvents();
                var search = picker.Controls.OfType<TextBox>().Single();
                search.Text = "transpor";
                var matches = picker.Controls.OfType<ListBox>().Single();
                Assert(matches.Items.Count == 1 && matches.SelectedItem.ToString().StartsWith("Transpor"),
                    "Busca na janela filtra e seleciona uma ação real");
                using (var capture = new Bitmap(picker.Width, picker.Height))
                {
                    picker.DrawToBitmap(capture, new Rectangle(Point.Empty, picker.Size));
                    capture.Save(Path.Combine(directory, "comandos.png"));
                }
                picker.Close();
            }
            var output = (ComboBox)type.GetField("midiOutput", flags).GetValue(editor);
            Assert(output.Items.Count > 0 && (string)output.Items[0] == "Som interno", "Transporte oferece som interno sem dispositivo MIDI");
            var model = type.GetField("score", flags);
            var edit = type.GetMethod("ProcessCmdKey", flags);
            var undo = type.GetField("undoButton", flags);
            var redo = type.GetField("redoButton", flags);
            var noteButtons = (System.Collections.Generic.List<Button>)type.GetField("durationButtons", flags).GetValue(editor);
            var pauseButtons = (System.Collections.Generic.List<Button>)type.GetField("restButtons", flags).GetValue(editor);
            var signs = (System.Collections.Generic.List<Button>)type.GetField("accidentalButtons", flags).GetValue(editor);
            var dots = (Button)type.GetField("dotButton", flags).GetValue(editor);
            var pauseSymbols = Descendants(editor).OfType<Button>().Where(button =>
                button.AccessibleName == "Fermata" || button.AccessibleName == "Respiração" || button.AccessibleName == "Cesura").ToArray();
            Assert(noteButtons.Count == 5 && pauseButtons.Count == 5 && signs.Count == 3 && pauseSymbols.Length == 3 &&
                noteButtons.Concat(pauseButtons).Concat(signs).Concat(pauseSymbols).All(b => b.Text.Length == 0 && !string.IsNullOrWhiteSpace(b.AccessibleName)),
                "Paleta tem figuras musicais e novos sinais com nomes acessíveis");
            using (var whole = new Bitmap(pauseButtons[0].Width, pauseButtons[0].Height))
            using (var half = new Bitmap(pauseButtons[1].Width, pauseButtons[1].Height))
            {
                pauseButtons[0].DrawToBitmap(whole, new Rectangle(Point.Empty, whole.Size));
                pauseButtons[1].DrawToBitmap(half, new Rectangle(Point.Empty, half.Size));
                Assert(Enumerable.Range(9, 14).Any(y => whole.GetPixel(whole.Width / 2, y) != half.GetPixel(half.Width / 2, y)),
                    "Pausas de semibreve e mínima têm desenhos diferentes");
            }
            var sheet = (ScorePage)type.GetField("sheet", flags).GetValue(editor);
            var entry = (TextBox)type.GetField("lyricEntry", flags).GetValue(editor);
            var lyricKey = entry.GetType().GetMethod("ProcessCmdKey", flags);
            Action<Keys> key = value => edit.Invoke(editor, new object[] { new Message(), value });
            Action<Keys> letterKey = value => lyricKey.Invoke(entry, new object[] { new Message(), value });
            var focus = type.GetField("focusMode", flags);
            key(Keys.F11);
            Assert((bool)focus.GetValue(editor), "F11 abre o modo foco de leitura");
            key(Keys.Escape);
            Assert(!(bool)focus.GetValue(editor), "Esc restaura o estúdio após o modo foco");
            Func<Note> first = () => ((Score)model.GetValue(editor)).Parts[0].Staves[0].FirstOrDefault();
            var mouseDown = typeof(ScorePage).GetMethod("OnMouseDown", flags);
            Action<Point> click = point => mouseDown.Invoke(sheet, new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) });
            int original = ((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step;
            key(Keys.Up);
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step == original + 1, "Atalho altera a nota selecionada");
            ((Button)undo.GetValue(editor)).PerformClick();
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step == original, "Botão Desfazer restaura a nota");
            ((Button)redo.GetValue(editor)).PerformClick();
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step == original + 1, "Botão Refazer reaplica a edição");
            ((Button)undo.GetValue(editor)).PerformClick();
            signs[2].PerformClick();
            Assert(first().Alter == 1, "Botão de sustenido altera a nota selecionada");
            ((Button)undo.GetValue(editor)).PerformClick();

            key(Keys.Shift | Keys.L);
            Assert(entry.Visible && entry.Text == "O" && entry.Top > sheet.OnSheet(sheet.Selection, 40).Y, "Shift+L abre a letra sob a nota");
            using (var image = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(image, new Rectangle(Point.Empty, editor.Size));
                image.Save(Path.Combine(directory, "editor-letra.png"));
            }
            entry.Text = "lu";
            letterKey(Keys.OemMinus);
            Assert(first().Lyric == "lu" && first().HyphenAfter && sheet.Selection.Tick == 4 && entry.Visible,
                "Hífen grava sílaba e avança à próxima nota");
            entry.Text = "ar";
            letterKey(Keys.Space);
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][1].Lyric == "ar" && sheet.Selection.Tick == 8 &&
                !((Score)model.GetValue(editor)).Parts[0].Staves[0][1].HyphenAfter, "Espaço encerra a palavra e avança");
            entry.Text = "rascunho";
            letterKey(Keys.Escape);
            Assert(!entry.Visible && ((Score)model.GetValue(editor)).Parts[0].Staves[0][2].Lyric == "a", "Esc cancela a edição da sílaba ativa");
            ((Button)undo.GetValue(editor)).PerformClick();
            ((Button)undo.GetValue(editor)).PerformClick();
            Assert(first().Lyric == "O" && !first().HyphenAfter, "Desfazer restaura a letra e a separação originais");
            type.GetMethod("RemoveLyric", flags).Invoke(editor, null);
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][2].Lyric == null, "Remover letra mantém a nota na pauta");
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { Score.New(false), null });
            var zoom = (ComboBox)type.GetField("zoom", flags).GetValue(editor);
            zoom.SelectedIndex = 4;
            Assert(sheet.ShowEntryPreview, "Partitura vazia abre em modo de entrada");
            var notePoint = sheet.OnSheet(sheet.Selection, 50);
            key(Keys.V);
            click(notePoint);
            Assert(first() == null, "Selecionar em pauta vazia não insere nota");
            key(Keys.N);
            click(notePoint);
            Assert(sheet.Zoom == 1.25f && first().Tick == 0 && first().Step == 28, "Entrada pelo mouse acerta posição e altura com zoom");
            key(Keys.V);
            key(Keys.D4);
            Assert(first().Length == 2, "A figura altera a duração da nota selecionada");
            dots.PerformClick();
            Assert(first().Length == 3, "Botão de ponto aumenta a colcheia selecionada");
            dots.PerformClick();
            Assert(first().Length == 2, "Clicar novamente retira o ponto de aumento");
            key(Keys.E);
            click(new Point(notePoint.X, notePoint.Y - 29));
            Assert(first() != null, "Borracha exige clique próximo ao símbolo");
            click(notePoint);
            Assert(first() == null, "Borracha apaga a nota e deixa uma pausa");
            ((Button)undo.GetValue(editor)).PerformClick();
            Assert(first() != null && first().Length == 2, "Desfazer recupera a nota apagada");
            key(Keys.Delete);
            Assert(first() == null, "Delete apaga a nota selecionada");
            pauseButtons[2].PerformClick();
            click(notePoint);
            Assert(first().IsRest && first().Length == 4, "Paleta insere pausa de semínima com o mouse");
            key(Keys.Shift | Keys.L);
            Assert(!entry.Visible, "Uma pausa não abre o editor de letra");
            pauseButtons[0].PerformClick();
            var secondBar = sheet.Engraving.MeasureOf(1);
            click(new Point((int)((secondBar.Left + secondBar.Width / 2) * sheet.Zoom),
                (int)((ScoreLayout.FirstStaffTop + 12) * sheet.Zoom)));
            Assert(sheet.Music.Parts[0].Staves[0].Any(n => n.Tick == 16 && n.IsRest && n.Length == 16),
                "Clique no centro do compasso insere pausa de semibreve no início");
            key(Keys.E);
            click(new Point((int)((secondBar.Left + secondBar.Width / 2) * sheet.Zoom),
                (int)((ScoreLayout.FirstStaffTop + 12) * sheet.Zoom)));
            Assert(!sheet.Music.Parts[0].Staves[0].Any(n => n.Tick == 16), "Borracha encontra a pausa centralizada");
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { Score.New(false), null });
            Descendants(editor).OfType<Button>().Single(button => button.Text == "Acordes").PerformClick();
            click(sheet.OnSheet(sheet.Selection, 50));
            click(sheet.OnSheet(sheet.Selection, 40));
            click(sheet.OnSheet(sheet.Selection, 30));
            Assert(sheet.Music.Parts[0].Staves[0].Count == 3 && sheet.Music.Parts[0].Staves[0].All(n => n.Tick == 0),
                "Botão Acordes e cliques escrevem uma tríade na mesma posição");
            key(Keys.V);
            click(sheet.OnSheet(sheet.Selection, 40));
            key(Keys.Delete);
            Assert(sheet.Music.Parts[0].Staves[0].Select(n => n.Step).SequenceEqual(new[] { 28, 32 }),
                "Delete apaga apenas a altura clicada no acorde");
            ((Button)undo.GetValue(editor)).PerformClick();
            key(Keys.Alt | Keys.Down);
            key(Keys.D2);
            Assert(sheet.Selection.NoteId != Guid.Empty && sheet.Music.Parts[0].Staves[0].Count == 3 &&
                sheet.Music.Parts[0].Staves[0].All(n => n.Length == 8), "Teclado seleciona no acorde e muda a duração de todas as alturas");
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { Score.New(false), null });
            click(sheet.OnSheet(sheet.Selection, 30));
            key(Keys.Shift | Keys.V);
            key(Keys.D2);
            click(sheet.OnSheet(sheet.Selection, 50));
            Assert(sheet.Music.Parts[0].Staves[0].Count(n => n.Tick == 0) == 2 && sheet.Music.Parts[0].Staves[0]
                .Any(n => n.Voice == 1 && n.Length == 8), "Editor alterna voz e insere acompanhamento sem substituir a melodia");
            key(Keys.V);
            key(Keys.Delete);
            Assert(sheet.Music.Parts[0].Staves[0].Count == 1 && first().Voice == 0,
                "Apagar na segunda voz preserva a primeira");
            var tieScore = Score.New(false);
            tieScore.Put(0, 0, 0, 4, 28, 0);
            tieScore.Put(0, 0, 4, 4, 28, 0);
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { tieScore, null });
            var tieButton = Descendants(editor).OfType<Button>().Single(b => b.AccessibleName == "Ligadura de prolongamento (Shift+T)");
            tieButton.PerformClick();
            Assert(first().TiedTo.HasValue, "Símbolo de ligadura na paleta une duas notas");
            key(Keys.Shift | Keys.T);
            Assert(!first().TiedTo.HasValue, "Atalho Shift+T remove a ligadura selecionada");
            ((Button)undo.GetValue(editor)).PerformClick();
            Assert(first().TiedTo.HasValue, "Desfazer recupera a ligadura");
            key(Keys.Right);
            key(Keys.Delete);
            Assert(!first().TiedTo.HasValue, "Apagar a nota de destino remove a ligadura pendente");
            var phrase = Score.New(false);
            phrase.Put(0, 0, 0, 4, 28, 0);
            phrase.Put(0, 0, 4, 4, 30, 0);
            phrase.Put(0, 0, 8, 4, 32, 0);
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { phrase, null });
            var slur = Descendants(editor).OfType<Button>().Single(b => b.AccessibleName == "Ligadura de expressão (Shift+S)");
            slur.PerformClick();
            Assert(sheet.SlurAnchor != null && sheet.SlurAnchor.NoteId == first().Id,
                "Paleta marca visualmente a primeira nota da frase");
            var ending = new StaffPosition { Tick = 8 };
            click(sheet.OnSheet(ending, 30));
            Assert(sheet.SlurAnchor == null && first().SlursTo.Single() == sheet.Music.Parts[0].Staves[0].Last().Id,
                "Clicar na última nota conclui a ligadura de expressão");
            key(Keys.Delete);
            Assert(first().SlursTo == null, "Apagar o final limpa o arco correspondente");
            ((Button)undo.GetValue(editor)).PerformClick();
            Assert(first().SlursTo != null, "Desfazer recupera o final e a ligadura");
            key(Keys.Left);
            key(Keys.Shift | Keys.S);
            key(Keys.Escape);
            Assert(sheet.SlurAnchor == null, "Esc cancela a marcação de frase");
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { Score.New(true), null });
            editor.Close();
        }
    }
}

