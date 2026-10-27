using UnityEngine.EventSystems;

namespace HeadSmash
{
    /// <summary>An Inspector-editable uGUI Button that fires once on press for responsive skills.</summary>
    public sealed class SmashPress : UnityEngine.UI.Button
    {
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
                onClick.Invoke();
        }
        // The action already fired on press; release must not fire it a second time.
        public override void OnPointerClick(PointerEventData eventData) { }
    }
}
