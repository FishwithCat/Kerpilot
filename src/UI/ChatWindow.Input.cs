using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public partial class ChatWindow
    {
        private void OnInputValueChanged(string text)
        {
            // In MultiLineNewline mode, Enter inserts '\n' into the text.
            // When IME is composing, Enter confirms the character (no '\n' inserted).
            // So detecting '\n' reliably distinguishes "send" from "IME confirm".
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                // Shift+Enter inserts a newline; plain Enter sends the message
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    ResizeInputField();
                    return;
                }
                // Strip the newline and send
                _inputField.text = text.Replace("\n", "").Replace("\r", "");
                OnSendClicked();
                return;
            }
            ResizeInputField();
        }

        private void ResizeInputField()
        {
            var textComp = _inputField.textComponent;
            var rectSize = textComp.rectTransform.rect.size;
            if (rectSize.x <= 0) return;

            // Force text generation with current rect to get accurate line count
            var settings = textComp.GetGenerationSettings(
                new Vector2(rectSize.x, 0f)); // width-constrained, unlimited height
            textComp.cachedTextGeneratorForLayout.Populate(_inputField.text, settings);

            float lineHeight = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize) + 2f;
            int lineCount = Mathf.Max(1, textComp.cachedTextGeneratorForLayout.lineCount);
            float padding = UIStyleConstants.Scaled(8);
            float desiredHeight = lineCount * lineHeight + padding;

            float minH = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMinHeight);
            float maxH = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMaxHeight);
            float clampedHeight = Mathf.Clamp(desiredHeight, minH, maxH);

            _inputElement.preferredHeight = clampedHeight;
            // Adjust input bar height: bar padding (12) + input field height
            float barPad = UIStyleConstants.Scaled(12);
            _inputBarElement.preferredHeight = clampedHeight + barPad;
        }

        private void ResetInputFieldSize()
        {
            float minH = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMinHeight);
            _inputElement.preferredHeight = minH;
            _inputBarElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.InputBarHeight);
        }

        private void OnSendClicked()
        {
            if (_isStreaming) return;

            string text = _inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputField.text = "";
            _inputField.ActivateInputField();
            ResetInputFieldSize();
            _autoScroll = true;

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
