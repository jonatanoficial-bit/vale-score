using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace ValeScore
{
    internal sealed class PublicationDialog : Form
    {
        private readonly Score score;
        private readonly int selectedPart;
        private readonly ComboBox scope = new ComboBox(), paper = new ComboBox(), orientation = new ComboBox();
        private readonly NumericUpDown margin = new NumericUpDown();
        private readonly Panel preview = new PreviewPanel();
        private readonly Button previous = new Button(), next = new Button();
        private readonly Label pageLabel = new Label();
        private Publication publication;
        private int page;
        public string ResultStatus { get; private set; }

        public PublicationDialog(Score score, int selectedPart, bool partOnly)
        {
            this.score = score;
            this.selectedPart = selectedPart;
            Text = "Preparar publicação";
            ClientSize = new Size(920, 660);
            MinimumSize = new Size(660, 460);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(245, 247, 250);

            var settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 67, Padding = new Padding(10, 4, 0, 0),
                WrapContents = false, AutoScroll = true, BackColor = BackColor };
            ConfigureCombo(scope, "Conteúdo", new[] { "Partitura completa", "Parte: " + score.Parts[selectedPart].Name }, 211, partOnly ? 1 : 0, settings);
            ConfigureCombo(paper, "Papel", Publication.PaperNames, 91, 0, settings);
            ConfigureCombo(orientation, "Orientação", new[] { "Retrato", "Paisagem" }, 110, 0, settings);
            margin.Minimum = 5;
            margin.Maximum = 35;
            margin.Value = 12;
            AddField(settings, "Margens (mm)", margin, 109);
            scope.SelectedIndexChanged += delegate { ChangeScope(); };
            paper.SelectedIndexChanged += delegate { ChangeScope(); };
            orientation.SelectedIndexChanged += delegate { ChangeScope(); };
            margin.ValueChanged += delegate { ChangeScope(); };

            preview.Dock = DockStyle.Fill;
            preview.BackColor = Color.FromArgb(109, 118, 139);
            preview.AccessibleName = "Prévia paginada da partitura";
            preview.Paint += PaintPreview;
            preview.Resize += delegate { preview.Invalidate(); };

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 47, Padding = new Padding(10, 5, 0, 0),
                WrapContents = false, AutoScroll = true, BackColor = BackColor };
            ConfigureButton(previous, "‹", 36, () => Navigate(-1), actions);
            previous.AccessibleName = "Página anterior";
            pageLabel.Size = new Size(112, 32);
            pageLabel.TextAlign = ContentAlignment.MiddleCenter;
            actions.Controls.Add(pageLabel);
            ConfigureButton(next, "›", 36, () => Navigate(1), actions);
            next.AccessibleName = "Próxima página";
            ConfigureButton(new Button(), "Imprimir / PDF…", 136, Print, actions);
            ConfigureButton(new Button(), "Páginas PNG…", 132, ExportPages, actions);
            var close = new Button { DialogResult = DialogResult.Cancel };
            ConfigureButton(close, "Fechar", 79, Close, actions);
            CancelButton = close;

            Controls.Add(preview);
            Controls.Add(settings);
            Controls.Add(actions);
            ChangeScope();
        }

        private sealed class PreviewPanel : Panel
        {
            public PreviewPanel() { DoubleBuffered = true; }
        }

        private static void AddField(FlowLayoutPanel settings, string caption, Control input, int width)
        {
            var field = new Panel { Width = width, Height = 54, Margin = new Padding(4, 0, 8, 0) };
            var label = new Label { Text = caption, AutoSize = true, Location = new Point(0, 1), ForeColor = Color.FromArgb(43, 52, 69) };
            input.SetBounds(0, 24, width, 27);
            input.AccessibleName = caption;
            field.Controls.Add(label);
            field.Controls.Add(input);
            settings.Controls.Add(field);
        }

        private static void ConfigureCombo(ComboBox combo, string caption, string[] values, int width, int index, FlowLayoutPanel settings)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.AddRange(values);
            combo.SelectedIndex = index;
            AddField(settings, caption, combo, width);
        }

        private static void ConfigureButton(Button button, string text, int width, Action action, FlowLayoutPanel group)
        {
            button.Text = text;
            button.Size = new Size(width, 32);
            button.Click += delegate { action(); };
            group.Controls.Add(button);
        }

        private void ChangeScope()
        {
            publication = new Publication(score, scope.SelectedIndex == 1 ? (int?)selectedPart : null,
                paper.SelectedIndex, Landscape, CurrentMargin);
            page = 0;
            Navigate(0);
        }

        private void Navigate(int offset)
        {
            page = Math.Max(0, Math.Min(publication.PageCount - 1, page + offset));
            pageLabel.Text = "Página " + (page + 1) + " / " + publication.PageCount;
            previous.Enabled = page > 0;
            next.Enabled = page + 1 < publication.PageCount;
            preview.Invalidate();
        }

        private int CurrentMargin { get { return Publication.Margin((float)margin.Value); } }
        private bool Landscape { get { return orientation.SelectedIndex == 1; } }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            if (publication == null || preview.Width < 60 || preview.Height < 60) return;
            Size physical = Publication.Paper(paper.SelectedIndex, Landscape);
            float scale = Math.Min((preview.Width - 42f) / physical.Width, (preview.Height - 42f) / physical.Height);
            var paperRect = new RectangleF((preview.Width - physical.Width * scale) / 2,
                (preview.Height - physical.Height * scale) / 2, physical.Width * scale, physical.Height * scale);
            e.Graphics.FillRectangle(Brushes.White, paperRect);
            float padding = CurrentMargin * scale;
            var available = RectangleF.Inflate(paperRect, -padding, -padding);
            using (var border = new Pen(Color.FromArgb(216, 220, 229)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                e.Graphics.DrawRectangle(border, available.X, available.Y, available.Width, available.Height);
            publication.Draw(e.Graphics, page, available);
        }

        private void Print()
        {
            try
            {
                using (var document = new PrintDocument { DocumentName = publication.Title, OriginAtMargins = true })
                using (var dialog = new PrintDialog { Document = document, UseEXDialog = true })
                {
                    if (document.PrinterSettings.IsValid) publication.Configure(document);
                    document.PrinterSettings.MinimumPage = 1;
                    document.PrinterSettings.MaximumPage = publication.PageCount;
                    document.PrinterSettings.FromPage = 1;
                    document.PrinterSettings.ToPage = publication.PageCount;
                    dialog.AllowSomePages = true;
                    int current = 0, last = publication.PageCount;
                    document.BeginPrint += delegate
                    {
                        if (document.PrinterSettings.PrintRange == PrintRange.SomePages)
                        {
                            current = Math.Max(0, Math.Min(publication.PageCount - 1, document.PrinterSettings.FromPage - 1));
                            last = Math.Max(current + 1, Math.Min(publication.PageCount, document.PrinterSettings.ToPage));
                        }
                    };
                    document.PrintPage += (sender, e) =>
                    {
                        publication.Draw(e.Graphics, current++, new RectangleF(0, 0, e.MarginBounds.Width, e.MarginBounds.Height));
                        e.HasMorePages = current < last;
                    };
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    publication.Configure(document);
                    document.Print();
                    ResultStatus = "Partitura enviada para impressão";
                }
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Impressão", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ExportPages()
        {
            string name = string.Join("_", publication.Title.Split(Path.GetInvalidFileNameChars())).TrimEnd('.', ' ');
            using (var dialog = new SaveFileDialog { Filter = "Páginas PNG compactadas (*.zip)|*.zip", FileName = name + " - páginas" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string directory = Path.GetDirectoryName(dialog.FileName);
                string temporary = Path.Combine(directory, ".valescore-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    Cursor = Cursors.WaitCursor;
                    using (var destination = new FileStream(temporary, FileMode.CreateNew))
                        publication.ExportPngPages(destination);
                    if (File.Exists(dialog.FileName)) File.Replace(temporary, dialog.FileName, null);
                    else File.Move(temporary, dialog.FileName);
                    ResultStatus = publication.PageCount + " página(s) PNG exportadas";
                    MessageBox.Show(this, ResultStatus, "Exportação", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Exportação", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally
                {
                    Cursor = Cursors.Default;
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
