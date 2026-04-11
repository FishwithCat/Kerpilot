using UnityEngine;

namespace Kerpilot
{
    public static class SpriteFactory
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
    }
}
