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
        private GameObject _inputRow;
        private ScrollRect _scrollRect;
        private InputField _inputField;
        private LayoutElement _inputElement;
        private Transform _contentTransform;
        private RectTransform _contentRectTransform;
        private MonoBehaviour _coroutineHost;
        private SettingsPanel _settingsPanel;
        private KerpilotSettings _settings;
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
                AddMessage(new ChatMessage(MessageSender.AI, "kerpilot ready. type a message to begin."));
            }
            else
            {
                // Restore previous conversation lines
                foreach (var msg in _conversationHistory)
                {
                    if (msg.Role == MessageRole.Tool || (msg.Role == MessageRole.Assistant && msg.ToolCalls != null))
                        continue;
                    InsertMessageLine(ChatBubbleFactory.CreateMessageLine(msg, _contentTransform));
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
                // ActivateInputField selects all text; defer caret move to deselect
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

        /// <summary>
        /// Inserts a message line before the inline input row so it always stays at the bottom.
        /// </summary>
        private void InsertMessageLine(GameObject lineObj)
        {
            if (_inputRow != null)
                lineObj.transform.SetSiblingIndex(_inputRow.transform.GetSiblingIndex());
        }

        private void AddMessage(ChatMessage msg)
        {
            var lineObj = ChatBubbleFactory.CreateMessageLine(msg, _contentTransform);
            InsertMessageLine(lineObj);
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
            label.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.AiFontSize);
            label.fontStyle = FontStyle.Italic;
            label.color = UIStyleConstants.ToolColor;
            label.alignment = TextAnchor.MiddleLeft;
            var fitter = obj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Insert before inline input
            InsertMessageLine(obj);
            return obj;
        }

        private void ResetBlockCursorBlink()
        {
            // no-op — blink handled natively by InputField.caretBlinkRate
        }

        private static GameObject CreateObj(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
