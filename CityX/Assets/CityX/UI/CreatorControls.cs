using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The creator's continuous controls: the colour wheel and the brush size
    /// slider.
    ///
    /// A COMPONENT, NOT LISTENERS ADDED BY THE BUILDER -- same reason as
    /// ButtonAction. A listener added from an editor script is not serialized,
    /// so a slider wired that way moves and changes nothing.
    /// </summary>
    public sealed class CreatorControls : MonoBehaviour, IPointerClickHandler, IDragHandler
    {
        public CharacterCreator Creator;

        [Tooltip("The hue/saturation wheel. Clicking it picks that colour.")]
        public RawImage Wheel;

        [Tooltip("Darkens the picked hue, so black and greys are reachable.")]
        public Slider Value;

        public Slider BrushSize;

        [Tooltip("Shows the colour currently being painted with.")]
        public Image Preview;

        public Text BrushLabel;

        /// <summary>The wheel's own resolution. Small: it is 200px on screen.</summary>
        public const int WheelSize = 256;

        private float _hue;
        private float _saturation;

        private void Awake()
        {
            if (Wheel != null && Wheel.texture == null)
            {
                Wheel.texture = BuildWheel();
            }
            if (Value != null)
            {
                Value.onValueChanged.AddListener(_ => Recolour());
            }
            if (BrushSize != null)
            {
                BrushSize.onValueChanged.AddListener(OnBrushSize);
                OnBrushSize(BrushSize.value);
            }
            Recolour();
        }

        private void OnBrushSize(float value)
        {
            int size = Mathf.RoundToInt(value);
            if (Creator != null)
            {
                Creator.BrushRadiusPixels = size;
            }
            if (BrushLabel != null)
            {
                BrushLabel.text = size.ToString();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Pick(eventData);
        }

        /// <summary>Dragging across the wheel picks continuously, like a real one.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            Pick(eventData);
        }

        private void Pick(PointerEventData eventData)
        {
            if (Wheel == null)
            {
                return;
            }

            var rect = (RectTransform)Wheel.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            // Normalised to the wheel's radius, so the maths does not care what
            // size the layout gave it.
            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
            Vector2 fromCentre = local / Mathf.Max(radius, 0.0001f);
            float distance = fromCentre.magnitude;
            if (distance > 1.0f)
            {
                return;   // outside the circle, in the corners of its square
            }

            _hue = Mathf.Repeat(Mathf.Atan2(fromCentre.y, fromCentre.x) / (Mathf.PI * 2.0f), 1.0f);
            _saturation = distance;
            Recolour();
        }

        private void Recolour()
        {
            float value = Value != null ? Value.value : 1.0f;
            Color picked = Color.HSVToRGB(_hue, _saturation, value);

            if (Creator != null)
            {
                Creator.Colour = picked;
                // Choosing a colour means you want to paint with it. Leaving
                // the eraser on would force every stroke white and make the
                // wheel look broken.
                Creator.Erasing = false;
            }
            if (Preview != null)
            {
                Preview.color = picked;
            }
        }

        /// <summary>Called by a palette swatch, so the wheel keeps up with it.</summary>
        public void Adopt(Color colour)
        {
            Color.RGBToHSV(colour, out _hue, out _saturation, out float v);
            if (Value != null)
            {
                Value.SetValueWithoutNotify(v);
            }
            Recolour();
        }

        /// <summary>
        /// Hue round the circle, saturation out from the middle. Transparent
        /// outside the circle so it reads as a disc rather than a square.
        /// </summary>
        public static Texture2D BuildWheel()
        {
            var texture = new Texture2D(WheelSize, WheelSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[WheelSize * WheelSize];
            const float centre = WheelSize * 0.5f;

            for (int y = 0; y < WheelSize; y++)
            {
                for (int x = 0; x < WheelSize; x++)
                {
                    float dx = (x + 0.5f - centre) / centre;
                    float dy = (y + 0.5f - centre) / centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance > 1.0f)
                    {
                        pixels[y * WheelSize + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2.0f), 1.0f);
                    Color colour = Color.HSVToRGB(hue, distance, 1.0f);

                    // Feathered at the rim, or the disc has a hard jagged edge.
                    colour.a = Mathf.Clamp01((1.0f - distance) * WheelSize * 0.25f);
                    pixels[y * WheelSize + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
