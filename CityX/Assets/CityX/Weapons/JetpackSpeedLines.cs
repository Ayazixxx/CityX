using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// Streaks of air rushing past the camera while flying fast -- the speed
    /// cue every fast game leans on. Thin streaks appear in a ring around the
    /// line of sight and fly back past the camera, more of them the faster
    /// the player goes (see <see cref="FlightFeel.StreakRate"/>).
    ///
    /// Drawn by script-emitted particles, the same way the rain is (nothing is
    /// emitted by rate or by shape; every streak is placed here), with its own
    /// material made at run time so no asset has to be built or wired.
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

        public const float StreakSeconds = 0.4f;

        /// <summary>A streak's width, metres (0.03 before 2026-10-15).</summary>
        public const float StreakWidth = 0.02f;

        /// <summary>The most a streak shows, at the outer edge of the ring (0.5 before 2026-10-15); nearer the middle of the screen it is fainter.</summary>
        public const float MaxAlpha = 0.25f;

        /// <summary>A streak's alpha at the inner edge of the ring, as a share of <see cref="MaxAlpha"/>.</summary>
        public const float InnerAlphaShare = 0.35f;

        public static readonly Color StreakColour = new Color(0.85f, 0.92f, 1.0f);

        /// <summary>A streak's alpha <paramref name="ring"/> metres from the line of sight: fainter toward the middle of the screen.</summary>
        public static float AlphaAt(float ring)
        {
            return MaxAlpha * Mathf.Lerp(InnerAlphaShare, 1.0f, Mathf.InverseLerp(MinRing, MaxRing, ring));
        }

        private ParticleSystem _particles;
        private Material _material;
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
            renderer.velocityScale = 0.04f;   // shorter streaks (0.05 before 2026-10-15)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _material = MakeMaterial();
            renderer.sharedMaterial = _material;
        }

        private static Material MakeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            var material = new Material(shader);
            material.SetFloat("_Surface", 1.0f);   // transparent
            material.SetFloat("_Blend", 2.0f);     // additive
            material.SetColor("_BaseColor", Color.white);
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
            float rate = FlightFeel.StreakRate(_fraction);
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
        }
    }
}
