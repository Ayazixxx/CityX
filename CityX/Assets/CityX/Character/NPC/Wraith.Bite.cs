using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE HUNT AND THE BITE (owner, 2026-11-05: "sneak up behind you and sink his teeth into your neck using the wraith neck bite.fbx animation. make blood effects where he bites").
    /// WraithHunt decides; here is the body doing it. The recording (SourceArt/WraithBite) is played bone by bone over the drifting pose, and the whole Wraith is moved so its teeth are
    /// on the prey's neck, whatever the recording's reach, since a player stands 2.6 m tall. In a session the owner's machine hunts and bites, and every other machine is told the
    /// phase and each spurt of blood (NetworkedWraith). A wild one's prey is the nearest of what the enemies fight (a player or a summon, Opponents); a summon's, the nearest
    /// enemy to its summoner (Wraith.Summon.cs): a body with a neck is bitten there, any other high on its back.
    /// </summary>
    public sealed partial class Wraith
    {
        /// <summary>The recording, under Resources.</summary>
        public const string BiteMotionResource = "Wraith/WraithBiteMotion";

        /// <summary>Its teeth count as on the neck within this, metres.</summary>
        public const float BiteReachMetres = 1.3f;

        /// <summary>How quickly the recording comes over the drifting pose and goes, seconds.</summary>
        public const float BiteBlendSeconds = 0.15f;

        /// <summary>How quickly its teeth close on the neck while lunging, and how closely they stay on it while biting, seconds.</summary>
        private const float LungeHomingSeconds = 0.14f;

        private const float BiteHoldSeconds = 0.05f;

        /// <summary>How fast it is thrown off, metres a second.</summary>
        public const float ThrownSpeed = 2.2f;

        /// <summary>How fast it comes round to face what it is lunging at, degrees a second.</summary>
        private const float LungeFacingDegreesPerSecond = 540.0f;

        /// <summary>
        /// The teeth sink in at the back of the neck and to its right, metres: both heads are as wide as the shoulders, so straight behind it sat half inside the prey's, and this is
        /// the side the game's camera looks past.
        /// </summary>
        private const float NeckBackMetres = 0.22f;

        private const float NeckSideMetres = 0.22f;

        private const float PreySearchSeconds = 1.0f;

        /// <summary>The owner's attack noise the prey can hear coming, and the crunch.</summary>
        private const float AttackVolume = 0.8f;

        /// <summary>The owner's recording (2026-11-07: "replace the wraith attack noise with this"), under Resources.</summary>
        public const string AttackClipName = "Wraith/WraithAttack";

        private const float CrunchVolume = 1.0f;

        private const float VoiceFullVolumeMetres = 10.0f;

        private const float VoiceSilentMetres = 40.0f;

        /// <summary>A body's blood as the bite lets it out: up and back out of the neck.</summary>
        private const float BloodStrength = 1.2f;

        private readonly WraithHunt _hunt = new WraithHunt();
        private BakedMotion _biteMotion;
        private BakedPose _bitePose;
        private Transform[] _biteBones = System.Array.Empty<Transform>();
        private float _biteBlend;
        private float _clipSeconds;
        private WraithPhase _shownPhase = WraithPhase.Drifting;
        /// <summary>What it hunts: a damageable body (a player, a summon, an enemy).</summary>
        private Component _prey;
        private Transform _preyNeck;
        private float _preySearch;
        private AudioSource _voice;
        private static AudioClip _attack;
        private static AudioClip _crunch;

        /// <summary>What it is doing just now, here or as its owner says.</summary>
        public WraithPhase Phase => IsCopy ? _shownPhase : _hunt.Phase;

        /// <summary>The body it is after.</summary>
        public Component Prey => _prey;

        /// <summary>The owner's machine: its phase changed (NetworkedWraith tells the others).</summary>
        public event System.Action<WraithPhase, Component> PhaseChanged;

        /// <summary>The owner's machine: its teeth let out blood here.</summary>
        public event System.Action<Vector3> Bled;

        /// <summary>The bite's blood let out so far on this machine (for tests).</summary>
        public int BloodShown { get; private set; }

        private void BindBite()
        {
            _biteMotion = BakedMotion.Load(BiteMotionResource);
            if (_biteMotion == null)
            {
                Debug.LogError($"CityX: no {BiteMotionResource} -- the Wraith cannot bite.");
                return;
            }
            _bitePose = _biteMotion.NewPose();
            _biteBones = new Transform[_biteMotion.Bones.Count];
            for (int i = 0; i < _biteBones.Length; i++)
            {
                // The arms, the spine, the neck, the head: it has no legs, and the hips are done apart.
                if (!_biteMotion.IsLowerBodyBone(i))
                {
                    _biteBones[i] = TransformSearch.FindDeep(transform, _biteMotion.Bones[i]);
                }
            }
        }

        /// <summary>The hunt's tick on the machine that runs it. True when the hunt has the body's movement, not the drift.</summary>
        private bool TickHunt(float deltaTime)
        {
            FindPrey(deltaTime);
            bool exists = PreyAlive();
            var sight = new WraithHunt.Prey
            {
                Exists = exists,
                Position = exists ? _prey.transform.position : Vector3.zero,
                Forward = exists ? _prey.transform.forward : Vector3.forward,
                Evading = exists && _prey is IOpponent opponent && opponent.IsInvulnerable,
            };
            bool reached = sight.Exists && Jaw != null && (NeckPoint() - Jaw.position).sqrMagnitude <= BiteReachMetres * BiteReachMetres;
            _hunt.Tick(deltaTime, transform.position, sight, reached);
            if (_hunt.Changed)
            {
                ShowPhase(_hunt.Phase);
                PhaseChanged?.Invoke(_hunt.Phase, _prey);
            }
            if (_hunt.DamageDue > 0.0f && sight.Exists)
            {
                Vector3 neck = NeckPoint();
                DamageContext.Strike((IDamageable)_prey, _hunt.DamageDue, transform.position, WraithHunt.BiteHitstunSeconds, DamageKind.Physical, 0.0f, neck,
                    gameObject);
                if (!(_prey is IBloodless))   // a summon bitten is a ghost bitten
                {
                    ShowBlood(neck);
                    Bled?.Invoke(neck);
                }
            }
            _clipSeconds += deltaTime;

            switch (_hunt.Phase)
            {
                case WraithPhase.Stalking:
                    Vector3 velocity = _hunt.Velocity;
                    float speed = velocity.magnitude;
                    if (speed > 1e-4f)
                    {
                        Vector3 direction = _steering.Steer(transform, velocity / speed, deltaTime, _prey != null ? _prey.transform : null);
                        transform.position += direction * (speed * deltaTime);
                    }
                    _stalkSpeed = speed;
                    TurnToward(_prey != null ? _prey.transform.position - transform.position : transform.forward, deltaTime);
                    Hover(deltaTime);
                    return true;
                case WraithPhase.Lunging:
                case WraithPhase.Biting:
                    // Moved in LateUpdate, once the pose has said where its teeth are.
                    _stalkSpeed = 0.0f;
                    if (_prey != null)
                    {
                        Vector3 heading = _prey.transform.position - transform.position;
                        heading.y = 0.0f;
                        if (heading.sqrMagnitude > 1e-4f)
                        {
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading.normalized, Vector3.up), LungeFacingDegreesPerSecond * deltaTime);
                        }
                    }
                    return true;
                case WraithPhase.Thrown:
                    transform.position -= transform.forward * (ThrownSpeed * deltaTime);
                    Hover(deltaTime);
                    return true;
                case WraithPhase.Recovering:
                    Hover(deltaTime);
                    return true;
                default:
                    _stalkSpeed = 0.0f;
                    return false;
            }
        }

        /// <summary>Told by its owner what it is doing (NetworkedWraith).</summary>
        public void ShowPhaseFromNetwork(WraithPhase phase, Component prey)
        {
            _prey = prey;
            _preyNeck = null;
            ShowPhase(phase);
        }

        /// <summary>Told by its owner that its teeth let out blood.</summary>
        public void ShowBloodFromNetwork(Vector3 at)
        {
            ShowBlood(at);
        }

        private void ShowPhase(WraithPhase phase)
        {
            _shownPhase = phase;
            _clipSeconds = 0.0f;
            if (phase == WraithPhase.Lunging)
            {
                // The tell: a glint at its head and its attack noise, half a second before the teeth.
                if (Application.isPlaying && Head != null)
                {
                    AttackGlint.Flash(Head.position);
                }
                Speak(AttackClip(), AttackVolume);
            }
            else if (phase == WraithPhase.Biting)
            {
                Speak(CrunchClip(), CrunchVolume);
            }
        }

        private void ShowBlood(Vector3 at)
        {
            BloodShown++;
            if (!Application.isPlaying)
            {
                return;
            }
            Vector3 away = _prey != null ? -_prey.transform.forward : -transform.forward;
            Gore.Blood(at, away + Vector3.up, BloodStrength, _prey != null ? _prey.transform : transform);
        }

        private void FindPrey(float deltaTime)
        {
            if (!_hunt.Hunting)
            {
                return;
            }
            if (_hunt.Phase == WraithPhase.Lunging || _hunt.Phase == WraithPhase.Biting)
            {
                return;   // committed to this one
            }
            _preySearch -= deltaTime;
            if (_preySearch > 0.0f && PreyAlive())
            {
                return;
            }
            if (_preySearch > 0.0f && _hunt.Phase == WraithPhase.Drifting)
            {
                return;
            }
            _preySearch = PreySearchSeconds;
            Component found = IsSummoned ? NearestHostile() : Opponents.NearestAlive(transform.position, WraithHunt.LoseMetres) as Component;
            if (found != _prey)
            {
                _prey = found;
                _preyNeck = null;
            }
        }

        /// <summary>Its prey is there and alive.</summary>
        private bool PreyAlive() => _prey != null && _prey is IDamageable body && body.IsAlive;

        /// <summary>
        /// Where its teeth go: the back of the prey's neck (a body built on the player's, a Wraith's head); a body with neither, high on its
        /// back (the Crimson Claw's head).
        /// </summary>
        private Vector3 NeckPoint()
        {
            if (_prey == null)
            {
                return transform.position;
            }
            if (_preyNeck == null)
            {
                Animator animator = _prey.GetComponentInChildren<Animator>();
                _preyNeck = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
                if (_preyNeck == null && _prey.TryGetComponent(out Wraith wraith))
                {
                    _preyNeck = wraith.Head;
                }
            }
            Transform body = _prey.transform;
            if (_preyNeck != null)
            {
                return _preyNeck.position - body.forward * NeckBackMetres + body.right * NeckSideMetres;
            }
            Collider shape = _prey.GetComponentInChildren<Collider>();
            if (shape == null)
            {
                return body.position + Vector3.up * BodyRig.ShoulderY - body.forward * NeckBackMetres;
            }
            Bounds bounds = shape.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y * 0.6f) - body.forward * (bounds.extents.z * 0.6f);
        }

        /// <summary>The recording over the pose already on the bones, and its teeth brought onto the neck.</summary>
        private void ApplyBiteOver(float deltaTime)
        {
            WraithPhase phase = Phase;
            bool playing = phase == WraithPhase.Lunging || phase == WraithPhase.Biting || phase == WraithPhase.Thrown || phase == WraithPhase.Recovering;
            _biteBlend = Mathf.MoveTowards(_biteBlend, playing ? 1.0f : 0.0f, deltaTime / BiteBlendSeconds);
            if (_biteBlend <= 0.0f || _biteMotion == null)
            {
                return;
            }
            if (playing)
            {
                _biteMotion.Sample(SegmentOf(phase), _clipSeconds, false, _bitePose);
            }
            for (int i = 0; i < _biteBones.Length; i++)
            {
                Transform bone = _biteBones[i];
                if (bone != null)
                {
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, _bitePose.Rotations[i], _biteBlend);
                }
            }
            if (Hips != null)
            {
                Hips.localRotation = Quaternion.Slerp(Hips.localRotation, _bitePose.Rotations[0], _biteBlend);   // a motion lists the root first
            }

            if (!IsCopy && Jaw != null && _prey != null && (phase == WraithPhase.Lunging || phase == WraithPhase.Biting))
            {
                float seconds = phase == WraithPhase.Lunging ? LungeHomingSeconds : BiteHoldSeconds;
                transform.position += (NeckPoint() - Jaw.position) * (1.0f - Mathf.Exp(-deltaTime / seconds));
            }
        }

        private static string SegmentOf(WraithPhase phase)
        {
            switch (phase)
            {
                case WraithPhase.Lunging: return "lunge";
                case WraithPhase.Biting: return "bite";
                case WraithPhase.Thrown: return "thrown";
                default: return "recover";
            }
        }

        private void Speak(AudioClip clip, float volume)
        {
            if (!Application.isPlaying || clip == null)
            {
                return;
            }
            if (_voice == null)
            {
                _voice = gameObject.AddComponent<AudioSource>();
                _voice.playOnAwake = false;
                _voice.spatialBlend = 1.0f;
                _voice.rolloffMode = AudioRolloffMode.Linear;
                _voice.minDistance = VoiceFullVolumeMetres;
                _voice.maxDistance = VoiceSilentMetres;
            }
            _voice.PlayOneShot(clip, volume);
        }

        /// <summary>The noise of its lunge, loaded once.</summary>
        public static AudioClip AttackClip()
        {
            if (_attack == null)
            {
                _attack = Resources.Load<AudioClip>(AttackClipName);
            }
            return _attack;
        }

        /// <summary>Teeth going in: a hard, dull burst.</summary>
        private static AudioClip CrunchClip()
        {
            if (_crunch == null)
            {
                _crunch = Noise("WraithCrunch", 0.22f, 0.9f, t => Mathf.Exp(-t * 7.0f));
            }
            return _crunch;
        }

        private static AudioClip Noise(string name, float seconds, float smoothing, System.Func<float, float> shape)
        {
            const int rate = 44100;
            int count = Mathf.RoundToInt(rate * seconds);
            var samples = new float[count];
            var random = new System.Random(name.Length * 7);
            float last = 0.0f;
            for (int i = 0; i < count; i++)
            {
                last = Mathf.Lerp((float)(random.NextDouble() * 2.0 - 1.0), last, smoothing);
                samples[i] = last * shape(i / (float)count);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
