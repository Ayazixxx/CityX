using UnityEngine;
using UnityEngine.EventSystems;

namespace CityX.UI
{
    /// <summary>
    /// What a button's face shows of being pointed at and held down: the new style's arrows (<see cref="ArrowGraphic"/>) and drawings
    /// (<see cref="DrawnFace"/>) both.
    /// </summary>
    public interface IButtonFace
    {
        /// <summary>0 to 1: how much it shows being pointed at (the arrows' yellow band, the drawings' red aura).</summary>
        float Highlight { set; }

        /// <summary>0 to 1: how far it is darkened, held down.</summary>
        float Pressed { set; }
    }

    /// <summary>
    /// How a button of the new style answers being pointed at: its face shows it (<see cref="IButtonFace"/>, on the same object: an arrow's
    /// yellow band, a drawing's red aura) as it eases in, and the button grows a little; held down, it darkens and sinks back (owner,
    /// 2026-11-10: "make yellow outline appear in between the black outline and the arrow when you hover over the buttons"). A button selected
    /// by keyboard or controller shows the same as one under the mouse.
    /// </summary>
    public sealed class ButtonLook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        /// <summary>How much bigger the button is, pointed at, and how much smaller held down.</summary>
        public const float HoverScale = 1.06f;

        public const float PressedScale = 0.97f;

        /// <summary>How long the band and the size take to change, seconds.</summary>
        public const float EaseSeconds = 0.08f;

        private IButtonFace _face;
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
            _face ??= GetComponent<IButtonFace>();
            if (_face != null)
            {
                _face.Highlight = Mathf.SmoothStep(0.0f, 1.0f, _shown);
                _face.Pressed = _held;
            }
            float size = Mathf.Lerp(1.0f, HoverScale, Mathf.SmoothStep(0.0f, 1.0f, _shown)) * Mathf.Lerp(1.0f, PressedScale, _held);
            transform.localScale = new Vector3(size, size, 1.0f);
        }
    }
}
