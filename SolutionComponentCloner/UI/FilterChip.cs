using System;
using System.Drawing;
using System.Windows.Forms;

namespace SolutionComponentCloner.UI
{
    /// <summary>A small pill that switches a list filter on or off; the active one is filled with the accent colour.</summary>
    internal sealed class FilterChip : Control
    {
        private const int HorizontalPadding = 10;
        private bool _active;
        private bool _hover;

        public FilterChip(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.FontRegular;
            Height = 24;
            Margin = new Padding(0, 8, 6, 0);
            Cursor = Cursors.Hand;
            Text = text;
        }

        public bool Active
        {
            get => _active;
            set
            {
                if (value == _active)
                {
                    return;
                }

                _active = value;
                Invalidate();
            }
        }

        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                Width = HorizontalPadding * 2 + TextRenderer.MeasureText(value ?? string.Empty, Theme.FontRegular, Size.Empty, TextFormatFlags.NoPadding).Width;
                Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; base.OnMouseLeave(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var filled = _active && Enabled;
            var background = filled ? Theme.Accent : _hover && Enabled ? Theme.Hover : Theme.PanelBackground;
            var border = filled ? Theme.Accent : _active ? Theme.BorderStrong : Theme.Border;
            var foreground = filled ? Color.White : Enabled ? Theme.TextPrimary : Theme.TextDisabled;

            using (var fill = new SolidBrush(background))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillRectangle(fill, ClientRectangle);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            TextRenderer.DrawText(e.Graphics, Text, Theme.FontRegular, ClientRectangle, foreground,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
    }
}
