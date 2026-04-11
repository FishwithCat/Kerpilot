using UnityEngine;

namespace Kerpilot
{
    public static class UIStyleConstants
    {
        // Window
        public const float WindowWidth = 420f;
        public const float WindowHeight = 560f;
        public const float HeaderHeight = 44f;
        public const float InputBarHeight = 48f;

        // Bubble
        public const float BubblePadding = 12f;
        public const float BubbleMaxWidthRatio = 0.75f;
        public const float MessageSpacing = 8f;

        // Font sizes
        public const int HeaderFontSize = 16;
        public const int MessageFontSize = 14;
        public const int TimestampFontSize = 12;
        public const int InputFontSize = 14;
        public const int SettingsLabelFontSize = 13;

        // Colors
        public static readonly Color BackgroundDark = HexColor("1E1E2E");
        public static readonly Color PanelDark = HexColor("2A2A3C");
        public static readonly Color AccentBlue = HexColor("5B6EF5");
        public static readonly Color UserBubbleColor = AccentBlue;
        public static readonly Color AiBubbleColor = HexColor("3A3A4C");
        public static readonly Color TextLight = HexColor("E0E0E0");
        public static readonly Color TextMuted = HexColor("888888");
        public static readonly Color InputBackground = HexColor("1A1A2A");
        public static readonly Color HeaderColor = HexColor("252538");
        public static readonly Color SendButtonColor = AccentBlue;
        public static readonly Color CloseButtonHover = HexColor("FF5555");

        // Font — use KSP's own font for sharp rendering
        private static Font _appFont;
        public static Font AppFont
        {
            get
            {
                if (_appFont == null)
                    _appFont = UISkinManager.defaultSkin.font
                               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _appFont;
            }
        }

        // Screen scaling — all sizes are defined at 1080p baseline, scaled to actual resolution.
        // Font sizes are rounded to integers so the font atlas renders at exact pixel boundaries.
        public static float ScreenScale => Screen.height / 1080f;
        public static float Scaled(float baseValue) => baseValue * ScreenScale;
        public static int ScaledInt(float baseValue) => Mathf.RoundToInt(baseValue * ScreenScale);
        public static int ScaledFont(int baseSize) => Mathf.Max(12, Mathf.RoundToInt(baseSize * ScreenScale));

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
