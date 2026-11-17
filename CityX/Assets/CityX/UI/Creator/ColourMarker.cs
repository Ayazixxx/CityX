using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// A circle on a creator colour square where the picked colour is: saturation across, value up (owner, 2026-11-17: "i want it in the box
    /// where you select your color so i can drag and down and make it a bit darker if needed or move it around how i want"). A ring round a disc
    /// of the colour itself, with a dark line round it so it shows on the pale corner. It follows the picker whatever moved it (a drag on the
    /// square, the hue, a swatch, a code typed in, the eyedropper); the square under it takes the drags. The pictures are drawn on waking, not
    /// saved: a texture made in code is not kept by the scene.
    /// </summary>
    public sealed class ColourMarker : MonoBehaviour
    {
        [Tooltip("The picker whose colour it marks: CreatorControls or HairColourControls.")]
        public MonoBehaviour Picker;

        [Tooltip("The ring.")]
        public RawImage Ring;

        [Tooltip("The disc inside it, in the picked colour.")]
        public RawImage Fill;

        /// <summary>The pictures' size, pixels.</summary>
        public const int PictureSize = 48;

        /// <summary>The ring's outer edge, its inner edge, and the dark line round its outside, as shares of the picture's half-width.</summary>
        public const float RingOuter = 0.92f, RingInner = 0.62f, Outline = 0.12f;

        private static Texture2D _ring;
        private static Texture2D _disc;

        private void Awake()
        {
            if (Ring != null)
            {
                Ring.texture = RingPicture;
            }
            if (Fill != null)
            {
                Fill.texture = DiscPicture;
            }
            Update();
        }

        private void Update()
        {
            if (!(Picker is IColourPicker picker) || !(transform.parent is RectTransform square))
            {
                return;
            }
            Rect area = square.rect;
            var at = new Vector2(area.width * Mathf.Clamp01(picker.Saturation), area.height * Mathf.Clamp01(picker.Value));
            var rect = (RectTransform)transform;
            if (rect.anchoredPosition != at)
            {
                rect.anchoredPosition = at;   // (anchored at the square's lower left corner)
            }
            if (Fill != null)
            {
                Fill.color = picker.Current;
            }
        }

        private static Texture2D RingPicture => _ring != null ? _ring : (_ring = Picture("CityXColourMarkerRing", RingPixels(PictureSize)));

        private static Texture2D DiscPicture => _disc != null ? _disc : (_disc = Picture("CityXColourMarkerDisc", DiscPixels(PictureSize)));

        private static Texture2D Picture(string name, Color32[] pixels)
        {
            var texture = new Texture2D(PictureSize, PictureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>The ring: white, a dark line round its outside so it shows on the pale corner, see-through inside and out. Edges smoothed.</summary>
        public static Color32[] RingPixels(int size)
        {
            var pixels = new Color32[size * size];
            float pixel = 2.0f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Radius(x, y, size);
                    float inside = Mathf.Clamp01((RingOuter - r) / pixel + 0.5f) * Mathf.Clamp01((r - RingInner) / pixel + 0.5f);
                    float white = Mathf.Clamp01((RingOuter - Outline - r) / pixel + 0.5f);
                    byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(25.0f, 255.0f, white));
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(255.0f * inside));
                }
            }
            return pixels;
        }

        /// <summary>The disc inside the ring: white (tinted the colour), just overlapping the ring's inner edge so no gap shows.</summary>
        public static Color32[] DiscPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = Mathf.Clamp01((RingInner + 0.02f - Radius(x, y, size)) / (2.0f / size) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255.0f * alpha));
                }
            }
            return pixels;
        }

        /// <summary>How far a pixel's middle is from the picture's, as a share of its half-width.</summary>
        private static float Radius(int x, int y, int size)
        {
            float half = size * 0.5f;
            return new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
        }
    }
}
