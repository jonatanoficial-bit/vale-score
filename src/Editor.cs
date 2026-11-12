using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ValeScore
{
    public sealed class Editor : Form
    {
        private static readonly Color Dark = Color.FromArgb(24, 30, 39);
        private static readonly Color PanelColor = Color.FromArgb(33, 41, 53);
        private static readonly Color TextColor = Color.FromArgb(237, 243, 248);
        private static readonly Color Muted = Color.FromArgb(164, 181, 197);
        private static readonly Color Teal = Color.FromArgb(21, 116, 100);
        private readonly ScorePage sheet = new ScorePage();
        private readonly Panel surface = new Panel { AutoScroll = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(207, 215, 221) };
        private readonly FlowLayoutPanel tools = new FlowLayoutPanel();
        private readonly ListBox tracks = new ListBox();
        private readonly Label pageLabel = new Label(), status = new Label(), selection = new Label();
        private readonly Button undoButton, redoButton, playButton, previousButton, nextButton;
        private readonly List<Button> durationButtons = new List<Button>();
        private readonly ComboBox accidentals = new ComboBox();
        private readonly Stack<byte[]> undo = new Stack<byte[]>(), redo = new Stack<byte[]>();
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Stopwatch clock = new Stopwatch();
        private MemoryStream playback;
        private SoundPlayer player;
        private int playbackGeneration;
        private bool preparing, syncingTracks;
        private Score score = Score.New(true);
        private string path;
        private byte[] saved;
        private int duration = 4;
        private StaffPosition position = new StaffPosition { Step = 28 };
        private readonly List<StaffPosition> lanes = new List<StaffPosition>();

        public Editor()
        {
            Text = "Vale Score";
            StartPosition = FormStartPosition.CenterScreen;
            var desktop = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(1230, desktop.Width), Math.Min(860, desktop.Height));
            MinimumSize = new Size(880, 610);
            BackColor = Dark;
            Font = new Font("Segoe UI", 10);
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            var menu = new MenuStrip { Dock = DockStyle.Top, BackColor = Dark, ForeColor = TextColor, Font = Font, Renderer = new DarkMenu() };
            var file = new ToolStripMenuItem("Arquivo");
            Item(file, "Nova partitura", Keys.Control | Keys.N, () => New(false));
            Item(file, "Abrir…", Keys.Control | Keys.O, Open);
            Item(file, "Salvar", Keys.Control | Keys.S, () => Save(false));
            Item(file, "Salvar como…", Keys.Control | Keys.Shift | Keys.S, () => Save(true));
            file.DropDownItems.Add(new ToolStripSeparator());
            Item(file, "Exportar MusicXML…", Keys.None, () => Export("MusicXML (*.musicxml)|*.musicxml", Output.MusicXml));
            Item(file, "Exportar WAV…", Keys.None, ExportWave);
            Item(file, "Imprimir / PDF…", Keys.Control | Keys.P, Print);
            Item(file, "Abrir partitura de exemplo", Keys.None, () => New(true));
            var edit = new ToolStripMenuItem("Editar");
            Item(edit, "Desfazer", Keys.Control | Keys.Z, () => History(undo, redo));
            Item(edit, "Refazer", Keys.Control | Keys.Y, () => History(redo, undo));
            Item(edit, "Propriedades da partitura…", Keys.None, Properties);
            Item(edit, "Adicionar compasso", Keys.None, AddBar);
            var help = new ToolStripMenuItem("Ajuda");
            Item(help, "Atalhos de edição", Keys.F1, () => MessageBox.Show(this,
                "Clique na pauta para inserir uma nota. Clique direito para selecionar sem editar.\n" +
                "← / → mudam a posição. ↑ / ↓ alteram a altura da nota ou do cursor.\n" +
                "A S D F G H J inserem Dó a Si na oitava atual. Enter insere a altura do cursor.\n" +
                "1 a 5 escolhem duração. Delete apaga a nota e restaura a pausa.\n" +
                "Espaço toca ou para. Ctrl+Z desfaz. Ctrl+Y refaz.", "Atalhos do Vale Score"));
            menu.Items.AddRange(new ToolStripItem[] { file, edit, help });

            var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(15, 12, 0, 4), WrapContents = false, BackColor = PanelColor };
            var brand = new Label { Text = "VALE SCORE", ForeColor = TextColor, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
                Width = 142, Height = 34, Font = new Font("Segoe UI Semibold", 15) };
            header.Controls.Add(brand);
            header.Controls.Add(Button("Novo", () => New(false)));
            header.Controls.Add(Button("Abrir", Open));
            header.Controls.Add(Button("Salvar", () => Save(false)));
            undoButton = Button("Desfazer", () => History(undo, redo));
            redoButton = Button("Refazer", () => History(redo, undo));
            header.Controls.Add(undoButton);
            header.Controls.Add(redoButton);
            playButton = Button("▶  Reproduzir", Play);
            playButton.Width = 125;
            playButton.BackColor = Teal;
            header.Controls.Add(playButton);

            var body = new Panel { Dock = DockStyle.Fill };
            var left = new Panel { Dock = DockStyle.Left, Width = 219, BackColor = PanelColor, Padding = new Padding(14) };
            var sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            left.Controls.Add(sidebar);
            sidebar.Controls.Add(Caption("INSTRUMENTOS"));
            tracks.Width = 181;
            tracks.Height = 144;
            tracks.BackColor = Dark;
            tracks.ForeColor = TextColor;
            tracks.BorderStyle = BorderStyle.None;
            tracks.AccessibleName = "Instrumentos e pautas";
            tracks.SelectedIndexChanged += delegate
            {
                if (syncingTracks || tracks.SelectedIndex < 0) return;
                var lane = lanes[tracks.SelectedIndex];
                position.Part = lane.Part;
                position.Staff = lane.Staff;
                position.Step = lane.Step;
                RefreshView();
            };
            sidebar.Controls.Add(tracks);
            var instrument = new ComboBox { Width = 181, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Novo instrumento" };
            instrument.Items.AddRange(new object[] { "Piano", "Violão", "Voz" });
            instrument.SelectedIndex = 0;
            sidebar.Controls.Add(instrument);
            sidebar.Controls.Add(Button("+ Instrumento", () => Change(() => score.AddPart((string)instrument.SelectedItem))));
            sidebar.Controls.Add(Button("+ Compasso", AddBar));
            sidebar.Controls.Add(Button("Título e andamento", Properties));
            sidebar.Controls.Add(Caption("SELEÇÃO"));
            selection.Width = 181;
            selection.Height = 75;
            selection.ForeColor = Muted;
            sidebar.Controls.Add(selection);
            sidebar.Controls.Add(Caption("ATALHOS"));
            sidebar.Controls.Add(new Label { Width = 181, Height = 94, ForeColor = Muted,
                Text = "Clique  Inserir nota\nClique direito  Selecionar\nDelete  Apagar nota\nEspaço  Tocar ou parar" });

            tools.Dock = DockStyle.Top;
            tools.Height = 48;
            tools.Padding = new Padding(9, 5, 0, 0);
            tools.BackColor = Dark;
            tools.WrapContents = false;
            string[] figures = { "Semibreve", "Mínima", "Semínima", "Colcheia", "Semicolcheia" };
            for (int i = 0; i < figures.Length; i++)
            {
                int value = Score.Durations[i];
                var button = Button(figures[i], () => SetDuration(value));
                durationButtons.Add(button);
                tools.Controls.Add(button);
            }
            accidentals.Width = 82;
            accidentals.DropDownStyle = ComboBoxStyle.DropDownList;
            accidentals.AccessibleName = "Alteração da nota";
            accidentals.Items.AddRange(new object[] { "Bemol", "Natural", "Sustenido" });
            accidentals.SelectedIndex = 1;
            accidentals.Margin = new Padding(8, 5, 0, 0);
            accidentals.SelectionChangeCommitted += delegate
            {
                var note = SelectedNote();
                if (note != null) Put(note.Step, note.Length);
                FocusWorkspace();
            };
            tools.Controls.Add(accidentals);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, BackColor = Dark, Padding = new Padding(10, 3, 0, 0), WrapContents = false };
            previousButton = Button("‹", () => MovePage(-1));
            previousButton.Width = 35;
            nextButton = Button("›", () => MovePage(1));
            nextButton.Width = 35;
            footer.Controls.Add(previousButton);
            pageLabel.Width = 112;
            pageLabel.Height = 34;
            pageLabel.TextAlign = ContentAlignment.MiddleCenter;
            pageLabel.ForeColor = TextColor;
            footer.Controls.Add(pageLabel);
            footer.Controls.Add(nextButton);
            status.Width = 540;
            status.Height = 34;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = Muted;
            status.Margin = new Padding(12, 2, 0, 0);
            footer.Controls.Add(status);

            var main = new Panel { Dock = DockStyle.Fill };
            surface.Controls.Add(sheet);
            surface.Resize += delegate { LayoutSheet(); };
            sheet.Pick = (pick, selectOnly) =>
            {
                position = pick;
                var note = SelectedNote();
                if (selectOnly)
                {
                    if (note != null)
                    {
                        position.Step = note.Step;
                        accidentals.SelectedIndex = note.Alter + 1;
                    }
                    RefreshView();
                }
                else Put(pick.Step, duration);
                FocusWorkspace();
            };
            main.Controls.Add(surface);
            main.Controls.Add(tools);
            main.Controls.Add(footer);
            body.Controls.Add(main);
            body.Controls.Add(left);
            Controls.Add(body);
            Controls.Add(header);
            Controls.Add(menu);
            MainMenuStrip = menu;

            timer.Tick += delegate
            {
                int tick = (int)(clock.Elapsed.TotalSeconds * score.Tempo / 15);
                if (tick >= score.Bars * 16) Stop();
                else if (tick != sheet.Playing)
                {
                    sheet.Playing = tick;
                    ChangePage(ScorePage.PageOf(score, tick));
                }
            };
            FormClosing += (sender, args) => { args.Cancel = !ConfirmLeave(); if (!args.Cancel) Stop(); };
            saved = score.Snapshot();
            SetDuration(4);
            RefreshView();
        }

        private sealed class DarkMenu : ToolStripProfessionalRenderer
        {
            public DarkMenu() : base(new MenuColors()) { }
        }
        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Teal; } }
            public override Color MenuItemSelectedGradientBegin { get { return Teal; } }
            public override Color MenuItemSelectedGradientEnd { get { return Teal; } }
            public override Color MenuItemPressedGradientBegin { get { return PanelColor; } }
            public override Color MenuItemPressedGradientEnd { get { return PanelColor; } }
            public override Color ToolStripDropDownBackground { get { return PanelColor; } }
            public override Color ImageMarginGradientBegin { get { return PanelColor; } }
            public override Color ImageMarginGradientMiddle { get { return PanelColor; } }
            public override Color ImageMarginGradientEnd { get { return PanelColor; } }
        }

        private Label Caption(string text)
        {
            return new Label { Text = text, Width = 180, Height = 25, ForeColor = Muted,
                Margin = new Padding(3, 13, 0, 1), Font = new Font("Segoe UI Semibold", 9) };
        }
        private Button Button(string text, Action action)
        {
            var button = new Button { Text = text, Height = 32, AutoSize = true, FlatStyle = FlatStyle.Flat,
                BackColor = PanelColor, ForeColor = TextColor, Cursor = Cursors.Hand, Margin = new Padding(3, 2, 3, 3) };
            button.FlatAppearance.BorderColor = Color.FromArgb(60, 76, 90);
            button.Click += delegate { action(); FocusWorkspace(); };
            return button;
        }
        private void Item(ToolStripMenuItem menu, string title, Keys shortcut, Action action)
        {
            var item = new ToolStripMenuItem(title) { ForeColor = TextColor, ShortcutKeys = shortcut };
            item.Click += delegate { action(); };
            menu.DropDownItems.Add(item);
        }
        private void FocusWorkspace() { ActiveControl = null; }
        private bool Dirty() { return !score.Snapshot().SequenceEqual(saved); }
        private void Report(string message) { status.Text = message; }
        private void Error(Exception ex) { Report(ex.Message); SystemSounds.Exclamation.Play(); }

        private void LayoutSheet()
        {
            surface.AutoScrollMinSize = new Size(ScorePage.PageWidth + 40, ScorePage.PageHeight + 48);
            sheet.Location = new Point(Math.Max(20, (surface.ClientSize.Width - sheet.Width) / 2) + surface.AutoScrollPosition.X,
                24 + surface.AutoScrollPosition.Y);
        }

        private void ChangePage(int page)
        {
            if (sheet.Page != page)
            {
                sheet.Page = page;
                surface.AutoScrollPosition = Point.Empty;
                LayoutSheet();
            }
            int count = ScorePage.PageCount(score);
            pageLabel.Text = "Página " + (page + 1) + " / " + count;
            previousButton.Enabled = page > 0;
            nextButton.Enabled = page + 1 < count;
            sheet.Invalidate();
        }

        private void MovePage(int offset)
        {
            int page = sheet.Page + offset;
            if (page >= 0 && page < ScorePage.PageCount(score)) ChangePage(page);
        }

        private void RefreshView()
        {
            sheet.Music = score;
            position.Part = Math.Min(position.Part, score.Parts.Count - 1);
            position.Staff = Math.Min(position.Staff, score.Parts[position.Part].Staves.Count - 1);
            position.Tick = Math.Min(position.Tick, score.Bars * 16 - 1);
            sheet.Selection = position;
            syncingTracks = true;
            tracks.BeginUpdate();
            tracks.Items.Clear();
            lanes.Clear();
            for (int i = 0; i < score.Parts.Count; i++)
            for (int j = 0; j < score.Parts[i].Staves.Count; j++)
            {
                tracks.Items.Add(score.Parts[i].Name + (score.Parts[i].Staves.Count == 2 ? (j == 0 ? " · sol" : " · fá") : ""));
                lanes.Add(new StaffPosition { Part = i, Staff = j, Step = j == 0 ? 28 : 21 });
                if (i == position.Part && j == position.Staff) tracks.SelectedIndex = lanes.Count - 1;
            }
            tracks.EndUpdate();
            syncingTracks = false;
            var note = SelectedNote();
            int step = note == null ? position.Step : note.Step;
            string altered = note == null || note.Alter == 0 ? "" : note.Alter > 0 ? "♯" : "♭";
            selection.Text = "Compasso " + (position.Tick / 16 + 1) + " · posição " + (position.Tick % 16 + 1) + "/16\n"
                + Score.Names[step % 7] + altered + (step / 7) + (note == null ? " · cursor" : " · nota");
            Text = score.Title + (Dirty() ? " *" : "") + " — Vale Score";
            undoButton.Enabled = undo.Count > 0;
            redoButton.Enabled = redo.Count > 0;
            ChangePage(ScorePage.PageOf(score, position.Tick));
        }

        private Note SelectedNote()
        {
            return score.Parts[position.Part].Staves[position.Staff].Find(n => n.Tick == position.Tick);
        }

        private bool Change(Action action)
        {
            var before = score.Snapshot();
            try
            {
                action();
                score.Validate();
                if (!before.SequenceEqual(score.Snapshot()))
                {
                    Stop();
                    undo.Push(before);
                    redo.Clear();
                    Report("Alteração registrada");
                }
            }
            catch (Exception ex)
            {
                score = Score.Restore(before);
                Error(ex);
                RefreshView();
                return false;
            }
            RefreshView();
            return true;
        }

        private void History(Stack<byte[]> from, Stack<byte[]> to)
        {
            if (from.Count == 0) return;
            Stop();
            to.Push(score.Snapshot());
            score = Score.Restore(from.Pop());
            RefreshView();
            Report(from == undo ? "Alteração desfeita" : "Alteração refeita");
        }

        private void SetDuration(int value)
        {
            duration = value;
            for (int i = 0; i < durationButtons.Count; i++)
                durationButtons[i].BackColor = Score.Durations[i] == value ? Teal : PanelColor;
        }

        private bool Put(int step, int length)
        {
            int min = position.Staff == 0 ? 25 : 13;
            if (step < min || step > min + 18) { Report("Altura fora da extensão editável da pauta."); RefreshView(); return false; }
            position.Step = step;
            return Change(() => score.Put(position.Part, position.Staff, position.Tick, length, step, accidentals.SelectedIndex - 1));
        }

        private void InsertWithKeyboard(int step)
        {
            if (Put(step, duration))
            {
                position.Tick = Math.Min(score.Bars * 16 - 1, position.Tick + duration);
                RefreshView();
            }
        }

        private void AddBar()
        {
            if (score.Bars == Score.MaxBars) { Report("Limite de 64 compassos atingido."); return; }
            Change(() => score.Bars++);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (key == Keys.Space && !(ActiveControl is ComboBox)) { Play(); return true; }
            if (ActiveControl is ComboBox || ActiveControl == tracks) return base.ProcessCmdKey(ref msg, key);
            if (key >= Keys.D1 && key <= Keys.D5) { SetDuration(Score.Durations[key - Keys.D1]); return true; }
            if (key == Keys.Delete)
            {
                var note = SelectedNote();
                if (note != null) Change(() => score.Parts[position.Part].Staves[position.Staff].Remove(note));
                return true;
            }
            if (key == Keys.Left || key == Keys.Right)
            {
                position.Tick = Math.Max(0, Math.Min(score.Bars * 16 - 1, position.Tick + (key == Keys.Right ? 1 : -1)));
                var note = SelectedNote();
                if (note != null) { position.Step = note.Step; accidentals.SelectedIndex = note.Alter + 1; }
                RefreshView();
                return true;
            }
            if (key == Keys.Up || key == Keys.Down)
            {
                var note = SelectedNote();
                int step = (note == null ? position.Step : note.Step) + (key == Keys.Up ? 1 : -1);
                int min = position.Staff == 0 ? 25 : 13;
                if (step >= min && step <= min + 18)
                {
                    if (note == null) { position.Step = step; RefreshView(); }
                    else { accidentals.SelectedIndex = note.Alter + 1; Put(step, note.Length); }
                }
                return true;
            }
            if (key == Keys.Enter) { InsertWithKeyboard(position.Step); return true; }
            int degree = Array.IndexOf(new[] { Keys.A, Keys.S, Keys.D, Keys.F, Keys.G, Keys.H, Keys.J }, key);
            if (degree >= 0) { InsertWithKeyboard(position.Step / 7 * 7 + degree); return true; }
            return base.ProcessCmdKey(ref msg, key);
        }

        private bool ConfirmLeave()
        {
            if (!Dirty()) return true;
            var choice = MessageBox.Show(this, "Deseja salvar as alterações da partitura atual?", "Vale Score",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return choice == DialogResult.No || (choice == DialogResult.Yes && Save(false));
        }

        private void LoadScore(Score model, string file)
        {
            Stop();
            score = model;
            path = file;
            saved = score.Snapshot();
            undo.Clear();
            redo.Clear();
            position = new StaffPosition { Step = 28 };
            accidentals.SelectedIndex = 1;
            RefreshView();
        }
        private void New(bool example)
        {
            if (ConfirmLeave()) { LoadScore(Score.New(example), null); Report(example ? "Exemplo carregado" : "Nova partitura"); }
        }
        private void Open()
        {
            if (!ConfirmLeave()) return;
            using (var dialog = new OpenFileDialog { Filter = "Projeto Vale Score (*.valescore)|*.valescore" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { LoadScore(Score.Open(dialog.FileName), dialog.FileName); Report("Projeto aberto"); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Não foi possível abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }
        private bool Save(bool copy)
        {
            string destination = path;
            if (destination == null || copy)
            {
                using (var dialog = new SaveFileDialog { Filter = "Projeto Vale Score (*.valescore)|*.valescore", FileName = SafeFileName() })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                    destination = dialog.FileName;
                }
            }
            try
            {
                score.Save(destination);
                path = destination;
                saved = score.Snapshot();
                RefreshView();
                Report("Projeto salvo");
                return true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Não foi possível salvar", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        }

        private string SafeFileName()
        {
            return string.Concat(score.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).TrimEnd('.', ' ');
        }

        private void Properties()
        {
            using (var dialog = new Form { Text = "Propriedades da partitura", Size = new Size(436, 272),
                FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Font })
            {
                var title = new TextBox { Text = score.Title, MaxLength = 80, Bounds = new Rectangle(18, 37, 382, 28) };
                var author = new TextBox { Text = score.Author, MaxLength = 80, Bounds = new Rectangle(18, 95, 382, 28) };
                var tempo = new NumericUpDown { Minimum = 30, Maximum = 240, Value = score.Tempo, Bounds = new Rectangle(18, 153, 90, 28) };
                var ok = new Button { Text = "Aplicar", Bounds = new Rectangle(219, 188, 87, 32), DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancelar", Bounds = new Rectangle(314, 188, 87, 32), DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] {
                    new Label { Text = "Título", AutoSize = true, Location = new Point(18, 12) }, title,
                    new Label { Text = "Compositor", AutoSize = true, Location = new Point(18, 70) }, author,
                    new Label { Text = "Andamento (BPM)", AutoSize = true, Location = new Point(18, 128) }, tempo, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Change(() => { score.Title = title.Text.Trim(); score.Author = author.Text.Trim(); score.Tempo = (int)tempo.Value; });
            }
        }

        private string ExportPath(string filter)
        {
            using (var dialog = new SaveFileDialog { Filter = filter, FileName = SafeFileName() })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }
        private void Export(string filter, Action<Score, Stream> write)
        {
            string destination = ExportPath(filter);
            if (destination == null) return;
            try
            {
                using (var buffer = new MemoryStream())
                {
                    write(score, buffer);
                    File.WriteAllBytes(destination, buffer.ToArray());
                }
                Report("Arquivo exportado");
            }
            catch (Exception ex) { Error(ex); }
        }

        private async void ExportWave()
        {
            string destination = ExportPath("Áudio WAV (*.wav)|*.wav");
            if (destination == null) return;
            var model = Score.Restore(score.Snapshot());
            Report("Renderizando áudio…");
            try
            {
                await Task.Run(() =>
                {
                    using (var wave = Output.Wave(model)) File.WriteAllBytes(destination, wave.ToArray());
                });
                Report("Áudio WAV exportado");
            }
            catch (Exception ex) { Error(ex); }
        }

        private void Print()
        {
            try
            {
                using (var document = new PrintDocument { DocumentName = score.Title, OriginAtMargins = true })
                using (var dialog = new PrintDialog { Document = document, UseEXDialog = true })
                {
                    document.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
                    int page = 0;
                    document.PrintPage += (sender, e) =>
                    {
                        float scale = Math.Min(e.MarginBounds.Width / (float)ScorePage.PageWidth, e.MarginBounds.Height / (float)ScorePage.PageHeight);
                        e.Graphics.TranslateTransform((e.MarginBounds.Width - ScorePage.PageWidth * scale) / 2, 0);
                        e.Graphics.ScaleTransform(scale, scale);
                        ScorePage.Draw(e.Graphics, score, page++, null, -1);
                        e.HasMorePages = page < ScorePage.PageCount(score);
                    };
                    if (dialog.ShowDialog(this) == DialogResult.OK) { document.Print(); Report("Partitura enviada para impressão"); }
                }
            }
            catch (Exception ex) { Error(ex); }
        }

        private async void Play()
        {
            if (preparing || timer.Enabled) { Stop(); return; }
            int generation = ++playbackGeneration;
            preparing = true;
            playButton.Text = "■  Parar";
            Report("Preparando reprodução…");
            var snapshot = Score.Restore(score.Snapshot());
            try
            {
                var wave = await Task.Run(() => Output.Wave(snapshot));
                if (generation != playbackGeneration || IsDisposed) { wave.Dispose(); return; }
                playback = wave;
                player = new SoundPlayer(playback);
                player.Load();
                player.Play();
                preparing = false;
                clock.Restart();
                ChangePage(0);
                timer.Start();
                Report("Reproduzindo desde o início");
            }
            catch (Exception ex)
            {
                if (generation == playbackGeneration && !IsDisposed) { Stop(); Error(ex); }
            }
        }
        private void Stop()
        {
            ++playbackGeneration;
            bool wasPlaying = preparing || timer.Enabled;
            preparing = false;
            timer.Stop();
            clock.Reset();
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (playback != null) { playback.Dispose(); playback = null; }
            sheet.Playing = -1;
            sheet.Invalidate();
            playButton.Text = "▶  Reproduzir";
            if (wasPlaying) Report("Reprodução encerrada");
        }
    }
}
