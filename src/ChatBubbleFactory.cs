using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public static class ChatBubbleFactory
    {
        private static Sprite _roundedSprite;

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
                    // Determine which corner region we're in
                    int cx = -1, cy = -1;
                    if (x < radius && y < radius) { cx = radius; cy = radius; }
                    else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
                    else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
                    else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }

                    if (cx >= 0)
                    {
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f); // anti-aliased edge
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

            // 9-slice border so the rounded corners don't stretch
            return Sprite.Create(tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        public static GameObject CreateBubble(ChatMessage msg, Transform parent)
        {
            bool isUser = msg.Sender == MessageSender.User;

            // Root row with horizontal layout
            var row = CreateObject("BubbleRow", parent);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = isUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            rowLayout.spacing = 0;
            var rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.flexibleWidth = 1f;

            // Spacer (pushes bubble to the correct side)
            if (isUser)
            {
                var spacer = CreateObject("Spacer", row.transform);
                var spacerElement = spacer.AddComponent<LayoutElement>();
                spacerElement.flexibleWidth = 1f;
            }

            // Bubble container (vertical: background + timestamp)
            var container = CreateObject("BubbleContainer", row.transform);
            var containerLayout = container.AddComponent<VerticalLayoutGroup>();
            containerLayout.childForceExpandWidth = false;
            containerLayout.childForceExpandHeight = false;
            containerLayout.childAlignment = isUser ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            containerLayout.spacing = UIStyleConstants.Scaled(2f);
            containerLayout.padding = new RectOffset(0, 0, 0, 0);
            var containerElement = container.AddComponent<LayoutElement>();
            containerElement.flexibleWidth = 0f;
            float maxBubbleWidth = UIStyleConstants.Scaled(
                UIStyleConstants.WindowWidth * UIStyleConstants.BubbleMaxWidthRatio);

            // Background with message text
            var bg = CreateObject("Background", container.transform);
            var bgImage = bg.AddComponent<Image>();
            bgImage.sprite = RoundedSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = isUser ? UIStyleConstants.UserBubbleColor : UIStyleConstants.AiBubbleColor;

            var bgLayout = bg.AddComponent<VerticalLayoutGroup>();
            int pad = UIStyleConstants.ScaledInt(UIStyleConstants.BubblePadding);
            int padV = UIStyleConstants.ScaledInt(UIStyleConstants.BubblePadding - 2);
            bgLayout.padding = new RectOffset(pad, pad, padV, padV);
            bgLayout.childForceExpandWidth = false;
            bgLayout.childForceExpandHeight = false;

            var bgFitter = bg.AddComponent<ContentSizeFitter>();
            bgFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bgFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Message text
            var textObj = CreateObject("MessageText", bg.transform);
            var text = textObj.AddComponent<Text>();
            text.text = msg.Text;
            text.font = UIStyleConstants.AppFont;
            text.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.MessageFontSize);
            text.color = UIStyleConstants.TextLight;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;
            var textElement = textObj.AddComponent<LayoutElement>();
            float maxTextWidth = maxBubbleWidth - pad * 2;
            textElement.preferredWidth = Mathf.Min(text.preferredWidth, maxTextWidth);
            textElement.flexibleWidth = 0;

            // Timestamp
            var tsObj = CreateObject("Timestamp", container.transform);
            var ts = tsObj.AddComponent<Text>();
            ts.text = msg.Timestamp.ToString("h:mm tt");
            ts.font = UIStyleConstants.AppFont;
            ts.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.TimestampFontSize);
            ts.color = UIStyleConstants.TextMuted;
            ts.alignment = isUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            var tsFitter = tsObj.AddComponent<ContentSizeFitter>();
            tsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Spacer for AI messages (pushes bubble left)
            if (!isUser)
            {
                var spacer = CreateObject("Spacer", row.transform);
                var spacerElement = spacer.AddComponent<LayoutElement>();
                spacerElement.flexibleWidth = 1f;
            }

            return row;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
