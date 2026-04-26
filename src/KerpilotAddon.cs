using System.Net;
using KSP.UI.Screens;
using UnityEngine;

namespace Kerpilot
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class KerpilotAddon : MonoBehaviour
    {
        private ChatWindow _chatWindow;
        private static ApplicationLauncherButton _toolbarButton;
        private Texture2D _toolbarIcon;
        private static bool _tlsInitialized;

        private void Awake()
        {
            EnsureTlsConfigured();
            _toolbarIcon = CreateToolbarIcon();
            GameEvents.onGUIApplicationLauncherReady.Add(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherUnreadifying.Add(OnLauncherUnready);
        }

        // KSP 1.12.5 ships Unity 2019 / Mono with a default ServicePointManager
        // protocol that excludes TLS 1.2/1.3 — every modern HTTPS endpoint
        // (api.openai.com, api.anthropic.com, openrouter.ai, ...) then fails
        // the handshake with "Unable to complete SSL connection". We force the
        // newer protocols on first scene load.
        private static void EnsureTlsConfigured()
        {
            if (_tlsInitialized) return;
            _tlsInitialized = true;
            try
            {
                // SecurityProtocolType values for Tls11/Tls12/Tls13 are not present
                // in the .NET 4.7.2 reference assemblies KSP targets — use the
                // numeric flags directly. Tls=192, Tls11=768, Tls12=3072, Tls13=12288.
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(192 | 768 | 3072 | 12288);
            }
            catch
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(192 | 768 | 3072); }
                catch { /* best-effort; UnityWebRequest cert handler is the fallback */ }
            }
        }

        private void Start()
        {
            _chatWindow = new ChatWindow();
            _chatWindow.OnClosed += SyncToolbarButtonOff;
            _chatWindow.Initialize(this);
        }

        private void Update()
        {
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.K))
                ToggleWindow();

            if (_chatWindow != null && _chatWindow.IsVisible)
                _chatWindow.HandleKeyInput();
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
                ApplicationLauncher.AppScenes.SPACECENTER
                    | ApplicationLauncher.AppScenes.FLIGHT
                    | ApplicationLauncher.AppScenes.MAPVIEW
                    | ApplicationLauncher.AppScenes.VAB
                    | ApplicationLauncher.AppScenes.SPH
                    | ApplicationLauncher.AppScenes.TRACKSTATION,
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
            if (_chatWindow == null) return;
            _chatWindow.Toggle();

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
            // Cartoonish bold K: teal bar + orange arms, pill strokes, transparent bg
            int size = 38;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);

            var teal   = new Color(0.35f, 0.86f, 0.75f);
            var orange = new Color(0.96f, 0.57f, 0.18f);
            var clear  = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;

                    float aBar = Mathf.Clamp01(3.5f - DistToSeg(px, py, 13f, 9f, 13f, 29f) + 0.5f);
                    float aUp  = Mathf.Clamp01(3.2f - DistToSeg(px, py, 16f, 19f, 28f, 29f) + 0.5f);
                    float aDn  = Mathf.Clamp01(3.2f - DistToSeg(px, py, 16f, 19f, 28f, 9f) + 0.5f);

                    Color c = clear;
                    c = AlphaBlend(c, teal, aBar);
                    c = AlphaBlend(c, orange, aUp);
                    c = AlphaBlend(c, orange, aDn);

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return tex;
        }

        private static float DistToSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 > 0 ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / len2) : 0f;
            float cx = ax + t * dx, cy = ay + t * dy;
            float ex = px - cx, ey = py - cy;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        private static Color AlphaBlend(Color dst, Color src, float srcA)
        {
            if (srcA <= 0f) return dst;
            float outA = srcA + dst.a * (1f - srcA);
            if (outA <= 0f) return new Color(0, 0, 0, 0);
            return new Color(
                (src.r * srcA + dst.r * dst.a * (1f - srcA)) / outA,
                (src.g * srcA + dst.g * dst.a * (1f - srcA)) / outA,
                (src.b * srcA + dst.b * dst.a * (1f - srcA)) / outA,
                outA);
        }
    }
}
