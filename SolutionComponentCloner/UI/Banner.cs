using System;
using System.Drawing;
using System.Windows.Forms;

namespace SolutionComponentCloner.UI
{
    internal enum BannerKind { Success, Error, Warning, Info }

    /// <summary>A Dynamics-style form notification: a coloured strip with an icon, a message and an optional link.</summary>
    internal sealed class Banner : Panel
    {
        private readonly Label _icon;
        private readonly Label _message;
        private readonly Label _link;
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 20000 };
        private readonly Label _secondLink;
        private Action _onLink;
        private Action _onSecondLink;

        public Banner()
        {
            Dock = DockStyle.Fill;
            Height = 32;
            Margin = new Padding(0);
            Visible = false;

            _icon = new Label
            {
                Font = Theme.FontIcon,
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _link = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 110,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Theme.FontRegular, FontStyle.Underline),
                ForeColor = Theme.Accent,
                Cursor = Cursors.Hand
            };
            _link.Click += (s, e) => _onLink?.Invoke();
            _secondLink = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 110,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Theme.FontRegular, FontStyle.Underline),
                ForeColor = Theme.Accent,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _secondLink.Click += (s, e) => _onSecondLink?.Invoke();
            _message = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.FontRegular,
                AutoEllipsis = true
            };

            Controls.Add(_message);
            Controls.Add(_icon);
            Controls.Add(_secondLink);
            Controls.Add(_link);
        }

        /// <summary>Shows a second link to the left of the main one; it is cleared by the next <see cref="ShowMessage"/>.</summary>
        public void SetSecondLink(string text, Action onClick)
        {
            _secondLink.Text = text ?? string.Empty;
            _secondLink.Width = TextRenderer.MeasureText(_secondLink.Text, _secondLink.Font).Width + 24;
            _secondLink.BackColor = BackColor;
            _secondLink.Visible = !string.IsNullOrEmpty(text);
            _onSecondLink = onClick;
        }

        public void ShowMessage(BannerKind kind, string message, string linkText = null, Action onLink = null, string toolTip = null)
        {
            SetSecondLink(null, null);
            Color background, foreground;
            string glyph;
            switch (kind)
            {
                case BannerKind.Success:
                    background = Theme.SuccessBackground; foreground = Theme.Success; glyph = Theme.GlyphCheck; break;
                case BannerKind.Error:
                    background = Theme.DangerBackground; foreground = Theme.Danger; glyph = Theme.GlyphError; break;
                case BannerKind.Info:
                    background = Theme.AccentSoft; foreground = Theme.AccentDark; glyph = Theme.GlyphInfo; break;
                default:
                    background = Theme.WarningBackground; foreground = Theme.Warning; glyph = Theme.GlyphWarning; break;
            }

            BackColor = background;
            _icon.BackColor = background;
            _message.BackColor = background;
            _link.BackColor = background;
            _icon.ForeColor = foreground;
            _message.ForeColor = foreground;
            _icon.Text = glyph;
            _message.Text = message;
            _link.Text = linkText ?? string.Empty;
            _link.Visible = !string.IsNullOrEmpty(linkText);
            _onLink = onLink;
            _toolTip.SetToolTip(_message, toolTip);
            Visible = true;
        }

        public void SetLinkText(string text)
        {
            _link.Text = text;
        }

        public void HideBanner()
        {
            Visible = false;
        }
    }
}
