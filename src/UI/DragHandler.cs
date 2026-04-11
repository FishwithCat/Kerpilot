using UnityEngine;
using UnityEngine.EventSystems;

namespace Kerpilot
{
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
