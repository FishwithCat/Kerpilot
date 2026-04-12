using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    /// <summary>
    /// Implements ILayoutElement on an InputField so the layout system
    /// automatically sizes it to fit the current text content.
    /// Listens for text changes and marks its own layout dirty to
    /// ensure the parent LayoutGroup re-queries preferredHeight.
    /// </summary>
    [RequireComponent(typeof(InputField))]
    public class InputFieldAutoResize : MonoBehaviour, ILayoutElement
    {
        private InputField _inputField;
        private float _maxHeight;
        private float _padding;

        public void Setup(float maxHeight, float padding)
        {
            _maxHeight = maxHeight;
            _padding = padding;
        }

        private void Awake()
        {
            _inputField = GetComponent<InputField>();
        }

        private void OnEnable()
        {
            _inputField.onValueChanged.AddListener(OnTextChanged);
        }

        private void OnDisable()
        {
            _inputField.onValueChanged.RemoveListener(OnTextChanged);
        }

        private void OnTextChanged(string _)
        {
            LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
        }

        // Higher than LayoutElement (1) so preferredHeight takes precedence,
        // while LayoutElement still provides flexibleWidth and minHeight.
        public int layoutPriority => 2;

        public float preferredHeight
        {
            get
            {
                if (_inputField == null) return -1;
                var tc = _inputField.textComponent;
                if (tc == null) return -1;

                float width = tc.rectTransform.rect.width;
                if (width <= 0f) return -1;

                var settings = tc.GetGenerationSettings(new Vector2(width, 0f));
                float h = tc.cachedTextGeneratorForLayout.GetPreferredHeight(
                    _inputField.text, settings) / tc.pixelsPerUnit;

                return Mathf.Min(h + _padding, _maxHeight);
            }
        }

        // Return -1 to defer to LayoutElement for all other properties.
        public float minWidth => -1;
        public float preferredWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => -1;
        public float flexibleHeight => -1;

        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
    }
}
