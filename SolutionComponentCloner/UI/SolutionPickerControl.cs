using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.UI
{
    /// <summary>
    /// A filter box above a plain ListView of matches — the same composition XrmToolBox's own
    /// MsCrmTools.SolutionComponentsMover uses for its solution picker. Deliberately avoids a
    /// ComboBox: filtering a ComboBox's bound Items while it's also driving Text/SelectedItem
    /// leads to exactly the selection bug this replaced. A TextBox and a ListView don't share
    /// that coupling, so filtering never fights with selecting.
    /// </summary>
    internal sealed class SolutionPickerControl
    {
        private List<SolutionListItem> _all = new List<SolutionListItem>();

        public Control Control { get; }
        public SolutionListItem SelectedSolution =>
            _listView.SelectedItems.Count > 0 ? _listView.SelectedItems[0].Tag as SolutionListItem : null;

        public event EventHandler SelectionChanged;

        private readonly TextBox _filterBox;
        private readonly ListView _listView;

        public SolutionPickerControl()
        {
            var container = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150
            };

            _filterBox = new TextBox
            {
                Dock = DockStyle.Top,
                Font = Theme.FontRegular,
                BorderStyle = BorderStyle.FixedSingle
            };
            _filterBox.TextChanged += (s, e) => ApplyFilter();

            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = Theme.FontSmall,
                BorderStyle = BorderStyle.FixedSingle
            };
            _listView.Columns.Add("Solution", 0);
            _listView.Columns.Add("Unique name", 0);
            _listView.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            _listView.Resize += (s, e) => ResizeColumns();

            // Panel.Controls order matters for docking: Fill first, then Top, so the filter box
            // claims its strip before the list view fills the remaining space.
            container.Controls.Add(_listView);
            container.Controls.Add(_filterBox);

            Control = container;
        }

        public void SetSolutions(List<SolutionListItem> solutions)
        {
            var previous = SelectedSolution;
            _all = solutions;
            ApplyFilter(previous?.SolutionId);
        }

        private void ApplyFilter(Guid? preserveSelection = null)
        {
            var filter = _filterBox.Text ?? string.Empty;
            var matches = _all.Where(s =>
                    (s.FriendlyName != null && s.FriendlyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (s.UniqueName != null && s.UniqueName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            var idToPreserve = preserveSelection ?? SelectedSolution?.SolutionId;

            _listView.BeginUpdate();
            _listView.Items.Clear();
            foreach (var solution in matches)
            {
                var item = new ListViewItem(solution.FriendlyName + (solution.IsManaged ? " (Managed)" : string.Empty))
                {
                    Tag = solution
                };
                item.SubItems.Add(solution.UniqueName);
                item.Selected = idToPreserve.HasValue && solution.SolutionId == idToPreserve.Value;
                _listView.Items.Add(item);
            }
            _listView.EndUpdate();
            ResizeColumns();
        }

        private void ResizeColumns()
        {
            if (_listView.Columns.Count < 2 || _listView.Width <= 0)
            {
                return;
            }

            var nameWidth = (int)(_listView.ClientSize.Width * 0.6);
            _listView.Columns[0].Width = nameWidth;
            _listView.Columns[1].Width = Math.Max(0, _listView.ClientSize.Width - nameWidth);
        }
    }
}
