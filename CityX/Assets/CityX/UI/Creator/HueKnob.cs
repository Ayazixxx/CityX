using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The hue strip's handle: a ring round a disc of the hue it is on (owner, 2026-11-17: "the hue selector shows which color you are on
    /// currently, like it puts a circle over your selected hue so you can hold click on the circle and move it around"). The Slider it sits
    /// on does the dragging. The pictures are drawn here on waking, not saved: a texture made in code is not kept by the scene.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public sealed class HueKnob : MonoBehaviour
    {
        [Tooltip("The white ring, the slider's handle.")]
        public RawImage Ring;

        [Tooltip("The disc inside it, in the hue the slider is on.")]
        public RawImage Fill;

        /// <summary>The pictures' size, pixels.</summary>
        public const int PictureSize = 48;

        /// <summary>The ring's outer edge, its inner edge, and the dark line round its outside, as shares of the picture's half-width.</summary>
        public const float RingOuter = 0.92f, RingInner = 0.66f, Outline = 0.08f;

        private static Texture2D _ring;
        private static Texture2D _disc;

        private Slider _slider;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
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
            if (Fill != null && _slider != null)
            {
                Fill.color = Color.HSVToRGB(_slider.value, 1.0f, 1.0f);
            }
        }

        private static Texture2D RingPicture => _ring != null ? _ring : (_ring = Picture("CityXHueKnobRing", RingPixels(PictureSize)));

        private static Texture2D DiscPicture => _disc != null ? _disc : (_disc = Picture("CityXHueKnobDisc", DiscPixels(PictureSize)));

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

        /// <summary>The ring: white, a dark line round its outside so it shows on the pale hues, see-through inside and out. Edges smoothed.</summary>
        public static Color32[] RingPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Radius(x, y, size);
                    float pixel = 2.0f / size;
                    float inside = Mathf.Clamp01((RingOuter - r) / pixel + 0.5f) * Mathf.Clamp01((r - RingInner) / pixel + 0.5f);
                    float white = Mathf.Clamp01((RingOuter - Outline - r) / pixel + 0.5f) * Mathf.Clamp01((r - RingInner - Outline) / pixel + 0.5f);
                    byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(30.0f, 255.0f, white));
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(255.0f * inside));
                }
            }
            return pixels;
        }

        /// <summary>The disc inside the ring: white (tinted the hue), just overlapping the ring's inner edge so no gap shows.</summary>
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
