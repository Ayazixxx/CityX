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

        [Tooltip("Saturation across, value up, for the currently selected hue. Clicking it picks that colour.")]
        public RawImage SVSquare;

        [Tooltip("Picks the hue the square is drawn for.")]
        public Slider Hue;

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

        /// <summary>
        /// The square's own resolution. Matches its on-screen size exactly
        /// (128 units), rather than the wheel's old 256 -- unlike the wheel,
        /// which baked every hue into one static ring built once, this square
        /// only ever shows a single hue and has to be rebuilt on every hue
        /// slider drag, so it stays cheap to regenerate at interactive rates.
        /// </summary>
        public const int SquareSize = 128;

        /// <summary>The hue strip's own resolution -- one hue per column.</summary>
        public const int HueStripSize = 256;

        /// <summary>An unlit button. Matches CharacterCreatorBuilder's own default.</summary>
        private static readonly Color Idle = new Color(0.11f, 0.12f, 0.15f, 0.95f);

        /// <summary>A pressed-in toggle, or the tool currently in hand.</summary>
        private static readonly Color Active = new Color(0.30f, 0.26f, 0.12f, 1.0f);

        private static readonly string[] IconKinds = { "brush", "bucket", "eyedropper" };

        private float _hue;
        private float _saturation;

        /// <summary>Matches the Value slider's own old default, so a fresh creator still opens on white.</summary>
        private float _value = 1.0f;

        private void Awake()
        {
            if (SVSquare != null && SVSquare.texture == null)
            {
                SVSquare.texture = BuildSquare(_hue);
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
            if (Hue != null)
            {
                Hue.onValueChanged.AddListener(OnHueChanged);
            }
            if (BrushSize != null)
            {
                BrushSize.onValueChanged.AddListener(OnBrushSize);
                OnBrushSize(BrushSize.value);
            }
            Recolour();
        }

        /// <summary>
        /// The square only ever shows ONE hue at a time, unlike the old
        /// wheel's static ring -- so every hue drag has to redraw it, not
        /// just move a slider over a texture that was already correct.
        /// </summary>
        private void OnHueChanged(float hue)
        {
            _hue = hue;
            if (SVSquare != null)
            {
                SVSquare.texture = BuildSquare(_hue);
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
            if (SVSquare == null)
            {
                return;
            }

            var rect = (RectTransform)SVSquare.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            // rect.rect is centred on (0,0) for a middle-pivoted transform, so
            // this normalises into [0,1] regardless of what size the layout
            // gave it -- x is saturation, y is value, matching the texture
            // BuildSquare paints. Clamped rather than rejected outside the
            // bounds: unlike the old wheel's circle-in-a-square, every point
            // in this rect is a real, valid colour, so a drag that slides
            // past the edge should still pick the nearest one on it.
            Rect area = rect.rect;
            _saturation = Mathf.Clamp01((local.x - area.xMin) / area.width);
            _value = Mathf.Clamp01((local.y - area.yMin) / area.height);
            Recolour();
        }

        private void Recolour()
        {
            Color picked = Color.HSVToRGB(_hue, _saturation, _value);

            if (Creator != null)
            {
                Creator.Colour = picked;
                // Choosing a colour means you want to paint with it. Leaving
                // the eraser on would force every stroke white and make the
                // square look broken.
                Creator.Erasing = false;
            }
            if (Preview != null)
            {
                Preview.color = picked;
            }
        }

        /// <summary>Called by a palette swatch, so the picker keeps up with it.</summary>
        public void Adopt(Color colour)
        {
            Color.RGBToHSV(colour, out _hue, out _saturation, out _value);
            if (Hue != null)
            {
                Hue.SetValueWithoutNotify(_hue);
            }
            if (SVSquare != null)
            {
                SVSquare.texture = BuildSquare(_hue);
            }
            Recolour();
        }

        /// <summary>
        /// Saturation across, value up, for one fixed hue -- Godot's own
        /// default ColorPicker shape (SHAPE_HSV_RECTANGLE), which the
        /// original character creator used unmodified. The wheel this
        /// replaced baked every hue into one static ring; this square only
        /// ever holds the one hue the slider is currently on, so the caller
        /// has to rebuild it whenever that hue changes rather than once.
        /// </summary>
        public static Texture2D BuildSquare(float hue)
        {
            var texture = new Texture2D(SquareSize, SquareSize, TextureFormat.RGB24, false)
            {
                name = "CityXColourSquare",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[SquareSize * SquareSize];
            for (int y = 0; y < SquareSize; y++)
            {
                float value = (y + 0.5f) / SquareSize;
                for (int x = 0; x < SquareSize; x++)
                {
                    float saturation = (x + 0.5f) / SquareSize;
                    pixels[y * SquareSize + x] = Color.HSVToRGB(hue, saturation, value);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Every hue at full saturation and value, left to right -- the strip behind the hue slider's handle.</summary>
        public static Texture2D BuildHueStrip()
        {
            var texture = new Texture2D(HueStripSize, 1, TextureFormat.RGB24, false)
            {
                name = "CityXHueStrip",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[HueStripSize];
            for (int x = 0; x < HueStripSize; x++)
            {
                pixels[x] = Color.HSVToRGB((x + 0.5f) / HueStripSize, 1.0f, 1.0f);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
