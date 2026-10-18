using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SolutionComponentCloner.Services;

namespace SolutionComponentCloner.UI
{
    /// <summary>Asks the user to confirm before copying model-driven apps or tables that are in the source completely.</summary>
    internal sealed class CopyWarningDialog : Form
    {
        public CopyWarningDialog(IList<CopyWarning> warnings, string targetName)
        {
            DialogStyle.Apply(this, "Review before copying", new Size(860, 420));
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimumSize = new Size(640, 300);

            var banner = new Label
            {
                Dock = DockStyle.Top,
                Height = 56,
                AutoSize = false,
                Padding = new Padding(16, 10, 16, 0),
                BackColor = Theme.WarningBackground,
                ForeColor = Theme.Warning,
                Font = Theme.FontBold,
                Text = $"Your selection includes {warnings.Count} component(s) that bring much more than themselves into {targetName}. " +
                       "Check what will happen to each one."
            };

            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontRegular
            };
            list.Columns.Add("Component", 240);
            list.Columns.Add("Kind", 160);
            list.Columns.Add("What will happen", 440);
            foreach (var warning in warnings)
            {
                var row = new ListViewItem(warning.Item.DisplayName);
                row.SubItems.Add(warning.Kind);
                row.SubItems.Add(warning.Message);
                list.Items.Add(row);
            }

            list.Resize += (s, e) =>
            {
                var rest = list.ClientSize.Width - list.Columns[0].Width - list.Columns[1].Width;
                list.Columns[2].Width = rest > 200 ? rest : 200;
            };

            var copy = DialogStyle.Button("Copy anyway", true);
            copy.DialogResult = DialogResult.OK;
            var cancel = DialogStyle.Button("Cancel", false);
            cancel.DialogResult = DialogResult.Cancel;
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(12, 10, 12, 0)
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(copy);

            Controls.Add(list);
            Controls.Add(banner);
            Controls.Add(buttons);
            AcceptButton = cancel;
            CancelButton = cancel;
        }
    }
}
