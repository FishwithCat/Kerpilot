using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kerpilot
{
    public class ChatWindow
    {
        private const string InputLockId = "KerpilotInputLock";

        private GameObject _canvasObj;
        private GameObject _windowPanel;
        private GameObject _messageArea;
        private GameObject _inputBar;
        private ScrollRect _scrollRect;
        private InputField _inputField;
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

        private void BuildUI()
        {
            // Canvas — no CanvasScaler scaling; all sizes computed manually via UIStyleConstants.Scaled()
            // so fonts render at exact integer pixel sizes (no upscale blur).
            _canvasObj = new GameObject("KerpilotCanvas");
            Object.DontDestroyOnLoad(_canvasObj);
            var canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
            _canvasObj.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Object.DontDestroyOnLoad(es);
            }

            // Window panel
            _windowPanel = CreateObj("WindowPanel", _canvasObj.transform);
            var panelImage = _windowPanel.AddComponent<Image>();
            panelImage.color = UIStyleConstants.BackgroundDark;
            var panelRect = _windowPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.5f);
            panelRect.anchorMax = new Vector2(1, 0.5f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(UIStyleConstants.Scaled(-20), 0);
            panelRect.sizeDelta = new Vector2(
                UIStyleConstants.Scaled(UIStyleConstants.WindowWidth),
                UIStyleConstants.Scaled(UIStyleConstants.WindowHeight));

            var panelLayout = _windowPanel.AddComponent<VerticalLayoutGroup>();
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.spacing = 0;
            panelLayout.padding = new RectOffset(0, 0, 0, 0);

            BuildHeader(_windowPanel.transform);
            BuildMessageArea(_windowPanel.transform);
            BuildInputBar(_windowPanel.transform);

            // Settings panel (initially hidden, occupies same space as message area + input bar)
            _settingsPanel = new SettingsPanel(_windowPanel.transform, _settings, ShowChat);
        }

        private void BuildHeader(Transform parent)
        {
            var header = CreateObj("Header", parent);
            var headerImage = header.AddComponent<Image>();
            headerImage.color = UIStyleConstants.HeaderColor;
            var headerElement = header.AddComponent<LayoutElement>();
            headerElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.HeaderHeight);
            headerElement.flexibleHeight = 0;

            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.padding = new RectOffset(
                UIStyleConstants.ScaledInt(14), UIStyleConstants.ScaledInt(8), 0, 0);
            headerLayout.spacing = UIStyleConstants.Scaled(8);

            // Title
            var titleObj = CreateObj("Title", header.transform);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "Kerpilot";
            titleText.font = UIStyleConstants.AppFont;
            titleText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.HeaderFontSize);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = UIStyleConstants.TextLight;
            var titleElement = titleObj.AddComponent<LayoutElement>();
            titleElement.flexibleWidth = 1f;

            // Settings button with programmatic gear icon
            var settingsObj = CreateObj("SettingsButton", header.transform);
            var settingsBtnImage = settingsObj.AddComponent<Image>();
            settingsBtnImage.sprite = CreateGearSprite();
            settingsBtnImage.color = UIStyleConstants.TextMuted;
            var settingsBtn = settingsObj.AddComponent<Button>();
            var settingsElement = settingsObj.AddComponent<LayoutElement>();
            settingsElement.preferredWidth = UIStyleConstants.Scaled(24);
            settingsElement.preferredHeight = UIStyleConstants.Scaled(24);

            settingsBtn.onClick.AddListener(() =>
            {
                if (_settingsPanel.IsVisible)
                    ShowChat();
                else
                    ShowSettings();
            });

            // Close button
            var closeObj = CreateObj("CloseButton", header.transform);
            var closeBtnImage = closeObj.AddComponent<Image>();
            closeBtnImage.color = new Color(1, 1, 1, 0); // transparent
            var closeBtn = closeObj.AddComponent<Button>();
            var closeElement = closeObj.AddComponent<LayoutElement>();
            closeElement.preferredWidth = UIStyleConstants.Scaled(30);
            closeElement.preferredHeight = UIStyleConstants.Scaled(30);

            var closeLabelObj = CreateObj("CloseLabel", closeObj.transform);
            var closeLabel = closeLabelObj.AddComponent<Text>();
            closeLabel.text = "\u2715";
            closeLabel.font = UIStyleConstants.AppFont;
            closeLabel.fontSize = UIStyleConstants.ScaledFont(16);
            closeLabel.color = UIStyleConstants.TextMuted;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            var closeLabelRect = closeLabelObj.GetComponent<RectTransform>();
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.sizeDelta = Vector2.zero;

            closeBtn.onClick.AddListener(Hide);

            // Drag handler
            var dragHandler = header.AddComponent<DragHandler>();
            dragHandler.Target = _windowPanel.GetComponent<RectTransform>();
        }

        private void BuildMessageArea(Transform parent)
        {
            _messageArea = CreateObj("MessageArea", parent);
            var areaElement = _messageArea.AddComponent<LayoutElement>();
            areaElement.flexibleHeight = 1f;

            // ScrollRect setup
            _scrollRect = _messageArea.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = UIStyleConstants.Scaled(30f);

            // Background
            var areaBg = _messageArea.AddComponent<Image>();
            areaBg.color = UIStyleConstants.BackgroundDark;

            // Viewport
            var viewport = CreateObj("Viewport", _messageArea.transform);
            var vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            var vpImage = viewport.AddComponent<Image>();
            vpImage.color = Color.white;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _scrollRect.viewport = vpRect;

            // Content
            var content = CreateObj("Content", viewport.transform);
            _contentTransform = content.transform;
            _contentRectTransform = content.GetComponent<RectTransform>();
            var contentRect = _contentRectTransform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            int contentPad = UIStyleConstants.ScaledInt(12);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = UIStyleConstants.Scaled(UIStyleConstants.MessageSpacing);
            contentLayout.padding = new RectOffset(contentPad, contentPad, contentPad, contentPad);

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = contentRect;
        }

        private void BuildInputBar(Transform parent)
        {
            _inputBar = CreateObj("InputBar", parent);
            var barImage = _inputBar.AddComponent<Image>();
            barImage.color = UIStyleConstants.PanelDark;
            var barElement = _inputBar.AddComponent<LayoutElement>();
            barElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.InputBarHeight);
            barElement.flexibleHeight = 0;

            var barLayout = _inputBar.AddComponent<HorizontalLayoutGroup>();
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = false;
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.padding = new RectOffset(
                UIStyleConstants.ScaledInt(8), UIStyleConstants.ScaledInt(8),
                UIStyleConstants.ScaledInt(6), UIStyleConstants.ScaledInt(6));
            barLayout.spacing = UIStyleConstants.Scaled(8);

            // Input field
            var inputObj = CreateObj("InputField", _inputBar.transform);
            var inputBg = inputObj.AddComponent<Image>();
            inputBg.sprite = ChatBubbleFactory.RoundedSprite;
            inputBg.type = Image.Type.Sliced;
            inputBg.color = UIStyleConstants.InputBackground;
            var inputElement = inputObj.AddComponent<LayoutElement>();
            inputElement.flexibleWidth = 1f;
            inputElement.preferredHeight = UIStyleConstants.Scaled(36);

            int inputTextPad = UIStyleConstants.ScaledInt(10);

            // Placeholder
            var placeholderObj = CreateObj("Placeholder", inputObj.transform);
            var placeholder = placeholderObj.AddComponent<Text>();
            placeholder.text = "Type a message...";
            placeholder.font = UIStyleConstants.AppFont;
            placeholder.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = UIStyleConstants.TextMuted;
            placeholder.alignment = TextAnchor.MiddleLeft;
            var phRect = placeholderObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(inputTextPad, 0);
            phRect.offsetMax = new Vector2(-inputTextPad, 0);

            // Input text
            var inputTextObj = CreateObj("Text", inputObj.transform);
            var inputText = inputTextObj.AddComponent<Text>();
            inputText.font = UIStyleConstants.AppFont;
            inputText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            inputText.color = UIStyleConstants.TextLight;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;
            var itRect = inputTextObj.GetComponent<RectTransform>();
            itRect.anchorMin = Vector2.zero;
            itRect.anchorMax = Vector2.one;
            itRect.offsetMin = new Vector2(inputTextPad, 0);
            itRect.offsetMax = new Vector2(-inputTextPad, 0);

            _inputField = inputObj.AddComponent<InputField>();
            _inputField.textComponent = inputText;
            _inputField.placeholder = placeholder;
            // Use MultiLineNewline so Enter does NOT trigger onEndEdit/deactivate.
            // This prevents IME confirmation Enter from being treated as "send".
            _inputField.lineType = InputField.LineType.MultiLineNewline;
            _inputField.onValueChanged.AddListener(OnInputValueChanged);

            // Lock game controls when input is focused
            var trigger = inputObj.AddComponent<EventTrigger>();
            var selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((_) => SetInputLock());
            trigger.triggers.Add(selectEntry);
            var deselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
            deselectEntry.callback.AddListener((_) => RemoveInputLock());
            trigger.triggers.Add(deselectEntry);

            // Send button
            var sendObj = CreateObj("SendButton", _inputBar.transform);
            var sendBg = sendObj.AddComponent<Image>();
            sendBg.sprite = ChatBubbleFactory.RoundedSprite;
            sendBg.type = Image.Type.Sliced;
            sendBg.color = UIStyleConstants.SendButtonColor;
            _sendButton = sendObj.AddComponent<Button>();
            var sendElement = sendObj.AddComponent<LayoutElement>();
            sendElement.preferredWidth = UIStyleConstants.Scaled(60);
            sendElement.preferredHeight = UIStyleConstants.Scaled(36);

            var sendLabelObj = CreateObj("SendLabel", sendObj.transform);
            var sendLabel = sendLabelObj.AddComponent<Text>();
            sendLabel.text = "Send";
            sendLabel.font = UIStyleConstants.AppFont;
            sendLabel.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            sendLabel.fontStyle = FontStyle.Bold;
            sendLabel.color = Color.white;
            sendLabel.alignment = TextAnchor.MiddleCenter;
            var slRect = sendLabelObj.GetComponent<RectTransform>();
            slRect.anchorMin = Vector2.zero;
            slRect.anchorMax = Vector2.one;
            slRect.sizeDelta = Vector2.zero;

            _sendButton.onClick.AddListener(OnSendClicked);
        }

        private void OnInputValueChanged(string text)
        {
            // In MultiLineNewline mode, Enter inserts '\n' into the text.
            // When IME is composing, Enter confirms the character (no '\n' inserted).
            // So detecting '\n' reliably distinguishes "send" from "IME confirm".
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                // Strip the newline and send
                _inputField.text = text.Replace("\n", "").Replace("\r", "");
                OnSendClicked();
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

        private IEnumerator StreamLlmResponse()
        {
            _isStreaming = true;
            _sendButton.interactable = false;

            // Show plain "Thinking..." label (no bubble)
            var thinkingObj = CreateObj("Thinking", _contentTransform);
            var thinkingText = thinkingObj.AddComponent<Text>();
            thinkingText.text = "Thinking...";
            thinkingText.font = UIStyleConstants.AppFont;
            thinkingText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.MessageFontSize);
            thinkingText.fontStyle = FontStyle.Italic;
            thinkingText.color = UIStyleConstants.TextMuted;
            thinkingText.alignment = TextAnchor.MiddleLeft;
            var thinkingFitter = thinkingObj.AddComponent<ContentSizeFitter>();
            thinkingFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _coroutineHost.StartCoroutine(ScrollToBottom());

            int round = 0;
            const int maxRounds = 5;
            bool needsMoreRounds = true;

            _scrollPending = true;
            string latestText = null;
            GameObject bubbleRow = null;
            Text messageText = null;

            // Single UI loop coroutine shared across all tool-call rounds
            _coroutineHost.StartCoroutine(StreamingUiLoop(() => messageText, () => latestText, () => _scrollPending));

            while (needsMoreRounds && round < maxRounds)
            {
                round++;
                needsMoreRounds = false;
                List<ToolCall> pendingToolCalls = null;

                yield return LlmClient.SendChatRequest(
                    _conversationHistory,
                    _settings,
                    onToken: (accumulated) =>
                    {
                        // On first visible token, replace "Thinking..." with a real bubble.
                        // Skip whitespace-only content (some models stream leading newlines).
                        if (bubbleRow == null)
                        {
                            if (accumulated.Trim().Length == 0)
                                return;
                            Object.Destroy(thinkingObj);
                            thinkingObj = null;
                            var msg = new ChatMessage(MessageSender.AI, accumulated.TrimStart());
                            bubbleRow = ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                            messageText = ChatBubbleFactory.GetMessageText(bubbleRow);
                            if (messageText != null)
                            {
                                var textLayout = messageText.GetComponent<LayoutElement>();
                                if (textLayout != null)
                                    textLayout.preferredWidth = UIStyleConstants.Scaled(
                                        UIStyleConstants.WindowWidth * UIStyleConstants.BubbleMaxWidthRatio)
                                        - UIStyleConstants.ScaledInt(UIStyleConstants.BubblePadding) * 2;
                            }
                        }
                        latestText = accumulated;
                    },
                    onComplete: (text) =>
                    {
                        _conversationHistory.Add(new ChatMessage(MessageSender.AI, text));
                        latestText = text;
                    },
                    onToolCalls: (toolCalls) =>
                    {
                        pendingToolCalls = toolCalls;
                    },
                    onError: (error) =>
                    {
                        // On error with no tokens yet, replace "Thinking..." with error in bubble
                        if (bubbleRow == null)
                        {
                            Object.Destroy(thinkingObj);
                            thinkingObj = null;
                            var msg = new ChatMessage(MessageSender.AI, error);
                            bubbleRow = ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                            messageText = ChatBubbleFactory.GetMessageText(bubbleRow);
                        }
                        latestText = error;
                    }
                );

                if (pendingToolCalls != null)
                {
                    // Add assistant tool-call message to history
                    _conversationHistory.Add(ChatMessage.CreateAssistantToolCall(pendingToolCalls));

                    // Destroy any premature bubble created by content tokens
                    // that arrived before the tool_calls chunks
                    if (bubbleRow != null)
                    {
                        Object.Destroy(bubbleRow);
                        bubbleRow = null;
                        messageText = null;
                        latestText = null;
                    }

                    // Destroy thinking label — we'll show per-tool status instead
                    if (thinkingObj != null)
                    {
                        Object.Destroy(thinkingObj);
                        thinkingObj = null;
                    }

                    // Execute each tool: show per-tool status label
                    foreach (var tc in pendingToolCalls)
                    {
                        var statusObj = CreateObj("ToolStatus", _contentTransform);
                        var statusText = statusObj.AddComponent<Text>();
                        statusText.text = ToolDefinitions.GetToolStatusLabel(tc.FunctionName);
                        statusText.font = UIStyleConstants.AppFont;
                        statusText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.MessageFontSize);
                        statusText.fontStyle = FontStyle.Italic;
                        statusText.color = UIStyleConstants.TextMuted;
                        statusText.alignment = TextAnchor.MiddleLeft;
                        var statusFitter = statusObj.AddComponent<ContentSizeFitter>();
                        statusFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        _coroutineHost.StartCoroutine(ScrollToBottom());

                        string result = null;
                        yield return ToolDefinitions.ExecuteToolCoroutine(
                            tc.FunctionName, tc.Arguments, r => result = r);
                        _conversationHistory.Add(ChatMessage.CreateToolResult(tc.Id, result ?? "{}"));

                        Object.Destroy(statusObj);
                    }

                    // Wait one frame for cleanup
                    yield return null;

                    // Show thinking label for next LLM round
                    thinkingObj = CreateObj("Thinking", _contentTransform);
                    thinkingText = thinkingObj.AddComponent<Text>();
                    thinkingText.text = "Thinking...";
                    thinkingText.font = UIStyleConstants.AppFont;
                    thinkingText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.MessageFontSize);
                    thinkingText.fontStyle = FontStyle.Italic;
                    thinkingText.color = UIStyleConstants.TextMuted;
                    thinkingText.alignment = TextAnchor.MiddleLeft;
                    var newFitter = thinkingObj.AddComponent<ContentSizeFitter>();
                    newFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    needsMoreRounds = true;
                }
            }

            // Final UI update with complete text
            _scrollPending = false;
            // Clean up thinking label if still present
            if (thinkingObj != null)
                Object.Destroy(thinkingObj);

            // If no bubble was created yet (e.g. non-streamed response after tool calls), create one now
            if (bubbleRow == null && latestText != null)
            {
                var msg = new ChatMessage(MessageSender.AI, latestText);
                bubbleRow = ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                messageText = ChatBubbleFactory.GetMessageText(bubbleRow);
            }
            else if (messageText != null && latestText != null)
            {
                messageText.text = latestText;
                LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
            }

            _isStreaming = false;
            _sendButton.interactable = true;
            _coroutineHost.StartCoroutine(ScrollToBottom());
        }

        /// <summary>
        /// Throttled UI update loop during streaming: updates bubble text and scrolls at ~10fps.
        /// </summary>
        private IEnumerator StreamingUiLoop(System.Func<Text> getMessageText, System.Func<string> getLatest, System.Func<bool> isActive)
        {
            string displayed = null;
            var wait = new WaitForSeconds(0.1f);
            while (isActive())
            {
                var messageText = getMessageText();
                string latest = getLatest();
                if (latest != null && latest != displayed && messageText != null)
                {
                    messageText.text = latest;
                    displayed = latest;
                    LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
                }
                if (_scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = 0f;
                yield return wait;
            }
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

        private static GameObject CreateObj(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Sprite _gearSprite;
        private static Sprite CreateGearSprite()
        {
            if (_gearSprite != null) return _gearSprite;

            int size = 64;
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

                    // Gear tooth profile: alternate between outer and inner radius
                    float toothAngle = angle * teeth / (2f * Mathf.PI);
                    float frac = toothAngle - Mathf.Floor(toothAngle);
                    // Square-ish teeth with smooth edges
                    float gearR = frac < 0.5f ? outerR : innerR;

                    float alpha;
                    if (dist < holeR - 0.5f)
                        alpha = 0f; // center hole
                    else if (dist < holeR + 0.5f)
                        alpha = dist - (holeR - 0.5f); // anti-alias inner edge
                    else if (dist < gearR - 0.5f)
                        alpha = 1f;
                    else if (dist < gearR + 0.5f)
                        alpha = (gearR + 0.5f) - dist; // anti-alias outer edge
                    else
                        alpha = 0f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            _gearSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _gearSprite;
        }
    }

    /// <summary>
    /// Handles dragging the window by its header bar.
    /// </summary>
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Target;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target != null)
                Target.anchoredPosition += eventData.delta;
        }
    }
}
