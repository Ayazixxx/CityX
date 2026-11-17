using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The creator's hair colour picker: a saturation/value square and a hue strip -- the same
    /// shape as the paint picker, so it reads the same -- and a row of hair-coloured swatches.
    /// Any colour is reachable; the swatches are shortcuts to the common ones.
    ///
    /// LIVES ON THE SQUARE ITSELF, because the square is what takes the clicks and drags. The
    /// card around it (heading, hue strip, swatches) is shown only while the character HAS hair:
    /// a colour picker for a bald head does nothing and only asks what it is for. The card is
    /// hidden with a CanvasGroup, not by switching it off, so this component -- which is on the
    /// square inside the card -- keeps running and can bring it back.
    ///
    /// A COMPONENT, NOT LISTENERS ADDED BY A BUILDER, for the reason ButtonAction gives: a
    /// listener added from an editor script does not survive the scene being saved.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class HairColourControls : MonoBehaviour, IPointerClickHandler, IDragHandler, IColourPicker
    {
        public CharacterCreator Creator;

        [Tooltip("Picks the hue the square is drawn for.")]
        public Slider Hue;

        [Tooltip("Shows the colour the hair currently is.")]
        public Image Preview;

        [Tooltip("The whole card. Faded out while the character is bald.")]
        public CanvasGroup Card;

        private RawImage _square;

        /// <summary>The square, fetched on first use so it also works where Awake has not run (the editor screenshot tool).</summary>
        private RawImage Square => _square != null ? _square : (_square = GetComponent<RawImage>());

        private float _hue;
        private float _saturation;
        private float _value = 1.0f;

        /// <summary>The colour the picker is showing.</summary>
        public Color Current => Color.HSVToRGB(_hue, _saturation, _value);

        public float Saturation => _saturation;

        public float Value => _value;

        private void Awake()
        {
            if (Square.texture == null)
            {
                Square.texture = CreatorControls.BuildSquare(_hue);
            }
            if (Hue != null)
            {
                var strip = Hue.GetComponent<RawImage>();
                if (strip != null && strip.texture == null)
                {
                    strip.texture = CreatorControls.BuildHueStrip();
                }
                Hue.onValueChanged.AddListener(OnHueChanged);
            }
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (Card == null)
            {
                return;
            }
            bool hasHair = Creator != null && Creator.HairStyle != CharacterHair.None;
            Card.alpha = hasHair ? 1.0f : 0.0f;
            Card.blocksRaycasts = hasHair;
            Card.interactable = hasHair;
        }

        /// <summary>
        /// Makes the picker show whatever colour the hair is now, WITHOUT changing it: for when the
        /// creator opens on a character who already has a colour (ReDraw), so the picker does not
        /// sit on the colour it was built with while the hair wears another.
        /// </summary>
        public void Refresh()
        {
            if (Creator == null)
            {
                return;
            }
            Color.RGBToHSV(Creator.HairColour, out _hue, out _saturation, out _value);
            Show();
        }

        /// <summary>Called by a swatch: picks that colour, and moves the picker to it.</summary>
        public void Adopt(Color colour)
        {
            Color.RGBToHSV(colour, out _hue, out _saturation, out _value);
            Show();
            Apply();
        }

        /// <summary>
        /// Draws the square for the current hue, and lets go of the one it replaces: a fresh texture
        /// is made on every hue drag and every refresh, and dropping the old ones on the floor
        /// would leak one 128x128 texture per event.
        /// </summary>
        private void RedrawSquare()
        {
            Texture old = Square.texture;
            Square.texture = CreatorControls.BuildSquare(_hue);
            if (old != null && old.name == "CityXColourSquare")
            {
                if (Application.isPlaying)
                {
                    Destroy(old);
                }
                else
                {
                    DestroyImmediate(old);
                }
            }
        }

        private void Show()
        {
            RedrawSquare();
            if (Hue != null)
            {
                Hue.SetValueWithoutNotify(_hue);
            }
            if (Preview != null)
            {
                Preview.color = Current;
            }
        }

        private void OnHueChanged(float hue)
        {
            _hue = hue;
            RedrawSquare();
            Apply();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Pick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Pick(eventData);
        }

        private void Pick(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            // x is saturation, y is value, clamped: a drag that slides past the edge still picks
            // the nearest colour on it (every point in the square is a real colour).
            Rect area = rect.rect;
            _saturation = Mathf.Clamp01((local.x - area.xMin) / area.width);
            _value = Mathf.Clamp01((local.y - area.yMin) / area.height);
            Apply();
        }

        private void Apply()
        {
            Color picked = Current;
            if (Creator != null)
            {
                Creator.SetHairColour(picked);
            }
            if (Preview != null)
            {
                Preview.color = picked;
            }
        }
    }
}
