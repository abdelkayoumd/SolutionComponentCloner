using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.UI
{
    /// <summary>
    /// A combo box that behaves like a search box: typing filters the solution list by
    /// substring match against friendly name / unique name instead of requiring the user to
    /// scroll a closed dropdown to find one solution among hundreds.
    /// </summary>
    internal sealed class SearchableSolutionCombo
    {
        private List<SolutionListItem> _all = new List<SolutionListItem>();
        private bool _suppressEvents;

        public ComboBox Control { get; }
        public SolutionListItem SelectedSolution { get; private set; }
        public event EventHandler SelectionChanged;

        public SearchableSolutionCombo()
        {
            Control = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = Theme.FontRegular,
                FlatStyle = FlatStyle.Flat,
                Height = 26
            };
            Control.TextChanged += OnTextChanged;
            Control.SelectedIndexChanged += OnSelectedIndexChanged;
        }

        public void SetSolutions(List<SolutionListItem> solutions)
        {
            _all = solutions;
            var previous = SelectedSolution;

            Repopulate(_all);

            if (previous != null)
            {
                var match = _all.FirstOrDefault(s => s.SolutionId == previous.SolutionId);
                if (match != null)
                {
                    Select(match, raiseEvent: false);
                }
            }
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }

            var text = Control.Text;

            if (SelectedSolution != null && !string.Equals(SelectedSolution.ToString(), text, StringComparison.Ordinal))
            {
                SelectedSolution = null;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            var matches = string.IsNullOrWhiteSpace(text)
                ? _all
                : _all.Where(s =>
                        (s.FriendlyName != null && s.FriendlyName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (s.UniqueName != null && s.UniqueName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();

            var selectionStart = Control.SelectionStart;
            var selectionLength = Control.SelectionLength;

            _suppressEvents = true;
            Control.BeginUpdate();
            Control.Items.Clear();
            foreach (var match in matches)
            {
                Control.Items.Add(match);
            }
            Control.EndUpdate();
            Control.Text = text;
            Control.SelectionStart = selectionStart;
            Control.SelectionLength = selectionLength;
            _suppressEvents = false;

            Control.DroppedDown = matches.Count > 0 && !string.IsNullOrEmpty(text);
        }

        private void OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }

            if (Control.SelectedIndex >= 0 && Control.Items[Control.SelectedIndex] is SolutionListItem item)
            {
                Select(item, raiseEvent: true);
            }
        }

        private void Repopulate(IEnumerable<SolutionListItem> items)
        {
            _suppressEvents = true;
            Control.BeginUpdate();
            Control.Items.Clear();
            foreach (var item in items)
            {
                Control.Items.Add(item);
            }
            Control.EndUpdate();
            _suppressEvents = false;
        }

        private void Select(SolutionListItem item, bool raiseEvent)
        {
            SelectedSolution = item;

            _suppressEvents = true;
            Control.Text = item.ToString();
            Control.SelectionStart = Control.Text.Length;
            _suppressEvents = false;

            if (raiseEvent)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
