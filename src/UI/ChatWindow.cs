using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public partial class ChatWindow
    {
        public event System.Action OnClosed;
        private const string InputLockId = "KerpilotInputLock";
        private const int MaxLogChars = 14000;

        private GameObject _canvasObj;
        private GameObject _windowPanel;
        private GameObject _messageArea;
        private GameObject _inputRow;
        private ScrollRect _scrollRect;
        private InputField _inputField;
        private LayoutElement _inputElement;
        private Transform _contentTransform;
        private RectTransform _contentRectTransform;
        private MonoBehaviour _coroutineHost;
        private SettingsPanel _settingsPanel;
        private KerpilotSettings _settings;
        private Text _logText;
        private readonly StringBuilder _logBuilder = new StringBuilder();
        // Static so conversation history survives scene changes
        private static readonly List<ChatMessage> _conversationHistory = new List<ChatMessage>();
        private bool _isStreaming;
        private bool _scrollPending;

        public bool IsVisible => _canvasObj != null && _canvasObj.activeSelf;

        public void Initialize(MonoBehaviour host)
        {
            _coroutineHost = host;
            _settings = KerpilotSettings.Load();
            BuildUI();

            if (_conversationHistory.Count == 0)
            {
                AppendToLog(FormatAiLine("kerpilot ready. type a message to begin."));
            }
            else
            {
                foreach (var msg in _conversationHistory)
                {
                    if (msg.Role == MessageRole.Tool || (msg.Role == MessageRole.Assistant && msg.ToolCalls != null))
                        continue;
                    AppendToLog(FormatMessageLine(msg));
                }
            }
            FlushLog();

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
            _coroutineHost.StartCoroutine(ScrollToBottom());
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
        }

        private void ShowSettings()
        {
            _messageArea.SetActive(false);
            _settingsPanel.Show();
        }

        private void FocusInput()
        {
            if (_inputField != null && _inputField.interactable)
            {
                _inputField.ActivateInputField();
                _coroutineHost.StartCoroutine(MoveCaretToEnd());
            }
        }

        private IEnumerator MoveCaretToEnd()
        {
            yield return null;
            if (_inputField != null)
            {
                _inputField.caretPosition = _inputField.text.Length;
                _inputField.selectionAnchorPosition = _inputField.text.Length;
                _inputField.selectionFocusPosition = _inputField.text.Length;
            }
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
            AppendToLog(FormatMessageLine(msg));
            FlushLog();
            _coroutineHost.StartCoroutine(ScrollToBottom());
        }

        private void AppendToLog(string richLine)
        {
            if (_logBuilder.Length > 0)
                _logBuilder.Append('\n');
            _logBuilder.Append(richLine);
        }

        private void FlushLog()
        {
            TrimLog();
            if (_logText != null)
                _logText.text = _logBuilder.ToString();
        }

        private void TrimLog()
        {
            int len = _logBuilder.Length;
            if (len <= MaxLogChars)
                return;
            // Find first newline within the portion we want to keep
            int searchFrom = len - MaxLogChars;
            int cutAt = -1;
            for (int i = searchFrom; i < len; i++)
            {
                if (_logBuilder[i] == '\n') { cutAt = i; break; }
            }
            if (cutAt >= 0 && cutAt < len - 1)
            {
                _logBuilder.Remove(0, cutAt + 1);
            }
        }

        private IEnumerator ScrollToBottom()
        {
            yield return null;
            yield return null;
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }

        private static string EscapeRichText(string text)
        {
            return text.Contains("<") ? text.Replace("<", "\u200B<") : text;
        }

        private static string FormatLine(string text, string colorHex, string prefix, bool italic = false)
        {
            string escaped = EscapeRichText(text);
            if (italic)
                return "<color=" + colorHex + "><i>" + prefix + escaped + "</i></color>";
            return "<color=" + colorHex + ">" + prefix + escaped + "</color>";
        }

        private static string FormatUserLine(string text)
            => FormatLine(text, UIStyleConstants.UserTextHex, "> ");

        private static string FormatAiLine(string text)
            => FormatLine(text, UIStyleConstants.AiTextHex, "  ");

        private static string FormatToolLine(string text)
            => FormatLine(text, UIStyleConstants.ToolHex, "  ", italic: true);

        private static string FormatMessageLine(ChatMessage msg)
        {
            return msg.Sender == MessageSender.User
                ? FormatUserLine(msg.Text)
                : FormatAiLine(msg.Text);
        }

        private static GameObject CreateObj(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
