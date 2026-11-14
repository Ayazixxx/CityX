using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the sword attacks (<see cref="SwordStrikes"/>) on a body, owner or network copy alike: <see cref="PlayerAnimationDriver"/> calls
    /// <see cref="Begin"/> when an attack starts (here, or replayed from the network, which is why a copy swings the same way) and
    /// <see cref="BeginGuard"/> when a guard is raised; this advances the attack, holds the READY stance for a few seconds after the last one,
    /// and hands each frame to <see cref="PlayerLocomotion"/>, whose LateUpdate poses the arm, the blade, the torso, the hips and the feet
    /// (after the Animator, like every other procedural pose).
    ///
    /// THE CARRY (owner, 2026-10-16: "my character holds ... the sword awkwardly"): whenever a TWO-HANDED sword is in the right hand and
    /// nothing else is using the body, it is held the way <see cref="SwordStrikes.Carry"/> says (low at the right side, trailing behind at a
    /// sprint) instead of the sword idle clip's arms held out at shoulder height. The sword is found under the hand on any body (a network copy
    /// mounts the same model there), so another player's copy carries it the same way with nothing sent. A ONE-HANDED sword is carried by its
    /// own clips (the sword1h_* locomotion, <see cref="SwordCarryArm"/>; owner, 2026-10-17: "make you own sword running and walking animations
    /// and make them look good"): nothing is posed over them, an attack blends in from the arm they swing, and the stance after it lowers back
    /// into them.
    ///
    /// The attack's clock is what the combat freezes for a hit-stop (<see cref="SetSpeed"/> 0): the pose holds exactly where the blade landed.
    ///
    /// AN ATTACK MOVES THE WHOLE BODY (owner, 2026-11-13: the swings "only use the arms so It looks odd ... the animation involves the whole body
    /// akin to dark souls combat"; docs/SWORD_FULL_BODY.md): where an attack has a clip (<see cref="SwordStrikes.Motion"/>), it is sampled on
    /// the attack's own clock, so a hit-stop, Melee Speed and an enemy's tell hold it as they hold the pose. The clip has the legs, the hips,
    /// the torso and the head: the stride, the coil against the cut, the weight driven through it. The sword arm, the blade and the hand on the
    /// handle are still the pose made here every frame, solved from the shoulders the clip has turned (PlayerLocomotion.ApplyBakedTrunk), so
    /// they suit whichever sword is held. The clip's feet are planted for the attack's own step (SwordStrikes.StepMetres); a body stopped
    /// short has its stride drawn in (PlayerLocomotion.SetClipStride).
    ///
    /// ON A BODY THAT IS MOVING THE LEGS ARE THE WALK'S (owner, 2026-11-13: "move freely while attacking ... zero slowdown"): the keys carry
    /// the body through a swing at its full pace, so the clip gives the hips and legs back to the walk or the run as the body moves off
    /// (SwordStrikes.ClipLegs, BakedPose.LegsWeight) and takes them again when it stands. The torso, the head and the arms are the
    /// attack's either way.
    /// </summary>
    public sealed class SwordPoseDriver : MonoBehaviour
    {
        /// <summary>How long the ready stance is held after the last attack or guard, seconds.</summary>
        public const float ReadyLingerSeconds = 3.0f;

        /// <summary>How quickly an attack or guard takes hold, seconds.</summary>
        public const float EngageSeconds = 0.08f;

        /// <summary>How quickly the carry takes hold (a sword drawn, a climb over), seconds.</summary>
        public const float CarryEngageSeconds = 0.25f;

        public const float DisengageSeconds = 0.35f;

        /// <summary>A guard comes up from, and the ready stance returns from, the pose the body is in over this long, seconds.</summary>
        public const float GuardTakeoverSeconds = 0.15f;

        public const float ReleaseTakeoverSeconds = 0.25f;

        /// <summary>The ready stance lowers into the posed carry over this long once it has lingered, seconds.</summary>
        public const float ReadyToCarrySeconds = 0.45f;

        /// <summary>A one-handed sword's ready stance lowers into its clips' carry over this long, seconds: the hand comes from the belly to the thigh, so it settles a little slower.</summary>
        public const float ReadyToClipCarrySeconds = 0.6f;

        /// <summary>
        /// In the ready stance or the guard the feet take the fighting stance and step (SwordFootwork) up to a guarded walk (half a walk, 2 m/s);
        /// walking off at full pace, the walk clip's legs take them back between these speeds, metres per second. Mid-attack the stance holds
        /// at any speed (the stepper keeps up).
        /// </summary>
        public const float StanceHeldBelowSpeed = 2.6f;

        public const float StanceGoneAboveSpeed = 3.4f;

        /// <summary>The carry trails the blade behind between these speeds (a walk is 4, a sprint 7.5), metres per second.</summary>
        public const float TrailFromSpeed = 4.5f;

        public const float TrailFullSpeed = 7.0f;

        private PlayerLocomotion _locomotion;
        private SwordStrikeKind _kind;
        private bool _striking;
        private bool _guarding;
        private bool _rebounding;
        private bool _oneHanded;
        private float _elapsed;
        private float _duration = 0.5f;
        private float _speed = 1.0f;
        private float _readyRemaining;
        private float _engage;
        private SwordFrame _lastFrame;
        private bool _hasLastFrame;
        private SwordFrame _takeoverFrom;
        private float _takeoverSeconds;
        private float _takeoverRemaining;
        private bool _carrying;
        private bool _carryOneHanded;

        /// <summary>An attack or guard starting from no pose (a one-handed sword's clips) blends in over this long instead of EngageSeconds, seconds.</summary>
        private float _fromClipSeconds;
        private float _run01;
        private float _stanceGate = 1.0f;

        /// <summary>How quickly an attack's clip takes the body, seconds, and how quickly it hands it back once the attack is over.</summary>
        public const float ClipEngageSeconds = 0.08f;

        /// <summary>
        /// From no clip (the carry at the thigh, the ready stance, a walk) an attack's clip comes in across this share of its wind-up: the
        /// body goes straight into the coil rather than by way of the clip's own standing start.
        /// </summary>
        public const float ClipEngageShareOfWindUp = 0.6f;

        public const float ClipReleaseSeconds = 0.2f;

        /// <summary>How long the next attack's clip takes to take over from one still on the body (a chain's next cut, a light turned into the heavy), seconds.</summary>
        public const float ClipCrossfadeSeconds = 0.12f;

        /// <summary>How long the legs take to go from the clip's stride to the walk's and back, seconds, as the body moves off or comes to a stand mid-swing.</summary>
        public const float ClipLegsSeconds = 0.15f;

        private BakedMotion _motion;
        private BakedPose _pose;
        private BakedPose _crossfadeFrom;
        private float _crossfadeRemaining;
        private SwordStrikeKind _clipKind;
        private float _clipSeconds;
        private float _clipWeight;
        private float _clipLegs = 1.0f;
        private bool _walkedUnderClip;
        private float _clipEngageSeconds = ClipEngageSeconds;
        private bool _clipPlaying;
        private bool _holdsBody;
        private Vector3 _beganAt;

        /// <summary>0 to 1: how much of the body an attack's clip holds.</summary>
        public float ClipWeight => _clipWeight;

        /// <summary>0 to 1: how much of the clip the legs have (1 standing, 0 on the move: the walk's or the run's own).</summary>
        public float ClipLegs => _clipLegs;

        /// <summary>An attack is in progress.</summary>
        public bool IsStriking => _striking;

        /// <summary>A guard is being held.</summary>
        public bool IsGuarding => _guarding;

        /// <summary>The blade is being thrown back off a guard (<see cref="SwordStrikes.Rebound1HName"/>).</summary>
        public bool IsRebounding => _rebounding;

        /// <summary>The rebound comes off the cut that struck the guard this quickly, seconds: a bounce is sudden.</summary>
        public const float ReboundTakeoverSeconds = 0.06f;

        /// <summary>
        /// An attack or a rebound that has run out settles into the ready stance over this long, seconds, from wherever it ended: the stance
        /// holds the arm its own way (the elbow low, the wrist cocked, the free hand left to the clips), and switching to it at once jumped.
        /// </summary>
        public const float SettleSeconds = 0.15f;

        /// <summary>The sword is being carried (no attack, guard or ready stance): a two-handed one posed (<see cref="SwordStrikes.Carry"/>), a one-handed one by its own clips.</summary>
        public bool IsCarrying => _carrying && !_striking && !_guarding && _readyRemaining <= 0.0f;

        /// <summary>The carry is a pose laid over the clips (a two-handed sword); a one-handed sword's carry is its clips' own.</summary>
        private bool CarryIsPosed => _carrying && !_carryOneHanded;

        public SwordStrikeKind Kind => _kind;

        /// <summary>Which hold the stance is for (the last attack's or guard's).</summary>
        public bool OneHanded => _oneHanded;

        /// <summary>0 to 1 through the attack in progress.</summary>
        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 0.0f;

        /// <summary>0 to 1: how fully the arms and torso are in the pose (0 means the clip's own).</summary>
        public float Engage => _engage;

        /// <summary>The clock's rate: 1 normally, 0 held still by a hit-stop.</summary>
        public float Speed => _speed;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.ClearSwordPose();
            }
            if (_trail != null)
            {
                _trail.Stop();
            }
            LetGoOfBody();
        }

        // ---- what a swing shows and sounds like ------------------------------------------------------------------------------------------
        // Here rather than in the combat because this runs on every body the attack plays on: the owner's and each other player's copy.

        private BladeTrail _trail;
        private MeleeAttacker _melee;
        private bool _lookedForTrail;
        private bool _whooshed;

        /// <summary>The light trailing from the blade (null until the first live window on a body that has a blade and the streak material).</summary>
        public BladeTrail Trail => _trail;

        /// <summary>True while the blade is live in the attack in progress: the stretch the trail is drawn for.</summary>
        public bool BladeIsLive => _striking && Progress >= SwordStrikes.ActiveStart(_kind) && Progress < SwordStrikes.ActiveEnd(_kind);

        /// <summary>
        /// The light trailing from the blade while it is live (the sword spin's streak, <see cref="BladeTrail"/>), and the swish of it leaving
        /// the hold: a cut is seen and heard, not inferred from the arm. The research has smears and trails on the strike only, never the wind-up
        /// (slynyrd, "Melee attacks"), and a swing's sound carrying its weight as much as its picture does.
        /// </summary>
        private void ShowTheCut(ISwordBlade held)
        {
            if (_striking && !_whooshed && Progress >= SwordStrikes.StrikeStart(_kind))
            {
                _whooshed = true;
                Vector3 point = held != null ? (held.BladeBaseWorld + held.BladeTipWorld) * 0.5f : transform.position + Vector3.up * 1.5f;
                SwingWhoosh.Play(SwordStrikes.SoundsHeavy(_kind), point);
            }

            bool live = BladeIsLive;
            if (!live)
            {
                if (_trail != null && _trail.IsEmitting)
                {
                    _trail.Stop();
                }
                return;
            }
            if (_trail != null && _trail.IsEmitting)
            {
                return;
            }
            if (!_lookedForTrail)
            {
                _lookedForTrail = true;
                _melee = GetComponent<MeleeAttacker>();
                _trail = GetComponent<BladeTrail>();   // shared with the spin and Octaslash; not ??, a missing component is a Unity "fake null"
            }
            Material streak = _melee != null ? _melee.SpinSlashMaterial : null;
            if (held == null || streak == null)
            {
                return;
            }
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<BladeTrail>();
            }
            _trail.Begin(held, streak);
        }

        /// <summary>
        /// An attack starts (an earlier one still going is replaced: the chain's next cut). It takes over from the pose the body is in (the last
        /// one's recovery, a guard, the carry) across its own wind-up and hold, so the body is wholly in the new pose the instant the strike leaves.
        /// </summary>
        public void Begin(SwordStrikeKind kind)
        {
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
            StartTakeover(timing.WindUp + timing.Hold);
            _whooshed = false;
            _kind = kind;
            _oneHanded = SwordStrikes.IsOneHanded(kind);
            _striking = true;
            _guarding = false;
            _elapsed = 0.0f;
            _duration = SwordStrikes.Seconds(kind);
            _speed = 1.0f;
            _readyRemaining = ReadyLingerSeconds;
            BeginClip(kind);
            enabled = true;
        }

        /// <summary>The attack's clip, if it has one, from its start; one still on the body is crossfaded from.</summary>
        private void BeginClip(SwordStrikeKind kind)
        {
            _clipPlaying = SwordStrikes.HasClip(kind);
            if (!_clipPlaying)
            {
                return;
            }
            if (_motion == null)
            {
                _motion = SwordStrikes.Motion;
                _pose = _motion.NewPose();
                _crossfadeFrom = _motion.NewPose();
            }
            _clipEngageSeconds = Mathf.Max(ClipEngageSeconds, SwordStrikes.TimingOf(kind).WindUp * ClipEngageShareOfWindUp);
            if (_clipWeight > 0.0f)
            {
                _crossfadeFrom.CopyBodyFrom(_pose);
                _crossfadeRemaining = ClipCrossfadeSeconds;
                _clipEngageSeconds = ClipEngageSeconds;
            }
            else
            {
                // From no clip: the legs start as the body is, standing or on the move, not by way of the other.
                _clipLegs = SwordStrikes.ClipLegs(kind, _locomotion != null ? _locomotion.OwnGroundSpeed : 0.0f);
            }
            _clipKind = kind;
            _clipSeconds = 0.0f;
            _beganAt = transform.position;
            _walkedUnderClip = false;
        }

        /// <summary>This frame of the attack's clip on the body, or the body let go of once the clip has faded out.</summary>
        private void ShowClip(float deltaTime)
        {
            if (_holdsBody && !_locomotion.HoldsBakedPose(_pose))
            {
                // Another move has taken the body (the baked pose is one slot: the spin, the Octaslash): the clip is over.
                _holdsBody = false;
                _clipPlaying = false;
                _clipWeight = 0.0f;
                _locomotion.SetClipStride(1.0f);
            }
            bool playing = _clipPlaying && _striking;
            float seconds = playing ? _clipEngageSeconds : _rebounding ? ReboundTakeoverSeconds : ClipReleaseSeconds;
            _clipWeight = Mathf.MoveTowards(_clipWeight, playing ? 1.0f : 0.0f, deltaTime / seconds);
            if (_clipWeight <= 0.0f || _pose == null)
            {
                LetGoOfBody();
                return;
            }
            if (playing)
            {
                _clipSeconds = _elapsed;
            }
            // An attack that is over holds the frame it reached (its last, standing ready, when it ran out) while it hands the body back.
            _motion.Sample(SwordStrikes.NameOf(_clipKind), _clipSeconds, false, _pose);
            if (_crossfadeRemaining > 0.0f)
            {
                _crossfadeRemaining -= deltaTime;
                _pose.MixBodyFrom(_crossfadeFrom, Mathf.SmoothStep(0.0f, 1.0f, 1.0f - _crossfadeRemaining / ClipCrossfadeSeconds));
            }
            _holdsBody = true;
            _clipLegs = Mathf.MoveTowards(_clipLegs, SwordStrikes.ClipLegs(_clipKind, _locomotion.OwnGroundSpeed), deltaTime / ClipLegsSeconds);
            _pose.LegsWeight = _clipLegs;
            _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _clipWeight));
            // (Only a body that stands is stepped by the attack: on the move the keys carry it, and there is no stride of the clip's to draw in.)
            _walkedUnderClip |= _clipLegs < 1.0f;
            _locomotion.SetClipStride(_walkedUnderClip ? 1.0f : _locomotion.StrideShareSince(_beganAt, SwordStrikes.Stepped(_clipKind, _clipSeconds)));
        }

        /// <summary>Lets go of the body, if the clip has it: the baked pose is one slot, and a spin or a reload may have it next.</summary>
        private void LetGoOfBody()
        {
            _clipWeight = 0.0f;
            if (_holdsBody && _locomotion != null)
            {
                if (_locomotion.HoldsBakedPose(_pose))
                {
                    _locomotion.SetBakedPose(null, 0.0f);
                }
                _locomotion.SetClipStride(1.0f);
            }
            _holdsBody = false;
        }

        /// <summary>A guard is raised and held until <see cref="Release"/> or <see cref="Cancel"/>.</summary>
        public void BeginGuard(bool oneHanded)
        {
            StartTakeover(GuardTakeoverSeconds);
            _oneHanded = oneHanded;
            _striking = false;
            _clipPlaying = false;
            _guarding = true;
            _speed = 1.0f;
            _readyRemaining = ReadyLingerSeconds;
            enabled = true;
        }

        /// <summary>
        /// The blade bounced off a guard: stopped dead on it for an instant (a blow must be seen to land: it used to fly off the very next frame), thrown back, held
        /// open, gathered into the ready stance (<see cref="SwordStrikes.Rebound"/>), inside the same <see cref="SwordStrikes.ReboundSeconds"/>. The whole body holds too.
        /// </summary>
        public void BeginRebound(bool oneHanded)
        {
            StartTakeover(ReboundTakeoverSeconds);
            _oneHanded = oneHanded;
            _striking = false;
            _clipPlaying = false;
            _guarding = false;
            _rebounding = true;
            _elapsed = 0.0f;
            _duration = SwordStrikes.ReboundSeconds - BlockDeflectEffect.ClashFreezeSeconds;
            _reboundHoldRemaining = BlockDeflectEffect.ClashFreezeSeconds;
            _reboundFrom = _hasLastFrame ? _lastFrame : SwordStrikes.Ready(oneHanded);
            _locomotion?.ImpactFreeze(BlockDeflectEffect.ClashFreezeSeconds);
            _speed = 1.0f;
            _readyRemaining = ReadyLingerSeconds;
            enabled = true;
        }

        /// <summary>How much of the hold the blade takes to get from where it struck to stopped on the guard: a frame or two, under the flash.</summary>
        private const float StopShareOfHold = 0.3f;

        private float _reboundHoldRemaining;
        private SwordFrame _reboundFrom;
        private float _guardJoltDelay;
        private float _guardJoltRemaining;

        /// <summary>A held guard is being knocked back by a blow.</summary>
        public bool IsGuardJolting => _guardJoltRemaining > 0.0f;

        /// <summary>A blow struck the held guard: it is knocked back after <paramref name="delaySeconds"/> (the body held still by the impact) and comes back. Nothing without a sword's guard up.</summary>
        public void GuardJolt(float delaySeconds)
        {
            if (!_guarding)
            {
                return;
            }
            _guardJoltDelay = delaySeconds;
            _guardJoltRemaining = SwordStrikes.GuardJoltSeconds;
            enabled = true;
        }

        /// <summary>How long the parry flourish takes, seconds: the guard snapped forward and back.</summary>
        public const float ParrySeconds = 0.22f;

        private float _parryRemaining;

        /// <summary>True while the parry flourish plays.</summary>
        public bool IsParrying => _parryRemaining > 0.0f;

        /// <summary>The guard caught a blow in its first instant: the sword snaps forward into it and back (on top of the held guard).</summary>
        public void Parry()
        {
            _parryRemaining = ParrySeconds;
            enabled = true;
        }

        /// <summary>Ends the attack or guard in progress; the ready stance lingers (a swing's end, a guard lowered).</summary>
        public void Release()
        {
            if (_rebounding)
            {
                return;   // the rebound plays out (the swing it came off ends as it starts), then lowers into the ready stance itself
            }
            if (_striking || _guarding)
            {
                StartTakeover(ReleaseTakeoverSeconds);
            }
            _striking = false;
            _clipPlaying = false;
            _guarding = false;
            _speed = 1.0f;
        }

        /// <summary>Ends everything and drops the stance at once (a ragdoll, a climb, the sword put away). The carry comes back by itself.</summary>
        public void Cancel()
        {
            _striking = false;
            _clipPlaying = false;
            _guarding = false;
            _rebounding = false;
            _speed = 1.0f;
            _readyRemaining = 0.0f;
            _takeoverRemaining = 0.0f;
            LetGoOfBody();
        }

        /// <summary>The next pose starts from the one the body is actually in and blends into its own keys, rather than snapping.</summary>
        private void StartTakeover(float seconds)
        {
            if (_hasLastFrame && _engage > 0.5f && seconds > 0.0f)
            {
                _takeoverFrom = _lastFrame;
                _takeoverSeconds = seconds;
                _takeoverRemaining = seconds;
                _fromClipSeconds = 0.0f;
            }
            else
            {
                // Nothing posed to take over from (a one-handed sword's clips carry it): the pose's weight comes up over the same time instead.
                _takeoverRemaining = 0.0f;
                _fromClipSeconds = seconds;
            }
        }

        /// <summary>Sets the attack clock's rate: 0 freezes the pose (a hit-stop), 1 runs it.</summary>
        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.0f, float.IsNaN(speed) ? 1.0f : speed);
        }

        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private Ragdoll _ragdoll;
        private GrappleSwing _grapple;
        private bool _looked;

        /// <summary>
        /// Something else owns the body: a climb or vault, a ragdoll, a grapple, a seat, the sword spin or another baked move (not this
        /// attack's own clip), or a clip that moves the whole body (a dance, push-ups, a spell's). The pose yields to all of them.
        /// </summary>
        private bool BodyIsBusy()
        {
            if (!_looked)
            {
                _looked = true;
                _climbing = GetComponent<PlayerClimbing>();
                _vaulting = GetComponent<PlayerVaulting>();
                _ragdoll = GetComponent<Ragdoll>();
                _grapple = GetComponent<GrappleSwing>();
            }
            return (_climbing != null && (_climbing.IsClimbing || _climbing.IsToppingOut))
                || (_vaulting != null && _vaulting.IsVaulting)
                || (_ragdoll != null && _ragdoll.IsLimp)
                || (_grapple != null && _grapple.IsActive)
                || (_locomotion != null && (_locomotion.IsSitting || _locomotion.InCarSeat
                    || (!_holdsBody && _locomotion.BakedPoseWeight > 0.01f) || _locomotion.ClipOwnsTheBody));
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the locomotion this frame's pose. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            ISwordBlade held = _locomotion != null ? _locomotion.HeldSword : null;
            bool busy = BodyIsBusy();
            if (busy && (_striking || _guarding || _rebounding || _readyRemaining > 0.0f || _engage > 0.0f))
            {
                Cancel();
                _engage = 0.0f;
            }
            _carrying = held != null && !busy;
            if (_carrying)
            {
                _carryOneHanded = held.IsOneHanded;
            }

            float clock = deltaTime * _speed;
            bool stoppedOnGuard = _rebounding && _reboundHoldRemaining > 0.0f;
            if (stoppedOnGuard)
            {
                _reboundHoldRemaining -= deltaTime;
                clock = 0.0f;
            }
            if (_striking || _rebounding)
            {
                _elapsed += clock;
                if (_elapsed >= _duration)
                {
                    _striking = false;
                    _rebounding = false;
                    _readyRemaining = ReadyLingerSeconds;
                    StartTakeover(SettleSeconds);
                }
            }
            else if (!_guarding && _readyRemaining > 0.0f)
            {
                _readyRemaining -= deltaTime;
                if (_readyRemaining <= 0.0f && CarryIsPosed)
                {
                    StartTakeover(ReadyToCarrySeconds);   // the guard lowered into the carry, not dropped
                }
            }
            ShowTheCut(held);

            bool fighting = _striking || _guarding || _rebounding || _readyRemaining > 0.0f;
            bool wantsPose = fighting || CarryIsPosed;
            float engageSeconds = wantsPose
                ? (_striking || _guarding ? Mathf.Max(EngageSeconds, _fromClipSeconds) : CarryEngageSeconds)
                : (_carrying ? ReadyToClipCarrySeconds : DisengageSeconds);   // a one-handed stance lowers into its clips' carry
            _engage = Mathf.MoveTowards(_engage, wantsPose ? 1.0f : 0.0f, deltaTime / engageSeconds);

            if (_locomotion == null)
            {
                return;
            }
            ShowClip(deltaTime);
            if (_engage <= 0.001f && !wantsPose)
            {
                _locomotion.ClearSwordPose();
                _hasLastFrame = false;
                return;
            }

            // How fast the body is going (measured, the same on a network copy): the carry trails the blade at a sprint, and walking off lets
            // the ready stance's feet go back to the walk.
            float speed = _locomotion.MeasuredGroundSpeed;
            float rate = 1.0f - Mathf.Exp(-deltaTime / 0.15f);
            _run01 = Mathf.Lerp(_run01, Mathf.InverseLerp(TrailFromSpeed, TrailFullSpeed, speed), rate);
            float stanceTarget = 1.0f - Mathf.InverseLerp(StanceHeldBelowSpeed, StanceGoneAboveSpeed, speed);
            _stanceGate = Mathf.Lerp(_stanceGate, stanceTarget, rate);

            SwordFrame frame;
            if (stoppedOnGuard)
            {
                float into = 1.0f - Mathf.Clamp01(_reboundHoldRemaining / BlockDeflectEffect.ClashFreezeSeconds);
                frame = SwordStrikes.StoppedOnGuard(_reboundFrom, _oneHanded, into / StopShareOfHold);
                // Thrown from here when the hold lets go.
                _takeoverFrom = frame;
                _takeoverSeconds = ReboundTakeoverSeconds;
                _takeoverRemaining = ReboundTakeoverSeconds;
            }
            else if (_rebounding)
            {
                frame = SwordStrikes.Rebound(_oneHanded, Progress);
            }
            else if (_striking)
            {
                frame = SwordStrikes.Evaluate(_kind, Progress);
            }
            else if (_guarding)
            {
                frame = SwordStrikes.Guard(_oneHanded);
                frame.Footwork *= _stanceGate;
            }
            else if (_readyRemaining > 0.0f)
            {
                frame = SwordStrikes.Ready(_oneHanded);
                frame.Footwork *= _stanceGate;
            }
            else if (CarryIsPosed)
            {
                frame = SwordStrikes.Carry(_run01);
            }
            else
            {
                // The sword gone (put away, dropped), or a one-handed sword's stance lowering into its clips' carry: the last pose fades back
                // to the clip rather than jumping to another first.
                frame = _hasLastFrame ? _lastFrame : SwordStrikes.Ready(_oneHanded);
            }
            if (_takeoverRemaining > 0.0f && !stoppedOnGuard)
            {
                _takeoverRemaining -= clock;   // held still by a hit-stop like the attack itself
                float s = 1.0f - Mathf.Clamp01(_takeoverRemaining / Mathf.Max(_takeoverSeconds, 1e-4f));
                frame = SwordStrikes.Lerp(_takeoverFrom, frame, Mathf.SmoothStep(0.0f, 1.0f, s));
            }
            if (_parryRemaining > 0.0f)
            {
                // A pulse: out and back over ParrySeconds, the sword pushed forward and up into the blow, the torso turned into it.
                _parryRemaining -= deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * (1.0f - Mathf.Clamp01(_parryRemaining / ParrySeconds)));
                frame.Hand += new Vector3(0.0f, 0.06f, 0.18f) * pulse;
                frame.TwistDegrees -= 12.0f * pulse;
                frame.LeanDegrees += 6.0f * pulse;
            }
            if (_guardJoltRemaining > 0.0f)
            {
                if (_guardJoltDelay > 0.0f)
                {
                    _guardJoltDelay -= deltaTime;
                }
                else
                {
                    _guardJoltRemaining -= deltaTime;
                    frame = SwordStrikes.GuardJolted(frame, 1.0f - Mathf.Clamp01(_guardJoltRemaining / SwordStrikes.GuardJoltSeconds));
                }
            }
            if (_striking || _rebounding)
            {
                // ON THE MOVE THE LEGS ARE THE RUN'S (owner, 2026-11-13: of swinging while walking and sprinting, "The steps look weird"):
                // the stance the feet step into for a cut and the hips sunk into it are a standing fighter's. Under a body the keys carry
                // they planted the feet wide and slid them along the ground at the body's pace. They fade as the body moves off, as the
                // clip's own legs do.
                float standing = _clipPlaying ? _clipLegs : SwordStrikes.StandingShare(_locomotion.OwnGroundSpeed);
                frame.Footwork *= standing;
                frame.HipDrop *= standing;
            }
            _lastFrame = frame;
            _hasLastFrame = true;
            frame.Weight = Mathf.SmoothStep(0.0f, 1.0f, _engage);
            _locomotion.SetSwordPose(frame);
        }
    }
}
