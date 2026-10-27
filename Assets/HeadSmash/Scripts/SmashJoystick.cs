using UnityEngine;
using UnityEngine.EventSystems;

namespace HeadSmash
{
    public sealed class SmashJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public Vector2 Value { get; private set; }
        int activePointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if (activePointer != int.MinValue) return;
            activePointer = e.pointerId;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local);
            float radius = rect.rect.width * .32f;
            Value = Vector2.ClampMagnitude(local / radius, 1);
            knob.anchoredPosition = Value * radius;
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == activePointer) ResetStick(); }
        public void ResetStick() { activePointer = int.MinValue; Value = Vector2.zero; if (knob != null) knob.anchoredPosition = Vector2.zero; }
        void OnDisable() => ResetStick();
        void OnApplicationFocus(bool focus) { if (!focus) ResetStick(); }
    }

}
