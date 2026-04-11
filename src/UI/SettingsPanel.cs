using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kerpilot
{
    public class SettingsPanel
    {
        private GameObject _root;
        private InputField _baseUrlInput;
        private InputField _apiKeyInput;
        private InputField _modelInput;
        private Text _statusText;
        private KerpilotSettings _settings;
        private readonly Action _onBack;

        public SettingsPanel(Transform parent, KerpilotSettings settings, Action onBack)
        {
            _settings = settings;
            _onBack = onBack;
            Build(parent);
            _root.SetActive(false);
        }

        public void Show()
        {
            // Refresh fields from current settings
            _baseUrlInput.text = _settings.BaseUrl;
            _apiKeyInput.text = _settings.ApiKey;
            _modelInput.text = _settings.ModelName;
            _statusText.text = "";
            _root.SetActive(true);
        }

        public void Hide()
        {
            InputLockManager.RemoveControlLock("KerpilotSettingsLock");
            _root.SetActive(false);
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        public void UpdateSettings(KerpilotSettings settings)
        {
            _settings = settings;
        }

        private void Build(Transform parent)
        {
            _root = CreateObj("SettingsPanel", parent);
            var rootElement = _root.AddComponent<LayoutElement>();
            rootElement.flexibleHeight = 1f;

            var rootLayout = _root.AddComponent<VerticalLayoutGroup>();
            int pad = UIStyleConstants.ScaledInt(16);
            rootLayout.padding = new RectOffset(pad, pad, pad, pad);
            rootLayout.spacing = UIStyleConstants.Scaled(12);
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childAlignment = TextAnchor.UpperLeft;

            var rootBg = _root.AddComponent<Image>();
            rootBg.color = UIStyleConstants.BackgroundDark;

            // Section title
            CreateLabel("Settings", _root.transform, UIStyleConstants.HeaderFontSize, FontStyle.Bold);

            // API Endpoint
            CreateLabel("API Endpoint", _root.transform, UIStyleConstants.SettingsLabelFontSize, FontStyle.Normal);
            _baseUrlInput = CreateInputField(_root.transform, "https://api.openai.com/v1");

            // API Key
            CreateLabel("API Key", _root.transform, UIStyleConstants.SettingsLabelFontSize, FontStyle.Normal);
            _apiKeyInput = CreateInputField(_root.transform, "sk-...", InputField.ContentType.Password);

            // Model
            CreateLabel("Model", _root.transform, UIStyleConstants.SettingsLabelFontSize, FontStyle.Normal);
            _modelInput = CreateInputField(_root.transform, "gpt-4");

            // Status text
            var statusObj = CreateObj("Status", _root.transform);
            _statusText = statusObj.AddComponent<Text>();
            _statusText.text = "";
            _statusText.font = UIStyleConstants.AppFont;
            _statusText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.SettingsLabelFontSize);
            _statusText.color = UIStyleConstants.AccentBlue;
            _statusText.alignment = TextAnchor.MiddleCenter;
            var statusElement = statusObj.AddComponent<LayoutElement>();
            statusElement.preferredHeight = UIStyleConstants.Scaled(20);

            // Buttons row
            var btnRow = CreateObj("ButtonRow", _root.transform);
            var btnLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = UIStyleConstants.Scaled(8);
            btnLayout.childForceExpandWidth = false;
            btnLayout.childForceExpandHeight = false;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            var btnRowElement = btnRow.AddComponent<LayoutElement>();
            btnRowElement.preferredHeight = UIStyleConstants.Scaled(36);

            // Spacer to center buttons
            var spacerLeft = CreateObj("Spacer", btnRow.transform);
            spacerLeft.AddComponent<LayoutElement>().flexibleWidth = 1f;

            CreateButton("Back", btnRow.transform, UIStyleConstants.PanelDark, OnBackClicked);
            CreateButton("Save", btnRow.transform, UIStyleConstants.AccentBlue, OnSaveClicked);

            var spacerRight = CreateObj("Spacer", btnRow.transform);
            spacerRight.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void OnSaveClicked()
        {
            _settings.BaseUrl = _baseUrlInput.text.Trim();
            _settings.ApiKey = _apiKeyInput.text.Trim();
            _settings.ModelName = _modelInput.text.Trim();

            if (string.IsNullOrEmpty(_settings.BaseUrl))
                _settings.BaseUrl = "https://api.openai.com/v1";
            if (string.IsNullOrEmpty(_settings.ModelName))
                _settings.ModelName = "gpt-4";

            _settings.Save();
            _statusText.text = "Settings saved!";
            _statusText.color = UIStyleConstants.AccentBlue;
        }

        private void OnBackClicked()
        {
            _onBack?.Invoke();
        }

        private Text CreateLabel(string text, Transform parent, int fontSize, FontStyle style)
        {
            var obj = CreateObj("Label", parent);
            var label = obj.AddComponent<Text>();
            label.text = text;
            label.font = UIStyleConstants.AppFont;
            label.fontSize = UIStyleConstants.ScaledFont(fontSize);
            label.fontStyle = style;
            label.color = UIStyleConstants.TextLight;
            label.alignment = TextAnchor.MiddleLeft;
            var fitter = obj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return label;
        }

        private InputField CreateInputField(Transform parent, string placeholder,
            InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            var fieldObj = CreateObj("InputField", parent);
            var fieldBg = fieldObj.AddComponent<Image>();
            fieldBg.sprite = SpriteFactory.RoundedSprite;
            fieldBg.type = Image.Type.Sliced;
            fieldBg.color = UIStyleConstants.InputBackground;
            var fieldElement = fieldObj.AddComponent<LayoutElement>();
            fieldElement.preferredHeight = UIStyleConstants.Scaled(32);

            int textPad = UIStyleConstants.ScaledInt(10);

            // Placeholder
            var phObj = CreateObj("Placeholder", fieldObj.transform);
            var ph = phObj.AddComponent<Text>();
            ph.text = placeholder;
            ph.font = UIStyleConstants.AppFont;
            ph.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            ph.fontStyle = FontStyle.Italic;
            ph.color = UIStyleConstants.TextMuted;
            ph.alignment = TextAnchor.MiddleLeft;
            var phRect = phObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(textPad, 0);
            phRect.offsetMax = new Vector2(-textPad, 0);

            // Input text
            var textObj = CreateObj("Text", fieldObj.transform);
            var inputText = textObj.AddComponent<Text>();
            inputText.font = UIStyleConstants.AppFont;
            inputText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            inputText.color = UIStyleConstants.TextLight;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(textPad, 0);
            textRect.offsetMax = new Vector2(-textPad, 0);

            var field = fieldObj.AddComponent<InputField>();
            field.textComponent = inputText;
            field.placeholder = ph;
            field.contentType = contentType;

            // Lock game controls when input is focused
            var trigger = fieldObj.AddComponent<EventTrigger>();
            var selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((_) =>
                InputLockManager.SetControlLock(ControlTypes.All, "KerpilotSettingsLock"));
            trigger.triggers.Add(selectEntry);
            var deselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
            deselectEntry.callback.AddListener((_) =>
                InputLockManager.RemoveControlLock("KerpilotSettingsLock"));
            trigger.triggers.Add(deselectEntry);

            return field;
        }

        private void CreateButton(string label, Transform parent, Color bgColor, Action onClick)
        {
            var btnObj = CreateObj(label + "Button", parent);
            var btnBg = btnObj.AddComponent<Image>();
            btnBg.sprite = SpriteFactory.RoundedSprite;
            btnBg.type = Image.Type.Sliced;
            btnBg.color = bgColor;
            var btn = btnObj.AddComponent<Button>();
            var btnElement = btnObj.AddComponent<LayoutElement>();
            btnElement.preferredWidth = UIStyleConstants.Scaled(80);
            btnElement.preferredHeight = UIStyleConstants.Scaled(32);

            var labelObj = CreateObj("Label", btnObj.transform);
            var labelText = labelObj.AddComponent<Text>();
            labelText.text = label;
            labelText.font = UIStyleConstants.AppFont;
            labelText.fontSize = UIStyleConstants.ScaledFont(UIStyleConstants.InputFontSize);
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private static GameObject CreateObj(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
