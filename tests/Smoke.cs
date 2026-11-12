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
        Assert(migrated.Version == 2 && migrated.Parts[0].Staves[1][0].Id == legacy.Parts[0].Staves[1][0].Id &&
            migrated.Parts[0].Staves[1][0].Lyric == null, "Projetos v1 abrem no novo esquema");
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
            xml.Save(Path.Combine(directory, "exemplo.musicxml"));
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
            var model = type.GetField("score", flags);
            var edit = type.GetMethod("ProcessCmdKey", flags);
            var undo = type.GetField("undoButton", flags);
            var redo = type.GetField("redoButton", flags);
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
            type.GetMethod("LoadScore", flags).Invoke(editor, new object[] { Score.New(true), null });
            editor.Close();
        }
    }
}
