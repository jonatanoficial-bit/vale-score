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
        private static readonly Color Dark = Color.FromArgb(19, 25, 33);
        private static readonly Color PanelColor = Color.FromArgb(28, 37, 48);
        private static readonly Color TextColor = Color.FromArgb(239, 244, 248);
        private static readonly Color Muted = Color.FromArgb(166, 184, 199);
        private static readonly Color Teal = Color.FromArgb(15, 110, 91);
        private static readonly string[] Figures = { "Semibreve", "Mínima", "Semínima", "Colcheia", "Semicolcheia" };
        private static readonly Keys[] PitchKeys = { Keys.A, Keys.S, Keys.D, Keys.F, Keys.G, Keys.H, Keys.J };
        private enum EditMode { Select, Write, Erase, Lyrics }
        private readonly ScorePage sheet = new ScorePage();
        private readonly Panel surface = new Panel { AutoScroll = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(167, 177, 186) };
        private readonly FlowLayoutPanel tools = new FlowLayoutPanel();
        private readonly ListBox tracks = new ListBox();
        private readonly Label pageLabel = new Label(), status = new Label(), selection = new Label(), modeHint = new Label();
        private readonly Button undoButton, redoButton, playButton, previousButton, nextButton;
        private readonly Button editLyricButton, eraseNoteButton;
        private readonly List<Button> durationButtons = new List<Button>();
        private readonly Dictionary<EditMode, Button> modeButtons = new Dictionary<EditMode, Button>();
        private readonly ComboBox accidentals = new ComboBox();
        private readonly ComboBox zoom = new ComboBox();
        private readonly LyricEntry lyricEntry = new LyricEntry();
        private readonly Stack<byte[]> undo = new Stack<byte[]>(), redo = new Stack<byte[]>();
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Stopwatch clock = new Stopwatch();
        private MemoryStream playback;
        private SoundPlayer player;
        private int playbackGeneration;
        private bool preparing, syncingTracks, editingLyric;
        private EditMode mode = EditMode.Select;
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
            Item(edit, "Editar letra da nota", Keys.None, StartLyrics).ShortcutKeyDisplayString = "Shift+L";
            Item(edit, "Remover letra da nota", Keys.None, RemoveLyric);
            Item(edit, "Propriedades da partitura…", Keys.None, Properties);
            Item(edit, "Adicionar compasso", Keys.None, AddBar);
            var help = new ToolStripMenuItem("Ajuda");
            Item(help, "Atalhos de edição", Keys.F1, () => MessageBox.Show(this,
                "V: Selecionar. N: Escrever notas. E: Apagar. Esc: Voltar à seleção.\n" +
                "Na pauta, use ← / → para navegar e ↑ / ↓ para mudar a altura.\n" +
                "No modo Notas, A S D F G H J inserem Dó a Si. Enter insere a altura do cursor.\n" +
                "1 a 5 escolhem a figura. Delete apaga a nota selecionada.\n" +
                "Shift+L abre a letra sob a nota. Espaço avança por palavra e hífen por sílaba.\n" +
                "Na letra, Enter confirma, Esc cancela e Shift+Tab volta à nota anterior.\n" +
                "Espaço toca ou para fora da letra. Ctrl+Z desfaz. Ctrl+Y refaz.", "Atalhos do Vale Score"));
            menu.Items.AddRange(new ToolStripItem[] { file, edit, help });

            var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(17, 9, 0, 4), WrapContents = false, BackColor = PanelColor };
            var brand = new Label { Text = "Vale Score", ForeColor = TextColor, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
                Width = 159, Height = 34, Font = new Font("Georgia", 17, FontStyle.Bold) };
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
            var left = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = PanelColor, Padding = new Padding(14, 8, 12, 0) };
            var sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            left.Controls.Add(sidebar);
            sidebar.Controls.Add(Caption("INSTRUMENTOS"));
            tracks.Width = 172;
            tracks.Height = 94;
            tracks.BackColor = Dark;
            tracks.ForeColor = TextColor;
            tracks.BorderStyle = BorderStyle.None;
            tracks.AccessibleName = "Instrumentos e pautas";
            tracks.SelectedIndexChanged += delegate
            {
                if (syncingTracks || tracks.SelectedIndex < 0) return;
                var lane = lanes[tracks.SelectedIndex];
                if (!CommitLyric()) { RefreshView(); return; }
                position.Part = lane.Part;
                position.Staff = lane.Staff;
                position.Step = lane.Step;
                RefreshView();
                RevealCursor();
            };
            sidebar.Controls.Add(tracks);
            var instrument = new ComboBox { Width = 172, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Novo instrumento" };
            instrument.Items.AddRange(new object[] { "Piano", "Violão", "Voz" });
            instrument.SelectedIndex = 0;
            sidebar.Controls.Add(instrument);
            sidebar.Controls.Add(Button("Adicionar instrumento", () =>
            {
                if (CommitLyric()) Change(() => score.AddPart((string)instrument.SelectedItem));
            }));
            sidebar.Controls.Add(Button("+ Compasso", AddBar));
            sidebar.Controls.Add(Button("Título e andamento", Properties));
            sidebar.Controls.Add(Caption("SELEÇÃO"));
            selection.Width = 172;
            selection.Height = 70;
            selection.ForeColor = TextColor;
            sidebar.Controls.Add(selection);
            editLyricButton = Button("Editar letra", StartLyrics);
            eraseNoteButton = Button("Apagar nota", RemoveNote);
            sidebar.Controls.Add(editLyricButton);
            sidebar.Controls.Add(eraseNoteButton);

            var modes = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 5, 0, 0),
                WrapContents = false, BackColor = Dark };
            AddMode(modes, EditMode.Select, "Selecionar", "V");
            AddMode(modes, EditMode.Write, "Notas", "N");
            AddMode(modes, EditMode.Erase, "Apagar", "E");
            AddMode(modes, EditMode.Lyrics, "Letra", "Shift+L");

            tools.Dock = DockStyle.Top;
            tools.Height = 42;
            tools.Padding = new Padding(12, 3, 0, 0);
            tools.BackColor = PanelColor;
            tools.WrapContents = false;
            for (int i = 0; i < Figures.Length; i++)
            {
                int value = Score.Durations[i];
                var button = Button(Figures[i], () => ChooseDuration(value));
                durationButtons.Add(button);
                tools.Controls.Add(button);
            }
            accidentals.Width = 93;
            accidentals.DropDownStyle = ComboBoxStyle.DropDownList;
            accidentals.AccessibleName = "Alteração da nota";
            accidentals.Items.AddRange(new object[] { "Bemol", "Natural", "Sustenido" });
            accidentals.SelectedIndex = 1;
            accidentals.Margin = new Padding(8, 5, 0, 0);
            accidentals.SelectionChangeCommitted += delegate
            {
                int alter = accidentals.SelectedIndex - 1;
                if (!CommitLyric()) return;
                if (mode != EditMode.Write)
                {
                    var note = SelectedNote();
                    if (note != null) Put(note.Step, note.Length, alter);
                }
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
            zoom.Width = 117;
            zoom.DropDownStyle = ComboBoxStyle.DropDownList;
            zoom.AccessibleName = "Zoom da partitura";
            zoom.Items.AddRange(new object[] { "Ajustar largura", "75%", "90%", "100%", "125%", "150%" });
            zoom.SelectedIndex = 0;
            zoom.Margin = new Padding(13, 5, 0, 0);
            zoom.SelectedIndexChanged += delegate { UpdateZoom(); RevealCursor(); FocusWorkspace(); };
            footer.Controls.Add(zoom);
            status.Width = 380;
            status.Height = 34;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = Muted;
            status.Margin = new Padding(12, 2, 0, 0);
            footer.Controls.Add(status);

            var main = new Panel { Dock = DockStyle.Fill };
            modeHint.Dock = DockStyle.Bottom;
            modeHint.Height = 28;
            modeHint.Padding = new Padding(14, 0, 0, 0);
            modeHint.TextAlign = ContentAlignment.MiddleLeft;
            modeHint.ForeColor = Muted;
            modeHint.BackColor = PanelColor;
            modeHint.Font = new Font("Segoe UI", 9);
            surface.Controls.Add(sheet);
            surface.Resize += delegate { UpdateZoom(); };
            lyricEntry.Visible = false;
            lyricEntry.Font = new Font("Georgia", 11);
            lyricEntry.TextAlign = HorizontalAlignment.Center;
            lyricEntry.MaxLength = Score.MaxLyricLength;
            lyricEntry.AccessibleName = "Letra da nota selecionada";
            lyricEntry.Command = HandleLyricKey;
            lyricEntry.TextChanged += delegate { PlaceLyricEntry(); };
            sheet.Controls.Add(lyricEntry);
            sheet.Pick = PickPosition;
            main.Controls.Add(surface);
            main.Controls.Add(tools);
            main.Controls.Add(modes);
            main.Controls.Add(modeHint);
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
                    ChangePage(sheet.Engraving.PageOf(tick));
                }
            };
            FormClosing += (sender, args) => { args.Cancel = !ConfirmLeave(); if (!args.Cancel) Stop(); };
            saved = score.Snapshot();
            SetDuration(4);
            RefreshView(true);
            SetMode(EditMode.Select);
            Report("Partitura de exemplo aberta");
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

        private sealed class LyricEntry : TextBox
        {
            public Func<Keys, bool> Command;

            protected override bool ProcessCmdKey(ref Message msg, Keys key)
            {
                return (Command != null && Command(key)) || base.ProcessCmdKey(ref msg, key);
            }

            protected override void OnKeyPress(KeyPressEventArgs e)
            {
                if (e.KeyChar == '-' && Command != null && Command(Keys.OemMinus)) e.Handled = true;
                if (e.KeyChar == ' ' && Command != null && Command(Keys.Space)) e.Handled = true;
                base.OnKeyPress(e);
            }
        }

        private void AddMode(FlowLayoutPanel panel, EditMode value, string title, string shortcut)
        {
            var button = Button(title, () =>
            {
                if (value == EditMode.Lyrics) StartLyrics();
                else SetMode(value);
            });
            button.AccessibleDescription = "Atalho " + shortcut;
            button.MinimumSize = new Size(88, 32);
            modeButtons.Add(value, button);
            panel.Controls.Add(button);
        }

        private void SetMode(EditMode value)
        {
            if (!CommitLyric()) return;
            mode = value;
            foreach (var item in modeButtons)
                item.Value.BackColor = item.Key == value ? Teal : PanelColor;
            sheet.ShowEntryPreview = mode == EditMode.Write;
            sheet.Cursor = mode == EditMode.Write ? Cursors.Cross : mode == EditMode.Lyrics ? Cursors.IBeam : Cursors.Default;
            sheet.Hover = null;
            sheet.Invalidate();
            string[] hints = {
                "Selecione uma nota. Use ↑ e ↓ para mudar a altura, ou Delete para apagar.",
                "Escolha a figura e clique na pauta. Use as setas para mover o cursor.",
                "Clique sobre a nota que deseja apagar. Ctrl+Z desfaz.",
                "Clique numa nota para escrever a letra. Espaço avança por palavra e hífen por sílaba."
            };
            modeHint.Text = hints[(int)value];
            ShowDuration();
        }

        private void PickPosition(StaffPosition pick, MouseButtons button)
        {
            if (!CommitLyric()) return;
            position = pick;
            var note = SelectedNote();
            if (button == MouseButtons.Right || mode == EditMode.Select)
            {
                if (note != null)
                {
                    position.Step = note.Step;
                    accidentals.SelectedIndex = note.Alter + 1;
                }
                RefreshView();
            }
            else if (mode == EditMode.Write)
            {
                Put(pick.Step, duration);
            }
            else if (mode == EditMode.Erase)
            {
                if (pick.HitNote) RemoveNote();
                else { RefreshView(); Report("Clique sobre a cabeça da nota para apagar."); }
            }
            else
            {
                RefreshView();
                BeginLyric();
            }
            FocusWorkspace();
        }

        private void RemoveNote()
        {
            if (!CommitLyric()) return;
            var note = SelectedNote();
            if (note == null) return;
            if (Change(() => score.Parts[position.Part].Staves[position.Staff].Remove(note)))
                Report("Nota apagada");
        }

        private void StartLyrics()
        {
            if (editingLyric) { lyricEntry.Focus(); return; }
            SetMode(EditMode.Lyrics);
            BeginLyric();
        }

        private void BeginLyric()
        {
            var note = SelectedNote();
            if (note == null) { Report("Escolha uma nota para escrever a letra."); return; }
            Stop();
            editingLyric = true;
            lyricEntry.Text = note.Lyric ?? "";
            lyricEntry.Visible = true;
            PlaceLyricEntry();
            RevealCursor();
            lyricEntry.BringToFront();
            lyricEntry.Focus();
            lyricEntry.SelectAll();
            modeHint.Text = "Espaço avança por palavra   ·   Hífen separa sílabas   ·   Enter confirma   ·   Esc cancela";
        }

        private void PlaceLyricEntry()
        {
            if (!editingLyric || sheet.Engraving.PageOf(position.Tick) != sheet.Page) return;
            Point target = sheet.OnSheet(position, ScoreLayout.LyricY - 3);
            int textWidth = TextRenderer.MeasureText(lyricEntry.Text, lyricEntry.Font).Width + 18;
            int width = Math.Max((int)(74 * sheet.Zoom), Math.Min(185, textWidth));
            int x = Math.Max(12, Math.Min(sheet.Width - width - 12, target.X - width / 2));
            lyricEntry.SetBounds(x, target.Y, width, lyricEntry.PreferredHeight);
        }

        private void EndLyric()
        {
            editingLyric = false;
            lyricEntry.Visible = false;
            modeHint.Text = "Clique numa nota para escrever a letra. Espaço avança por palavra e hífen por sílaba.";
            FocusWorkspace();
        }

        private bool CommitLyric(int advance = 0, bool? hyphenAfter = null)
        {
            if (!editingLyric) return true;
            var note = SelectedNote();
            bool hyphen = hyphenAfter ?? note.HyphenAfter;
            string value = lyricEntry.Text;
            if (!Change(() => score.SetLyric(position.Part, position.Staff, position.Tick, value, hyphen)))
            {
                lyricEntry.Focus();
                return false;
            }
            EndLyric();
            if (advance != 0 && MoveToAdjacentNote(advance)) BeginLyric();
            return true;
        }

        private bool HandleLyricKey(Keys key)
        {
            if (key == Keys.Escape) { EndLyric(); Report("Edição da letra cancelada"); return true; }
            if (key == Keys.Enter) { CommitLyric(); return true; }
            if (key == Keys.Space) { CommitLyric(1, false); return true; }
            if (key == Keys.OemMinus || key == Keys.Subtract) { CommitLyric(1, true); return true; }
            if (key == Keys.Tab) { CommitLyric(1); return true; }
            if (key == (Keys.Shift | Keys.Tab)) { CommitLyric(-1); return true; }
            return false;
        }

        private void RemoveLyric()
        {
            if (!CommitLyric()) return;
            var note = SelectedNote();
            if (note == null || note.Lyric == null) { Report("A nota selecionada está sem letra."); return; }
            if (Change(() => score.SetLyric(position.Part, position.Staff, position.Tick, null, false)))
                Report("Letra removida da nota");
        }

        private bool MoveToAdjacentNote(int direction)
        {
            var lane = score.Parts[position.Part].Staves[position.Staff];
            var note = direction > 0 ? lane.Find(n => n.Tick > position.Tick) : lane.FindLast(n => n.Tick < position.Tick);
            if (note == null) return false;
            position.Tick = note.Tick;
            position.Step = note.Step;
            accidentals.SelectedIndex = note.Alter + 1;
            RefreshView();
            RevealCursor();
            return true;
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
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 69, 83);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 65, 80);
            button.Click += delegate { action(); FocusWorkspace(); };
            return button;
        }
        private ToolStripMenuItem Item(ToolStripMenuItem menu, string title, Keys shortcut, Action action)
        {
            var item = new ToolStripMenuItem(title) { ForeColor = TextColor, ShortcutKeys = shortcut };
            item.Click += delegate { action(); };
            menu.DropDownItems.Add(item);
            return item;
        }
        private void FocusWorkspace() { if (!editingLyric) ActiveControl = null; }
        private bool Dirty() { return !score.Snapshot().SequenceEqual(saved); }
        private void Report(string message) { status.Text = message; }
        private void Error(Exception ex) { Report(ex.Message); SystemSounds.Exclamation.Play(); }

        private void LayoutSheet()
        {
            surface.AutoScrollMinSize = new Size(sheet.Width + 40, sheet.Height + 48);
            sheet.Location = new Point(Math.Max(20, (surface.ClientSize.Width - sheet.Width) / 2) + surface.AutoScrollPosition.X,
                24 + surface.AutoScrollPosition.Y);
        }

        private void UpdateZoom()
        {
            float scale = zoom.SelectedIndex <= 0
                ? Math.Max(0.60f, Math.Min(1.20f, (surface.ClientSize.Width - 54f) / ScorePage.PageWidth))
                : new[] { 0.75f, 0.90f, 1f, 1.25f, 1.50f }[zoom.SelectedIndex - 1];
            if (sheet.Zoom != scale) sheet.SetZoom(scale);
            LayoutSheet();
            PlaceLyricEntry();
        }

        private void RevealCursor()
        {
            if (sheet.Engraving == null || sheet.Engraving.PageOf(position.Tick) != sheet.Page) return;
            Point target = sheet.OnSheet(position, editingLyric ? ScoreLayout.LyricY + 12 : 32);
            Point view = new Point(sheet.Left + target.X, sheet.Top + target.Y);
            Point current = surface.AutoScrollPosition;
            int margin = editingLyric ? Math.Min(104, surface.ClientSize.Width / 2) : 38;
            int x = Math.Max(margin, Math.Min(surface.ClientSize.Width - margin, view.X));
            int y = Math.Max(65, Math.Min(surface.ClientSize.Height - 42, view.Y));
            surface.AutoScrollPosition = new Point(Math.Max(0, -current.X + view.X - x), Math.Max(0, -current.Y + view.Y - y));
        }

        private void ChangePage(int page)
        {
            if (sheet.Page != page)
            {
                sheet.Page = page;
                surface.AutoScrollPosition = Point.Empty;
                LayoutSheet();
            }
            int count = sheet.Engraving.PageCount;
            pageLabel.Text = "Página " + (page + 1) + " / " + count;
            previousButton.Enabled = page > 0;
            nextButton.Enabled = page + 1 < count;
            sheet.Invalidate();
        }

        private void MovePage(int offset)
        {
            if (!CommitLyric()) return;
            int page = sheet.Page + offset;
            if (page < 0 || page >= sheet.Engraving.PageCount) return;
            position.Tick = sheet.Engraving.OnPage(page).First()[0].Index * Score.BarTicks;
            RefreshView();
        }

        private void RefreshView(bool rebuild = false)
        {
            if (rebuild) sheet.LoadScore(score);
            position.Part = Math.Min(position.Part, score.Parts.Count - 1);
            position.Staff = Math.Min(position.Staff, score.Parts[position.Part].Staves.Count - 1);
            position.Tick = Math.Min(position.Tick, score.Bars * 16 - 1);
            sheet.Selection = position;
            syncingTracks = true;
            tracks.BeginUpdate();
            if (rebuild)
            {
                tracks.Items.Clear();
                lanes.Clear();
                for (int i = 0; i < score.Parts.Count; i++)
                for (int j = 0; j < score.Parts[i].Staves.Count; j++)
                {
                    tracks.Items.Add(score.Parts[i].Name + (score.Parts[i].Staves.Count == 2 ? (j == 0 ? " · sol" : " · fá") : ""));
                    lanes.Add(new StaffPosition { Part = i, Staff = j, Step = j == 0 ? 28 : 21 });
                }
            }
            tracks.SelectedIndex = lanes.FindIndex(l => l.Part == position.Part && l.Staff == position.Staff);
            tracks.EndUpdate();
            syncingTracks = false;
            var note = SelectedNote();
            int step = note == null ? position.Step : note.Step;
            string altered = note == null || note.Alter == 0 ? "" : note.Alter > 0 ? "♯" : "♭";
            selection.Text = "Compasso " + (position.Tick / 16 + 1) + "  ·  " + (position.Tick % 16 + 1) + "/16\n"
                + Score.Names[step % 7] + altered + (step / 7) + (note == null ? "  ·  Cursor" : "  ·  " + Figures[Array.IndexOf(Score.Durations, note.Length)])
                + (note != null && note.Lyric != null ? "\n“" + note.Lyric + "”" : "");
            Text = score.Title + (Dirty() ? " *" : "") + " — Vale Score";
            undoButton.Enabled = undo.Count > 0;
            redoButton.Enabled = redo.Count > 0;
            editLyricButton.Enabled = eraseNoteButton.Enabled = note != null;
            ChangePage(sheet.Engraving.PageOf(position.Tick));
            ShowDuration();
            PlaceLyricEntry();
        }

        private Note SelectedNote()
        {
            return score.Parts[position.Part].Staves[position.Staff].Find(n => n.Tick == position.Tick);
        }

        private bool Change(Action action)
        {
            var before = score.Snapshot();
            bool changed;
            try
            {
                action();
                score.Validate();
                changed = !before.SequenceEqual(score.Snapshot());
                if (changed)
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
                RefreshView(true);
                return false;
            }
            RefreshView(changed);
            return true;
        }

        private void History(Stack<byte[]> from, Stack<byte[]> to)
        {
            if (!CommitLyric()) return;
            if (from.Count == 0) return;
            Stop();
            to.Push(score.Snapshot());
            score = Score.Restore(from.Pop());
            RefreshView(true);
            RevealCursor();
            Report(from == undo ? "Alteração desfeita" : "Alteração refeita");
        }

        private void SetDuration(int value)
        {
            duration = value;
            ShowDuration();
        }

        private void ShowDuration()
        {
            var note = SelectedNote();
            int active = mode == EditMode.Select && note != null ? note.Length : duration;
            for (int i = 0; i < durationButtons.Count; i++)
                durationButtons[i].BackColor = Score.Durations[i] == active ? Teal : PanelColor;
        }

        private void ChooseDuration(int value)
        {
            if (!CommitLyric()) return;
            var note = SelectedNote();
            if (mode == EditMode.Select && note != null)
            {
                Put(note.Step, value, note.Alter);
                return;
            }
            if (mode != EditMode.Write) SetMode(EditMode.Write);
            SetDuration(value);
        }

        private bool Put(int step, int length, int? alteration = null)
        {
            int min = position.Staff == 0 ? 25 : 13;
            if (step < min || step > min + 18) { Report("Altura fora da extensão editável da pauta."); RefreshView(); return false; }
            position.Step = step;
            return Change(() => score.Put(position.Part, position.Staff, position.Tick, length, step, alteration ?? (accidentals.SelectedIndex - 1)));
        }

        private void InsertWithKeyboard(int step)
        {
            if (Put(step, duration))
            {
                int next = position.Tick + duration;
                if (next < score.Bars * Score.BarTicks)
                {
                    position.Tick = next;
                    RefreshView();
                    RevealCursor();
                }
                else Report("Fim da partitura. Adicione um compasso para continuar.");
            }
        }

        private void AddBar()
        {
            if (!CommitLyric()) return;
            if (score.Bars == Score.MaxBars) { Report("Limite de 64 compassos atingido."); return; }
            Change(() => score.Bars++);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (editingLyric) return HandleLyricKey(key) || base.ProcessCmdKey(ref msg, key);
            if (key == (Keys.Shift | Keys.L)) { StartLyrics(); return true; }
            if (ActiveControl is ComboBox || ActiveControl == tracks) return base.ProcessCmdKey(ref msg, key);
            if (key == Keys.Space) { Play(); return true; }
            if (key == Keys.V || key == Keys.Escape) { SetMode(EditMode.Select); return true; }
            if (key == Keys.N) { SetMode(EditMode.Write); return true; }
            if (key == Keys.E) { SetMode(EditMode.Erase); return true; }
            if (key >= Keys.D1 && key <= Keys.D5) { ChooseDuration(Score.Durations[key - Keys.D1]); return true; }
            if (key == Keys.Delete) { RemoveNote(); return true; }
            if (key == Keys.Left || key == Keys.Right)
            {
                int direction = key == Keys.Right ? 1 : -1;
                if (mode != EditMode.Write && MoveToAdjacentNote(direction)) return true;
                position.Tick = Math.Max(0, Math.Min(score.Bars * 16 - 1, position.Tick + direction));
                var note = SelectedNote();
                if (note != null) { position.Step = note.Step; accidentals.SelectedIndex = note.Alter + 1; }
                RefreshView();
                RevealCursor();
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
                    else Put(step, note.Length, note.Alter);
                }
                return true;
            }
            if (mode != EditMode.Write) return base.ProcessCmdKey(ref msg, key);
            if (key == Keys.Enter) { InsertWithKeyboard(position.Step); return true; }
            int degree = Array.IndexOf(PitchKeys, key);
            if (degree >= 0) { InsertWithKeyboard(position.Step / 7 * 7 + degree); return true; }
            return base.ProcessCmdKey(ref msg, key);
        }

        private bool ConfirmLeave()
        {
            if (!CommitLyric()) return false;
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
            RefreshView(true);
            SetMode(score.Parts.SelectMany(part => part.Staves).All(staff => staff.Count == 0) ? EditMode.Write : EditMode.Select);
            surface.AutoScrollPosition = Point.Empty;
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
            if (!CommitLyric()) return false;
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
            if (!CommitLyric()) return;
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
            if (!CommitLyric()) return null;
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
            if (!CommitLyric()) return;
            try
            {
                using (var document = new PrintDocument { DocumentName = score.Title, OriginAtMargins = true })
                using (var dialog = new PrintDialog { Document = document, UseEXDialog = true })
                {
                    document.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
                    var layout = sheet.Engraving;
                    int page = 0;
                    document.PrintPage += (sender, e) =>
                    {
                        float scale = Math.Min(e.MarginBounds.Width / (float)ScorePage.PageWidth, e.MarginBounds.Height / (float)ScorePage.PageHeight);
                        e.Graphics.TranslateTransform((e.MarginBounds.Width - ScorePage.PageWidth * scale) / 2, 0);
                        e.Graphics.ScaleTransform(scale, scale);
                        ScorePage.Draw(e.Graphics, score, layout, page++, null, -1, null);
                        e.HasMorePages = page < layout.PageCount;
                    };
                    if (dialog.ShowDialog(this) == DialogResult.OK) { document.Print(); Report("Partitura enviada para impressão"); }
                }
            }
            catch (Exception ex) { Error(ex); }
        }

        private async void Play()
        {
            if (!CommitLyric()) return;
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
