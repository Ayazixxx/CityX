using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The colour bar under a creator colour picker shows the picked colour's code, and takes one typed in (owner, 2026-11-17: "make the color
    /// code visible at the bottom and it lets you enter color codes to get an exact hue"): click it, type #RRGGBB (or RGB), press Enter or
    /// click away, and the picker goes to that exact colour. A code it cannot read puts the old one back. While it is being typed in it is
    /// left alone; otherwise it follows the picker, whatever moved it (the square, the hue, a swatch, the eyedropper).
    /// </summary>
    [RequireComponent(typeof(InputField))]
    public sealed class ColourCodeField : MonoBehaviour
    {
        [Tooltip("The picker the code is read from and typed into: CreatorControls or HairColourControls.")]
        public MonoBehaviour Picker;

        /// <summary>The code's ink on a dark colour and on a light one.</summary>
        private static readonly Color OnDark = new Color(0.95f, 0.94f, 0.90f), OnLight = new Color(0.08f, 0.08f, 0.10f);

        /// <summary>A colour at least this light (its luma) takes the dark ink.</summary>
        public const float LightFrom = 0.55f;

        private InputField _field;

        private IColourPicker Target => Picker as IColourPicker;

        private void Awake()
        {
            _field = GetComponent<InputField>();
            _field.onEndEdit.AddListener(Typed);
            _field.onValidateInput = (text, index, typed) => System.Uri.IsHexDigit(typed) || typed == '#' ? char.ToUpperInvariant(typed) : '\0';
            Update();
        }

        private void Update()
        {
            IColourPicker target = Target;
            if (target == null || _field == null)
            {
                return;
            }
            Color colour = target.Current;
            if (!_field.isFocused)
            {
                string code = ColourCode.Of(colour);
                if (_field.text != code)
                {
                    _field.SetTextWithoutNotify(code);
                }
            }
            Color ink = Luma(colour) >= LightFrom ? OnLight : OnDark;
            if (_field.textComponent != null)
            {
                _field.textComponent.color = ink;
            }
            _field.caretColor = ink;
        }

        private void Typed(string typed)
        {
            IColourPicker target = Target;
            if (target != null && ColourCode.TryRead(typed, out Color colour))
            {
                target.Adopt(colour);
            }
            if (target != null)
            {
                _field.SetTextWithoutNotify(ColourCode.Of(target.Current));
            }
        }

        /// <summary>How light a colour looks (Rec. 601 luma, as the eye weighs red, green and blue).</summary>
        public static float Luma(Color colour) => 0.299f * colour.r + 0.587f * colour.g + 0.114f * colour.b;
    }
}
