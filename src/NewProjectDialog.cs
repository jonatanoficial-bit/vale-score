using System;
using System.Drawing;
using System.Windows.Forms;

namespace ValeScore
{
    internal sealed class NewProjectDialog : Form
    {
        private readonly Score draft;
        private readonly TextBox title = new TextBox(), composer = new TextBox();
        private readonly NumericUpDown bars = new NumericUpDown(), tempo = new NumericUpDown();
        private readonly ComboBox meter = new ComboBox(), key = new ComboBox();
        public Score Project { get { return draft; } }

        public NewProjectDialog(Score draft, string template)
        {
            this.draft = draft;
            Text = "Configurar nova partitura";
            ClientSize = new Size(504, 375);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);
            BackColor = StudioTheme.Chrome;
            ForeColor = StudioTheme.Ivory;
            Controls.Add(new Label { Text = template, AutoEllipsis = true, Bounds = new Rectangle(20, 16, 462, 31),
                Font = new Font("Segoe UI Semibold", 16) });
            title.MaxLength = composer.MaxLength = 80;
            title.Text = draft.Title == "Partitura sem título" ? "" : draft.Title;
            composer.Text = draft.Author;
            bars.Minimum = 1;
            bars.Maximum = Score.MaxBars;
            bars.Value = draft.Bars;
            tempo.Minimum = 30;
            tempo.Maximum = 240;
            tempo.Value = draft.Tempo;
            meter.DropDownStyle = key.DropDownStyle = ComboBoxStyle.DropDownList;
            meter.Items.AddRange(Score.Meters);
            meter.SelectedIndex = Array.IndexOf(Score.Meters, draft.Beats + "/" + draft.BeatType);
            for (int i = 0; i < 30; i++) key.Items.Add(KeySignature.Name(i % 15 - 7, i >= 15));
            key.SelectedIndex = draft.Fifths + 7 + (draft.MinorKey ? 15 : 0);
            StyleInput(title);
            StyleInput(composer);
            StyleInput(meter);
            StyleInput(key);
            StyleInput(bars);
            StyleInput(tempo);
            AddField("Título", title, 58);
            AddField("Compositor", composer, 99);
            AddField("Fórmula de compasso", meter, 140);
            AddField("Tonalidade", key, 181);
            AddField("Quantidade de compassos", bars, 222);
            AddField("Andamento (BPM)", tempo, 263);
            var cancel = new StudioButton { Text = "Cancelar", DialogResult = DialogResult.Cancel, ForeColor = ForeColor,
                BackColor = StudioTheme.Raised, Bounds = new Rectangle(260, 322, 103, 33) };
            var create = new StudioButton { Text = "Criar partitura", ForeColor = StudioTheme.Night,
                BackColor = StudioTheme.Gold, Bounds = new Rectangle(373, 322, 111, 33) };
            create.Click += delegate
            {
                var notation = ((string)meter.SelectedItem).Split('/');
                draft.Title = string.IsNullOrWhiteSpace(title.Text) ? "Partitura sem título" : title.Text.Trim();
                draft.Author = composer.Text.Trim();
                draft.Bars = (int)bars.Value;
                draft.Tempo = (int)tempo.Value;
                draft.SetMeter(int.Parse(notation[0]), int.Parse(notation[1]));
                draft.SetKey(key.SelectedIndex % 15 - 7, key.SelectedIndex >= 15);
                draft.Validate();
                DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { cancel, create });
            CancelButton = cancel;
            AcceptButton = create;
            ActiveControl = title;
        }

        private static void StyleInput(Control input)
        {
            input.BackColor = StudioTheme.Night;
            input.ForeColor = StudioTheme.Ivory;
        }

        private void AddField(string label, Control input, int y)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(20, y + 4) });
            input.AccessibleName = label;
            input.SetBounds(222, y, 261, 29);
            Controls.Add(input);
        }
    }
}

