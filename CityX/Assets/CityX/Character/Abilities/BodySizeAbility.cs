using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// GROW AND SHRINK, the Wizard's first two spells (owner, 2026-10-15: "one that makes you 10x as big and one that makes you 10x as small",
    /// "30 seconds each"), on the body. Grow takes it to ten times its size, Shrink to a tenth, over a second and a bit, for 30 s from the cast;
    /// then it changes back. The same spell again ends it early; the other spell goes straight to the other size.
    ///
    /// THE SIZE IS THE BODY'S TRANSFORM SCALE: the model, the capsule it walks in, the ragdoll and whatever it holds all scale with it, and the
    /// camera pulls back or in to match (ThirdPersonCamera reads the body's scale). How a resized body moves -- faster and heavier as a giant,
    /// scurrying when tiny -- is BodySize's (PlayerLocomotion reads <see cref="Scale"/>); fall damage counts a giant's falls in its own
    /// lengths (PlayerVitals.FallScale). Climbing, vaulting and foot placement, tuned in metres for a 2.6 m body, are off while resized.
    ///
    /// GROWING NEEDS ROOM: each step of a growth is tried as a capsule first; under a ceiling the body grows to fit and carries on once out.
    ///
    /// OTHER PLAYERS SEE IT through the body's NetworkedBodyMotion, which carries the scale (a late joiner too); the cast itself (the swirl of
    /// magic, the sound, the raised hand) travels as an ability event (<see cref="Fired"/>/<see cref="PlayRemote"/>). A copy never sets its
    /// own scale.
    /// </summary>
    public sealed class BodySizeAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "body-size";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's body when a spell starts or is ended early (Kind: GrowKind, ShrinkKind or EndKind).</summary>
        public event System.Action<AbilityEvent> Fired;

        public const int GrowKind = 0;
        public const int ShrinkKind = 1;
        public const int EndKind = 2;

        /// <summary>The least time between two casts, seconds (before the Cooldown Reduction stat).</summary>
        public const float CooldownSeconds = 1.0f;

        /// <summary>How long the hand is raised for a cast, seconds.</summary>
        public const float CastPoseSeconds = 0.7f;

        /// <summary>A growth leaves this share of the body's height free under its feet for the ground and kerbs (not tested for room).</summary>
        public const float FootClearanceFraction = 0.15f;

        /// <summary>A growth may brush a wall: its capsule is tried this much thinner than the body's.</summary>
        public const float RoomRadiusFraction = 0.9f;

        public const float Volume = 0.8f;

        private static readonly int StrideRateParameter = Animator.StringToHash(StrideRateName);

        /// <summary>The animator's float that sets how fast the walk and run cycles play (1 at the body's own size).</summary>
        public const string StrideRateName = "StrideRate";

        /// <summary>The size logic (the owner's body): what was cast, the change, the time left.</summary>
        public BodySize Size { get; } = new BodySize();

        /// <summary>How big the body is now: its scale (on a copy, what the network says).</summary>
        public float Scale => transform.localScale.y;

        public bool IsResized => Mathf.Abs(Scale - 1.0f) > BodySize.NormalTolerance;

        /// <summary>0..1, how far the casting hand is raised.</summary>
        public float CastFraction => Mathf.Clamp01(_castPose / (CastPoseSeconds * 0.4f));

        /// <summary>How many casts this body has shown (its own and other players'): what a test reads.</summary>
        public int SwirlsShown { get; private set; }

        private CharacterController _controller;
        private PlayerVitals _vitals;
        private Animator _animator;
        private bool _animatorHasStride;
        private bool _bound;
        private float _cooldown;
        private float _castPose;
        private float _applied = 1.0f;
        private float _baseSkin = 0.03f;
        private bool _wasDead;
        private static readonly Collider[] Overlaps = new Collider[32];
        private static AudioClip _growSound;
        private static AudioClip _shrinkSound;

        private void Awake()
        {
            Bind();
        }

        private void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            _controller = GetComponent<CharacterController>();
            _baseSkin = _controller != null ? _controller.skinWidth / Mathf.Max(0.01f, transform.localScale.y) : 0.03f;
            _vitals = GetComponent<PlayerVitals>();
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                foreach (AnimatorControllerParameter parameter in _animator.parameters)
                {
                    if (parameter.nameHash == StrideRateParameter && parameter.type == AnimatorControllerParameterType.Float)
                    {
                        _animatorHasStride = true;
                    }
                }
            }
        }

        public bool CanCast => _cooldown <= 0.0f && (_vitals == null || !_vitals.IsDead);

        public bool TryGrow() => TryCast(BodySize.Spell.Grow);

        public bool TryShrink() => TryCast(BodySize.Spell.Shrink);

        /// <summary>Casts Grow or Shrink (the one in effect, again: ends it). False while cooling down or dead.</summary>
        public bool TryCast(BodySize.Spell spell)
        {
            if (!CanCast || spell == BodySize.Spell.None)
            {
                return false;
            }
            Bind();
            _cooldown = CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            BodySize.Spell now = Size.Cast(spell);
            int kind = now == BodySize.Spell.None ? EndKind : now == BodySize.Spell.Grow ? GrowKind : ShrinkKind;
            _castPose = CastPoseSeconds;
            Swirl(kind);
            Fired?.Invoke(new AbilityEvent(kind, transform.position, Vector3.up, BodySize.HoldSeconds));
            return true;
        }

        /// <summary>
        /// Another player's cast, on their copy here: the swirl, the sound and the raised hand. The size itself comes with the body's transform.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind < GrowKind || evt.Kind > EndKind)
            {
                return;
            }
            Swirl(evt.Kind);
            RemoteAbilityPose.Show(gameObject, CastPoseSeconds * 0.5f, 0.25f);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>One step of time: the spell's clock and the change (the owner's body), and the stride on every body. Public for tests.</summary>
        public void Tick(float deltaTime)
        {
            Bind();
            _cooldown = Mathf.Max(0.0f, _cooldown - deltaTime);
            _castPose = Mathf.Max(0.0f, _castPose - deltaTime);

            bool dead = _vitals != null && _vitals.IsDead;
            if (_wasDead && !dead && (Size.IsResized || Size.IsChanging))
            {
                Size.Reset();   // back on its feet after dying: its own size again, at once
            }
            _wasDead = dead;

            // A copy's size is the network's (Size never runs there: nothing is cast on a copy). Only the body that cast it, and only while it
            // is or has just been resized, sets its scale.
            if (!dead)
            {
                Size.Tick(deltaTime, Fits);
            }
            if (Size.IsChanging || Size.IsResized || !Mathf.Approximately(_applied, Size.Scale))
            {
                Apply(Size.Scale);
            }

            if (_animatorHasStride && _animator != null)
            {
                _animator.SetFloat(StrideRateParameter, 1.0f / Mathf.Sqrt(Mathf.Max(0.01f, Scale)));
            }
            if (_vitals != null)
            {
                _vitals.FallScale = Mathf.Max(1.0f, Scale);
            }
        }

        private void Apply(float scale)
        {
            if (_controller != null)
            {
                // The controller's step and skin are world metres (only its height and radius scale with the body): the skin is kept in
                // proportion (a tiny body would float on 3 cm of it), and the step never above what Unity allows for the new size (PlayerLocomotion
                // sets it each move, in proportion too).
                float most = Mathf.Min((_controller.height + _controller.radius * 2.0f) * scale, _controller.height) * 0.9f;
                _controller.stepOffset = Mathf.Min(_controller.stepOffset, most);
                _controller.skinWidth = _baseSkin * scale;
            }
            transform.localScale = Vector3.one * scale;
            _applied = scale;
        }

        /// <summary>Whether the body would fit at <paramref name="scale"/>: its capsule, from a little above its feet up, touches nothing solid.</summary>
        private bool Fits(float scale)
        {
            if (_controller == null)
            {
                return true;
            }
            float radius = _controller.radius * scale * RoomRadiusFraction;
            float height = _controller.height * scale;
            float baseY = _controller.center.y * scale - height * 0.5f;
            Vector3 feet = transform.position + Vector3.up * baseY;
            Vector3 bottom = feet + Vector3.up * (radius + height * FootClearanceFraction);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + height * FootClearanceFraction, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Overlaps[i];
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<IDamageable>() != null
                    || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic))
                {
                    continue;   // the body itself, people, loose things: pushed aside, not walls
                }
                return false;
            }
            return true;
        }

        /// <summary>The magic of a cast: motes of violet light spiralling round the body (up as it grows, down as it shrinks), and its sound.</summary>
        private void Swirl(int kind)
        {
            SwirlsShown++;
            if (!Application.isPlaying)
            {
                return;
            }
            bool growing = kind == GrowKind || (kind == EndKind && Scale < 1.0f);
            var swirl = new GameObject("SizeMagic");
            swirl.transform.SetParent(transform, false);
            swirl.AddComponent<MagicSwirl>().Begin(growing, _controller != null ? _controller.height : 2.6f,
                _controller != null ? _controller.radius : 0.45f);
            var source = swirl.AddComponent<AudioSource>();
            source.clip = growing ? GrowSound() : ShrinkSound();
            source.spatialBlend = 0.85f;
            source.minDistance = 4.0f * Mathf.Max(1.0f, Scale);
            source.maxDistance = 120.0f * Mathf.Max(1.0f, Scale);
            source.volume = Volume;
            source.Play();
        }

        public static AudioClip GrowSound() => _growSound != null ? _growSound : (_growSound = MakeSound(true));

        public static AudioClip ShrinkSound() => _shrinkSound != null ? _shrinkSound : (_shrinkSound = MakeSound(false));

        /// <summary>
        /// A spell's sound, made in code: a deep swell sweeping up two octaves as the body grows (down as it shrinks), a shimmer of bells over it,
        /// and a soft whoomp at the end.
        /// </summary>
        private static AudioClip MakeSound(bool up)
        {
            const int rate = 44100;
            float seconds = BodySize.ChangeSeconds + 0.5f;
            int count = Mathf.RoundToInt(rate * seconds);
            var samples = new float[count];
            var random = new System.Random(up ? 11 : 13);
            double phase = 0.0, phase2 = 0.0;
            float[] bells = { 1318.5f, 1760.0f, 2217.5f, 2637.0f, 3520.0f };
            float[] bellAt = new float[12];
            int[] bellNote = new int[12];
            for (int b = 0; b < bellAt.Length; b++)
            {
                bellAt[b] = (float)random.NextDouble() * BodySize.ChangeSeconds;
                bellNote[b] = random.Next(bells.Length);
            }
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float along = Mathf.Clamp01(t / BodySize.ChangeSeconds);
                float sweep = up ? along : 1.0f - along;
                float frequency = 110.0f * Mathf.Pow(2.0f, sweep * 2.0f);
                phase += 2.0 * System.Math.PI * frequency / rate;
                phase2 += 2.0 * System.Math.PI * frequency * 1.5 / rate;
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / (BodySize.ChangeSeconds + 0.1f)));
                float tone = (float)(System.Math.Sin(phase) * 0.55 + System.Math.Sin(phase2) * 0.2 + System.Math.Sin(phase * 2.0) * 0.12) * swell;
                float shimmer = 0.0f;
                for (int b = 0; b < bellAt.Length; b++)
                {
                    float since = t - bellAt[b];
                    if (since >= 0.0f && since < 0.6f)
                    {
                        shimmer += Mathf.Sin(2.0f * Mathf.PI * bells[bellNote[b]] * (up ? 1.0f : 0.75f) * since) * Mathf.Exp(-since * 9.0f) * 0.12f;
                    }
                }
                float end = t - BodySize.ChangeSeconds;
                float whoomp = end >= 0.0f ? Mathf.Sin(2.0f * Mathf.PI * (up ? 60.0f : 160.0f) * end) * Mathf.Exp(-end * 10.0f) * 0.6f : 0.0f;
                samples[i] = Mathf.Clamp((tone * 0.5f + shimmer + whoomp) * 0.8f, -1.0f, 1.0f);
            }
            AudioClip clip = AudioClip.Create(up ? "GrowSpell" : "ShrinkSpell", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// The motes of a cast: short violet streaks orbiting the body, rising (or falling) along it over the change, then fading. A child of the
        /// body in its own units, so they grow and shrink with it.
        /// </summary>
        private sealed class MagicSwirl : MonoBehaviour
        {
            private const int Motes = 16;
            private const float Turns = 2.2f;
            private const float Width = 0.05f;

            private static readonly Color Core = new Color(0.86f, 0.62f, 1.0f, 1.0f);
            private static readonly Color Tail = new Color(0.45f, 0.18f, 0.95f, 0.0f);

            private LineRenderer[] _streaks;
            private float[] _phase;
            private float[] _lag;
            private bool _rising;
            private float _height;
            private float _radius;
            private float _elapsed;
            private float Life => BodySize.ChangeSeconds + 0.5f;

            public void Begin(bool rising, float height, float radius)
            {
                _rising = rising;
                _height = height;
                _radius = radius * 1.9f;
                _streaks = new LineRenderer[Motes];
                _phase = new float[Motes];
                _lag = new float[Motes];
                var random = new System.Random(unchecked(Time.frameCount * 31 + Motes));
                for (int i = 0; i < Motes; i++)
                {
                    _phase[i] = i / (float)Motes * Mathf.PI * 2.0f;
                    _lag[i] = (float)random.NextDouble() * 0.35f;
                    var streak = new GameObject("Mote").AddComponent<LineRenderer>();
                    streak.transform.SetParent(transform, false);
                    streak.material = BlockDeflectEffect.SparkMaterial();
                    streak.useWorldSpace = false;
                    streak.positionCount = 4;
                    streak.numCapVertices = 2;
                    streak.startColor = Core;
                    streak.endColor = Tail;
                    streak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    streak.receiveShadows = false;
                    _streaks[i] = streak;
                }
                Place();
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                Place();
                if (_elapsed >= Life)
                {
                    Destroy(gameObject);
                }
            }

            private void Place()
            {
                float scale = transform.lossyScale.y;
                for (int i = 0; i < _streaks.Length; i++)
                {
                    float t = Mathf.Clamp01((_elapsed - _lag[i]) / BodySize.ChangeSeconds);
                    float fade = Mathf.Clamp01((Life - _elapsed) / 0.5f) * Mathf.Clamp01((_elapsed - _lag[i]) / 0.15f);
                    for (int p = 0; p < 4; p++)
                    {
                        float back = p * 0.035f;   // the tail: where the mote was a moment ago
                        float tt = Mathf.Max(0.0f, t - back);
                        float along = _rising ? tt : 1.0f - tt;
                        float angle = _phase[i] + tt * Turns * Mathf.PI * 2.0f;
                        float radius = _radius * (1.0f - 0.35f * Mathf.Sin(along * Mathf.PI));
                        _streaks[i].SetPosition(p, new Vector3(Mathf.Cos(angle) * radius, along * _height * 1.05f, Mathf.Sin(angle) * radius));
                    }
                    _streaks[i].widthMultiplier = Width * scale * fade;
                }
            }
        }
    }
}
