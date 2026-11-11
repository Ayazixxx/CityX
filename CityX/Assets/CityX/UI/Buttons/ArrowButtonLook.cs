using UnityEngine;
using UnityEngine.EventSystems;

namespace CityX.UI
{
    /// <summary>
    /// How an arrow button answers being pointed at: the yellow band eases in between its outline and the arrow and the arrow grows a little;
    /// held down, it darkens and sinks back (owner, 2026-11-10: "make yellow outline appear in between the black outline and the arrow when you
    /// hover over the buttons"). A button selected by keyboard or controller shows the same as one under the mouse.
    /// </summary>
    public sealed class ArrowButtonLook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        /// <summary>How much bigger the button is, pointed at, and how much smaller held down.</summary>
        public const float HoverScale = 1.06f;

        public const float PressedScale = 0.97f;

        /// <summary>How long the band and the size take to change, seconds.</summary>
        public const float EaseSeconds = 0.08f;

        public ArrowGraphic Arrow;

        private bool _over;
        private bool _selected;
        private bool _down;
        private float _shown;
        private float _held;

        /// <summary>Pointed at, or selected.</summary>
        public bool Lit => _over || _selected;

        public void OnPointerEnter(PointerEventData eventData) => _over = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            _down = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _down = true;

        public void OnPointerUp(PointerEventData eventData) => _down = false;

        public void OnSelect(BaseEventData eventData) => _selected = true;

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        private void OnDisable()
        {
            _over = _selected = _down = false;
            _shown = _held = 0.0f;
            Show();
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / EaseSeconds;
            _shown = Mathf.MoveTowards(_shown, Lit ? 1.0f : 0.0f, step);
            _held = Mathf.MoveTowards(_held, _down ? 1.0f : 0.0f, step);
            Show();
        }

        private void Show()
        {
            if (Arrow != null)
            {
                Arrow.Highlight = Mathf.SmoothStep(0.0f, 1.0f, _shown);
                Arrow.Pressed = _held;
            }
            float size = Mathf.Lerp(1.0f, HoverScale, Mathf.SmoothStep(0.0f, 1.0f, _shown)) * Mathf.Lerp(1.0f, PressedScale, _held);
            transform.localScale = new Vector3(size, size, 1.0f);
        }
    }
}
