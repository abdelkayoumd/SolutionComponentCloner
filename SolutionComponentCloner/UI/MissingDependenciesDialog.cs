using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.UI
{
    /// <summary>
    /// Shows the components the target solution depends on but does not contain, and lets the user tick
    /// which ones to add to it.
    /// </summary>
    internal sealed class MissingDependenciesDialog : Form
    {
        private readonly GroupedComponentGrid _grid;

        /// <summary>The components ticked when "Add to target" was clicked.</summary>
        public List<SolutionComponentItem> Chosen => _grid.SelectedComponents.ToList();

        public MissingDependenciesDialog(SolutionComponentLoadResult result, string targetName, bool targetIsManaged)
        {
            DialogStyle.Apply(this, "Missing dependencies", new Size(900, 560));
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(640, 360);

            var nothingMissing = result.Items.Count == 0;
            var summary = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = nothingMissing ? 44 : 62,
                Padding = new Padding(16, 12, 16, 0),
                ForeColor = Theme.TextPrimary,
                Text = nothingMissing
                    ? $"{targetName} contains everything it depends on."
                    : $"{targetName} depends on {result.Items.Count} component{(result.Items.Count == 1 ? string.Empty : "s")} it does not contain. " +
                      "Unmanaged ones are ticked: add them so the solution imports cleanly elsewhere. " +
                      "Managed ones usually come from another solution that must be installed in the destination environment first."
            };

            // Managed components belong to another solution; pre-tick only what the user can reasonably add.
            foreach (var item in result.Items)
            {
                item.Selected = !targetIsManaged && item.IsManaged != true;
            }

            _grid = new GroupedComponentGrid { Dock = DockStyle.Fill, ShowCheckBoxes = !targetIsManaged };
            _grid.SetItems(result.Items);
            _grid.ExpandAll();

            var canAdd = !nothingMissing && !targetIsManaged;
            var add = DialogStyle.Button("Add to target", true);
            add.DialogResult = DialogResult.OK;
            add.Visible = canAdd;
            var close = DialogStyle.Button(canAdd ? "Cancel" : "Close", !canAdd);
            close.DialogResult = DialogResult.Cancel;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(12, 10, 12, 0)
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(add);

            if (result.Warnings.Count > 0)
            {
                buttons.Controls.Add(new Label
                {
                    Text = string.Join("  ", result.Warnings),
                    AutoSize = true,
                    ForeColor = Theme.Warning,
                    Margin = new Padding(0, 6, 12, 0)
                });
            }

            _grid.ComponentSelectionChanged += (s, e) =>
            {
                var count = _grid.SelectedCount;
                add.Enabled = count > 0;
                add.Text = count > 0 ? $"Add {count} to target" : "Add to target";
            };
            _grid.NotifySelectionChanged();

            Controls.Add(_grid);
            Controls.Add(summary);
            Controls.Add(buttons);
            AcceptButton = canAdd ? add : close;
            CancelButton = close;
        }
    }
}
