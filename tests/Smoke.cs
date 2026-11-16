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

    private static void Run(string directory)
    {
        var score = Score.New(true);
        score.Validate();
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
        using (var editor = new Editor())
        {
            editor.Show();
            Application.DoEvents();
            Assert(editor.Visible && editor.Text.Contains("Prelúdio"), "Janela nativa abre com partitura");
            using (var image = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(image, new Rectangle(Point.Empty, editor.Size));
                image.Save(Path.Combine(directory, "editor.png"));
            }
            var type = typeof(Editor);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
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
            Assert(noteButtons.Count == 5 && pauseButtons.Count == 5 && signs.Count == 3 &&
                noteButtons.Concat(pauseButtons).Concat(signs).All(b => b.Text.Length == 0 && !string.IsNullOrWhiteSpace(b.AccessibleName)),
                "Paleta tem 13 símbolos com nomes acessíveis");
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
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { Score.New(true), null });
            editor.Close();
        }
    }
}
