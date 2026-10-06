using System;
using System.Drawing;
using System.Windows.Forms;

namespace SolutionComponentCloner.UI
{
    /// <summary>A flat command-bar item: a blue glyph followed by a label, like the Dynamics 365 command bar.</summary>
    internal sealed class CommandButton : Control
    {
        private const int HorizontalPadding = 10;
        private const int GlyphGap = 6;
        private bool _hover;
        private bool _down;
        private string _glyph = string.Empty;
        private bool _strong;

        public CommandButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            SetStyle(ControlStyles.Selectable, false);
            Height = 40;
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            Font = Theme.FontRegular;
            RecalculateWidth();
        }

        public string Glyph
        {
            get => _glyph;
            set { _glyph = value ?? string.Empty; RecalculateWidth(); Invalidate(); }
        }

        public bool Strong
        {
            get => _strong;
            set { _strong = value; RecalculateWidth(); Invalidate(); }
        }

        public override string Text
        {
            get => base.Text;
            set { base.Text = value; RecalculateWidth(); Invalidate(); }
        }

        private Font TextFont => _strong ? Theme.FontBold : Theme.FontRegular;

        private void RecalculateWidth()
        {
            var glyphWidth = _glyph.Length == 0 ? 0 : TextRenderer.MeasureText(_glyph, Theme.FontIcon, Size.Empty, TextFormatFlags.NoPadding).Width + GlyphGap;
            var textWidth = TextRenderer.MeasureText(Text ?? string.Empty, TextFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            Width = HorizontalPadding * 2 + glyphWidth + textWidth;
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; base.OnMouseUp(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var background = !Enabled ? Theme.PanelBackground : _down ? Theme.Pressed : _hover ? Theme.Hover : Theme.PanelBackground;
            e.Graphics.Clear(background);

            var x = HorizontalPadding;
            if (_glyph.Length > 0)
            {
                var glyphSize = TextRenderer.MeasureText(_glyph, Theme.FontIcon, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, _glyph, Theme.FontIcon,
                    new Rectangle(x, 0, glyphSize.Width, Height), Enabled ? Theme.Accent : Theme.TextDisabled,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += glyphSize.Width + GlyphGap;
            }

            TextRenderer.DrawText(e.Graphics, Text, TextFont, new Rectangle(x, 0, Width - x, Height),
                Enabled ? Theme.TextPrimary : Theme.TextDisabled,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.Left);
        }
    }
}
