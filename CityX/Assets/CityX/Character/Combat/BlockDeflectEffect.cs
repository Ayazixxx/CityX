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
    ///
    /// A BLADE MEETING A GUARD IS THE BIG ONE (owner, 2026-11-04: "it should feel impactful hitting an opponent who is blocking and when you block an attack the
    /// opponents weapon should bounce off with a loud noise"): several things on the same frame, as a hit that lands has (HitImpactEffects). Both fighters held still for
    /// an instant, a loud clash with a thunk under it, a flash and a big burst of sparks where the blades meet, and the camera jolted. Then the blocker's guard is knocked
    /// back and the body slides (SwordStrikes.GuardJolted), and the attacker's blade is thrown back (SwordPoseDriver.BeginRebound). A parry stands firm: only the attacker moves.
    /// </summary>
    public sealed class BlockDeflectEffect : MonoBehaviour
    {
        /// <summary>The name a deflection travels to other players under.</summary>
        public const string StateName = "block_deflect";

        /// <summary>The name a sword guard meeting a blade travels under: the same sparks, a recorded clash instead of the ting.</summary>
        public const string ClashStateName = "guard_clash";

        /// <summary>The owner's sword-on-sword recordings (2026-10-17), in Resources: one is picked at random for each clash.</summary>
        public static readonly string[] ClashClipNames = { "SwordClash/SwordClash1", "SwordClash/SwordClash2", "SwordClash/SwordClash3" };

        public const float ClashVolume = 1.0f;

        /// <summary>The thunk laid under the recording: the weight of the blow.</summary>
        public const float ClashBodyVolume = 0.85f;

        /// <summary>
        /// A clash is at full volume this near, metres, and only fades beyond it. The game's camera is 5 m behind the player and hears for the player, so with the
        /// usual one-shot's 1 m a clash under the player's own nose came through at a fifth of its volume.
        /// </summary>
        public const float ClashFullVolumeMetres = 12.0f;

        /// <summary>The name of a clash's sound objects.</summary>
        public const string ClashSoundName = "GuardClashSound";

        /// <summary>How long a blade meeting a guard holds both fighters still, seconds. Past about 0.12 s a freeze reads as a dropped frame.</summary>
        public const float ClashFreezeSeconds = 0.1f;

        /// <summary>How far a blow slides the body holding the guard, metres, a heavy one further. Kept small: the blocker must stay in reach to punish the opening.</summary>
        public const float GuardShoveMetres = 0.15f;

        public const float HeavyGuardShoveMetres = 0.3f;

        public const float GuardShoveSeconds = 0.16f;

        public const float GuardShakeMetres = 0.08f;

        public const float GuardShakeSeconds = 0.2f;

        public const int SparkCount = 9;
        public const float SparkSeconds = 0.2f;
        public const float SparkSpeed = 6.0f;
        public const float SparkLength = 0.16f;
        public const float SparkWidth = 0.012f;

        // A blade driven into the ground (the one-handed heavy; owner, 2026-11-13: "hits the ground and has like a sparks effect from the
        // ground"): more of them than a clash, thrown up and out of the ground, and they hang in the air longer.
        public const int GroundSparkCount = 44;
        public const float GroundSparkSeconds = 0.45f;
        public const float GroundSparkSpeed = 9.0f;
        public const float GroundSparkLength = 0.42f;
        public const float GroundSparkWidth = 0.03f;

        /// <summary>How many blades driven into the ground have thrown their sparks since the game started, and where the last one struck (for tests).</summary>
        public static int GroundStrikesShown { get; private set; }

        public static Vector3 LastGroundStrike { get; private set; }

        /// <summary>
        /// Sparks thrown up out of the ground at <paramref name="point"/>, a flash and the ring of the blade: a sword driven into the
        /// ground. Called on every body that plays the blow (SwordPoseDriver), so every player sees and hears it with nothing sent.
        /// </summary>
        public static void GroundStrike(Vector3 point)
        {
            GroundStrikesShown++;
            LastGroundStrike = point;
            if (!Application.isPlaying)
            {
                return;
            }
            var burst = new GameObject("GroundStrikeSparks");
            burst.transform.position = point;
            burst.AddComponent<SparkBurst>().Begin(point, Vector3.up, GroundSparkCount, GroundSparkSpeed, GroundSparkWidth, GroundSparkSeconds, GroundSparkLength);
            AttackGlint.Flash(point);
            PlayClashSound(ClashBody(), point, ClashBodyVolume);
        }

        public const int ClashSparkCount = 26;
        public const float ClashSparkSeconds = 0.3f;
        public const float ClashSparkSpeed = 10.0f;
        public const float ClashSparkLength = 0.34f;
        public const float ClashSparkWidth = 0.026f;

        /// <summary>How far in front of the chest the guard meets the blow, in metres.</summary>
        public const float GuardReach = 0.55f;

        /// <summary>Where along the guard's blade the blow meets it, hilt to tip.</summary>
        public const float BladeMeetShare = 0.55f;

        public const float Volume = 0.55f;

        private static readonly Color SparkColour = new Color(1.0f, 0.86f, 0.45f);

        private static Material _material;
        private static AudioClip _ting;
        private static AudioClip[] _clashes;
        private static AudioClip _clashBody;

        private PlayerVitals _vitals;
        private PlayerAnimationDriver _driver;
        private PlayerLocomotion _locomotion;
        private bool _parried;

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
            if (_vitals != null) _vitals.Parried += OnParried;
            if (_vitals != null) _vitals.GuardBroken += OnGuardBroken;
            if (_driver != null) _driver.RemoteDeflected += OnRemoteDeflected;
            if (_driver != null) _driver.RemoteGuardDeflected += OnRemoteGuardDeflected;
        }

        private void OnDisable()
        {
            if (_vitals != null) _vitals.Deflected -= OnDeflected;
            if (_vitals != null) _vitals.GuardStruck -= OnGuardStruck;
            if (_vitals != null) _vitals.Parried -= OnParried;
            if (_vitals != null) _vitals.GuardBroken -= OnGuardBroken;
            if (_driver != null) _driver.RemoteDeflected -= OnRemoteDeflected;
            if (_driver != null) _driver.RemoteGuardDeflected -= OnRemoteGuardDeflected;
        }

        /// <summary>The blow about to clash was caught in the guard's first instant (said just before <see cref="OnGuardStruck"/>).</summary>
        private void OnParried(Vector3 from)
        {
            _parried = true;
        }

        /// <summary>A sword guard met a blow (a parry or a soak, owner 2026-10-17's guard rebound): the clash, and what it does to this body.</summary>
        private void OnGuardStruck(Vector3 from)
        {
            Vector3 direction = Toward(from);
            Show(GuardPoint(direction), direction, true);
            _driver?.AnnounceState(ClashStateName);
            _locomotion?.ImpactFreeze(ClashFreezeSeconds);
            if (ThirdPersonCamera.Local != null && ThirdPersonCamera.Local.Target == transform)
            {
                ThirdPersonCamera.Local.AddShake(GuardShakeMetres, GuardShakeSeconds);
            }
            bool parried = _parried;
            _parried = false;
            if (!parried)
            {
                _locomotion?.Shove(-direction, DamageContext.HeavyBlow ? HeavyGuardShoveMetres : GuardShoveMetres, GuardShoveSeconds);
                _driver?.SwordPoser?.GuardJolt(ClashFreezeSeconds);
            }
        }

        /// <summary>A blow broke the guard open: it still clashes, and the wound and the stagger follow (HitImpactEffects).</summary>
        private void OnGuardBroken()
        {
            Show(GuardPoint(transform.forward), transform.forward, true);
            _driver?.AnnounceState(ClashStateName);
        }

        private void OnDeflected(Vector3 from, DamageKind kind)
        {
            _parried = false;
            if (kind == DamageKind.Physical && _locomotion != null && _locomotion.HeldSword != null)
            {
                OnGuardStruck(from);   // a blade stopped outright by Block Chance bounces like any other (the attacker is thrown back the same)
                return;
            }
            Vector3 direction = Toward(from);
            if (kind == DamageKind.Bullet && KnockTheGuard(from, direction))
            {
                return;
            }
            Show(Chest() + direction * GuardReach, direction);
            _driver?.AnnounceState(StateName);
        }

        /// <summary>
        /// A bullet turned aside by a held sword's guard (docs/BULLET_DEFLECTION.md): it glances off the blade where the blade is, and knocks
        /// the guard to a new angle (SwordStrikes.PickDeflect), which every other player's copy is told by name. False with no sword guard up
        /// (a shield, a spear, a staff, a fist's block): the bullet glances off in front of the chest as before.
        /// </summary>
        private bool KnockTheGuard(Vector3 from, Vector3 direction)
        {
            SwordPoseDriver poser = _driver != null ? _driver.SwordPoser : null;
            ISwordBlade held = _locomotion != null ? _locomotion.HeldSword : null;
            if (poser == null || !poser.IsGuarding || held == null || held.IsSpear || held.IsStaff)
            {
                return false;
            }
            int pose = SwordStrikes.PickDeflect(poser.OneHanded, poser.DeflectPose, transform.InverseTransformDirection(from - Chest()), Random.value);
            Show(GuardPoint(direction), direction);
            poser.Deflect(pose);
            _driver.AnnounceState(SwordStrikes.DeflectName(pose));
            return true;
        }

        /// <summary>The player this body is a copy of had a bullet knock their sword's guard to <paramref name="pose"/>: the same glance off the blade, the same new angle.</summary>
        private void OnRemoteGuardDeflected(int pose)
        {
            Vector3 direction = transform.forward;
            Show(GuardPoint(direction), direction);
            _driver?.SwordPoser?.Deflect(pose);
        }

        private void OnRemoteDeflected(bool clash)
        {
            Vector3 direction = transform.forward;
            if (!clash)
            {
                Show(Chest() + direction * GuardReach, direction);
                return;
            }
            Show(GuardPoint(direction), direction, true);
            _locomotion?.ImpactFreeze(ClashFreezeSeconds);
            SwordPoseDriver poser = _driver != null ? _driver.SwordPoser : null;
            if (poser != null && !poser.IsParrying)
            {
                poser.GuardJolt(ClashFreezeSeconds);
            }
        }

        /// <summary>Where a blow meets the guard: on the blade held up, or in front of the chest with none in hand.</summary>
        private Vector3 GuardPoint(Vector3 direction)
        {
            ISwordBlade held = _locomotion != null ? _locomotion.HeldSword : null;
            return held != null ? Vector3.Lerp(held.BladeBaseWorld, held.BladeTipWorld, BladeMeetShare) : Chest() + direction * GuardReach;
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
            burst.AddComponent<SparkBurst>().Begin(point, direction, clash);
            if (clash)
            {
                AttackGlint.Flash(point);
                PlayClashSound(ClashBody(), point, ClashBodyVolume);
            }
            if (clip != null)
            {
                PlayClashSound(clip, point, ClashVolume);
            }
            else if (!clash)
            {
                AudioSource.PlayClipAtPoint(Ting(), point, Volume);
            }
        }

        /// <summary>One layer of a clash: in the world, so a far-off fight is quieter, but at full volume anywhere near it (<see cref="ClashFullVolumeMetres"/>).</summary>
        private static void PlayClashSound(AudioClip clip, Vector3 point, float volume)
        {
            var sound = new GameObject(ClashSoundName);
            sound.transform.position = point;
            AudioSource source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = ClashFullVolumeMetres;
            source.dopplerLevel = 0.0f;
            source.Play();
            Destroy(sound, clip.length + 0.1f);
        }

        /// <summary>The weight under a clash: a low thump, a hard strike and a dull clank of steel, gone in a third of a second.</summary>
        public static AudioClip ClashBody()
        {
            if (_clashBody != null)
            {
                return _clashBody;
            }
            const int rate = 44100;
            const float seconds = 0.32f;
            int count = Mathf.RoundToInt(rate * seconds);
            var samples = new float[count];
            float[] partials = { 830.0f, 1290.0f, 1870.0f };
            float[] loudness = { 0.42f, 0.28f, 0.18f };
            float[] decay = { 14.0f, 18.0f, 24.0f };
            var noise = new System.Random(5);
            float phase = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float frequency = 110.0f - 55.0f * Mathf.Min(1.0f, t / 0.15f);
                phase += 2.0f * Mathf.PI * frequency / rate;
                float value = Mathf.Sin(phase) * Mathf.Exp(-t * 18.0f) * 0.9f;
                for (int p = 0; p < partials.Length; p++)
                {
                    value += loudness[p] * Mathf.Exp(-decay[p] * t) * Mathf.Sin(2.0f * Mathf.PI * partials[p] * t);
                }
                value += (float)(noise.NextDouble() * 2.0 - 1.0) * 0.8f * Mathf.Exp(-t * 300.0f);
                samples[i] = Mathf.Clamp(value, -1.0f, 1.0f) * 0.95f;
            }
            _clashBody = AudioClip.Create("GuardClashBody", count, 1, rate, false);
            _clashBody.SetData(samples, 0);
            return _clashBody;
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
            private float _seconds;
            private float _length;

            public void Begin(Vector3 point, Vector3 direction, bool clash)
            {
                if (clash)
                {
                    Begin(point, direction, ClashSparkCount, ClashSparkSpeed, ClashSparkWidth, ClashSparkSeconds, ClashSparkLength);
                }
                else
                {
                    Begin(point, direction, SparkCount, SparkSpeed, SparkWidth, SparkSeconds, SparkLength);
                }
            }

            public void Begin(Vector3 point, Vector3 direction, int count, float speed, float width, float seconds, float length)
            {
                var random = new System.Random(unchecked((int)(Time.frameCount * 7919 + point.GetHashCode())));
                _seconds = seconds;
                _length = length;
                _streaks = new LineRenderer[count];
                _velocities = new Vector3[count];
                _heads = new Vector3[count];
                Vector3 forward = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
                for (int i = 0; i < count; i++)
                {
                    // Out over the half facing the blow, a little upward.
                    Vector3 spread = new Vector3((float)random.NextDouble() * 2.0f - 1.0f, (float)random.NextDouble() * 1.4f - 0.4f,
                        (float)random.NextDouble() * 2.0f - 1.0f);
                    Vector3 velocity = (forward + spread * 0.9f).normalized * speed * (0.6f + (float)random.NextDouble() * 0.6f);
                    _velocities[i] = velocity;
                    _heads[i] = point;
                    var streak = new GameObject("Spark").AddComponent<LineRenderer>();
                    streak.transform.SetParent(transform, false);
                    streak.material = SparkMaterial();
                    streak.useWorldSpace = true;
                    streak.positionCount = 2;
                    streak.numCapVertices = 0;
                    streak.startWidth = width;
                    streak.endWidth = width * 0.3f;
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
                float life = Mathf.Clamp01(1.0f - _elapsed / _seconds);
                for (int i = 0; i < _streaks.Length; i++)
                {
                    _velocities[i] += Physics.gravity * Time.deltaTime * 0.5f;
                    _heads[i] += _velocities[i] * Time.deltaTime;
                    Vector3 tail = _heads[i] - _velocities[i].normalized * _length * life;
                    _streaks[i].SetPosition(0, _heads[i]);
                    _streaks[i].SetPosition(1, tail);
                    _streaks[i].widthMultiplier = life;
                }
                if (_elapsed >= _seconds)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
