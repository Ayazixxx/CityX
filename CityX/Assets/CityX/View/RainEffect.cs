using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Rain, ported from player/rain_effect.gd. Purely local -- visual and
    /// audible, nothing else. Nothing in the world gets wet and nothing else
    /// can tell it is raining.
    ///
    /// IT HANGS OFF THE PLAYER, NOT THE CAMERA. Godot's own reasoning, and it
    /// is the whole trick: the emission box follows where the player IS, so
    /// there is always rain around them, but camera bob and aiming never drag
    /// it about. Individual drops are emitted into WORLD space and left there,
    /// so they fall straight down regardless of the player moving underneath
    /// them -- which is what stops the rain sliding sideways when you run.
    /// </summary>
    public sealed class RainEffect : MonoBehaviour
    {
        public const int DropCount = 500;

        /// <summary>Forty metres square around the player. Past that it is not visible anyway.</summary>
        public static readonly Vector3 EmissionBox = new Vector3(40.0f, 2.0f, 40.0f);

        public const float EmissionHeight = 18.0f;

        /// <summary>
        /// Constant, not accelerating. Rain reaches terminal velocity long
        /// before it gets anywhere near a city, so a drop that speeds up as it
        /// falls reads as something that was dropped rather than as weather.
        /// </summary>
        public const float FallSpeed = 22.0f;

        public const float DropHeight = 0.9f;
        public const float DropWidth = 0.03f;

        /// <summary>Pale blue-white. The same drop colour the title screen's rain uses.</summary>
        public static readonly Color DropColour =
            new Color(200.0f / 255.0f, 210.0f / 255.0f, 255.0f / 255.0f, 0.55f);

        [Tooltip("rain_noises.mp3, looping while it rains.")]
        public AudioClip RainSound;

        [Tooltip("The unlit transparent material each drop is drawn with.")]
        public Material DropMaterial;

        private ParticleSystem _particles;
        private AudioSource _sound;

        public bool IsActive => _particles != null && _particles.isEmitting;

        private void Awake()
        {
            Build();
            SetActive(false);
        }

        private void Build()
        {
            var host = new GameObject("Drops");
            host.transform.SetParent(transform, false);
            host.transform.localPosition = new Vector3(0.0f, EmissionHeight, 0.0f);

            _particles = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = DropCount;
            main.startLifetime = EmissionHeight / FallSpeed;
            main.startSpeed = new ParticleSystem.MinMaxCurve(FallSpeed, FallSpeed * 1.1f);
            main.startSize = DropHeight;
            main.startColor = DropColour;
            main.gravityModifier = 0.0f;
            // WORLD, so a drop stays where it was emitted and falls straight
            // down while the player runs out from under it.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _particles.emission;
            // The rate that keeps DropCount alive at once: each drop lives
            // exactly as long as it takes to fall EmissionHeight, so the count
            // and the rate cannot drift apart when either is retuned.
            emission.rateOverTime = DropCount / main.startLifetime.constant;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = EmissionBox;
            shape.rotation = new Vector3(90.0f, 0.0f, 0.0f);   // pointing down

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            // Stretched along its own travel rather than billboarded flat.
            // Godot achieves the same with a fixed-Y billboard: a drop has to
            // read as a falling streak from any horizontal angle, and a plain
            // camera-facing quad goes edge-on and vanishes from some of them.
            renderer.lengthScale = DropHeight / Mathf.Max(DropWidth, 0.0001f);
            renderer.velocityScale = 0.0f;
            renderer.sharedMaterial = DropMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _sound = gameObject.AddComponent<AudioSource>();
            _sound.clip = RainSound;
            _sound.loop = true;
            _sound.playOnAwake = false;
            _sound.spatialBlend = 0.0f;
        }

        public void SetActive(bool active)
        {
            if (_particles == null)
            {
                return;
            }

            if (active)
            {
                // STARTED ALREADY MID-STORM. Turning rain on should not look
                // like an empty box visibly filling with drops over the next
                // second -- so the system is run forward by one drop's whole
                // fall before the first frame anybody sees.
                _particles.Simulate(_particles.main.startLifetime.constant, true, true);
                _particles.Play();
                if (_sound != null && _sound.clip != null && !_sound.isPlaying)
                {
                    _sound.Play();
                }
            }
            else
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_sound != null)
                {
                    _sound.Stop();
                }
            }
        }
    }
}
