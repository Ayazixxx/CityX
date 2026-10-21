using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// Streaks of air rushing past the camera while flying fast -- the speed
    /// cue every fast game leans on. Streaks appear in a ring around the
    /// line of sight and fly back past the camera, more of them the faster
    /// the player goes (see <see cref="FlightFeel.StreakRate"/>).
    ///
    /// SOFT, LONG AND FAINT (owner, 2026-10-15: other games' lines look "way better when they are mostly invisible instead of just being
    /// white lines"): a streak is a wisp, not a line -- a long stretched sprite with a Gaussian cross-section and a fade in and out along
    /// its length (<see cref="MakeTexture"/>), drawn additive at a twelfth of full strength, so against the sky it all but vanishes and
    /// against a dark tower it is a breath of air going past. Before this it was a hard-edged 2 cm quad at a quarter strength: a white hair.
    ///
    /// Drawn by script-emitted particles, the same way the rain is (nothing is
    /// emitted by rate or by shape; every streak is placed here), with its own
    /// material and texture made at run time so no asset has to be built or wired.
    /// Attached to the camera by the jetpack when it is equipped and removed
    /// when it comes off.
    /// </summary>
    public sealed class JetpackSpeedLines : MonoBehaviour
    {
        /// <summary>How far ahead of the camera streaks appear, metres.</summary>
        public const float MinAhead = 7.0f;
        public const float MaxAhead = 16.0f;

        /// <summary>
        /// How far from the line of sight, metres: a ring, so the middle of the screen stays clear. Pushed out to the screen's edges and the streaks
        /// thinner, shorter and fainter (owner, 2026-10-15: "make the lines ... a little more subtle"; other games keep them to the edges).
        /// </summary>
        public const float MinRing = 4.5f;
        public const float MaxRing = 10.0f;

        public const float StreakSeconds = 0.55f;

        /// <summary>
        /// A streak sprite's width, metres (0.02 before the soft wisp of 2026-10-15). Only the middle of it shows: the cross-section is a Gaussian of
        /// <see cref="CoreShare"/> of the width, so the visible core is about <see cref="VisibleWidth"/>, with no edge anywhere.
        /// </summary>
        public const float StreakWidth = 0.14f;

        /// <summary>The Gaussian's sigma across the sprite, as a share of its width.</summary>
        public const float CoreShare = 0.22f;

        /// <summary>About how wide a streak reads on screen: the part of the cross-section over a tenth of its peak.</summary>
        public const float VisibleWidth = StreakWidth * CoreShare * 3.0f;

        /// <summary>
        /// The most a streak shows, at the outer edge of the ring (0.25 before 2026-10-15, when the alpha did nothing: the material drew opaque);
        /// nearer the middle of the screen it is fainter. Rendered at 0.09 a wisp was all but gone even against the dark; 0.12 reads in motion.
        /// </summary>
        public const float MaxAlpha = 0.12f;

        /// <summary>A streak's alpha at the inner edge of the ring, as a share of <see cref="MaxAlpha"/>.</summary>
        public const float InnerAlphaShare = 0.25f;

        /// <summary>How much of <see cref="FlightFeel.StreakRate"/> is emitted: fewer, longer streaks read as air, many short ones as a swarm.</summary>
        public const float RateShare = 0.7f;

        /// <summary>A streak's length per metre a second of speed: a 60 m/s dash draws a 4.8 m wisp (0.04 before 2026-10-15).</summary>
        public const float LengthPerSpeed = 0.08f;

        public static readonly Color StreakColour = new Color(0.80f, 0.88f, 1.0f);

        /// <summary>A streak's alpha <paramref name="ring"/> metres from the line of sight: fainter toward the middle of the screen.</summary>
        public static float AlphaAt(float ring)
        {
            return MaxAlpha * Mathf.Lerp(InnerAlphaShare, 1.0f, Mathf.InverseLerp(MinRing, MaxRing, ring));
        }

        /// <summary>The sprite's alpha at (<paramref name="u"/> along the streak, <paramref name="v"/> across it), both 0..1: a Gaussian across, a fade in and out along.</summary>
        public static float SpriteAlpha(float u, float v)
        {
            float across = (v - 0.5f) / CoreShare;
            float along = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(u / 0.25f)) * Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((1.0f - u) / 0.25f));
            return Mathf.Exp(-across * across) * along;
        }

        private ParticleSystem _particles;
        private Material _material;
        private Texture2D _texture;
        private float _fraction;
        private Vector3 _velocity;
        private float _debt;

        public static JetpackSpeedLines Attach(Transform camera)
        {
            var host = new GameObject("JetpackSpeedLines");
            host.transform.SetParent(camera, false);
            return host.AddComponent<JetpackSpeedLines>();
        }

        /// <summary>Streaks alive right now, for tests.</summary>
        public int LiveCount => _particles != null ? _particles.particleCount : 0;

        private void Awake()
        {
            _particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 160;
            main.startLifetime = StreakSeconds;
            main.startSpeed = 0.0f;
            main.startSize = StreakWidth;
            main.startColor = new Color(StreakColour.r, StreakColour.g, StreakColour.b, MaxAlpha);
            main.gravityModifier = 0.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            // Fade in and out so a streak never pops.
            ParticleSystem.ColorOverLifetimeModule colour = _particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = fade;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.0f;
            renderer.velocityScale = LengthPerSpeed;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _texture = MakeTexture();
            _material = MakeMaterial(_texture);
            renderer.sharedMaterial = _material;
        }

        /// <summary>The wisp: 128 texels along the streak (a stretched particle runs its texture's u along its velocity), 32 across.</summary>
        public static Texture2D MakeTexture()
        {
            const int along = 128, across = 32;
            var texture = new Texture2D(along, across, TextureFormat.RGBA32, false) { name = "SpeedLineWisp", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[along * across];
            for (int y = 0; y < across; y++)
            {
                for (int x = 0; x < along; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(255.0f * SpriteAlpha((x + 0.5f) / along, (y + 0.5f) / across));
                    pixels[y * along + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Material MakeMaterial(Texture2D wisp)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            var material = new Material(shader) { name = "SpeedLineWisp", hideFlags = HideFlags.DontSave };
            // Transparent and additive FOR REAL: _Surface and _Blend alone are what the material inspector reads, not what the shader does; without
            // the blend factors, the keyword and the queue the particles drew OPAQUE (every alpha here was ignored, which is why they were white
            // lines whatever the alpha said: found by rendering the wisp, which came out a solid bar). The same setup as SelfSmiteBolt's.
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 2.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            material.SetTexture("_BaseMap", wisp);
            material.mainTexture = wisp;
            return material;
        }

        /// <summary>Called every frame by the jetpack: how fast (0..1) and which way the player is travelling.</summary>
        public void Set(float speedFraction, Vector3 velocity)
        {
            _fraction = speedFraction;
            _velocity = velocity;
        }

        private void LateUpdate()
        {
            float rate = FlightFeel.StreakRate(_fraction) * RateShare;
            if (rate <= 0.0f || _velocity.sqrMagnitude < 0.01f)
            {
                _debt = 0.0f;
                return;
            }

            _debt += rate * Time.deltaTime;
            int count = Mathf.Min((int)_debt, 12);
            _debt -= (int)_debt;

            Transform view = transform.parent != null ? transform.parent : transform;
            Vector3 travel = _velocity.normalized;
            float speed = _velocity.magnitude;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2.0f;
                float ring = Random.Range(MinRing, MaxRing);
                Vector3 position = view.position
                    + view.forward * Random.Range(MinAhead, MaxAhead)
                    + view.right * (Mathf.Cos(angle) * ring)
                    + view.up * (Mathf.Sin(angle) * ring);
                _particles.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    // Opposite to travel, so they rush back past the flyer.
                    velocity = -travel * (speed * 0.9f),
                    // Fainter the nearer the middle of the screen.
                    startColor = new Color(StreakColour.r, StreakColour.g, StreakColour.b, AlphaAt(ring)),
                }, 1);
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }
    }
}
