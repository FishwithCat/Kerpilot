using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public static class ChatBubbleFactory
    {
        private static Sprite _roundedSprite;
        private static Sprite _gearSprite;

        public static Sprite GearSprite
        {
            get
            {
                if (_gearSprite == null)
                    _gearSprite = CreateGearSprite(64);
                return _gearSprite;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite == null)
                    _roundedSprite = CreateRoundedRectSprite(128, 4);
                return _roundedSprite;
            }
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = -1, cy = -1;
                    if (x < radius && y < radius) { cx = radius; cy = radius; }
                    else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
                    else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
                    else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }

                    if (cx >= 0)
                    {
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            return Sprite.Create(tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        private static Sprite CreateGearSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            float center = size / 2f;
            float outerR = size * 0.45f;
            float innerR = size * 0.28f;
            float holeR = size * 0.15f;
            int teeth = 8;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float toothAngle = angle * teeth / (2f * Mathf.PI);
                    float frac = toothAngle - Mathf.Floor(toothAngle);
                    float gearR = frac < 0.5f ? outerR : innerR;

                    float alpha;
                    if (dist < holeR - 0.5f)
                        alpha = 0f;
                    else if (dist < holeR + 0.5f)
                        alpha = dist - (holeR - 0.5f);
                    else if (dist < gearR - 0.5f)
                        alpha = 1f;
                    else if (dist < gearR + 0.5f)
                        alpha = (gearR + 0.5f) - dist;
                    else
                        alpha = 0f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Creates a terminal-style row with HorizontalLayoutGroup and a green "> " prompt prefix.
        /// Used by both message lines and the inline input row.
        /// </summary>
        public static GameObject CreateTerminalRow(string name, Transform parent, bool showPrefix)
        {
            var row = CreateObject(name, parent);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.spacing = UIStyleConstants.Scaled(4);
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            var rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.flexibleWidth = 1f;

            if (showPrefix)
            {
                var prefixObj = CreateObject("Prefix", row.transform);
                var prefixText = prefixObj.AddComponent<Text>();
                prefixText.text = ">";
                prefixText.font = UIStyleConstants.AppFont;
                prefixText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.UserFontSize);
                prefixText.color = UIStyleConstants.PromptColor;
                prefixText.alignment = TextAnchor.UpperLeft;
                var prefixFitter = prefixObj.AddComponent<ContentSizeFitter>();
                prefixFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                prefixFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            return row;
        }

        /// <summary>
        /// Creates a terminal-style message line (no bubble background, no timestamp).
        /// User messages get a green "> " prefix; AI messages have no prefix.
        /// </summary>
        public static GameObject CreateMessageLine(ChatMessage msg, Transform parent)
        {
            bool isUser = msg.Sender == MessageSender.User;

            if (isUser)
            {
                var row = CreateTerminalRow("MessageLine", parent, showPrefix: true);
                AddContentText(row, msg.Text, UIStyleConstants.UserTextColor);
                return row;
            }

            // AI messages: wrap in an outer container with top/bottom margin and left indent
            var wrapper = CreateObject("MessageLine", parent);
            var wrapperLayout = wrapper.AddComponent<VerticalLayoutGroup>();
            wrapperLayout.childForceExpandWidth = true;
            wrapperLayout.childForceExpandHeight = false;
            wrapperLayout.spacing = 0;
            int marginY = UIStyleConstants.ScaledInt(4);
            int indentX = UIStyleConstants.ScaledInt(6);
            wrapperLayout.padding = new RectOffset(indentX, 0, marginY, marginY);
            var wrapperFitter = wrapper.AddComponent<ContentSizeFitter>();
            wrapperFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            wrapperFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var wrapperElement = wrapper.AddComponent<LayoutElement>();
            wrapperElement.flexibleWidth = 1f;

            var textObj = CreateObject("Content", wrapper.transform);
            var text = textObj.AddComponent<Text>();
            text.text = msg.Text;
            text.font = UIStyleConstants.AppFont;
            text.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.AiFontSize);
            text.color = UIStyleConstants.AiTextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;
            var textElement = textObj.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;

            return wrapper;
        }

        private static void AddContentText(GameObject row, string content, Color color)
        {
            var textObj = CreateObject("Content", row.transform);
            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = UIStyleConstants.AppFont;
            text.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.UserFontSize);
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;
            var textElement = textObj.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
        }

        /// <summary>
        /// Finds the Content Text component within a message line created by CreateMessageLine.
        /// </summary>
        public static Text GetMessageText(GameObject lineRow)
        {
            var t = lineRow.transform.Find("Content");
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
