using System.Drawing;
using System.Windows.Forms;

namespace SolutionComponentCloner.UI
{
    internal static class DialogStyle
    {
        public static Form Apply(Form form, string title, Size size)
        {
            form.Text = title;
            form.ClientSize = size;
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.Font = Theme.FontRegular;
            form.BackColor = Theme.PanelBackground;
            return form;
        }

        public static Button Button(string text, bool primary)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                Font = primary ? Theme.FontBold : Theme.FontRegular,
                BackColor = primary ? Theme.Accent : Theme.PanelBackground,
                ForeColor = primary ? Color.White : Theme.Accent,
                Padding = new Padding(14, 2, 14, 2),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Theme.Accent;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            return button;
        }

        public static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextSecondary,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 12, 6)
            };
        }
    }
}
