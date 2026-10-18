using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.UI
{
    /// <summary>Collects what is needed to create an unmanaged solution to copy into.</summary>
    internal sealed class NewSolutionDialog : Form
    {
        private static readonly Regex UniqueNamePattern = new Regex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex VersionPattern = new Regex(@"^\d+(\.\d+){1,3}$", RegexOptions.Compiled);

        private readonly TextBox _displayName = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _uniqueName = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _version = new TextBox { Dock = DockStyle.Fill, Text = "1.0.0.0" };
        private readonly ComboBox _publisher = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _error = new Label { AutoSize = true, ForeColor = Theme.Danger, Font = Theme.FontSmall, Margin = new Padding(0, 4, 0, 0) };
        private readonly Button _ok = DialogStyle.Button("Create", true);
        private readonly HashSet<string> _existing;
        private bool _uniqueEdited;
        private bool _updatingUnique;

        public string FriendlyName => _displayName.Text.Trim();
        public string UniqueName => _uniqueName.Text.Trim();
        public string Version => _version.Text.Trim();
        public PublisherListItem Publisher => _publisher.SelectedItem as PublisherListItem;

        public NewSolutionDialog(IEnumerable<PublisherListItem> publishers, IEnumerable<string> existingUniqueNames)
        {
            _existing = new HashSet<string>(existingUniqueNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            DialogStyle.Apply(this, "New target solution", new Size(500, 270));

            _publisher.Items.AddRange(publishers.Cast<object>().ToArray());
            if (_publisher.Items.Count > 0)
            {
                _publisher.SelectedIndex = 0;
            }

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(16, 16, 16, 0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddRow(grid, "Display name", _displayName);
            AddRow(grid, "Unique name", _uniqueName);
            AddRow(grid, "Version", _version);
            AddRow(grid, "Publisher", _publisher);
            grid.Controls.Add(new Label(), 0, 4);
            grid.Controls.Add(_error, 1, 4);

            var cancel = DialogStyle.Button("Cancel", false);
            cancel.DialogResult = DialogResult.Cancel;
            _ok.DialogResult = DialogResult.OK;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(12, 10, 12, 0)
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(_ok);

            Controls.Add(grid);
            Controls.Add(buttons);
            AcceptButton = _ok;
            CancelButton = cancel;

            _displayName.TextChanged += (s, e) =>
            {
                if (!_uniqueEdited)
                {
                    _updatingUnique = true;
                    _uniqueName.Text = SuggestUniqueName(_displayName.Text);
                    _updatingUnique = false;
                }

                Validate();
            };
            _uniqueName.TextChanged += (s, e) =>
            {
                if (!_updatingUnique)
                {
                    _uniqueEdited = true;
                }

                Validate();
            };
            _version.TextChanged += (s, e) => Validate();
            _publisher.SelectedIndexChanged += (s, e) => Validate();
            Validate();
        }

        private static void AddRow(TableLayoutPanel grid, string label, Control input)
        {
            input.Margin = new Padding(0, 4, 0, 4);
            var row = grid.RowCount++;
            grid.Controls.Add(DialogStyle.FieldLabel(label), 0, row);
            grid.Controls.Add(input, 1, row);
        }

        /// <summary>"My new solution" becomes "MyNewSolution": letters and digits only, starting with a letter.</summary>
        public static string SuggestUniqueName(string displayName)
        {
            var builder = new StringBuilder();
            foreach (var word in Regex.Split(displayName ?? string.Empty, "[^A-Za-z0-9]+").Where(w => w.Length > 0))
            {
                builder.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1));
            }

            var name = builder.ToString();
            if (name.Length > 0 && !char.IsLetter(name[0]))
            {
                name = "S" + name;
            }

            return name;
        }

        public new bool Validate()
        {
            string problem = null;
            if (FriendlyName.Length == 0)
            {
                problem = "Enter a display name.";
            }
            else if (!UniqueNamePattern.IsMatch(UniqueName))
            {
                problem = "The unique name must start with a letter and contain only letters, digits and underscores.";
            }
            else if (_existing.Contains(UniqueName))
            {
                problem = "A solution with this unique name already exists.";
            }
            else if (!VersionPattern.IsMatch(Version))
            {
                problem = "The version must look like 1.0.0.0.";
            }
            else if (Publisher == null)
            {
                problem = "Choose a publisher.";
            }

            _error.Text = problem ?? string.Empty;
            _ok.Enabled = problem == null;
            return problem == null;
        }
    }
}
