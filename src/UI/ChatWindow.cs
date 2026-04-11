using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public partial class ChatWindow
    {
        public event System.Action OnClosed;
        private const string InputLockId = "KerpilotInputLock";

        private GameObject _canvasObj;
        private GameObject _windowPanel;
        private GameObject _messageArea;
        private GameObject _inputBar;
        private ScrollRect _scrollRect;
        private InputField _inputField;
        private LayoutElement _inputElement;
        private LayoutElement _inputBarElement;
        private Button _sendButton;
        private Transform _contentTransform;
        private RectTransform _contentRectTransform;
        private MonoBehaviour _coroutineHost;
        private SettingsPanel _settingsPanel;
        private KerpilotSettings _settings;
        // Static so conversation history survives scene changes
        private static readonly List<ChatMessage> _conversationHistory = new List<ChatMessage>();
        private bool _isStreaming;
        private bool _scrollPending;
        private bool _autoScroll = true;

        public bool IsVisible => _canvasObj != null && _canvasObj.activeSelf;

        public void Initialize(MonoBehaviour host)
        {
            _coroutineHost = host;
            _settings = KerpilotSettings.Load();
            BuildUI();

            if (_conversationHistory.Count == 0)
            {
                AddMessage(new ChatMessage(MessageSender.AI, "Hello! I'm Kerpilot. How can I help you today?"));
            }
            else
            {
                // Restore previous conversation bubbles
                foreach (var msg in _conversationHistory)
                {
                    if (msg.Role == MessageRole.Tool || (msg.Role == MessageRole.Assistant && msg.ToolCalls != null))
                        continue;
                    ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                }
                _coroutineHost.StartCoroutine(ScrollToBottom());
            }

            Hide();
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        public void Show()
        {
            _canvasObj.SetActive(true);
        }

        public void Hide()
        {
            _canvasObj.SetActive(false);
            RemoveInputLock();
            OnClosed?.Invoke();
        }

        public void Destroy()
        {
            _coroutineHost.StopAllCoroutines();
            RemoveInputLock();
            if (_canvasObj != null)
                Object.Destroy(_canvasObj);
        }

        private void ShowChat()
        {
            _settingsPanel.Hide();
            _messageArea.SetActive(true);
            _inputBar.SetActive(true);
        }

        private void ShowSettings()
        {
            _messageArea.SetActive(false);
            _inputBar.SetActive(false);
            _settingsPanel.Show();
        }

        private void SetInputLock()
        {
            InputLockManager.SetControlLock(ControlTypes.All, InputLockId);
        }

        private void RemoveInputLock()
        {
            InputLockManager.RemoveControlLock(InputLockId);
        }

        private void AddMessage(ChatMessage msg)
        {
            ChatBubbleFactory.CreateBubble(msg, _contentTransform);
            _coroutineHost.StartCoroutine(ScrollToBottom());
        }

        private IEnumerator ScrollToBottom()
        {
            yield return null; // wait one frame for layout rebuild
            yield return null; // extra frame for content size fitter
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }

        private GameObject CreateStatusLabel(string text)
        {
            var obj = CreateObj(text, _contentTransform);
            var label = obj.AddComponent<Text>();
            label.text = text;
            label.font = UIStyleConstants.AppFont;
            label.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.MessageFontSize);
            label.fontStyle = FontStyle.Italic;
            label.color = UIStyleConstants.TextMuted;
            label.alignment = TextAnchor.MiddleLeft;
            var fitter = obj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return obj;
        }

        private static GameObject CreateObj(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
