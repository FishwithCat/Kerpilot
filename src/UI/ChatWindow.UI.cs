using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kerpilot
{
    public partial class ChatWindow
    {
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
            settingsBtnImage.sprite = ChatBubbleFactory.GearSprite;
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

            // Track user scroll to disable auto-scroll when user scrolls up
            _scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        private void OnScrollValueChanged(Vector2 pos)
        {
            // Near bottom (within threshold) → re-enable auto-scroll
            // Scrolled up → disable auto-scroll so user can read history
            _autoScroll = pos.y <= 0.01f;
        }

        private void BuildInputBar(Transform parent)
        {
            _inputBar = CreateObj("InputBar", parent);
            var barImage = _inputBar.AddComponent<Image>();
            barImage.color = UIStyleConstants.PanelDark;
            _inputBarElement = _inputBar.AddComponent<LayoutElement>();
            _inputBarElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.InputBarHeight);
            _inputBarElement.flexibleHeight = 0;

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
            _inputElement = inputObj.AddComponent<LayoutElement>();
            _inputElement.flexibleWidth = 1f;
            _inputElement.minHeight = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMinHeight);
            _inputElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMinHeight);

            int inputPadH = UIStyleConstants.ScaledInt(10);
            int inputPadV = UIStyleConstants.ScaledInt(6);

            // Placeholder
            var placeholderObj = CreateObj("Placeholder", inputObj.transform);
            var placeholder = placeholderObj.AddComponent<Text>();
            placeholder.text = "Type a message...";
            placeholder.font = UIStyleConstants.AppFont;
            placeholder.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = UIStyleConstants.TextMuted;
            placeholder.alignment = TextAnchor.UpperLeft;
            var phRect = placeholderObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(inputPadH, inputPadV);
            phRect.offsetMax = new Vector2(-inputPadH, -inputPadV);

            // Input text
            var inputTextObj = CreateObj("Text", inputObj.transform);
            var inputText = inputTextObj.AddComponent<Text>();
            inputText.font = UIStyleConstants.AppFont;
            inputText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            inputText.color = UIStyleConstants.TextLight;
            inputText.alignment = TextAnchor.UpperLeft;
            inputText.horizontalOverflow = HorizontalWrapMode.Wrap;
            inputText.verticalOverflow = VerticalWrapMode.Overflow;
            inputText.supportRichText = false;
            var itRect = inputTextObj.GetComponent<RectTransform>();
            itRect.anchorMin = Vector2.zero;
            itRect.anchorMax = Vector2.one;
            itRect.offsetMin = new Vector2(inputPadH, inputPadV);
            itRect.offsetMax = new Vector2(-inputPadH, -inputPadV);

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
            sendElement.minWidth = UIStyleConstants.Scaled(60);
            sendElement.preferredWidth = UIStyleConstants.Scaled(60);
            sendElement.minHeight = UIStyleConstants.Scaled(36);
            sendElement.preferredHeight = UIStyleConstants.Scaled(36);
            sendElement.flexibleWidth = 0;

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
    }
}
