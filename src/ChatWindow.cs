using System.Collections;
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
        private ScrollRect _scrollRect;
        private InputField _inputField;
        private Transform _contentTransform;
        private MonoBehaviour _coroutineHost;

        public bool IsVisible => _canvasObj != null && _canvasObj.activeSelf;

        public void Initialize(MonoBehaviour host)
        {
            _coroutineHost = host;
            BuildUI();
            AddMessage(new ChatMessage(MessageSender.AI, "Hello! I'm Kerpilot. How can I help you today?"));
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
            var area = CreateObj("MessageArea", parent);
            var areaElement = area.AddComponent<LayoutElement>();
            areaElement.flexibleHeight = 1f;

            // ScrollRect setup
            _scrollRect = area.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = UIStyleConstants.Scaled(30f);

            // Background
            var areaBg = area.AddComponent<Image>();
            areaBg.color = UIStyleConstants.BackgroundDark;

            // Viewport
            var viewport = CreateObj("Viewport", area.transform);
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
            var contentRect = content.GetComponent<RectTransform>();
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
            var bar = CreateObj("InputBar", parent);
            var barImage = bar.AddComponent<Image>();
            barImage.color = UIStyleConstants.PanelDark;
            var barElement = bar.AddComponent<LayoutElement>();
            barElement.preferredHeight = UIStyleConstants.Scaled(UIStyleConstants.InputBarHeight);
            barElement.flexibleHeight = 0;

            var barLayout = bar.AddComponent<HorizontalLayoutGroup>();
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = false;
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.padding = new RectOffset(
                UIStyleConstants.ScaledInt(8), UIStyleConstants.ScaledInt(8),
                UIStyleConstants.ScaledInt(6), UIStyleConstants.ScaledInt(6));
            barLayout.spacing = UIStyleConstants.Scaled(8);

            // Input field
            var inputObj = CreateObj("InputField", bar.transform);
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
            _inputField.onEndEdit.AddListener(OnInputEndEdit);

            // Lock game controls when input is focused
            var trigger = inputObj.AddComponent<EventTrigger>();
            var selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((_) => SetInputLock());
            trigger.triggers.Add(selectEntry);
            var deselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
            deselectEntry.callback.AddListener((_) => RemoveInputLock());
            trigger.triggers.Add(deselectEntry);

            // Send button
            var sendObj = CreateObj("SendButton", bar.transform);
            var sendBg = sendObj.AddComponent<Image>();
            sendBg.sprite = ChatBubbleFactory.RoundedSprite;
            sendBg.type = Image.Type.Sliced;
            sendBg.color = UIStyleConstants.SendButtonColor;
            var sendBtn = sendObj.AddComponent<Button>();
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

            sendBtn.onClick.AddListener(OnSendClicked);
        }

        private void OnInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnSendClicked();
            }
        }

        private void OnSendClicked()
        {
            string text = _inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputField.text = "";
            _inputField.ActivateInputField();

            AddMessage(new ChatMessage(MessageSender.User, text));
            _coroutineHost.StartCoroutine(SimulateResponse());
        }

        private IEnumerator SimulateResponse()
        {
            yield return new WaitForSeconds(1f);
            AddMessage(new ChatMessage(MessageSender.AI, "Thinking..."));
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
