using UnityEngine;

namespace Kerpilot
{
    public static class UIStyleConstants
    {
        // Window
        public const float WindowWidth = 480f;
        public const float WindowHeight = 560f;
        public const float HeaderHeight = 44f;
        public const float InputFieldMinHeight = 36f;
        public const float InputFieldMaxHeight = 120f;

        // Terminal
        public const float MessageSpacing = 2f;
        public const float TypingCharsPerSecond = 80f;
        public const float TypingCatchUpMultiplier = 4f;
        public const int TypingCatchUpThreshold = 20;

        // Font sizes
        public const int HeaderFontSize = 16;
        public const int UserFontSize = 14;
        public const int AiFontSize = 12;
        public const int InputFontSize = 14;
        public const int SettingsLabelFontSize = 12;

        // Colors — terminal palette
        public static readonly Color BackgroundDark = HexColor("0D1117");
        public static readonly Color PanelDark = HexColor("161B22");
        public static readonly Color HeaderColor = HexColor("161B22");
        public static readonly Color InputBackground = HexColor("0D1117");
        public static readonly Color AccentBlue = HexColor("59DBBF");
        public static readonly Color TextLight = HexColor("E0E0E0");
        public static readonly Color TextMuted = HexColor("8B949E");
        // Terminal-specific colors
        public static readonly Color PromptColor = HexColor("59DBBF");
        public static readonly Color UserTextColor = HexColor("59DBBF");
        public static readonly Color AiTextColor = HexColor("E6EDF3");
        public static readonly Color ToolColor = HexColor("F5912E");
        // Rich-text hex strings for <color> tags (derived from above)
        public const string UserTextHex = "#59DBBF";
        public const string AiTextHex = "#E6EDF3";
        public const string ToolHex = "#F5912E";

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
