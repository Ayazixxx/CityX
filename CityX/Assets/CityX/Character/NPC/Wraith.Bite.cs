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

        /// <summary>The punch's recording (SourceArt/WraithPunch), under Resources.</summary>
        public const string PunchMotionResource = "Wraith/WraithPunchMotion";

        /// <summary>
        /// A body this tall, or reaching this far out from its middle, is large: only punched, metres (the Crimson Claw is 3.1 m tall and 2.4 m
        /// out; a player stands 2.6 m and reaches about 0.6).
        /// </summary>
        public const float LargePreyMetres = 3.5f;

        public const float LargePreyReachMetres = 1.5f;

        /// <summary>How far out a player-sized body reaches from its middle, metres: a wider one is lunged at from that much further off.</summary>
        private const float OrdinaryPreyRadius = 0.6f;

        /// <summary>Its fist counts as on the prey within this of the prey's body, metres.</summary>
        public const float PunchReachMetres = 0.7f;

        private readonly WraithHunt _hunt = new WraithHunt();
        private BakedMotion _punchMotion;
        private BakedPose _punchPose;
        private Transform[] _punchBones = System.Array.Empty<Transform>();
        private Transform _fist;
        private float _fistReach = 1.0f;
        private bool _playingPunch;
        private bool _preyLarge;
        private float _preyExtent;
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
            BindPunch();
        }

        /// <summary>The punch's recording on the same bones, and how far ahead of its middle the fist is as the blow lands.</summary>
        private void BindPunch()
        {
            _punchMotion = BakedMotion.Load(PunchMotionResource);
            if (_punchMotion == null)
            {
                Debug.LogError($"CityX: no {PunchMotionResource} -- the Wraith cannot punch.");
                return;
            }
            _punchPose = _punchMotion.NewPose();
            _punchBones = new Transform[_punchMotion.Bones.Count];
            for (int i = 0; i < _punchBones.Length; i++)
            {
                if (!_punchMotion.IsLowerBodyBone(i))
                {
                    _punchBones[i] = TransformSearch.FindDeep(transform, _punchMotion.Bones[i]);
                }
            }
            _fist = TransformSearch.FindDeep(transform, "hand_r");
            if (_fist == null)
            {
                return;
            }
            // The pose the blow lands in, put on the bones for a moment and taken off again: the fist's reach is this body's own.
            var saved = new Quaternion[_punchBones.Length];
            for (int i = 0; i < _punchBones.Length; i++)
            {
                saved[i] = _punchBones[i] != null ? _punchBones[i].localRotation : Quaternion.identity;
            }
            Quaternion hips = Hips != null ? Hips.localRotation : Quaternion.identity;
            _punchMotion.Sample("follow", 0.0f, false, _punchPose);
            for (int i = 0; i < _punchBones.Length; i++)
            {
                if (_punchBones[i] != null) _punchBones[i].localRotation = _punchPose.Rotations[i];
            }
            if (Hips != null) Hips.localRotation = _punchPose.Rotations[0];
            Vector3 reach = transform.InverseTransformPoint(_fist.position);
            _fistReach = Mathf.Max(0.3f, reach.z * transform.lossyScale.z);
            for (int i = 0; i < _punchBones.Length; i++)
            {
                if (_punchBones[i] != null) _punchBones[i].localRotation = saved[i];
            }
            if (Hips != null) Hips.localRotation = hips;
        }

        /// <summary>How big its prey is: tall enough to be only punched, and how far it reaches out from its middle.</summary>
        private void MeasurePrey()
        {
            _preyLarge = false;
            _preyExtent = OrdinaryPreyRadius;
            if (_prey == null)
            {
                return;
            }
            Bounds bounds = default;
            bool any = false;
            if (_prey.TryGetComponent(out CharacterController capsule) && capsule.enabled)
            {
                bounds = capsule.bounds;
                any = true;
            }
            else
            {
                foreach (Collider part in _prey.GetComponentsInChildren<Collider>())
                {
                    if (part.isTrigger || !part.enabled)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        bounds = part.bounds;
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(part.bounds);
                    }
                }
            }
            if (!any)
            {
                return;
            }
            _preyLarge = bounds.size.y >= LargePreyMetres || Mathf.Max(bounds.extents.x, bounds.extents.z) >= LargePreyReachMetres;
            _preyExtent = Mathf.Max(OrdinaryPreyRadius, Mathf.Max(bounds.extents.x, bounds.extents.z));
        }

        /// <summary>Its fist is on the prey's body.</summary>
        private bool FistOnPrey()
        {
            if (_fist == null || _prey == null)
            {
                return false;
            }
            Vector3 fist = _fist.position;
            Vector3 middle = _prey.transform.position;
            var across = new Vector2(fist.x - middle.x, fist.z - middle.z);
            float under = Mathf.Max(0.0f, middle.y - fist.y);
            return across.magnitude - _preyExtent <= PunchReachMetres && under <= PunchReachMetres;
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
                Large = exists && _preyLarge,
                Radius = exists ? _preyExtent - OrdinaryPreyRadius : 0.0f,
            };
            bool reached = _hunt.Phase == WraithPhase.WindingUp
                ? sight.Exists && FistOnPrey()
                : sight.Exists && Jaw != null && (NeckPoint() - Jaw.position).sqrMagnitude <= BiteReachMetres * BiteReachMetres;
            _hunt.Tick(deltaTime, transform.position, sight, reached);
            if (_hunt.Changed)
            {
                ShowPhase(_hunt.Phase);
                PhaseChanged?.Invoke(_hunt.Phase, _prey);
            }
            if (_hunt.DamageDue > 0.0f && sight.Exists && _hunt.Phase == WraithPhase.Punching)
            {
                // The blow, as the swing ends: no blood, a knock.
                Vector3 at = _fist != null ? _fist.position : _prey.transform.position + Vector3.up * BodyRig.ShoulderY;
                DamageContext.Strike((IDamageable)_prey, _hunt.DamageDue, transform.position, WraithHunt.PunchHitstunSeconds, DamageKind.Physical, 0.0f, at,
                    gameObject);
            }
            else if (_hunt.DamageDue > 0.0f && sight.Exists)
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
                case WraithPhase.WindingUp:
                case WraithPhase.Punching:
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
            MeasurePrey();
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
            else if (phase == WraithPhase.WindingUp && Application.isPlaying && Head != null)
            {
                AttackGlint.Flash(Head.position);   // the punch's tell; no sound of its own yet (owner, 2026-11-11)
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
            if (_hunt.Phase == WraithPhase.Lunging || _hunt.Phase == WraithPhase.Biting || _hunt.Phase == WraithPhase.WindingUp || _hunt.Phase == WraithPhase.Punching)
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
                _hunt.NewPrey();
                MeasurePrey();
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
            bool punching = phase == WraithPhase.WindingUp || phase == WraithPhase.Punching;
            bool playing = punching || phase == WraithPhase.Lunging || phase == WraithPhase.Biting || phase == WraithPhase.Thrown || phase == WraithPhase.Recovering;
            _biteBlend = Mathf.MoveTowards(_biteBlend, playing ? 1.0f : 0.0f, deltaTime / BiteBlendSeconds);
            if (playing)
            {
                _playingPunch = punching;   // fading out, the last of whichever it played
            }
            BakedMotion motion = _playingPunch ? _punchMotion : _biteMotion;
            BakedPose pose = _playingPunch ? _punchPose : _bitePose;
            Transform[] bones = _playingPunch ? _punchBones : _biteBones;
            if (_biteBlend <= 0.0f || motion == null)
            {
                return;
            }
            if (playing)
            {
                motion.Sample(SegmentOf(phase), _clipSeconds, false, pose);
            }
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone != null)
                {
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, pose.Rotations[i], _biteBlend);
                }
            }
            if (Hips != null)
            {
                Hips.localRotation = Quaternion.Slerp(Hips.localRotation, pose.Rotations[0], _biteBlend);   // a motion lists the root first
            }

            if (!IsCopy && Jaw != null && _prey != null && (phase == WraithPhase.Lunging || phase == WraithPhase.Biting))
            {
                float seconds = phase == WraithPhase.Lunging ? LungeHomingSeconds : BiteHoldSeconds;
                transform.position += (NeckPoint() - Jaw.position) * (1.0f - Mathf.Exp(-deltaTime / seconds));
            }
            else if (!IsCopy && _prey != null && phase == WraithPhase.WindingUp)
            {
                // Through the swing it closes to where the blow lands with its fist on the prey's body: as far off its middle as the prey reaches
                // out and the fist reaches ahead, its own height kept.
                Vector3 toPrey = _prey.transform.position - transform.position;
                toPrey.y = 0.0f;
                if (toPrey.sqrMagnitude > 1e-4f)
                {
                    Vector3 stand = _prey.transform.position - toPrey.normalized * (_preyExtent + _fistReach * 0.85f);
                    stand.y = transform.position.y;
                    transform.position += (stand - transform.position) * (1.0f - Mathf.Exp(-deltaTime / LungeHomingSeconds));
                }
            }
        }

        private static string SegmentOf(WraithPhase phase)
        {
            switch (phase)
            {
                case WraithPhase.Lunging: return "lunge";
                case WraithPhase.Biting: return "bite";
                case WraithPhase.Thrown: return "thrown";
                case WraithPhase.WindingUp: return "swing";
                case WraithPhase.Punching: return "follow";
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
