using System.Drawing;

namespace SolutionComponentCloner.UI
{
    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(91, 60, 196);
        public static readonly Color AccentDark = Color.FromArgb(68, 42, 158);
        public static readonly Color AccentSoft = Color.FromArgb(238, 234, 251);
        public static readonly Color PageBackground = Color.FromArgb(246, 247, 250);
        public static readonly Color PanelBackground = Color.White;
        public static readonly Color Border = Color.FromArgb(224, 226, 233);
        public static readonly Color TextPrimary = Color.FromArgb(33, 37, 48);
        public static readonly Color TextSecondary = Color.FromArgb(110, 116, 132);
        public static readonly Color Success = Color.FromArgb(30, 143, 79);
        public static readonly Color Danger = Color.FromArgb(198, 40, 40);
        public static readonly Color GridAltRow = Color.FromArgb(250, 250, 252);

        public static readonly Font FontRegular = new Font("Segoe UI", 9F);
        public static readonly Font FontSmall = new Font("Segoe UI", 8F);
        public static readonly Font FontTitle = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        public static readonly Font FontSectionHeader = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        public static readonly Font FontButton = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
    }
}
