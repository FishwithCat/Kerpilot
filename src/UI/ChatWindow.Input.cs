using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public partial class ChatWindow
    {
        private void OnInputValueChanged(string text)
        {
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                // Shift+Enter inserts a newline; plain Enter sends the message
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    return;
                }
                // Strip the newline and send
                _inputField.text = text.Replace("\n", "").Replace("\r", "");
                OnSendClicked();
                return;
            }
        }

        /// <summary>
        /// Called from KerpilotAddon.Update() each frame when the window is visible.
        /// Handles Ctrl+C (abort streaming) and Up arrow (recall last input).
        /// </summary>
        public void HandleKeyInput()
        {
            if (Input.GetKeyDown(KeyCode.C) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                if (_isStreaming) AbortStreaming();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !_isStreaming &&
                _lastUserInput != null && _inputField != null &&
                _inputField.isFocused && string.IsNullOrEmpty(_inputField.text))
            {
                _inputField.text = _lastUserInput;
                FocusInput();
            }
        }

        private void AbortStreaming()
        {
            _streamingCancelled = true;

            if (_streamingCoroutine != null)
            {
                _coroutineHost.StopCoroutine(_streamingCoroutine);
                _streamingCoroutine = null;
            }
            StopThinkingAnimation();
            _scrollPending = false;

            RebuildLogFromHistory();
            AppendToLog(FormatAiLine("[interrupted]"));
            FlushLog();

            _isStreaming = false;
            _inputField.interactable = true;
            _coroutineHost.StartCoroutine(ScrollToBottom());
            FocusInput();
        }

        private void OnSendClicked()
        {
            if (_isStreaming) return;

            string text = _inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _lastUserInput = text;
            _inputField.text = "";
            _inputField.ActivateInputField();

            if (text.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
            {
                ClearChat();
                return;
            }

            if (!_settings.IsConfigured)
            {
                AddMessage(new ChatMessage(MessageSender.User, text));
                AddMessage(new ChatMessage(MessageSender.AI,
                    "Please configure your API key in Settings to start chatting."));
                ShowSettings();
                return;
            }

            var userMsg = new ChatMessage(MessageSender.User, text);
            _conversationHistory.Add(userMsg);
            AddMessage(userMsg);
            _streamingCoroutine = _coroutineHost.StartCoroutine(StreamLlmResponse());
        }
    }
}
