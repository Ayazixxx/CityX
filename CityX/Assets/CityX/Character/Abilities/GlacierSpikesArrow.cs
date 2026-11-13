using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// Glacier Spikes' aim on the ground while it is armed (docs/GLACIER_SPIKES.md), a skillshot arrow as in League of Legends: the band the
    /// spikes will fill, from just ahead of the caster's feet to where the line ends (its full reach, or a wall), with a pointed tip past the
    /// end, laid over kerbs and stairs. Only the caster sees it.
    /// </summary>
    public sealed class GlacierSpikesArrow : MonoBehaviour
    {
        /// <summary>How far the tip runs past the line's end, and how far above the ground the arrow lies, metres.</summary>
        public const float TipLength = 1.8f;

        public const float Lift = 0.06f;

        /// <summary>How fast the arrow breathes, a second.</summary>
        public const float PulseRate = 3.0f;

        public static readonly Color Edge = new Color(0.45f, 0.85f, 1.0f, 0.8f);
        public static readonly Color Middle = new Color(0.35f, 0.75f, 1.0f, 0.14f);

        private static Material _material;

        private GlacierGroundStrip _strip;
        private MeshRenderer _renderer;
        private float _time;

        public bool IsShowing => _renderer != null && _renderer.enabled;

        /// <summary>How long the arrow it shows is, line and tip, metres.</summary>
        public float ShownLength { get; private set; }

        public static GlacierSpikesArrow Make()
        {
            var go = new GameObject("GlacierSpikesArrow");
            var arrow = go.AddComponent<GlacierSpikesArrow>();
            arrow._strip = new GlacierGroundStrip("GlacierAim");
            go.AddComponent<MeshFilter>().sharedMesh = arrow._strip.Mesh;
            arrow._renderer = go.AddComponent<MeshRenderer>();
            arrow._renderer.sharedMaterial = ArrowMaterial();
            arrow._renderer.shadowCastingMode = ShadowCastingMode.Off;
            arrow._renderer.receiveShadows = false;
            arrow._renderer.lightProbeUsage = LightProbeUsage.Off;
            arrow._renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            arrow._renderer.enabled = false;
            return arrow;
        }

        /// <summary>Shows the arrow along <paramref name="path"/>, laid on the ground again when <paramref name="relay"/> (the line moved).</summary>
        public void Show(GlacierSpikesPath path, IGlacierGround ground, bool relay, float deltaTime)
        {
            if (!path.IsCastable)
            {
                Hide();
                return;
            }
            if (relay || !_renderer.enabled)
            {
                _strip.Lay(path, ground, Lift, TipLength, true);
                ShownLength = path.Length + TipLength;
            }
            _time += deltaTime;
            float pulse = 0.85f + 0.15f * Mathf.Sin(_time * PulseRate * Mathf.PI * 2.0f);
            Color middle = Middle;
            middle.a *= pulse;
            _strip.Paint(Edge, middle, float.MaxValue, 0.0f, 0.0f, 1.0f);
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private static Material ArrowMaterial() => _material != null ? _material : (_material = SoftGlow.Make("GlacierAim", Color.white, false));
    }
}
