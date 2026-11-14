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
        using (var buffer = new MemoryStream())
        {
            MidiSequence.Export(score, buffer);
            buffer.Position = 0;
            var midi = MidiSequence.Read(buffer);
            Assert(midi.Tracks == 2 && Math.Abs(midi.Duration - 10) < .001, "MIDI tipo 1 exporta pista, andamento e duração");
            var pitches = midi.Events.Where(e => (e.Status & 240) == 0x90 && e.Data2 != 0).ToArray();
            Assert(pitches.Length == score.Parts[0].Staves.Sum(lane => lane.Count(n => !n.IsRest)) &&
                pitches.Any(e => e.Data1 == 60 && e.Seconds == 0), "MIDI exporta alturas e preserva pausas");
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
