using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The creator's continuous controls -- the colour wheel and the two
    /// sliders -- and the job of keeping every button on the panel showing the
    /// state it actually represents.
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

        [Tooltip("Reads 'BRUSH 4' -- the number lives in the heading, to save a row.")]
        public Text BrushLabel;

        [Header("Buttons that show a state")]
        [Tooltip("Brush, Bucket, Eyedropper, in Tool order. Lit for the active one.")]
        public Image[] ToolButtons = new Image[0];

        [Tooltip("The icon on each tool button, so it can be redrawn if it is missing.")]
        public RawImage[] ToolIconImages = new RawImage[0];

        public Image MirrorButton;
        public Image EraserButton;

        public Button UndoButton;
        public Button RedoButton;

        /// <summary>The wheel's own resolution. Small: it is 128 units on screen.</summary>
        public const int WheelSize = 256;

        /// <summary>An unlit button. Matches CharacterCreatorBuilder's own default.</summary>
        private static readonly Color Idle = new Color(0.11f, 0.12f, 0.15f, 0.95f);

        /// <summary>A pressed-in toggle, or the tool currently in hand.</summary>
        private static readonly Color Active = new Color(0.30f, 0.26f, 0.12f, 1.0f);

        private static readonly string[] IconKinds = { "brush", "bucket", "eyedropper" };

        private float _hue;
        private float _saturation;

        private void Awake()
        {
            if (Wheel != null && Wheel.texture == null)
            {
                Wheel.texture = BuildWheel();
            }
            // The icons are drawn by the builder and saved into the scene with
            // it, so this is only the fallback for a scene that lost them.
            for (int i = 0; i < ToolIconImages.Length && i < IconKinds.Length; i++)
            {
                if (ToolIconImages[i] != null && ToolIconImages[i].texture == null)
                {
                    ToolIconImages[i].texture = ToolIcons.Build(IconKinds[i]);
                }
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

        /// <summary>
        /// Keeps the panel honest about what is switched on.
        ///
        /// Polled rather than pushed because the state it reflects is changed
        /// from several places -- a swatch clears the eraser, the eyedropper
        /// switches itself back to the brush after one pick -- and a button
        /// that only repaints when its own listener fires goes stale every time
        /// something else moves it.
        /// </summary>
        private void Update()
        {
            if (Creator == null)
            {
                return;
            }

            for (int i = 0; i < ToolButtons.Length; i++)
            {
                if (ToolButtons[i] != null)
                {
                    ToolButtons[i].color = (int)Creator.Current == i ? Active : Idle;
                }
            }
            if (MirrorButton != null)
            {
                MirrorButton.color = Creator.Mirroring ? Active : Idle;
            }
            if (EraserButton != null)
            {
                EraserButton.color = Creator.Erasing ? Active : Idle;
            }
            if (UndoButton != null)
            {
                UndoButton.interactable = Creator.CanUndo;
            }
            if (RedoButton != null)
            {
                RedoButton.interactable = Creator.CanRedo;
            }
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
                BrushLabel.text = "BRUSH  " + size;
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
                name = "CityXColourWheel",
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
