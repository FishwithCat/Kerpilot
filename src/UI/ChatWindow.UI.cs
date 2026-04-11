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

            // Settings panel (initially hidden, occupies same space as message area)
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
            titleText.text = "kerpilot";
            titleText.font = UIStyleConstants.AppFont;
            titleText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.HeaderFontSize);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = UIStyleConstants.TextLight;
            var titleElement = titleObj.AddComponent<LayoutElement>();
            titleElement.flexibleWidth = 1f;

            // Settings button with programmatic gear icon
            var settingsObj = CreateObj("SettingsButton", header.transform);
            var settingsBtnImage = settingsObj.AddComponent<Image>();
            settingsBtnImage.sprite = SpriteFactory.GearSprite;
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

            int contentPad = UIStyleConstants.ScaledInt(8);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = UIStyleConstants.Scaled(UIStyleConstants.MessageSpacing);
            contentLayout.padding = new RectOffset(contentPad, contentPad, contentPad, contentPad);

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = contentRect;

            // Click anywhere in the console area to focus input
            var clickHandler = _messageArea.AddComponent<EventTrigger>();
            var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClick.callback.AddListener((_) => FocusInput());
            clickHandler.triggers.Add(pointerClick);

            // Single rich-text log
            BuildLogText(_contentTransform);

            // Inline input at the bottom of the content area
            BuildInlineInput(_contentTransform);
        }

        private void BuildLogText(Transform contentParent)
        {
            var logObj = CreateObj("Log", contentParent);
            _logText = logObj.AddComponent<Text>();
            _logText.font = UIStyleConstants.AppFont;
            _logText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.AiFontSize);
            _logText.color = UIStyleConstants.AiTextColor;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            _logText.alignment = TextAnchor.UpperLeft;
            _logText.supportRichText = true;
            var logElement = logObj.AddComponent<LayoutElement>();
            logElement.flexibleWidth = 1f;
        }

        private void BuildInlineInput(Transform contentParent)
        {
            // Input row with "> " prompt prefix
            _inputRow = CreateObj("InputRow", contentParent);
            var rowLayout = _inputRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.spacing = UIStyleConstants.Scaled(4);
            var rowFitter = _inputRow.AddComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rowElement = _inputRow.AddComponent<LayoutElement>();
            rowElement.flexibleWidth = 1f;

            // Prompt prefix
            var prefixObj = CreateObj("Prefix", _inputRow.transform);
            var prefixText = prefixObj.AddComponent<Text>();
            prefixText.text = ">";
            prefixText.font = UIStyleConstants.AppFont;
            prefixText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.UserFontSize);
            prefixText.color = UIStyleConstants.PromptColor;
            prefixText.alignment = TextAnchor.UpperLeft;
            var prefixFitter = prefixObj.AddComponent<ContentSizeFitter>();
            prefixFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            prefixFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var inputObj = CreateObj("InputField", _inputRow.transform);
            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0, 0, 0, 0); // fully transparent
            _inputElement = inputObj.AddComponent<LayoutElement>();
            _inputElement.flexibleWidth = 1f;
            _inputElement.minHeight = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMinHeight);
            _inputElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.InputFieldMinHeight);

            int inputPadH = UIStyleConstants.ScaledInt(2);
            int inputPadV = UIStyleConstants.ScaledInt(2);

            var inputTextObj = CreateObj("Text", inputObj.transform);
            var inputText = inputTextObj.AddComponent<Text>();
            inputText.font = UIStyleConstants.AppFont;
            inputText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            inputText.color = UIStyleConstants.UserTextColor;
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
            _inputField.lineType = InputField.LineType.MultiLineNewline;
            _inputField.caretWidth = UIStyleConstants.ScaledInt(8);
            _inputField.caretBlinkRate = 0.53f;
            _inputField.caretColor = UIStyleConstants.AiTextColor;
            _inputField.selectionColor = new Color(0.35f, 0.43f, 0.76f, 0.4f);
            _inputField.onValueChanged.AddListener(OnInputValueChanged);

            // Lock game controls when input is focused
            var trigger = inputObj.AddComponent<EventTrigger>();
            var selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((_) => SetInputLock());
            trigger.triggers.Add(selectEntry);
            var deselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
            deselectEntry.callback.AddListener((_) => RemoveInputLock());
            trigger.triggers.Add(deselectEntry);
        }
    }
}
