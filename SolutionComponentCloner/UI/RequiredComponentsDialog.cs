using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.UI
{
    /// <summary>Shows what "Include required components" would add, using the same tree as the main window.</summary>
    internal sealed class RequiredComponentsDialog : Form
    {
        public RequiredComponentsDialog(SolutionComponentLoadResult result, int selectedCount)
        {
            DialogStyle.Apply(this, "Required components", new Size(900, 560));
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(640, 360);

            var summary = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 44,
                Padding = new Padding(16, 12, 16, 0),
                Font = Theme.FontBold,
                ForeColor = Theme.TextPrimary,
                Text = result.Items.Count == 0
                    ? $"Nothing extra is required: your {selectedCount} selected component(s) have no other dependencies."
                    : $"{result.Items.Count} required component(s) would be added along with your {selectedCount} selected."
            };

            var grid = new GroupedComponentGrid { Dock = DockStyle.Fill, ShowCheckBoxes = false };
            grid.SetItems(result.Items);
            grid.ExpandAll();

            var ok = DialogStyle.Button("Close", true);
            ok.DialogResult = DialogResult.OK;
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(12, 10, 12, 0)
            };
            buttons.Controls.Add(ok);

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

            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(buttons);
            AcceptButton = ok;
        }
    }
}
