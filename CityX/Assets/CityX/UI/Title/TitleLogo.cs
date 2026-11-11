using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The title's 3D logo (SourceArt/TitleLogo, from the owner's drawing, 2026-11-11): held in the top left corner of the title camera's view
    /// whatever the screen's shape, and its magenta strokes lit as a failing neon sign (<see cref="NeonFlicker"/>), the light they throw on
    /// the letters flickering with them.
    ///
    /// The model sits in this object with its front to local -Z, x right and y up, and <see cref="Bounds"/> is its extent there.
    /// </summary>
    public sealed class TitleLogo : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("The camera whose top left corner it is held in.")]
        public Camera View;

        [Tooltip("The model's extent in this object's own space.")]
        public Bounds Bounds;

        [Tooltip("Where the logo's top left corner is held, in the view (0,0 bottom left, 1,1 top right).")]
        public Vector2 TopLeft = new Vector2(0.03f, 0.96f);

        [Tooltip("How tall the logo is, as a share of the view's height.")]
        public float HeightShare = 0.40f;

        [Tooltip("How far in front of the camera it hangs, metres.")]
        public float Distance = 6.0f;

        [Tooltip("Turned this far about its own upright, degrees, so it reads as a solid thing and not a sticker: its right side away.")]
        public float Yaw = 14.0f;

        [Tooltip("The neon tubes.")]
        public Renderer Neon;

        [Tooltip("The neon's colour fully lit; HDR, so it blooms.")]
        [ColorUsage(false, true)]
        public Color NeonColour = new Color(4.0f, 0.15f, 4.2f);

        [Tooltip("The light the neon throws on the letters, flickering with it.")]
        public Light Glow;

        [Tooltip("The glow's intensity, fully lit.")]
        public float GlowIntensity = 3.0f;

        [Tooltip("Which sign it is: the same seed flickers the same way.")]
        public int Seed = 3;

        private NeonFlicker _flicker;
        private MaterialPropertyBlock _block;

        /// <summary>The neon's brightness last shown, 0 to 1.</summary>
        public float Brightness { get; private set; } = 1.0f;

        public bool Shown => gameObject.activeSelf;

        public void SetShown(bool shown) => gameObject.SetActive(shown);

        /// <summary>
        /// Where a model of extent <paramref name="bounds"/>, turned <paramref name="yaw"/> degrees about its upright, goes in the camera's own
        /// space (x right, y up, z ahead), and how much it is scaled, to have its top left front corner at <paramref name="topLeft"/> in the
        /// view, <paramref name="distance"/> ahead, and be <paramref name="heightShare"/> of the view tall.
        /// </summary>
        public static void Place(float fieldOfView, float aspect, float distance, Vector2 topLeft, float heightShare, float yaw, Bounds bounds,
            out Vector3 position, out float scale)
        {
            float halfHeight = distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * aspect;
            scale = heightShare * 2.0f * halfHeight / Mathf.Max(1e-4f, bounds.size.y);
            var corner = new Vector3((2.0f * topLeft.x - 1.0f) * halfWidth, (2.0f * topLeft.y - 1.0f) * halfHeight, distance);
            Vector3 modelCorner = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z) * scale;
            position = corner - Quaternion.Euler(0.0f, yaw, 0.0f) * modelCorner;
        }

        private void LateUpdate()
        {
            Hold();
            Flicker(Time.time);
        }

        /// <summary>Puts the logo in its corner of the view, now.</summary>
        public void Hold()
        {
            if (View == null)
            {
                return;
            }
            Place(View.fieldOfView, View.aspect, Distance, TopLeft, HeightShare, Yaw, Bounds, out Vector3 position, out float scale);
            Transform eye = View.transform;
            transform.SetPositionAndRotation(eye.TransformPoint(position), eye.rotation * Quaternion.Euler(0.0f, Yaw, 0.0f));
            transform.localScale = Vector3.one * scale;
        }

        /// <summary>The neon and its light as they are at <paramref name="time"/> seconds.</summary>
        public void Flicker(float time)
        {
            _flicker ??= new NeonFlicker(Seed);
            Brightness = _flicker.Brightness(time);
            if (Neon != null)
            {
                _block ??= new MaterialPropertyBlock();
                Neon.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, NeonColour * Brightness);
                Neon.SetPropertyBlock(_block);
            }
            if (Glow != null)
            {
                Glow.intensity = GlowIntensity * Brightness;
            }
        }
    }
}
