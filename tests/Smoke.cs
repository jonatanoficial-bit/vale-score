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
        Assert(Score.Restore(before).Parts[0].Staves[0][0].Length == 4, "Snapshot recupera estado anterior");
        string path = Path.Combine(args[0], "Composição – teste.valescore");
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
            xml.Save(Path.Combine(args[0], "exemplo.musicxml"));
        }
        using (var wave = Output.Wave(score))
        {
            var bytes = wave.ToArray();
            Assert(System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && bytes.Skip(44).Any(b => b != 0),
                "WAV PCM contém amostras de áudio");
            Assert(Math.Abs((bytes.Length - 44) / 2.0 / Output.SampleRate - score.Bars * 240.0 / score.Tempo) < 0.001,
                "Duração do áudio segue o andamento");
            File.WriteAllBytes(Path.Combine(args[0], "exemplo.wav"), bytes);
        }
        using (var bitmap = new Bitmap(ScorePage.PageWidth, ScorePage.PageHeight))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            ScorePage.Draw(graphics, score, 0, null, -1);
            Assert(bitmap.GetPixel(400, 184).R < 140, "Pauta desenhada na página");
            bitmap.Save(Path.Combine(args[0], "partitura.png"));
        }
        Application.EnableVisualStyles();
        using (var editor = new Editor())
        {
            editor.Show();
            Application.DoEvents();
            Assert(editor.Visible && editor.Text.Contains("Prelúdio"), "Janela nativa abre com partitura");
            using (var image = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(image, editor.ClientRectangle);
                image.Save(Path.Combine(args[0], "editor.png"));
            }
            var type = typeof(Editor);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var model = type.GetField("score", flags);
            var edit = type.GetMethod("ProcessCmdKey", flags);
            var undo = type.GetField("undoButton", flags);
            var redo = type.GetField("redoButton", flags);
            int original = ((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step;
            edit.Invoke(editor, new object[] { new Message(), Keys.Up });
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step == original + 1, "Atalho altera a nota selecionada");
            ((Button)undo.GetValue(editor)).PerformClick();
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step == original, "Botão Desfazer restaura a nota");
            ((Button)redo.GetValue(editor)).PerformClick();
            Assert(((Score)model.GetValue(editor)).Parts[0].Staves[0][0].Step == original + 1, "Botão Refazer reaplica a edição");
            ((Button)undo.GetValue(editor)).PerformClick();
            editor.Close();
        }
    }
}
