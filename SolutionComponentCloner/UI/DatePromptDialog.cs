using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SolutionComponentCloner.UI
{
    /// <summary>Asks for a date and, optionally, whose changes to keep; used by "Select modified since".</summary>
    internal sealed class DatePromptDialog : Form
    {
        private const string Anyone = "Anyone";

        private readonly DateTimePicker _picker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Width = 200
        };

        private readonly ComboBox _modifiedBy = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240
        };

        private readonly string _currentUser;

        public DateTime Value => _picker.Value;

        /// <summary>The person whose changes to select, or null for anyone.</summary>
        public string ModifiedBy
        {
            get
            {
                var picked = _modifiedBy.SelectedItem as string;
                if (picked == null || picked == Anyone)
                {
                    return null;
                }

                return picked == MeLabel(_currentUser) ? _currentUser : picked;
            }
        }

        public DatePromptDialog(string title, string prompt, DateTime initial, IEnumerable<string> modifiedByNames = null, string currentUser = null)
        {
            var names = (modifiedByNames ?? Enumerable.Empty<string>()).ToList();
            var askWho = names.Count > 0 || !string.IsNullOrEmpty(currentUser);
            _currentUser = currentUser;

            DialogStyle.Apply(this, title, new Size(380, askWho ? 210 : 150));
            _picker.Value = initial;

            var label = new Label { Text = prompt, AutoSize = true, Location = new Point(16, 16), ForeColor = Theme.TextPrimary };
            _picker.Location = new Point(16, 46);
            Controls.Add(label);
            Controls.Add(_picker);

            if (askWho)
            {
                _modifiedBy.Items.AddRange(ModifiedByChoices(names, currentUser).Cast<object>().ToArray());
                _modifiedBy.SelectedIndex = 0;
                _modifiedBy.Location = new Point(16, 110);
                Controls.Add(new Label { Text = "Modified by:", AutoSize = true, Location = new Point(16, 84), ForeColor = Theme.TextPrimary });
                Controls.Add(_modifiedBy);
            }

            var ok = DialogStyle.Button("Select", true);
            ok.DialogResult = DialogResult.OK;
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
            buttons.Controls.Add(ok);

            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>"Anyone", then the connected user as "Me (name)", then everyone else who modified a component.</summary>
        internal static List<string> ModifiedByChoices(IEnumerable<string> names, string currentUser)
        {
            var choices = new List<string> { Anyone };
            if (!string.IsNullOrEmpty(currentUser))
            {
                choices.Add(MeLabel(currentUser));
            }

            choices.AddRange(names.Where(n => !string.Equals(n, currentUser, StringComparison.OrdinalIgnoreCase)));
            return choices;
        }

        private static string MeLabel(string currentUser) => $"Me ({currentUser})";
    }
}
