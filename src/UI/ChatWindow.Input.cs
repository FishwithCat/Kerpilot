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

        private void OnSendClicked()
        {
            if (_isStreaming) return;

            string text = _inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputField.text = "";
            _inputField.ActivateInputField();

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
            _coroutineHost.StartCoroutine(StreamLlmResponse());
        }
    }
}
