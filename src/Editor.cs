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
        private static readonly Color Dark = Color.FromArgb(19, 23, 35);
        private static readonly Color PanelColor = Color.FromArgb(28, 34, 49);
        private static readonly Color TextColor = Color.FromArgb(245, 245, 253);
        private static readonly Color Muted = Color.FromArgb(180, 187, 210);
        private static readonly Color Accent = Color.FromArgb(86, 62, 160);
        private static readonly Color Lavender = Color.FromArgb(197, 178, 255);
        private static readonly string[] Figures = { "Semibreve", "Mínima", "Semínima", "Colcheia", "Semicolcheia" };
        private static readonly Keys[] PitchKeys = { Keys.A, Keys.S, Keys.D, Keys.F, Keys.G, Keys.H, Keys.J };
        private enum EditMode { Select, Write, Rest, Erase, Lyrics }
        private readonly ScorePage sheet = new ScorePage();
        private readonly Panel surface = new Panel { AutoScroll = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(122, 132, 153) };
        private readonly FlowLayoutPanel tools = new FlowLayoutPanel();
        private readonly ListBox tracks = new ListBox();
        private readonly Label pageLabel = new Label(), status = new Label(), selection = new Label(), modeHint = new Label();
        private readonly Button undoButton, redoButton, playButton, previousButton, nextButton;
        private readonly Button editLyricButton, eraseNoteButton;
        private readonly Label documentTitle = new Label(), documentInfo = new Label();
        private readonly Label playbackLabel = new Label();
        private readonly TransportRail transportRail = new TransportRail();
        private readonly ComboBox midiOutput = new ComboBox();
        private readonly ToolStripMenuItem inputMenu = new ToolStripMenuItem("Teclado MIDI");
        private readonly ContextMenuStrip exportMenu = new ContextMenuStrip();
        private readonly List<Button> durationButtons = new List<Button>();
        private readonly List<Button> restButtons = new List<Button>();
        private readonly List<Button> accidentalButtons = new List<Button>();
        private readonly Button dotButton, doubleDotButton;
        private readonly Dictionary<EditMode, Button> modeButtons = new Dictionary<EditMode, Button>();
        private readonly ToolTip figureTips = new ToolTip();
        private readonly ComboBox zoom = new ComboBox();
        private readonly LyricEntry lyricEntry = new LyricEntry();
        private readonly Stack<byte[]> undo = new Stack<byte[]>(), redo = new Stack<byte[]>();
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Timer recoveryTimer = new Timer { Interval = 12000 };
        private readonly Recovery recovery = new Recovery(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ValeScore", "Recuperacao"));
        private byte[] recoverySnapshot;
        private Score copiedBar;
        private readonly Stopwatch clock = new Stopwatch();
        private MemoryStream playback;
        private SoundPlayer player;
        private int playbackGeneration;
        private int inputGeneration;
        private MidiDevices.Input midiInput;
        private System.Threading.CancellationTokenSource midiCancellation;
        private System.Threading.Tasks.Task midiPlaybackTask;
        private MidiSequence loadedMidi;
        private string midiName;
        private bool playingMidiFile;
        private double playbackLength;
        private bool preparing, syncingTracks, editingLyric;
        private EditMode mode = EditMode.Select;
        private Score score = Score.New(true);
        private string path;
        private byte[] saved;
        private int duration = 4;
        private int inputAlter;
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
            Item(file, "Recuperar projeto…", Keys.None, Recover);
            Item(file, "Abrir MIDI para ouvir…", Keys.None, OpenMidi);
            Item(file, "Importar MIDI como partitura…", Keys.Control | Keys.I, ImportMidiScore);
            Item(file, "Importar MusicXML ou MXL…", Keys.None, ImportMusicXml);
            Item(file, "Salvar", Keys.Control | Keys.S, () => Save(false));
            Item(file, "Salvar como…", Keys.Control | Keys.Shift | Keys.S, () => Save(true));
            file.DropDownItems.Add(new ToolStripSeparator());
            Item(file, "Exportar MIDI…", Keys.None, ExportMidi);
            Item(file, "Exportar MusicXML…", Keys.None, ExportMusicXml);
            Item(file, "Exportar MusicXML compactado…", Keys.None, ExportMxl);
            Item(file, "Exportar WAV…", Keys.None, ExportWave);
            Item(file, "Exportar página como PNG…", Keys.None, ExportPng);
            Item(file, "Imprimir / PDF…", Keys.Control | Keys.P, Print);
            Item(file, "Abrir partitura de exemplo", Keys.None, () => New(true));
            var edit = new ToolStripMenuItem("Editar");
            Item(edit, "Desfazer", Keys.Control | Keys.Z, () => History(undo, redo));
            Item(edit, "Refazer", Keys.Control | Keys.Y, () => History(redo, undo));
            Item(edit, "Editar letra da nota", Keys.None, StartLyrics).ShortcutKeyDisplayString = "Shift+L";
            Item(edit, "Remover letra da nota", Keys.None, RemoveLyric);
            Item(edit, "Propriedades da partitura…", Keys.None, Properties);
            Item(edit, "Adicionar compasso", Keys.None, AddBar);
            Item(edit, "Duplicar compasso atual", Keys.Control | Keys.D, DuplicateCurrentBar);
            Item(edit, "Copiar compasso atual", Keys.Control | Keys.Shift | Keys.C, CopyCurrentBar);
            Item(edit, "Colar no compasso atual", Keys.Control | Keys.Shift | Keys.V, PasteCurrentBar);
            Item(edit, "Inserir compasso antes do atual", Keys.None, InsertCurrentBar);
            Item(edit, "Remover compasso atual…", Keys.None, RemoveCurrentBar);
            Item(edit, "Ir para compasso…", Keys.Control | Keys.G, GoToBar);
            Item(edit, "Transpor…", Keys.Control | Keys.T, TransposeRange);
            var help = new ToolStripMenuItem("Ajuda");
            Item(help, "Atalhos de edição", Keys.F1, () => MessageBox.Show(this,
                "V seleciona, N escreve notas, R escreve pausas e E apaga. Esc volta à seleção.\n" +
                "Na pauta, use ← / → para navegar e ↑ / ↓ para mudar a altura.\n" +
                "No modo Notas, A S D F G H J inserem Dó a Si. Enter insere a altura do cursor.\n" +
                "1 a 5 escolhem a figura da ferramenta ativa. Delete apaga a figura selecionada.\n" +
                "Shift+L abre a letra sob a nota. Espaço avança por palavra e hífen por sílaba.\n" +
                "Na letra, Enter confirma, Esc cancela e Shift+Tab volta à nota anterior.\n" +
                "Espaço toca ou para fora da letra. Ctrl+Z desfaz. Ctrl+Y refaz.", "Atalhos do Vale Score"));
            var playbackMenu = new ToolStripMenuItem("Reprodução");
            Item(playbackMenu, "Tocar ou parar partitura", Keys.None, Play);
            Item(playbackMenu, "Abrir MIDI para ouvir…", Keys.None, OpenMidi);
            Item(playbackMenu, "Tocar ou parar MIDI aberto", Keys.None, PlayLoadedMidi);
            playbackMenu.DropDownItems.Add(inputMenu);
            inputMenu.ForeColor = TextColor;
            inputMenu.DropDownOpening += delegate { PopulateInputs(); };
            menu.Items.AddRange(new ToolStripItem[] { file, edit, playbackMenu, help });

            var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(17, 9, 0, 4), WrapContents = false, BackColor = PanelColor };
            var brand = new Label { Text = "Vale Score", ForeColor = Lavender, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
                Width = 168, Height = 34, Font = new Font("Segoe UI Semibold", 17) };
            header.Controls.Add(brand);
            header.Controls.Add(Button("Novo", () => New(false)));
            header.Controls.Add(Button("Abrir", Open));
            header.Controls.Add(Button("Importar MIDI", ImportMidiScore));
            header.Controls.Add(Button("Salvar", () => Save(false)));
            undoButton = Button("Desfazer", () => History(undo, redo));
            redoButton = Button("Refazer", () => History(redo, undo));
            header.Controls.Add(undoButton);
            header.Controls.Add(redoButton);
            var exportButton = Button("↓  Exportar");
            exportButton.BackColor = Accent;
            exportMenu.BackColor = PanelColor;
            exportMenu.ForeColor = TextColor;
            exportMenu.Renderer = new DarkMenu();
            AddExportItem("MIDI  ·  .mid", ExportMidi);
            AddExportItem("MusicXML  ·  .musicxml", ExportMusicXml);
            AddExportItem("MusicXML compactado  ·  .mxl", ExportMxl);
            AddExportItem("Instrumento selecionado  ·  MusicXML", ExportPart);
            AddExportItem("Áudio  ·  .wav", ExportWave);
            AddExportItem("Página atual  ·  .png", ExportPng);
            AddExportItem("Imprimir ou salvar PDF", Print);
            exportButton.Click += delegate { exportMenu.Show(exportButton, new Point(0, exportButton.Height)); };
            header.Controls.Add(exportButton);

            var body = new Panel { Dock = DockStyle.Fill };
            var left = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = PanelColor, Padding = new Padding(14, 8, 12, 0) };
            var sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            left.Controls.Add(sidebar);
            documentTitle.Width = 172;
            documentTitle.Height = 49;
            documentTitle.Font = new Font("Segoe UI Semibold", 13);
            documentTitle.ForeColor = TextColor;
            sidebar.Controls.Add(documentTitle);
            documentInfo.Width = 172;
            documentInfo.Height = 24;
            documentInfo.ForeColor = Lavender;
            documentInfo.Font = new Font("Segoe UI Semibold", 9);
            sidebar.Controls.Add(documentInfo);
            sidebar.Controls.Add(Caption("PAUTAS"));
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
            eraseNoteButton = Button("Apagar seleção", RemoveNote);
            sidebar.Controls.Add(editLyricButton);
            sidebar.Controls.Add(eraseNoteButton);

            var modes = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 5, 0, 0),
                WrapContents = false, BackColor = Dark };
            AddMode(modes, EditMode.Select, "Selecionar", "V");
            AddMode(modes, EditMode.Write, "Notas", "N");
            AddMode(modes, EditMode.Rest, "Pausas", "R");
            AddMode(modes, EditMode.Erase, "Apagar", "E");
            AddMode(modes, EditMode.Lyrics, "Letra", "Shift+L");

            tools.Dock = DockStyle.Top;
            tools.Height = 62;
            tools.Padding = new Padding(12, 0, 0, 0);
            tools.BackColor = PanelColor;
            tools.WrapContents = false;
            AddFigureGroup("FIGURAS", durationButtons, false);
            AddFigureGroup("PAUSAS", restButtons, true);
            var alterations = AddPaletteGroup("ALTERAÇÕES", 3);
            string[] names = { "Bemol", "Bequadro", "Sustenido" };
            for (int i = 0; i < names.Length; i++)
            {
                int alter = i - 1;
                var button = new MusicButton((g, ink) => MusicGlyphs.Accidental(g, ink, 0, 0, alter));
                AddMusicButton(alterations, button, names[i], () => ChooseAccidental(alter));
                accidentalButtons.Add(button);
            }
            var dots = AddPaletteGroup("PONTOS", 2);
            dotButton = new MusicButton((g, ink) => g.FillEllipse(ink, -2.4f, -2.4f, 4.8f, 4.8f));
            doubleDotButton = new MusicButton((g, ink) =>
            {
                g.FillEllipse(ink, -6, -2.4f, 4.8f, 4.8f);
                g.FillEllipse(ink, 1.2f, -2.4f, 4.8f, 4.8f);
            });
            AddMusicButton(dots, (MusicButton)dotButton, "Ponto de aumento (.)", () => ApplyDots(1));
            AddMusicButton(dots, (MusicButton)doubleDotButton, "Dois pontos de aumento", () => ApplyDots(2));
            Disposed += delegate { figureTips.Dispose(); exportMenu.Dispose(); timer.Dispose(); recoveryTimer.Dispose(); };

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
            footer.Controls.Add(Button("Ir para…", GoToBar));
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

            var transport = new Panel { Dock = DockStyle.Bottom, Height = 53, BackColor = PanelColor };
            var transportControls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
                Padding = new Padding(9, 3, 0, 0), BackColor = PanelColor };
            playButton = Button("▶  Tocar", Play);
            playButton.AutoSize = false;
            playButton.Width = 87;
            playButton.BackColor = Accent;
            transportControls.Controls.Add(playButton);
            transportControls.Controls.Add(Button("Abrir MIDI…", OpenMidi));
            midiOutput.DropDownStyle = ComboBoxStyle.DropDownList;
            midiOutput.AccessibleName = "Saída de áudio ou MIDI";
            midiOutput.Width = 168;
            midiOutput.Margin = new Padding(9, 5, 3, 0);
            midiOutput.DropDown += delegate { RefreshOutputs(); };
            transportControls.Controls.Add(midiOutput);
            playbackLabel.Text = "0:00 / 0:00";
            playbackLabel.AutoSize = false;
            playbackLabel.Width = 118;
            playbackLabel.Height = 33;
            playbackLabel.TextAlign = ContentAlignment.MiddleCenter;
            playbackLabel.ForeColor = Lavender;
            transportControls.Controls.Add(playbackLabel);
            transportRail.Dock = DockStyle.Bottom;
            transport.Controls.Add(transportControls);
            transport.Controls.Add(transportRail);
            RefreshOutputs();

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
            main.Controls.Add(transport);
            body.Controls.Add(main);
            body.Controls.Add(left);
            Controls.Add(body);
            Controls.Add(header);
            Controls.Add(menu);
            MainMenuStrip = menu;

            timer.Tick += delegate
            {
                UpdateTransport(clock.Elapsed.TotalSeconds);
                if (playingMidiFile)
                {
                    if (midiCancellation == null && clock.Elapsed.TotalSeconds >= playbackLength) Stop();
                    return;
                }
                int tick = (int)(clock.Elapsed.TotalSeconds * score.Tempo / 15);
                if (tick >= score.Bars * 16)
                {
                    if (midiCancellation == null) Stop();
                }
                else if (tick != sheet.Playing)
                {
                    sheet.Playing = tick;
                    ChangePage(sheet.Engraving.PageOf(tick));
                }
            };
            FormClosing += (sender, args) =>
            {
                args.Cancel = !ConfirmLeave();
                if (!args.Cancel) { Stop(); CloseMidiInput(); ClearRecovery(); }
            };
            saved = score.Snapshot();
            SetDuration(4);
            RefreshView(true);
            SetMode(EditMode.Select);
            Report("Partitura de exemplo aberta");
            recoveryTimer.Tick += delegate { SaveRecovery(); };
            Shown += delegate
            {
                recoveryTimer.Start();
                try { if (recovery.HasCopies()) Report("Há cópias de recuperação em Arquivo → Recuperar projeto"); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            };
        }

        private sealed class DarkMenu : ToolStripProfessionalRenderer
        {
            public DarkMenu() : base(new MenuColors()) { }
        }
        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Accent; } }
            public override Color MenuItemSelectedGradientBegin { get { return Accent; } }
            public override Color MenuItemSelectedGradientEnd { get { return Accent; } }
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

        private FlowLayoutPanel AddPaletteGroup(string title, int count)
        {
            var group = new Panel { Width = count * 35, Height = 59, Margin = new Padding(0, 0, 13, 0) };
            group.Controls.Add(new Label { Text = title, Location = new Point(1, 2), AutoSize = true,
                ForeColor = Muted, Font = new Font("Segoe UI Semibold", 8) });
            var row = new FlowLayoutPanel { Location = new Point(0, 17), Width = group.Width, Height = 41, WrapContents = false };
            group.Controls.Add(row);
            tools.Controls.Add(group);
            return row;
        }

        private void AddMusicButton(FlowLayoutPanel row, MusicButton button, string label, Action action)
        {
            button.AccessibleName = label;
            button.BackColor = PanelColor;
            button.ForeColor = TextColor;
            button.FlatAppearance.BorderColor = Color.FromArgb(61, 78, 94);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 65, 80);
            figureTips.SetToolTip(button, label);
            button.Click += delegate { action(); FocusWorkspace(); };
            row.Controls.Add(button);
        }

        private void AddFigureGroup(string title, List<Button> buttons, bool rest)
        {
            var row = AddPaletteGroup(title, Figures.Length);
            for (int i = 0; i < Figures.Length; i++)
            {
                int length = Score.Durations[i];
                var button = MusicButton.Figure(length, rest);
                string label = rest ? "Pausa de " + Figures[i].ToLowerInvariant() : Figures[i];
                AddMusicButton(row, button, label, () => ChooseDuration(length, rest));
                buttons.Add(button);
            }
        }

        private void ChooseAccidental(int alter)
        {
            if (!CommitLyric()) return;
            var note = SelectedNote();
            if (mode == EditMode.Select && note != null)
            {
                Put(note.Step, note.Length, alter);
            }
            else
            {
                inputAlter = alter;
                if (mode != EditMode.Write) SetMode(EditMode.Write);
            }
            ShowDuration();
        }

        private void SetMode(EditMode value)
        {
            if (!CommitLyric()) return;
            mode = value;
            foreach (var item in modeButtons)
                item.Value.BackColor = item.Key == value ? Accent : PanelColor;
            sheet.ShowEntryPreview = mode == EditMode.Write || mode == EditMode.Rest;
            sheet.PreviewRest = mode == EditMode.Rest;
            sheet.Cursor = sheet.ShowEntryPreview ? Cursors.Cross : mode == EditMode.Lyrics ? Cursors.IBeam : Cursors.Default;
            sheet.Hover = null;
            sheet.Invalidate();
            string[] hints = {
                "Selecione uma nota. Use ↑ e ↓ para mudar a altura, ou Delete para apagar.",
                "Escolha a figura e clique na pauta. Use as setas para mover o cursor.",
                "Escolha uma pausa e clique na posição desejada. Enter insere no cursor.",
                "Clique na nota ou pausa que deseja apagar. Ctrl+Z desfaz.",
                "Clique numa nota para escrever a letra. Espaço avança por palavra e hífen por sílaba."
            };
            modeHint.Text = hints[(int)value];
            ShowDuration();
        }

        private void PickPosition(StaffPosition pick, MouseButtons button)
        {
            if (!CommitLyric()) return;
            if (button == MouseButtons.Left && duration == Score.BarTicks && (mode == EditMode.Rest || mode == EditMode.Write))
                pick.Tick -= pick.Tick % Score.BarTicks;
            position = pick;
            var note = SelectedNote();
            if (button == MouseButtons.Right || mode == EditMode.Select)
            {
                if (note != null)
                {
                    position.Step = note.Step;
                }
                RefreshView();
            }
            else if (mode == EditMode.Write)
            {
                Put(pick.Step, duration);
            }
            else if (mode == EditMode.Rest)
            {
                PutRest(duration);
            }
            else if (mode == EditMode.Erase)
            {
                if (pick.HitNote) RemoveNote();
                else { RefreshView(); Report("Clique sobre uma nota ou pausa para apagar."); }
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
            var note = SelectedEvent();
            if (note == null) return;
            if (Change(() => score.Parts[position.Part].Staves[position.Staff].Remove(note)))
                Report(note.IsRest ? "Pausa apagada" : "Nota apagada");
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
            bool pitchedOnly = mode == EditMode.Lyrics;
            var note = direction > 0
                ? lane.Find(n => n.Tick > position.Tick && (!pitchedOnly || !n.IsRest))
                : lane.FindLast(n => n.Tick < position.Tick && (!pitchedOnly || !n.IsRest));
            if (note == null) return false;
            position.Tick = note.Tick;
            if (!note.IsRest) position.Step = note.Step;
            RefreshView();
            RevealCursor();
            return true;
        }

        private Label Caption(string text)
        {
            return new Label { Text = text, Width = 180, Height = 25, ForeColor = Muted,
                Margin = new Padding(3, 13, 0, 1), Font = new Font("Segoe UI Semibold", 9) };
        }
        private Button Button(string text, Action action = null)
        {
            var button = new Button { Text = text, Height = 32, AutoSize = true, FlatStyle = FlatStyle.Flat,
                BackColor = PanelColor, ForeColor = TextColor, Cursor = Cursors.Hand, Margin = new Padding(3, 2, 3, 3) };
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 69, 83);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 65, 80);
            if (action != null) button.Click += delegate { action(); FocusWorkspace(); };
            return button;
        }
        private ToolStripMenuItem Item(ToolStripMenuItem menu, string title, Keys shortcut, Action action)
        {
            var item = new ToolStripMenuItem(title) { ForeColor = TextColor, ShortcutKeys = shortcut };
            item.Click += delegate { action(); };
            menu.DropDownItems.Add(item);
            return item;
        }
        private void AddExportItem(string title, Action action)
        {
            var item = new ToolStripMenuItem(title) { ForeColor = TextColor };
            item.Click += delegate { action(); };
            exportMenu.Items.Add(item);
        }
        private void ExportMidi() { Export("Arquivo MIDI (*.mid)|*.mid", MidiSequence.Export); }
        private void ExportMusicXml() { Export("MusicXML (*.musicxml)|*.musicxml", Output.MusicXml); }
        private void ExportMxl() { Export("MusicXML compactado (*.mxl)|*.mxl", Output.CompressedMusicXml); }
        private void ExportPart()
        {
            Export("Parte MusicXML (*.musicxml)|*.musicxml", (model, output) =>
                Output.MusicXml(model.ExtractPart(position.Part), output), score.Parts[position.Part].Name);
        }
        private void FocusWorkspace() { if (!editingLyric) ActiveControl = null; }
        private bool Dirty() { return saved == null || !score.Snapshot().SequenceEqual(saved); }
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
            var selected = SelectedEvent();
            var note = selected != null && !selected.IsRest ? selected : null;
            int step = note == null ? position.Step : note.Step;
            string altered = note == null || note.Alter == 0 ? "" : note.Alter > 0 ? "♯" : "♭";
            selection.Text = "Compasso " + (position.Tick / 16 + 1) + "  ·  " + (position.Tick % 16 + 1) + "/16\n"
                + (selected != null && selected.IsRest ? "Pausa" : Score.Names[step % 7] + altered + (step / 7))
                + (selected == null ? "  ·  Cursor" : "  ·  " + Figures[Array.IndexOf(Score.Durations, Score.BaseDuration(selected.Length))]
                    + new string('·', Score.DotCount(selected.Length)))
                + (note != null && note.Lyric != null ? "\n“" + note.Lyric + "”" : "");
            bool dirty = Dirty();
            Text = score.Title + (dirty ? " *" : "") + " — Vale Score";
            documentTitle.Text = score.Title;
            documentInfo.Text = score.Tempo + " BPM  /  4⁄4  /  " + (dirty ? "Editado" : path == null ? "Local" : "Salvo");
            undoButton.Enabled = undo.Count > 0;
            redoButton.Enabled = redo.Count > 0;
            editLyricButton.Enabled = note != null;
            eraseNoteButton.Enabled = selected != null;
            ChangePage(sheet.Engraving.PageOf(position.Tick));
            ShowDuration();
            PlaceLyricEntry();
        }

        private Note SelectedNote()
        {
            var note = SelectedEvent();
            return note != null && !note.IsRest ? note : null;
        }

        private Note SelectedEvent()
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
            var selected = SelectedEvent();
            bool showingSelection = mode == EditMode.Select && selected != null;
            int active = showingSelection ? selected.Length : duration;
            bool rest = showingSelection ? selected.IsRest : mode == EditMode.Rest;
            for (int i = 0; i < durationButtons.Count; i++)
            {
                bool same = Score.Durations[i] == Score.BaseDuration(active);
                durationButtons[i].BackColor = !rest && same ? Accent : PanelColor;
                restButtons[i].BackColor = rest && same ? Accent : PanelColor;
            }
            dotButton.BackColor = Score.DotCount(active) == 1 ? Accent : PanelColor;
            doubleDotButton.BackColor = Score.DotCount(active) == 2 ? Accent : PanelColor;
            int accidental = showingSelection && !selected.IsRest ? selected.Alter : inputAlter;
            for (int i = 0; i < accidentalButtons.Count; i++)
                accidentalButtons[i].BackColor = !rest && i == accidental + 1 ? Accent : PanelColor;
            sheet.PreviewLength = duration;
            sheet.PreviewAlter = inputAlter;
            sheet.Invalidate();
        }

        private void ChooseDuration(int value, bool rest)
        {
            if (!CommitLyric()) return;
            var selected = SelectedEvent();
            if (mode == EditMode.Select && selected != null && selected.IsRest == rest)
            {
                if (rest) PutRest(value);
                else Put(selected.Step, value, selected.Alter);
                return;
            }
            var tool = rest ? EditMode.Rest : EditMode.Write;
            if (mode != tool) SetMode(tool);
            SetDuration(value);
        }

        private void ApplyDots(int count)
        {
            if (!CommitLyric()) return;
            var selected = SelectedEvent();
            bool editing = mode == EditMode.Select && selected != null;
            int active = editing ? selected.Length : duration;
            int basis = Score.BaseDuration(active);
            int numerator = count == 1 ? 3 : 7;
            int denominator = count == 1 ? 2 : 4;
            bool clearing = Score.DotCount(active) == count;
            int length = clearing ? basis : basis * numerator / denominator;
            if (!clearing && (basis * numerator % denominator != 0 || length > Score.BarTicks))
            {
                Report("O ponto não cabe na grade rítmica desta figura.");
                return;
            }
            if (editing)
            {
                if (selected.IsRest) PutRest(length);
                else Put(selected.Step, length, selected.Alter);
            }
            else SetDuration(length);
        }

        private bool Put(int step, int length, int? alteration = null)
        {
            int min = position.Staff == 0 ? 25 : 13;
            if (step < min || step > min + 18) { Report("Altura fora da extensão editável da pauta."); RefreshView(); return false; }
            position.Step = step;
            return Change(() => score.Put(position.Part, position.Staff, position.Tick, length, step, alteration ?? inputAlter));
        }

        private bool PutRest(int length)
        {
            return Change(() => score.PutRest(position.Part, position.Staff, position.Tick, length));
        }

        private void AdvanceInput()
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

        private void AddBar()
        {
            if (!CommitLyric()) return;
            if (score.Bars == Score.MaxBars) { Report("Limite de 64 compassos atingido."); return; }
            Change(() => score.Bars++);
        }

        private void GoToBar()
        {
            if (!CommitLyric()) return;
            using (var dialog = new Form { Text = "Ir para compasso", ClientSize = new Size(320, 108),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false, MaximizeBox = false, Font = Font })
            {
                var number = new NumericUpDown { Minimum = 1, Maximum = score.Bars,
                    Value = position.Tick / Score.BarTicks + 1, Bounds = new Rectangle(18, 18, 104, 28), AccessibleName = "Número do compasso" };
                var go = new Button { Text = "Ir para", DialogResult = DialogResult.OK, Bounds = new Rectangle(134, 62, 80, 28) };
                var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(222, 62, 80, 28) };
                dialog.Controls.AddRange(new Control[] { number, go, cancel });
                dialog.AcceptButton = go;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK) SelectBar((int)number.Value - 1);
            }
        }

        private void SelectBar(int bar)
        {
            position.Tick = bar * Score.BarTicks;
            RefreshView();
            RevealCursor();
            Report("Compasso " + (bar + 1) + " de " + score.Bars);
        }

        private void DuplicateCurrentBar()
        {
            if (!CommitLyric()) return;
            int bar = position.Tick / Score.BarTicks;
            if (Change(() => score.DuplicateBar(bar))) SelectBar(bar + 1);
        }

        private void InsertCurrentBar()
        {
            if (!CommitLyric()) return;
            int bar = position.Tick / Score.BarTicks;
            if (Change(() => score.InsertBar(bar))) SelectBar(bar);
        }

        private void CopyCurrentBar()
        {
            if (!CommitLyric()) return;
            copiedBar = score.CopyBar(position.Tick / Score.BarTicks);
            Report("Compasso copiado. Ctrl+Shift+V cola nas pautas do compasso atual.");
        }

        private void PasteCurrentBar()
        {
            if (!CommitLyric()) return;
            if (copiedBar == null) { Report("Copie um compasso primeiro."); return; }
            if (Change(() => score.PasteBar(position.Tick / Score.BarTicks, copiedBar)))
                Report("Compasso colado. Ctrl+Z desfaz.");
        }

        private void RemoveCurrentBar()
        {
            if (!CommitLyric()) return;
            int bar = position.Tick / Score.BarTicks;
            if (MessageBox.Show(this, "Remover o compasso " + (bar + 1) + " de todas as pautas? Ctrl+Z desfaz.",
                "Remover compasso", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (Change(() => score.RemoveBar(bar))) SelectBar(Math.Min(bar, score.Bars - 1));
        }

        private void TransposeRange()
        {
            if (!CommitLyric()) return;
            using (var dialog = new Form { Text = "Transpor partitura", ClientSize = new Size(405, 240),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false, MaximizeBox = false, Font = Font })
            {
                int bar = position.Tick / Score.BarTicks + 1;
                var first = new NumericUpDown { Minimum = 1, Maximum = score.Bars, Value = bar, AccessibleName = "Primeiro compasso" };
                var last = new NumericUpDown { Minimum = 1, Maximum = score.Bars, Value = bar, AccessibleName = "Último compasso" };
                var method = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Tipo de transposição" };
                method.Items.AddRange(new object[] { "Cromática (semitons)", "Diatônica (graus)" });
                method.SelectedIndex = 0;
                var amount = new NumericUpDown { Minimum = -24, Maximum = 24, Value = 1, AccessibleName = "Deslocamento" };
                var whole = new CheckBox { Text = "Todas as partes", Checked = true, AutoSize = true, Location = new Point(16, 160) };
                Control[] fields = { first, last, method, amount };
                string[] names = { "Primeiro compasso", "Último compasso", "Tipo", "Deslocamento" };
                for (int i = 0; i < fields.Length; i++)
                {
                    int y = 15 + i * 34;
                    dialog.Controls.Add(new Label { Text = names[i], AutoSize = true, Location = new Point(16, y + 4) });
                    fields[i].SetBounds(178, y, 208, 28);
                    dialog.Controls.Add(fields[i]);
                }
                var apply = new Button { Text = "Transpor", Bounds = new Rectangle(206, 197, 84, 28) };
                var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(302, 197, 84, 28) };
                apply.Click += delegate
                {
                    if (first.Value > last.Value)
                    {
                        MessageBox.Show(dialog, "O último compasso precisa ser igual ou posterior ao primeiro.");
                        return;
                    }
                    int count = 0;
                    if (Change(() => count = score.Transpose((int)first.Value - 1, (int)last.Value - 1,
                        whole.Checked ? null : (int?)position.Part, (int)amount.Value, method.SelectedIndex == 0)))
                    {
                        Report(count + " notas no intervalo de transposição");
                        dialog.DialogResult = DialogResult.OK;
                    }
                    else MessageBox.Show(dialog, status.Text, "Transposição", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                dialog.Controls.AddRange(new Control[] { whole, apply, cancel });
                dialog.AcceptButton = apply;
                dialog.CancelButton = cancel;
                dialog.ShowDialog(this);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (editingLyric) return HandleLyricKey(key) || base.ProcessCmdKey(ref msg, key);
            if (key == (Keys.Shift | Keys.L)) { StartLyrics(); return true; }
            if (ActiveControl is ComboBox || ActiveControl == tracks) return base.ProcessCmdKey(ref msg, key);
            if (key == Keys.Space) { Play(); return true; }
            if (key == Keys.V || key == Keys.Escape) { SetMode(EditMode.Select); return true; }
            if (key == Keys.N) { SetMode(EditMode.Write); return true; }
            if (key == Keys.R) { SetMode(EditMode.Rest); return true; }
            if (key == Keys.E) { SetMode(EditMode.Erase); return true; }
            if (key >= Keys.D1 && key <= Keys.D5)
            {
                var selected = SelectedEvent();
                bool rest = mode == EditMode.Rest || (mode == EditMode.Select && selected != null && selected.IsRest);
                ChooseDuration(Score.Durations[key - Keys.D1], rest);
                return true;
            }
            if (key == Keys.Delete) { RemoveNote(); return true; }
            if (key == Keys.OemPeriod || key == Keys.Decimal) { ApplyDots(1); return true; }
            if (key == Keys.Left || key == Keys.Right)
            {
                int direction = key == Keys.Right ? 1 : -1;
                if (mode != EditMode.Write && mode != EditMode.Rest && MoveToAdjacentNote(direction)) return true;
                position.Tick = Math.Max(0, Math.Min(score.Bars * 16 - 1, position.Tick + direction));
                var note = SelectedNote();
                if (note != null) position.Step = note.Step;
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
            if (key == Keys.Enter && mode == EditMode.Rest)
            {
                if (PutRest(duration)) AdvanceInput();
                return true;
            }
            if (mode != EditMode.Write) return base.ProcessCmdKey(ref msg, key);
            if (key == Keys.Enter)
            {
                if (Put(position.Step, duration)) AdvanceInput();
                return true;
            }
            int degree = Array.IndexOf(PitchKeys, key);
            if (degree >= 0)
            {
                if (Put(position.Step / 7 * 7 + degree, duration)) AdvanceInput();
                return true;
            }
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
            ClearRecovery();
            score = model;
            path = file;
            saved = score.Snapshot();
            undo.Clear();
            redo.Clear();
            position = new StaffPosition { Step = 28 };
            inputAlter = 0;
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
            using (var dialog = new OpenFileDialog { Filter = "Partituras (*.valescore;*.musicxml;*.mxl;*.xml)|*.valescore;*.musicxml;*.mxl;*.xml|Projeto Vale Score (*.valescore)|*.valescore|MusicXML (*.musicxml;*.mxl;*.xml)|*.musicxml;*.mxl;*.xml" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (!string.Equals(Path.GetExtension(dialog.FileName), ".valescore", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenMusicXmlProject(dialog.FileName);
                        return;
                    }
                    var model = Score.Open(dialog.FileName);
                    if (!ConfirmLeave()) return;
                    LoadScore(model, dialog.FileName);
                    Report("Projeto aberto");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Não foi possível abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void ImportMusicXml()
        {
            using (var dialog = new OpenFileDialog { Filter = "MusicXML (*.musicxml;*.mxl;*.xml)|*.musicxml;*.mxl;*.xml" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { OpenMusicXmlProject(dialog.FileName); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Importação MusicXML", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void OpenMusicXmlProject(string file)
        {
            var imported = MusicXmlImport.Open(file);
            if (MessageBox.Show(this, imported.Report + Environment.NewLine + Environment.NewLine +
                "Abrir a partitura convertida?", "Importação MusicXML", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK ||
                !ConfirmLeave()) return;
            LoadScore(imported.Project, null);
            saved = null;
            SaveRecovery();
            RefreshView();
            Report("MusicXML importado. Salve como projeto Vale Score.");
        }

        private void SaveRecovery()
        {
            try
            {
                var current = score.Snapshot();
                if (saved != null && current.SequenceEqual(saved))
                {
                    if (recoverySnapshot != null) ClearRecovery();
                    return;
                }
                if (recoverySnapshot != null && current.SequenceEqual(recoverySnapshot)) return;
                recovery.Save(score);
                recoverySnapshot = current;
            }
            catch (IOException) { Report("Não foi possível criar a cópia de recuperação."); }
            catch (UnauthorizedAccessException) { Report("Sem acesso à pasta de recuperação."); }
        }

        private void ClearRecovery()
        {
            try { recovery.Clear(); recoverySnapshot = null; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void Recover()
        {
            try
            {
                if (!recovery.HasCopies()) { Report("Nenhuma cópia de recuperação disponível"); return; }
                using (var dialog = new OpenFileDialog { Title = "Recuperar projeto",
                    InitialDirectory = recovery.Folder, Filter = "Cópia Vale Score (*.valescore)|*.valescore" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var recovered = Score.Open(dialog.FileName);
                    if (!ConfirmLeave()) return;
                    LoadScore(recovered, null);
                    saved = null;
                    SaveRecovery();
                    RefreshView();
                    Report("Projeto recuperado. Use Salvar para escolher o arquivo final.");
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Recuperação", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
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
                ClearRecovery();
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

        private string ExportPath(string filter, string suffix = null)
        {
            if (!CommitLyric()) return null;
            using (var dialog = new SaveFileDialog { Filter = filter,
                FileName = SafeFileName() + (suffix == null ? "" : " - " + string.Concat(suffix.Select(c =>
                    Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).TrimEnd('.', ' ')) })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }
        private void Export(string filter, Action<Score, Stream> write, string suffix = null)
        {
            string destination = ExportPath(filter, suffix);
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

        private void ExportPng()
        {
            int page = sheet.Page;
            Export("Imagem PNG (*.png)|*.png", (model, output) => Output.PagePng(model, sheet.Engraving,
                Math.Min(page, sheet.Engraving.PageCount - 1), output));
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

        private void RefreshOutputs()
        {
            var previous = midiOutput.SelectedItem as MidiDevices.Device;
            midiOutput.Items.Clear();
            midiOutput.Items.Add("Som interno");
            foreach (var device in MidiDevices.Outputs()) midiOutput.Items.Add(device);
            midiOutput.SelectedIndex = 0;
            if (previous == null) return;
            for (int i = 1; i < midiOutput.Items.Count; i++)
            {
                var device = (MidiDevices.Device)midiOutput.Items[i];
                if (device.Id == previous.Id && device.Name == previous.Name) { midiOutput.SelectedIndex = i; break; }
            }
        }

        private void PopulateInputs()
        {
            foreach (ToolStripItem old in inputMenu.DropDownItems.Cast<ToolStripItem>().ToArray()) old.Dispose();
            inputMenu.DropDownItems.Clear();
            Item(inputMenu, midiInput == null ? "Entrada desligada" : "Desconectar teclado", Keys.None, CloseMidiInput)
                .Enabled = midiInput != null;
            var devices = MidiDevices.Inputs();
            foreach (var device in devices)
                Item(inputMenu, device.Name, Keys.None, () => ConnectMidiInput(device));
            if (devices.Count == 0) Item(inputMenu, "Nenhum teclado MIDI detectado", Keys.None, () => { }).Enabled = false;
        }

        private void ConnectMidiInput(MidiDevices.Device device)
        {
            if (!CommitLyric()) return;
            CloseMidiInput();
            int generation = inputGeneration;
            try
            {
                midiInput = new MidiDevices.Input(device, pitch =>
                {
                    // WinMM callbacks must leave UI and driver operations to their respective threads.
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (generation == inputGeneration && !IsDisposed && mode == EditMode.Write &&
                                !preparing && !timer.Enabled && !editingLyric) EnterMidiPitch(pitch);
                        }));
                    }
                    catch (InvalidOperationException) { }
                });
                SetMode(EditMode.Write);
                Report("Entrada MIDI ativa · " + device.Name);
            }
            catch (Exception ex) { Error(ex); }
        }

        private void CloseMidiInput()
        {
            ++inputGeneration;
            if (midiInput == null) return;
            midiInput.Dispose();
            midiInput = null;
            Report("Entrada MIDI desligada");
        }

        private void EnterMidiPitch(int pitch)
        {
            int octave = pitch / 12 - 1;
            int tone = pitch % 12;
            int step = Array.FindLastIndex(Score.Semitones, value => value <= tone);
            if (Put(octave * 7 + step, duration, tone - Score.Semitones[step])) AdvanceInput();
        }

        private void ImportMidiScore()
        {
            using (var dialog = new OpenFileDialog { Filter = "Arquivos MIDI (*.mid;*.midi)|*.mid;*.midi" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var sequence = MidiSequence.Open(dialog.FileName);
                    var imported = MidiImport.Convert(sequence, Path.GetFileNameWithoutExtension(dialog.FileName));
                    var decision = MessageBox.Show(this, imported.Report + Environment.NewLine + Environment.NewLine +
                        "Abrir como um novo projeto editável?", "Importar MIDI", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                    if (decision != DialogResult.OK || !ConfirmLeave()) return;
                    LoadScore(imported.Project, null);
                    saved = null;
                    loadedMidi = sequence;
                    midiName = Path.GetFileName(dialog.FileName);
                    RefreshView();
                    Report(imported.Imported + " notas importadas · salve como projeto Vale Score");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Não foi possível importar o MIDI", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void OpenMidi()
        {
            if (!CommitLyric()) return;
            using (var dialog = new OpenFileDialog { Filter = "Arquivos MIDI (*.mid;*.midi)|*.mid;*.midi" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var sequence = MidiSequence.Open(dialog.FileName);
                    Stop();
                    loadedMidi = sequence;
                    midiName = Path.GetFileName(dialog.FileName);
                    PlayLoadedMidi();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Não foi possível abrir o MIDI", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void PlayLoadedMidi()
        {
            if (loadedMidi == null) { OpenMidi(); return; }
            StartPlayback(loadedMidi);
        }

        private void Play() { StartPlayback(null); }

        private async void StartPlayback(MidiSequence file)
        {
            if (!CommitLyric()) return;
            if (preparing || timer.Enabled) { Stop(); return; }
            if (midiPlaybackTask != null && !midiPlaybackTask.IsCompleted)
            {
                Report("A saída MIDI está encerrando a reprodução.");
                return;
            }
            int generation = ++playbackGeneration;
            preparing = true;
            playingMidiFile = file != null;
            playButton.Text = "■  Parar";
            Report("Preparando reprodução…");
            var output = midiOutput.SelectedItem as MidiDevices.Device;
            var cancellation = new System.Threading.CancellationTokenSource();
            midiCancellation = cancellation;
            try
            {
                playbackLength = file == null ? score.Bars * 240.0 / score.Tempo : file.Duration;
                UpdateTransport(0);
                if (output != null)
                {
                    MidiSequence sequence = file;
                    if (sequence == null)
                    {
                        using (var buffer = new MemoryStream())
                        {
                            MidiSequence.Export(score, buffer);
                            buffer.Position = 0;
                            sequence = MidiSequence.Read(buffer);
                        }
                    }
                    if (file == null) ChangePage(0);
                    preparing = false;
                    timer.Start();
                    Report(file == null ? "Partitura · " + output.Name : midiName + " · " + output.Name);
                    midiPlaybackTask = MidiDevices.Play(sequence, output, clock, cancellation.Token);
                    await midiPlaybackTask;
                    if (generation == playbackGeneration && !IsDisposed) Stop();
                    return;
                }

                var snapshot = file == null ? Score.Restore(score.Snapshot()) : null;
                var wave = await Task.Run(() => file == null ? Output.Wave(snapshot) : Output.MidiWave(file, cancellation.Token));
                if (generation != playbackGeneration || IsDisposed) { wave.Dispose(); return; }
                playback = wave;
                player = new SoundPlayer(playback);
                player.Load();
                player.Play();
                preparing = false;
                clock.Restart();
                if (file == null) ChangePage(0);
                timer.Start();
                Report(file == null ? "Partitura · Som interno" : midiName + " · Som interno");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (generation == playbackGeneration && !IsDisposed) { Stop(); Error(ex); }
            }
            finally
            {
                if (ReferenceEquals(midiCancellation, cancellation)) midiCancellation = null;
                cancellation.Dispose();
            }
        }

        private void UpdateTransport(double elapsed)
        {
            elapsed = Math.Min(elapsed, playbackLength);
            transportRail.UpdateProgress(playbackLength > 0 ? elapsed / playbackLength : 0);
            playbackLabel.Text = FormatTime(elapsed) + " / " + FormatTime(playbackLength);
        }

        private static string FormatTime(double seconds)
        {
            int time = (int)seconds;
            return (time / 60) + ":" + (time % 60).ToString("00");
        }

        private void Stop()
        {
            ++playbackGeneration;
            bool wasPlaying = preparing || timer.Enabled;
            preparing = false;
            playingMidiFile = false;
            if (midiCancellation != null) midiCancellation.Cancel();
            timer.Stop();
            clock.Reset();
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (playback != null) { playback.Dispose(); playback = null; }
            sheet.Playing = -1;
            sheet.Invalidate();
            playButton.Text = "▶  Tocar";
            UpdateTransport(0);
            if (wasPlaying) Report("Reprodução encerrada");
        }
    }
}
