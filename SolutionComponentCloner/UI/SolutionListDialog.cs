using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.UI
{
    /// <summary>Lists the solutions that contain one component ("Which solutions contain this?").</summary>
    internal sealed class SolutionListDialog : Form
    {
        public SolutionListDialog(SolutionComponentItem component, List<SolutionListItem> solutions)
        {
            DialogStyle.Apply(this, "Solutions containing this component", new Size(720, 420));
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(520, 300);

            var summary = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 44,
                Padding = new Padding(16, 12, 16, 0),
                Font = Theme.FontBold,
                ForeColor = Theme.TextPrimary,
                AutoEllipsis = true,
                Text = solutions.Count == 0
                    ? $"No visible solution lists {component.DisplayName}."
                    : $"{component.DisplayName} ({component.ComponentTypeName}) is in {solutions.Count} solution{(solutions.Count == 1 ? string.Empty : "s")}."
            };

            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = Theme.FontRegular,
                BorderStyle = BorderStyle.None
            };
            list.Columns.Add("Solution", 260);
            list.Columns.Add("Unique name", 220);
            list.Columns.Add("State", 100);
            list.Columns.Add("Version", 100);
            foreach (var solution in solutions)
            {
                var row = new ListViewItem(solution.FriendlyName);
                row.SubItems.Add(solution.UniqueName);
                row.SubItems.Add(solution.IsManaged ? "Managed" : "Unmanaged");
                row.SubItems.Add(solution.Version);
                list.Items.Add(row);
            }

            var note = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = 36,
                Padding = new Padding(16, 4, 16, 0),
                ForeColor = Theme.TextSecondary,
                Text = "A solution that holds a table with all its subcomponents does not list its columns, forms and views one by one, " +
                       "so it is not shown here for those."
            };

            var close = DialogStyle.Button("Close", true);
            close.DialogResult = DialogResult.OK;
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(12, 10, 12, 0)
            };
            buttons.Controls.Add(close);

            Controls.Add(list);
            Controls.Add(summary);
            Controls.Add(note);
            Controls.Add(buttons);
            AcceptButton = close;
            CancelButton = close;
        }
    }
}
