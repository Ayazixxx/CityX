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

        /// <summary>The name a sword guard meeting a blade travels under: the same sparks, a recorded clash instead of the ting.</summary>
        public const string ClashStateName = "guard_clash";

        /// <summary>The owner's sword-on-sword recordings (2026-10-17), in Resources: one is picked at random for each clash.</summary>
        public static readonly string[] ClashClipNames = { "SwordClash/SwordClash1", "SwordClash/SwordClash2", "SwordClash/SwordClash3" };

        public const float ClashVolume = 0.8f;

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
        private static AudioClip[] _clashes;

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
            if (_vitals != null) _vitals.GuardStruck += OnGuardStruck;
            if (_driver != null) _driver.RemoteDeflected += OnRemoteDeflected;
        }

        private void OnDisable()
        {
            if (_vitals != null) _vitals.Deflected -= OnDeflected;
            if (_vitals != null) _vitals.GuardStruck -= OnGuardStruck;
            if (_driver != null) _driver.RemoteDeflected -= OnRemoteDeflected;
        }

        /// <summary>A sword guard met a blow (a parry or a soak, owner 2026-10-17's guard rebound): sparks and one of the recorded clashes.</summary>
        private void OnGuardStruck(Vector3 from)
        {
            Vector3 direction = Toward(from);
            Show(Chest() + direction * GuardReach, direction, true);
            _driver?.AnnounceState(ClashStateName);
        }

        private void OnDeflected(Vector3 from, DamageKind kind)
        {
            Vector3 direction = Toward(from);
            Show(Chest() + direction * GuardReach, direction);
            _driver?.AnnounceState(StateName);
        }

        private void OnRemoteDeflected(bool clash)
        {
            Show(Chest() + transform.forward * GuardReach, transform.forward, clash);
        }

        private Vector3 Toward(Vector3 from)
        {
            Vector3 toward = from - Chest();
            toward.y = 0.0f;
            return toward.sqrMagnitude > 1e-4f ? toward.normalized : transform.forward;
        }

        private Vector3 Chest()
        {
            float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
            return transform.position + Vector3.up * (height * 0.62f);
        }

        /// <summary>How many sword-on-sword clashes this body has shown: what a test reads.</summary>
        public int ClashesShown { get; private set; }

        /// <summary>The clip the last clash played (one of the recordings; null if they are missing and the ting played).</summary>
        public AudioClip LastClashClip { get; private set; }

        /// <summary>
        /// Sparks fanning out of <paramref name="point"/> toward <paramref name="direction"/>, and the sound: a recorded sword clash picked
        /// at random when a blade met the guard (<paramref name="clash"/>), the ting otherwise (a bullet glancing off, a Block Chance stop).
        /// </summary>
        public void Show(Vector3 point, Vector3 direction, bool clash = false)
        {
            Shown++;
            AudioClip clip = null;
            if (clash)
            {
                ClashesShown++;
                clip = LastClashClip = RandomClash();
            }
            if (!Application.isPlaying)
            {
                return;
            }
            var burst = new GameObject("DeflectSparks");
            burst.transform.position = point;
            burst.AddComponent<SparkBurst>().Begin(point, direction);
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, point, ClashVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(Ting(), point, Volume);
            }
        }

        /// <summary>The owner's clash recordings, loaded once.</summary>
        public static AudioClip[] Clashes()
        {
            if (_clashes == null)
            {
                var found = new System.Collections.Generic.List<AudioClip>();
                foreach (string name in ClashClipNames)
                {
                    AudioClip clip = Resources.Load<AudioClip>(name);
                    if (clip != null) found.Add(clip);
                }
                _clashes = found.ToArray();
            }
            return _clashes;
        }

        /// <summary>One of the recordings, each equally likely; null if none loaded.</summary>
        public static AudioClip RandomClash()
        {
            AudioClip[] clashes = Clashes();
            return clashes.Length > 0 ? clashes[Random.Range(0, clashes.Length)] : null;
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
                SeeThroughMaterial.Apply(_material, additive: true);   // additive, as the bullet tracers are
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
