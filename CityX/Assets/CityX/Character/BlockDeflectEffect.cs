using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT A DEFLECTED BLOW LOOKS AND SOUNDS LIKE (2026-10-14, the Block Chance stat: a block that stops a blow outright, a bullet glancing
    /// off): a burst of sparks off the guard toward whoever struck, and a bright metallic ting. On the body that blocked (PlayerVitals.Deflected),
    /// and on every other player's copy of it: the owner announces it by name like a gun shot (PlayerAnimationDriver.AnnounceState), and a copy
    /// plays it in front of the body (PlayerAnimationDriver.RemoteDeflected).
    ///
    /// Added at runtime by PlayerInputReader.Awake, like PlayerWetness (every body has one: a copy's reader is switched off only after it woke).
    /// Drawn and voiced in code: a few line streaks and a sound made of decaying partials, no assets.
    /// </summary>
    public sealed class BlockDeflectEffect : MonoBehaviour
    {
        /// <summary>The name a deflection travels to other players under.</summary>
        public const string StateName = "block_deflect";

        public const int SparkCount = 9;
        public const float SparkSeconds = 0.2f;
        public const float SparkSpeed = 6.0f;
        public const float SparkLength = 0.16f;
        public const float SparkWidth = 0.012f;

        /// <summary>How far in front of the chest the guard meets the blow, in metres.</summary>
        public const float GuardReach = 0.55f;

        public const float Volume = 0.55f;

        private static readonly Color SparkColour = new Color(1.0f, 0.86f, 0.45f);

        private static Material _material;
        private static AudioClip _ting;

        private PlayerVitals _vitals;
        private PlayerAnimationDriver _driver;
        private PlayerLocomotion _locomotion;

        /// <summary>How many deflections this body has shown (here or from another player): what a test reads.</summary>
        public int Shown { get; private set; }

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _driver = GetComponent<PlayerAnimationDriver>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnEnable()
        {
            if (_vitals != null) _vitals.Deflected += OnDeflected;
            if (_driver != null) _driver.RemoteDeflected += OnRemoteDeflected;
        }

        private void OnDisable()
        {
            if (_vitals != null) _vitals.Deflected -= OnDeflected;
            if (_driver != null) _driver.RemoteDeflected -= OnRemoteDeflected;
        }

        private void OnDeflected(Vector3 from, DamageKind kind)
        {
            Vector3 chest = Chest();
            Vector3 toward = from - chest;
            toward.y = 0.0f;
            Vector3 direction = toward.sqrMagnitude > 1e-4f ? toward.normalized : transform.forward;
            Show(chest + direction * GuardReach, direction);
            _driver?.AnnounceState(StateName);
        }

        private void OnRemoteDeflected()
        {
            Show(Chest() + transform.forward * GuardReach, transform.forward);
        }

        private Vector3 Chest()
        {
            float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
            return transform.position + Vector3.up * (height * 0.62f);
        }

        /// <summary>Sparks fanning out of <paramref name="point"/> toward <paramref name="direction"/>, and the ting.</summary>
        public void Show(Vector3 point, Vector3 direction)
        {
            Shown++;
            if (!Application.isPlaying)
            {
                return;
            }
            var burst = new GameObject("DeflectSparks");
            burst.transform.position = point;
            burst.AddComponent<SparkBurst>().Begin(point, direction);
            AudioSource.PlayClipAtPoint(Ting(), point, Volume);
        }

        /// <summary>A short, bright metallic ting: a click and four inharmonic partials ringing down fast, as struck steel does.</summary>
        public static AudioClip Ting()
        {
            if (_ting != null)
            {
                return _ting;
            }
            const int rate = 44100;
            const float seconds = 0.35f;
            int count = Mathf.RoundToInt(rate * seconds);
            var samples = new float[count];
            float[] partials = { 2350.0f, 3720.0f, 5310.0f, 7100.0f };
            float[] loudness = { 0.5f, 0.32f, 0.2f, 0.1f };
            float[] decay = { 11.0f, 15.0f, 21.0f, 30.0f };
            var noise = new System.Random(3);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float value = 0.0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    value += loudness[p] * Mathf.Exp(-decay[p] * t) * Mathf.Sin(2.0f * Mathf.PI * partials[p] * t);
                }
                // The strike itself: a few milliseconds of noise.
                value += (float)(noise.NextDouble() * 2.0 - 1.0) * 0.6f * Mathf.Exp(-t * 900.0f);
                samples[i] = Mathf.Clamp(value, -1.0f, 1.0f) * 0.8f;
            }
            _ting = AudioClip.Create("DeflectTing", count, 1, rate, false);
            _ting.SetData(samples, 0);
            return _ting;
        }

        internal static Material SparkMaterial()
        {
            if (_material != null)
            {
                return _material;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            _material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { color = Color.white };
            if (_material.HasProperty("_Surface"))
            {
                _material.SetFloat("_Surface", 1.0f);
                _material.SetFloat("_Blend", 2.0f);   // additive, as the bullet tracers are
            }
            return _material;
        }

        /// <summary>The sparks themselves: short streaks flying out and fading, then gone.</summary>
        private sealed class SparkBurst : MonoBehaviour
        {
            private LineRenderer[] _streaks;
            private Vector3[] _velocities;
            private Vector3[] _heads;
            private float _elapsed;

            public void Begin(Vector3 point, Vector3 direction)
            {
                var random = new System.Random(unchecked((int)(Time.frameCount * 7919 + point.GetHashCode())));
                _streaks = new LineRenderer[SparkCount];
                _velocities = new Vector3[SparkCount];
                _heads = new Vector3[SparkCount];
                Vector3 forward = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
                for (int i = 0; i < SparkCount; i++)
                {
                    // Out over the half facing the blow, a little upward.
                    Vector3 spread = new Vector3((float)random.NextDouble() * 2.0f - 1.0f, (float)random.NextDouble() * 1.4f - 0.4f,
                        (float)random.NextDouble() * 2.0f - 1.0f);
                    Vector3 velocity = (forward + spread * 0.9f).normalized * SparkSpeed * (0.6f + (float)random.NextDouble() * 0.6f);
                    _velocities[i] = velocity;
                    _heads[i] = point;
                    var streak = new GameObject("Spark").AddComponent<LineRenderer>();
                    streak.transform.SetParent(transform, false);
                    streak.material = SparkMaterial();
                    streak.useWorldSpace = true;
                    streak.positionCount = 2;
                    streak.numCapVertices = 0;
                    streak.startWidth = SparkWidth;
                    streak.endWidth = SparkWidth * 0.3f;
                    streak.startColor = SparkColour;
                    streak.endColor = new Color(SparkColour.r, SparkColour.g * 0.6f, 0.1f, 0.0f);
                    streak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    streak.receiveShadows = false;
                    streak.SetPosition(0, point);
                    streak.SetPosition(1, point);
                    _streaks[i] = streak;
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float life = Mathf.Clamp01(1.0f - _elapsed / SparkSeconds);
                for (int i = 0; i < _streaks.Length; i++)
                {
                    _velocities[i] += Physics.gravity * Time.deltaTime * 0.5f;
                    _heads[i] += _velocities[i] * Time.deltaTime;
                    Vector3 tail = _heads[i] - _velocities[i].normalized * SparkLength * life;
                    _streaks[i].SetPosition(0, _heads[i]);
                    _streaks[i].SetPosition(1, tail);
                    _streaks[i].widthMultiplier = life;
                }
                if (_elapsed >= SparkSeconds)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
