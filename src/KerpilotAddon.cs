using KSP.UI.Screens;
using UnityEngine;

namespace Kerpilot
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class KerpilotAddon : MonoBehaviour
    {
        private ChatWindow _chatWindow;
        private ApplicationLauncherButton _toolbarButton;
        private Texture2D _toolbarIcon;

        private void Awake()
        {
            _toolbarIcon = CreateToolbarIcon();
            GameEvents.onGUIApplicationLauncherReady.Add(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherUnreadifying.Add(OnLauncherUnready);
        }

        private void Start()
        {
            _chatWindow = new ChatWindow();
            _chatWindow.OnClosed += SyncToolbarButtonOff;
            _chatWindow.Initialize(this);
        }

        private void SyncToolbarButtonOff()
        {
            if (_toolbarButton != null)
                _toolbarButton.SetFalse(false);
        }


        private void OnDestroy()
        {
            _chatWindow?.Destroy();
            RemoveToolbarButton();
            GameEvents.onGUIApplicationLauncherReady.Remove(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherUnreadifying.Remove(OnLauncherUnready);
        }

        private void OnLauncherReady()
        {
            AddToolbarButton();
        }

        private void OnLauncherUnready(GameScenes scene)
        {
            RemoveToolbarButton();
        }

        private void AddToolbarButton()
        {
            if (_toolbarButton != null) return;

            _toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                () => _chatWindow?.Show(),
                () => _chatWindow?.Hide(),
                null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS,
                _toolbarIcon
            );
        }

        private void RemoveToolbarButton()
        {
            if (_toolbarButton == null) return;
            ApplicationLauncher.Instance.RemoveModApplication(_toolbarButton);
            _toolbarButton = null;
        }

        private void ToggleWindow()
        {
            _chatWindow?.Toggle();

            // Sync toolbar button state
            if (_toolbarButton != null)
            {
                if (_chatWindow.IsVisible)
                    _toolbarButton.SetTrue(false);
                else
                    _toolbarButton.SetFalse(false);
            }
        }

        private static Texture2D CreateToolbarIcon()
        {
            // Generate a simple 38x38 icon with "K" look
            int size = 38;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var accent = UIStyleConstants.AccentBlue;
            var bg = new Color(accent.r, accent.g, accent.b, 0.9f);

            // Fill with accent color rounded
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int radius = 6;
                    int cx = -1, cy = -1;
                    if (x < radius && y < radius) { cx = radius; cy = radius; }
                    else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
                    else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
                    else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }

                    if (cx >= 0)
                    {
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        tex.SetPixel(x, y, new Color(bg.r, bg.g, bg.b, bg.a * alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, bg);
                    }
                }
            }

            // Draw a simple "K" letter (pixel art style)
            Color white = Color.white;
            int ox = 12, oy = 8; // offset
            // Vertical bar of K
            for (int y = oy; y < oy + 22; y++)
                for (int x = ox; x < ox + 3; x++)
                    tex.SetPixel(x, y, white);
            // Upper diagonal of K
            for (int i = 0; i < 11; i++)
                for (int t = 0; t < 3; t++)
                    if (ox + 3 + i + t < size && oy + 11 + i < size)
                        tex.SetPixel(ox + 3 + i + t, oy + 11 + i, white);
            // Lower diagonal of K
            for (int i = 0; i < 11; i++)
                for (int t = 0; t < 3; t++)
                    if (ox + 3 + i + t < size && oy + 11 - i >= 0)
                        tex.SetPixel(ox + 3 + i + t, oy + 11 - i, white);

            tex.Apply();
            return tex;
        }
    }
}
