using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ValeScore
{
    internal sealed class QuickChoice
    {
        public readonly string Title, Detail;
        public readonly Action Run;
        public QuickChoice(string title, string detail, Action run) { Title = title; Detail = detail; Run = run; }
        public override string ToString() { return Title; }

        public bool Matches(string query)
        {
            string name = Fold(Title + " " + Detail);
            return Fold(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(term => name.Contains(term));
        }

        private static string Fold(string text)
        {
            return new string(text.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        }
    }

    internal sealed class QuickPicker : Form
    {
        private static readonly Color Frame = Color.FromArgb(25, 38, 47);
        private static readonly Color ListBase = Color.FromArgb(17, 29, 36);
        private static readonly Color SelectedBase = Color.FromArgb(42, 74, 79);
        private static readonly Color MainText = Color.FromArgb(242, 247, 246);
        private static readonly Color Hint = Color.FromArgb(179, 199, 202);
        private readonly QuickChoice[] choices;
        private readonly ListBox results = new ListBox();
        private readonly TextBox search = new TextBox();
        private readonly Label message = new Label();
        private readonly Button run;
        private readonly Font detail = new Font("Segoe UI", 9);
        public QuickChoice Choice { get; private set; }

        public QuickPicker(string title, string prompt, IEnumerable<QuickChoice> choices, string action)
        {
            this.choices = choices.ToArray();
            Text = title;
            ClientSize = new Size(565, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 11);
            ForeColor = MainText;
            BackColor = Frame;
            KeyPreview = true;
            var caption = new Label { Text = prompt, Bounds = new Rectangle(20, 17, 520, 27), ForeColor = MainText,
                Font = new Font("Segoe UI Semibold", 12) };
            search.Bounds = new Rectangle(20, 51, 525, 29);
            search.BackColor = ListBase;
            search.ForeColor = MainText;
            search.BorderStyle = BorderStyle.FixedSingle;
            search.AccessibleName = prompt;
            search.TextChanged += delegate { Filter(); };
            results.Bounds = new Rectangle(20, 96, 525, 259);
            results.IntegralHeight = false;
            results.DrawMode = DrawMode.OwnerDrawFixed;
            results.ItemHeight = 49;
            results.BackColor = ListBase;
            results.ForeColor = MainText;
            results.BorderStyle = BorderStyle.None;
            results.AccessibleName = "Resultados";
            results.DrawItem += DrawResult;
            results.DoubleClick += delegate { Choose(); };
            results.SelectedIndexChanged += delegate { UpdateSelection(); };
            message.Bounds = new Rectangle(20, 371, 285, 35);
            message.ForeColor = Hint;
            message.TextAlign = ContentAlignment.MiddleLeft;
            message.Font = detail;
            var cancel = new StudioButton { Text = "Cancelar", DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(321, 369, 103, 33), BackColor = SelectedBase, ForeColor = MainText };
            run = new StudioButton { Text = action, Bounds = new Rectangle(434, 369, 111, 33),
                BackColor = Color.FromArgb(226, 187, 125), ForeColor = ListBase };
            run.Click += delegate { Choose(); };
            Controls.AddRange(new Control[] { caption, search, results, message, cancel, run });
            CancelButton = cancel;
            AcceptButton = run;
            ActiveControl = search;
            Disposed += delegate { detail.Dispose(); };
            Filter();
        }

        private void Filter()
        {
            results.BeginUpdate();
            results.Items.Clear();
            foreach (var choice in choices.Where(c => c.Matches(search.Text))) results.Items.Add(choice);
            if (results.Items.Count > 0) results.SelectedIndex = 0;
            results.EndUpdate();
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            run.Enabled = results.SelectedItem != null;
            message.Text = results.Items.Count == 0 ? "Nenhum resultado" : "↑ ↓  Navegar    Enter  Confirmar";
        }

        private void DrawResult(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var choice = (QuickChoice)results.Items[e.Index];
            bool active = (e.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(active ? SelectedBase : ListBase)) e.Graphics.FillRectangle(fill, e.Bounds);
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, choice.Title, Font,
                new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 5, e.Bounds.Width - 24, 22), MainText, flags);
            TextRenderer.DrawText(e.Graphics, choice.Detail, detail,
                new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 27, e.Bounds.Width - 24, 17), Hint, flags);
            e.DrawFocusRectangle();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (search.Focused && (keyData == Keys.Up || keyData == Keys.Down) && results.Items.Count > 0)
            {
                results.SelectedIndex = Math.Max(0, Math.Min(results.Items.Count - 1,
                    results.SelectedIndex + (keyData == Keys.Up ? -1 : 1)));
                int visible = Math.Max(1, results.ClientSize.Height / results.ItemHeight);
                if (results.SelectedIndex < results.TopIndex) results.TopIndex = results.SelectedIndex;
                else if (results.SelectedIndex >= results.TopIndex + visible) results.TopIndex = results.SelectedIndex - visible + 1;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Choose()
        {
            Choice = results.SelectedItem as QuickChoice;
            if (Choice != null) DialogResult = DialogResult.OK;
        }
    }
}
