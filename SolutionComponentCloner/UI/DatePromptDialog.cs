using System;
using System.Drawing;
using System.Windows.Forms;

namespace SolutionComponentCloner.UI
{
    /// <summary>Asks for a date, used by "Select modified since".</summary>
    internal sealed class DatePromptDialog : Form
    {
        private readonly DateTimePicker _picker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Width = 200
        };

        public DateTime Value => _picker.Value;

        public DatePromptDialog(string title, string prompt, DateTime initial)
        {
            DialogStyle.Apply(this, title, new Size(380, 150));
            _picker.Value = initial;

            var label = new Label { Text = prompt, AutoSize = true, Location = new Point(16, 16), ForeColor = Theme.TextPrimary };
            _picker.Location = new Point(16, 46);

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

            Controls.Add(label);
            Controls.Add(_picker);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
