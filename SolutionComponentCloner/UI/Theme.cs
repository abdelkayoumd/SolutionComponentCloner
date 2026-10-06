using System.Drawing;

namespace SolutionComponentCloner.UI
{
    internal static class Theme
    {
        public static readonly Color Navy = Color.FromArgb(0, 32, 80);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentDark = Color.FromArgb(16, 110, 190);
        public static readonly Color AccentSoft = Color.FromArgb(239, 246, 252);
        public static readonly Color AccentSelected = Color.FromArgb(222, 236, 249);
        public static readonly Color PageBackground = Color.FromArgb(250, 249, 248);
        public static readonly Color PanelBackground = Color.White;
        public static readonly Color GroupBackground = Color.FromArgb(243, 242, 241);
        public static readonly Color Hover = Color.FromArgb(243, 242, 241);
        public static readonly Color Pressed = Color.FromArgb(237, 235, 233);
        public static readonly Color Border = Color.FromArgb(237, 235, 233);
        public static readonly Color BorderStrong = Color.FromArgb(138, 136, 134);
        public static readonly Color CheckBorder = Color.FromArgb(96, 94, 92);
        public static readonly Color TextPrimary = Color.FromArgb(50, 49, 48);
        public static readonly Color TextSecondary = Color.FromArgb(96, 94, 92);
        public static readonly Color TextDisabled = Color.FromArgb(161, 159, 157);
        public static readonly Color Success = Color.FromArgb(16, 124, 16);
        public static readonly Color SuccessBackground = Color.FromArgb(223, 246, 221);
        public static readonly Color Danger = Color.FromArgb(164, 38, 44);
        public static readonly Color DangerBackground = Color.FromArgb(253, 231, 233);
        public static readonly Color Warning = Color.FromArgb(108, 90, 0);
        public static readonly Color WarningBackground = Color.FromArgb(255, 244, 206);

        public static readonly Font FontRegular = new Font("Segoe UI", 9F);
        public static readonly Font FontSmall = new Font("Segoe UI", 8F);
        public static readonly Font FontBold = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        public static readonly Font FontTitle = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        public static readonly Font FontAppTitle = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        public static readonly Font FontIcon = new Font("Segoe MDL2 Assets", 10F);
        public static readonly Font FontIconSmall = new Font("Segoe MDL2 Assets", 8F);

        public const string GlyphCopy = "";
        public const string GlyphSelectAll = "";
        public const string GlyphClearSelection = "";
        public const string GlyphCollapse = "";
        public const string GlyphExpand = "";
        public const string GlyphRefresh = "";
        public const string GlyphDownload = "";
        public const string GlyphSearch = "";
        public const string GlyphChevronDown = "";
        public const string GlyphChevronRight = "";
        public const string GlyphWaffle = "";
        public const string GlyphCheck = "";
        public const string GlyphError = "";
        public const string GlyphWarning = "";
    }
}
