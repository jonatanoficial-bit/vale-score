using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ValeScore
{
    internal sealed class StartScreen : UserControl
    {
        private static readonly Color Ground = StudioTheme.Night;
        private static readonly Color Card = StudioTheme.Panel;
        private static readonly Color PanelInk = StudioTheme.Raised;
        private static readonly Color White = StudioTheme.Ivory;
        private static readonly Color Quiet = StudioTheme.Quiet;
        private static readonly Color Gold = StudioTheme.Gold;
        private static readonly Color Mint = StudioTheme.Mint;
        private readonly ListBox recentList = new ListBox();
        private readonly Label emptyHint;
        private readonly Button resume, openRecent, removeRecent, recovery;
        private readonly Font detailFont = new Font("Segoe UI", 9);
        public Action<string> OpenRecent { get; set; }
        public Action<string> RemoveRecent { get; set; }

        public StartScreen(Action create, Action open, Action importMidi, Action recover, Action resumeEdit,
            Action<ExampleScore> openExample)
        {
            BackColor = Ground;
            ForeColor = White;
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 10);
            AccessibleName = "Central de projetos";
            Disposed += delegate { detailFont.Dispose(); };

            var frame = new CinematicFrame { Dock = DockStyle.Fill, Padding = new Padding(36, 22, 36, 24),
                ColumnCount = 2, RowCount = 2 };
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Ground, Margin = new Padding(0) };
            var brand = new Label { Text = "VALE SCORE", AutoSize = true, ForeColor = Gold, Location = new Point(0, 1),
                Font = new Font("Georgia", 16, FontStyle.Bold) };
            var protocol = new Label { Text = "NOTATION STUDIO  ·  EDIÇÃO PROFISSIONAL", AutoSize = true, ForeColor = Mint, Location = new Point(2, 29),
                Font = new Font("Segoe UI Semibold", 8) };
            var title = new Label { Text = "Central de projetos", AutoSize = true, ForeColor = White, Location = new Point(0, 48),
                Font = new Font("Segoe UI Semibold", 25) };
            resume = ActionButton("Continuar edição", resumeEdit, false);
            resume.Visible = false;
            resume.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.AddRange(new Control[] { brand, protocol, title, resume });
            header.Resize += delegate { resume.Location = new Point(Math.Max(0, header.Width - resume.Width - 4), 58); };
            frame.Controls.Add(header, 0, 0);
            frame.SetColumnSpan(header, 2);

            var left = Section(new Padding(0, 0, 10, 0));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
            var actions = Line();
            actions.Controls.Add(ActionButton("Criar partitura", create, true));
            actions.Controls.Add(ActionButton("Abrir arquivo…", open, false));
            left.Controls.Add(actions, 0, 0);
            left.Controls.Add(Heading("Projetos recentes"), 0, 1);

            var stack = new Panel { BackColor = Card, Dock = DockStyle.Fill, Margin = new Padding(1, 0, 1, 4) };
            recentList.Dock = DockStyle.Fill;
            recentList.IntegralHeight = false;
            recentList.DrawMode = DrawMode.OwnerDrawFixed;
            recentList.ItemHeight = 56;
            recentList.BorderStyle = BorderStyle.None;
            recentList.BackColor = Card;
            recentList.ForeColor = White;
            recentList.AccessibleName = "Projetos recentes";
            recentList.DrawItem += DrawRecent;
            recentList.SelectedIndexChanged += delegate { UpdateRecentButtons(); };
            recentList.DoubleClick += delegate { OpenSelected(); };
            recentList.KeyDown += (sender, args) =>
            {
                if (args.KeyCode != Keys.Enter) return;
                args.SuppressKeyPress = true;
                OpenSelected();
            };
            emptyHint = new Label { Text = "Os projetos salvos ou abertos aparecerão aqui.", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter, ForeColor = Quiet, Height = 70,
                Font = new Font("Segoe UI", 10) };
            stack.Controls.Add(recentList);
            stack.Controls.Add(emptyHint);
            left.Controls.Add(stack, 0, 2);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = Card, Padding = new Padding(0, 4, 0, 0) };
            openRecent = ActionButton("Abrir selecionado", OpenSelected, false);
            removeRecent = ActionButton("Remover da lista", () =>
            {
                var entry = recentList.SelectedItem as string;
                if (entry != null && RemoveRecent != null) RemoveRecent(entry);
            }, false);
            recovery = ActionButton("Recuperar projeto…", recover, false);
            bottom.Controls.Add(openRecent);
            bottom.Controls.Add(removeRecent);
            bottom.Controls.Add(ActionButton("Importar MIDI…", importMidi, false));
            bottom.Controls.Add(recovery);
            left.Controls.Add(bottom, 0, 3);
            frame.Controls.Add(left, 0, 1);

            var right = Section(new Padding(10, 0, 0, 0));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            for (int i = 0; i < ExampleScore.Library.Length; i++) right.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
            right.Controls.Add(Heading("Partituras de exemplo"), 0, 0);
            for (int i = 0; i < ExampleScore.Library.Length; i++)
            {
                var example = ExampleScore.Library[i];
                var entry = new ExampleCard(example) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 7) };
                entry.Click += delegate { openExample(example); };
                right.Controls.Add(entry, 0, i + 1);
            }
            frame.Controls.Add(right, 1, 1);
            Controls.Add(frame);
            UpdateRecentButtons();
        }

        public void RefreshProjects(string[] paths, bool hasRecovery, bool canResume)
        {
            recentList.BeginUpdate();
            recentList.Items.Clear();
            recentList.Items.AddRange(paths);
            if (paths.Length > 0) recentList.SelectedIndex = 0;
            recentList.EndUpdate();
            recentList.Visible = paths.Length > 0;
            emptyHint.Visible = paths.Length == 0;
            openRecent.Visible = paths.Length > 0;
            removeRecent.Visible = paths.Length > 0;
            recovery.Visible = hasRecovery;
            resume.Visible = canResume;
            UpdateRecentButtons();
        }

        private static TableLayoutPanel Section(Padding margin)
        {
            var section = new StudioSection { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Card,
                Padding = new Padding(18, 15, 18, 14), Margin = margin };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return section;
        }

        private static FlowLayoutPanel Line()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Card, Margin = new Padding(0) };
        }

        private static Label Heading(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = White,
                Font = new Font("Segoe UI Semibold", 15), TextAlign = ContentAlignment.MiddleLeft };
        }

        private static StudioButton ActionButton(string text, Action action, bool primary)
        {
            var button = new StudioButton { Text = text, AutoSize = true, MinimumSize = new Size(90, 31),
                Padding = new Padding(8, 0, 8, 0), BackColor = primary ? Gold : PanelInk, ForeColor = White,
                Margin = new Padding(0, 2, 8, 3), Cursor = Cursors.Hand };
            button.Click += delegate { action(); };
            return button;
        }

        private void OpenSelected()
        {
            var path = recentList.SelectedItem as string;
            if (path != null && OpenRecent != null) OpenRecent(path);
        }

        private void UpdateRecentButtons()
        {
            bool selected = recentList.SelectedItem != null;
            openRecent.Enabled = selected;
            removeRecent.Enabled = selected;
        }

        private void DrawRecent(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            string path = (string)recentList.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(selected ? PanelInk : Card)) e.Graphics.FillRectangle(fill, e.Bounds);
            if (selected)
                using (var rail = new SolidBrush(Mint)) e.Graphics.FillRectangle(rail, e.Bounds.X, e.Bounds.Y + 6, 3, e.Bounds.Height - 12);
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, Path.GetFileNameWithoutExtension(path), Font,
                new Rectangle(e.Bounds.X + 11, e.Bounds.Y + 9, e.Bounds.Width - 20, 22), White, flags);
            TextRenderer.DrawText(e.Graphics, Path.GetDirectoryName(path), detailFont,
                new Rectangle(e.Bounds.X + 11, e.Bounds.Y + 32, e.Bounds.Width - 20, 18), Quiet, flags);
            e.DrawFocusRectangle();
        }

        private sealed class ExampleCard : StudioButton
        {
            private readonly ExampleScore example;
            private readonly Score score;
            private readonly ScoreLayout layout;
            private readonly Font titleFont = new Font("Segoe UI Semibold", 12);
            private readonly Font detailFont = new Font("Segoe UI", 9);

            public ExampleCard(ExampleScore example)
            {
                this.example = example;
                AccessibleName = "Abrir exemplo: " + example.Title;
                AccessibleDescription = example.Detail;
                BackColor = PanelInk;
                ForeColor = White;
                Cursor = Cursors.Hand;
                score = example.Create();
                layout = new ScoreLayout(score);
                Disposed += delegate { titleFont.Dispose(); detailFont.Dispose(); };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Width < 240 || Height < 56) return;
                int previewHeight = Math.Min(74, Height - 18);
                var bounds = new Rectangle(11, (Height - previewHeight) / 2, 110, previewHeight);
                var state = e.Graphics.Save();
                e.Graphics.SetClip(bounds);
                e.Graphics.FillRectangle(Brushes.FloralWhite, bounds);
                e.Graphics.TranslateTransform(bounds.Left, bounds.Top);
                e.Graphics.ScaleTransform(.31f, .31f);
                e.Graphics.TranslateTransform(-29, -142);
                ScorePage.Draw(e.Graphics, score, layout, 0, null, -1, null);
                e.Graphics.Restore(state);
                var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                int textWidth = Width - 173;
                int y = Height / 2 - 22;
                using (var accent = new SolidBrush(Gold)) e.Graphics.FillRectangle(accent, 132, y - 5, 28, 2);
                TextRenderer.DrawText(e.Graphics, example.Title, titleFont, new Rectangle(136, y, textWidth, 23), White, flags);
                TextRenderer.DrawText(e.Graphics, example.Detail, detailFont, new Rectangle(136, y + 28, textWidth, 19), Quiet, flags);
                TextRenderer.DrawText(e.Graphics, "›", titleFont, new Rectangle(Width - 30, y + 6, 20, 25), Gold, flags);
            }
        }
    }
}

