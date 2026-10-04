using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// One swing of the sword, defined the way a soulslike defines it: how long
    /// it winds up, how long the blade is actually dangerous, how long you are
    /// stuck recovering, and what it costs. Public fields so the /sword console
    /// command can retune any of it live.
    /// </summary>
    [System.Serializable]
    public sealed class SoulsAttack
    {
        [Tooltip("Animator state played for this swing.")]
        public string Animation;

        [Tooltip("Seconds of wind-up before the blade can hurt anything.")]
        public float Startup;

        [Tooltip("Seconds the blade is live (the ACTIVE frames). Each target is hit at most once per swing.")]
        public float Active;

        [Tooltip("Seconds of recovery after the active window, during which you are committed and open.")]
        public float Recovery;

        [Tooltip("Fraction of Recovery that must have passed before a buffered next attack may fire. Lower = snappier chains.")]
        public float ChainAt;

        [Tooltip("Stamina spent to start this swing.")]
        public float StaminaCost;

        [Tooltip("Multiplier on the sword's base damage.")]
        public float DamageScale;

        [Tooltip("Seconds a target is staggered when this lands.")]
        public float HitstunSeconds;

        [Tooltip("Metres the body steps forward across startup+active -- the small lunge every soulslike swing has.")]
        public float LungeMetres;

        [Tooltip("Length in seconds of the Mixamo clip at its own pace (measured -- see SoulsSwordCapture.MeasureClips).")]
        public float ClipSeconds;

        [Tooltip("Where in the clip (0-1) the swing starts, skipping its idle lead-in.")]
        public float ClipStart;

        [Tooltip("Where in the clip (0-1) the swing is cut off. The stretch from ClipStart to ClipEnd is played over Startup+Active+Recovery.")]
        public float ClipEnd;

        public float Total => Startup + Active + Recovery;

        /// <summary>
        /// Playback speed that fits the clip's ClipStart..ClipEnd stretch into
        /// Total seconds, so the animation's strike lands inside the Active
        /// window. 1 when the clip is not described.
        /// </summary>
        public float AnimatorSpeed
        {
            get
            {
                float window = ClipSeconds * (ClipEnd - ClipStart);
                return window > 0.01f && Total > 0.01f ? window / Total : 1.0f;
            }
        }

        /// <summary>Seconds into the clip to start playing from.</summary>
        public float StartSeconds => ClipSeconds * ClipStart;

        public SoulsAttack(string animation, float startup, float active, float recovery, float chainAt,
            float staminaCost, float damageScale, float hitstunSeconds, float lungeMetres,
            float clipSeconds, float clipStart, float clipEnd)
        {
            Animation = animation;
            Startup = startup;
            Active = active;
            Recovery = recovery;
            ChainAt = chainAt;
            StaminaCost = staminaCost;
            DamageScale = damageScale;
            HitstunSeconds = hitstunSeconds;
            LungeMetres = lungeMetres;
            ClipSeconds = clipSeconds;
            ClipStart = clipStart;
            ClipEnd = clipEnd;
        }
    }

    /// <summary>
    /// What the sword combat needs from the sword in hand: where the blade is
    /// right now and how hard it hits. An interface because the Weapons
    /// assembly depends on Character, not the other way round.
    /// </summary>
    public interface ISwordBlade
    {
        /// <summary>Where the blade meets the grip, in world space.</summary>
        Vector3 BladeBaseWorld { get; }

        /// <summary>The blade's tip, in world space.</summary>
        Vector3 BladeTipWorld { get; }

        /// <summary>
        /// The way the cutting edge faces (perpendicular to the blade), in world space, or zero when the blade does not say. The sword
        /// spin rolls the hand to lead with it.
        /// </summary>
        Vector3 EdgeDirectionWorld => Vector3.zero;

        float GetMeleeDamage();
    }

    public enum SoulsPhase
    {
        Idle,
        Startup,
        Active,
        Recovery,
    }

    /// <summary>
    /// The sword, fought the way a soulslike fights it. Added to the body when
    /// a sword is equipped (SwordWeapon.OnEquip); nothing else about the body
    /// changes, and bare fists / the bottle keep MeleeAttacker's own combat.
    ///
    /// THE RULES, from how Dark Souls actually works (see
    /// docs/SOULSLIKE_COMBAT.md for the research and sources):
    ///
    ///  * COMMITMENT. Every swing is Startup -> Active -> Recovery and you
    ///    cannot walk out of it or cancel it; the only ways out are being hit
    ///    (which staggers you) or rolling during Recovery.
    ///  * ACTIVE FRAMES ONLY. The blade hurts only during the Active window,
    ///    and it is a real capsule swept along the actual sword, so a swing
    ///    that visibly misses, misses. Each target is hit once per swing.
    ///  * STAMINA. Every swing, roll and blocked hit spends the shared bar;
    ///    recovery pauses briefly after spending, then refills at 45/s. You may
    ///    swing with any stamina left, even if the cost is more than remains.
    ///  * INPUT BUFFER. An attack pressed early is remembered and fires the
    ///    instant the chain window opens, instead of being dropped.
    ///  * HEAVY vs LIGHT. The heavy has far more startup and recovery, far more
    ///    damage, and a long stagger -- a decision, not an upgrade.
    ///  * HELD BLOCK. Blocking costs stamina per hit absorbed; a hit you cannot
    ///    afford breaks your guard and staggers you wide open.
    ///
    /// EVERY RULE IS A FUNCTION OF A DELTA, like MeleeAttacker: Tick(dt) is the
    /// only thing that advances anything, so all of it is testable without a
    /// scene.
    /// </summary>
    public sealed class SoulsSwordCombat : MonoBehaviour
    {
        // --- the moveset ---------------------------------------------------

        // THE ACTIVE WINDOWS ARE ALIGNED TO WHERE EACH CLIP ACTUALLY STRIKES,
        // measured with SoulsSwordCapture.MeasureClips (the blade tip's speed
        // against clip time), not guessed. The Mixamo clips are long
        // (1.3s / 1.8s / 4.8s) with the real strike buried inside, so each
        // attack plays only a ClipStart..ClipEnd slice of its clip, stretched
        // to Startup+Active+Recovery:
        //   sword_attack   1.27s  strike at 52-62% (tip 26 m/s)   slice 36-85%
        //   sword_attack2  1.83s  double sweep at 40-58% (25-31)  slice 30-85%
        //   sword_charged  4.80s  overhead slam at 43-51% (19-25) slice 10-66%
        [Header("Light chain (left click)")]
        public SoulsAttack Light1 = new SoulsAttack("sword_attack", 0.20f, 0.12f, 0.28f, 0.55f, 22.0f, 1.00f, 0.40f, 0.25f, 1.267f, 0.36f, 0.85f);
        public SoulsAttack Light2 = new SoulsAttack("sword_attack2", 0.16f, 0.28f, 0.41f, 0.55f, 22.0f, 1.05f, 0.40f, 0.25f, 1.833f, 0.30f, 0.85f);
        public SoulsAttack Light3 = new SoulsAttack("sword_attack", 0.27f, 0.16f, 0.37f, 0.60f, 26.0f, 1.30f, 0.55f, 0.35f, 1.267f, 0.36f, 0.85f);

        [Header("Heavy (no key bound yet)")]
        public SoulsAttack Heavy = new SoulsAttack("sword_charged_attack", 0.98f, 0.24f, 0.48f, 0.60f, 40.0f, 2.00f, 0.90f, 0.50f, 4.8f, 0.10f, 0.66f);

        [Header("Feel")]
        [Tooltip("How long an early attack press is remembered. Long enough that a click at the start of a light swing still chains at its window.")]
        public float BufferSeconds = 0.7f;

        [Tooltip("Seconds stamina recovery pauses after spending it on a swing.")]
        public float RegenDelaySeconds = 0.6f;

        [Tooltip("Stamina per second once recovery resumes. Dark Souls 3: 45.")]
        public float StaminaRegenPerSecond = 45.0f;

        [Tooltip("Seconds the swing freezes (animation and timing both) the instant its blade connects. " +
                 "A mocap swing has no contact in it, so without this a hit feels like the blade passed through; " +
                 "a few frames of stillness is what sells the impact. 0 turns it off.")]
        public float HitStopSeconds = 0.07f;

        [Tooltip("A heavy attack's hit-stop is this many times longer.")]
        public float HeavyHitStopMultiplier = 1.8f;

        [Header("Hit volumes (live only during the active frames)")]
        [Tooltip("Radius of the blade capsule swept along the actual sword.")]
        public float BladeRadius = 0.16f;

        [Tooltip("Sweep samples between last frame's blade and this frame's, so a fast swing cannot tunnel through a target.")]
        public int SweepSamples = 3;

        [Tooltip("Metres in front of the body the reach volume extends. The Mixamo swings keep the blade close to the body " +
                 "(the measured tip rarely gets past 1m ahead in a light attack), so the blade alone would miss a target at " +
                 "arm's length; this box in front of the wielder catches it. 0 turns it off (blade only).")]
        public float ReachMetres = 2.2f;

        [Tooltip("Half the width of the reach volume, in metres.")]
        public float ReachHalfWidth = 0.8f;

        [Header("Roll")]
        [Tooltip("Stamina a roll costs.")]
        public float RollStaminaCost = 20.0f;

        [Tooltip("Seconds of invincibility on a roll. Dark Souls 3: 12 frames = 0.4s.")]
        public float RollInvulnerabilitySeconds = 0.35f;

        [Tooltip("Seconds stamina recovery pauses after a roll.")]
        public float RollRegenDelaySeconds = 0.6f;

        [Header("Block")]
        [Tooltip("Stamina lost per point of damage a held block absorbs.")]
        public float BlockStaminaPerDamage = 1.0f;

        [Tooltip("Seconds you are staggered when a hit breaks your guard.")]
        public float GuardBreakStunSeconds = 1.0f;

        [Tooltip("Seconds YOU are staggered when your swing is blocked -- the blade bouncing off.")]
        public float BlockedRecoilSeconds = 0.35f;

        public LayerMask TargetLayers = ~0;

        /// <summary>
        /// Set by the input layer while the body cannot start an action (mid
        /// roll). A request made while locked is still remembered by the
        /// buffer and fires the moment this clears -- an attack pressed during
        /// a roll comes out as the roll ends.
        /// </summary>
        [System.NonSerialized]
        public bool Locked;

        public const string BlockStateName = "SwordBlock";

        /// <summary>
        /// What a swing or a guard blends BACK to when it ends: the upper-body
        /// layer's empty state, which hands the arms back to the base layer's
        /// walk cycle. (It used to be a crossfade to "SwordLocomotion" on the
        /// base layer, which restarted the leg cycle mid-stride.)
        /// </summary>
        public const string IdleStateName = PlayerAnimationDriver.UpperEmptyStateName;

        [Tooltip(
            "Fraction of walking speed while a swing is under way. The swings play on the upper body only, " +
            "so the legs keep walking under them; 0 roots the body for the whole swing, 1 is no penalty.")]
        public float CommittedMoveSpeedFraction = 0.4f;

        // The 2hsw_blocking clip is a 0.5s raise-the-guard-and-lower-it. A
        // held guard replays just its held stretch, so the pose stays up for
        // as long as the button does instead of pulsing back to idle.
        [Header("Guard pose")]
        public float BlockClipSeconds = 0.5f;
        public float BlockHoldFrom = 0.5f;
        public float BlockHoldTo = 0.8f;

        // --- state ---------------------------------------------------------

        private enum Buffered { None, Light, Heavy }

        private ISwordBlade _sword;
        private bool _equipped;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private PlayerAnimationDriver _driver;
        private Animator _animator;
        private CharacterController _controller;

        private SoulsPhase _phase = SoulsPhase.Idle;
        private SoulsAttack _current;
        private float _phaseTime;
        private int _chainIndex = -1;
        private bool _currentIsLight;

        private Buffered _buffered = Buffered.None;
        private float _bufferedYaw;
        private float _bufferRemaining;

        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
        private readonly Collider[] _overlap = new Collider[32];
        private Vector3 _prevBase;
        private Vector3 _prevTip;
        private bool _havePrevBlade;

        private bool _blocking;
        private PlayerLocomotion _locomotion;

        /// <summary>Seconds of hit-stop still to run; the swing's clock and clip are held while it is above zero.</summary>
        private float _hitStopRemaining;

        // --- public surface ------------------------------------------------

        /// <summary>True while a landed hit is holding the swing still.</summary>
        public bool IsInHitStop => _hitStopRemaining > 0.0f;

        public bool IsEquipped => _equipped;
        public SoulsPhase Phase => _phase;

        /// <summary>True from the first frame of a swing until its recovery ends -- the body is rooted.</summary>
        public bool IsCommitted => _phase != SoulsPhase.Idle;

        public bool IsBlocking => _blocking;
        public SoulsAttack CurrentAttack => _current;

        /// <summary>Which light in the chain was last thrown (0-2), or -1 when not mid-chain.</summary>
        public int ChainIndex => _chainIndex;

        /// <summary>Raised with the swing being started.</summary>
        public event System.Action<SoulsAttack> AttackStarted;

        /// <summary>Raised for every target a swing actually damages.</summary>
        public event System.Action<IDamageable> AttackHit;

        /// <summary>Raised when an attack was asked for but there was no stamina to start it.</summary>
        public event System.Action OutOfStamina;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Finds the siblings. Public because EditMode never runs Awake.</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _melee = GetComponent<MeleeAttacker>();
            _driver = GetComponent<PlayerAnimationDriver>();
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _animator = GetComponentInChildren<Animator>();
        }

        public void Equip(ISwordBlade sword)
        {
            Bind();
            _sword = sword;
            _equipped = true;
            if (_vitals != null)
            {
                _vitals.StaminaRegenPerSecondOverride = StaminaRegenPerSecond;
                _vitals.GuardBreakStunSeconds = GuardBreakStunSeconds;
                _vitals.BlockStaminaPerDamage = BlockStaminaPerDamage;
            }
        }

        public void Unequip(ISwordBlade sword)
        {
            if (sword != null && !ReferenceEquals(sword, _sword))
            {
                return;
            }
            Interrupt();
            SetBlockHeld(false);
            _equipped = false;
            _sword = null;
            if (_vitals != null)
            {
                _vitals.StaminaRegenPerSecondOverride = 0.0f;
            }
        }

        // --- requests (input) ---------------------------------------------

        /// <summary>Asks for a light attack. Remembered for BufferSeconds if the body is busy.</summary>
        public void RequestLight(float yawDegrees)
        {
            Buffer(Buffered.Light, yawDegrees);
        }

        /// <summary>Asks for the heavy attack.</summary>
        public void RequestHeavy(float yawDegrees)
        {
            Buffer(Buffered.Heavy, yawDegrees);
        }

        private void Buffer(Buffered what, float yawDegrees)
        {
            if (!_equipped)
            {
                return;
            }
            _buffered = what;
            _bufferedYaw = yawDegrees;
            _bufferRemaining = BufferSeconds;

            // Free to act right now: go this frame instead of waiting for the
            // next Tick, so a click starts the swing on the frame it happened.
            if (_phase == SoulsPhase.Idle && !Locked && _vitals != null && !_vitals.IsStunned && !_vitals.IsDead)
            {
                TryStartBuffered(chained: false);
            }
        }

        /// <summary>
        /// Whether a roll may begin now. Idle or Recovery only: startup and
        /// active frames are the commitment.
        /// </summary>
        public bool CanRoll => _equipped && (_phase == SoulsPhase.Idle || _phase == SoulsPhase.Recovery);

        /// <summary>
        /// Starts a roll: cancels whatever recovery is left (the one exit a
        /// committed swing has), drops a held block and forgets anything queued.
        /// The caller pays the stamina and grants the i-frames.
        /// </summary>
        public void BeginRoll()
        {
            if (_phase != SoulsPhase.Idle)
            {
                EndSwing();
            }
            SetBlockHeld(false);
            _buffered = Buffered.None;
            _bufferRemaining = 0.0f;
        }

        /// <summary>Called every frame with whether the block button is down.</summary>
        public void SetBlockHeld(bool held)
        {
            bool canBlock = held && _equipped && _phase == SoulsPhase.Idle
                && _vitals != null && !_vitals.IsDead && !_vitals.IsStunned
                && !_vitals.IsExhausted && _vitals.Stamina > 0.0f;

            if (canBlock)
            {
                if (!_blocking)
                {
                    _blocking = true;
                    _vitals.BlockDrainsStamina = true;
                    _vitals.BlockStaminaPerDamage = BlockStaminaPerDamage;
                    _vitals.GuardBreakStunSeconds = GuardBreakStunSeconds;
                    PlayBlockPose();
                }
                // Re-raised every frame, so the block lasts exactly as long as
                // the button does rather than timing out after 0.7s.
                _vitals.StartBlock();
                HoldBlockPose();
            }
            else if (_blocking)
            {
                _blocking = false;
                if (_vitals != null)
                {
                    _vitals.EndBlock();
                    _vitals.BlockDrainsStamina = false;
                }
                // Lower the guard now rather than waiting for the clip to run out.
                if (InState(BlockStateName) && _driver != null)
                {
                    _driver.PlayLocalState(IdleStateName);
                }
            }
        }

        private void PlayBlockPose()
        {
            if (_driver != null)
            {
                _driver.PlayLocalState(BlockStateName);
            }
        }

        /// <summary>
        /// Keeps the guard up: if the block state has run to the end of its
        /// held stretch, replays that stretch; if something knocked the body
        /// out of the state entirely, raises the guard again.
        /// </summary>
        private void HoldBlockPose()
        {
            if (_animator == null || _driver == null)
            {
                return;
            }
            if (!InState(BlockStateName))
            {
                PlayBlockPose();
                return;
            }
            // The guard lives on the upper-body layer (see PlayerAnimationDriver.LayerFor).
            int layer = _driver.LayerFor(BlockStateName);
            if (_animator.IsInTransition(layer))
            {
                return;
            }
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(BlockStateName) && info.normalizedTime >= BlockHoldTo)
            {
                _driver.PlayLocalState(BlockStateName, 1.0f, BlockHoldFrom * BlockClipSeconds);
            }
        }

        // --- the state machine --------------------------------------------

        /// <summary>Advances everything by one frame.</summary>
        public void Tick(float deltaTime)
        {
            if (!_equipped)
            {
                return;
            }

            // A disabled CharacterController is how a ragdoll (or a car seat)
            // takes the body over; a swing must not carry on through that.
            bool bodyTakenOver = _controller != null && !_controller.enabled;
            if (bodyTakenOver || (_vitals != null && (_vitals.IsStunned || _vitals.IsDead)))
            {
                // Hit mid-swing: the swing is gone, and so is anything queued.
                // Being staggered is the price of trading blows with no poise.
                Interrupt();
                SetBlockHeld(false);
                return;
            }

            if (_bufferRemaining > 0.0f)
            {
                _bufferRemaining -= deltaTime;
                if (_bufferRemaining <= 0.0f)
                {
                    _buffered = Buffered.None;
                }
            }

            if (_phase == SoulsPhase.Idle)
            {
                if (_buffered != Buffered.None && !Locked)
                {
                    TryStartBuffered(chained: false);
                }
                return;
            }

            // HIT-STOP: a landed hit holds the whole swing still for a few frames -- the clock here AND
            // the clip (stopped in LandOn) -- then lets both go on together, so they cannot drift apart.
            if (_hitStopRemaining > 0.0f)
            {
                _hitStopRemaining -= deltaTime;
                if (_hitStopRemaining > 0.0f)
                {
                    return;
                }
                ReleaseHitStop();
                return;
            }

            _phaseTime += deltaTime;

            // A loop, not an if: a long frame can carry the swing across more
            // than one boundary, and none of them may be skipped (a swallowed
            // active window is a swallowed hit).
            for (int guard = 0; guard < 4; guard++)
            {
                if (_phase == SoulsPhase.Startup)
                {
                    ApplyLunge(deltaTime);
                    if (_phaseTime < _current.Startup)
                    {
                        return;
                    }
                    _phaseTime -= _current.Startup;
                    _phase = SoulsPhase.Active;
                    _hitThisSwing.Clear();
                    _havePrevBlade = false;
                }

                if (_phase == SoulsPhase.Active)
                {
                    ApplyLunge(deltaTime);
                    SweepBlade();
                    if (_phaseTime < _current.Active)
                    {
                        return;
                    }
                    _phaseTime -= _current.Active;
                    _phase = SoulsPhase.Recovery;
                }

                if (_phase == SoulsPhase.Recovery)
                {
                    if (_buffered != Buffered.None && _current.Recovery > 0.0f
                        && _phaseTime >= _current.Recovery * _current.ChainAt)
                    {
                        if (TryStartBuffered(chained: true))
                        {
                            return;
                        }
                    }
                    if (_phaseTime >= _current.Recovery)
                    {
                        EndSwing();
                    }
                    return;
                }
            }
        }

        private bool TryStartBuffered(bool chained)
        {
            Buffered what = _buffered;
            float yaw = _bufferedYaw;
            _buffered = Buffered.None;

            SoulsAttack next;
            bool light;
            if (what == Buffered.Heavy)
            {
                next = Heavy;
                light = false;
            }
            else
            {
                int index = chained && _currentIsLight ? (_chainIndex + 1) % 3 : 0;
                next = index == 0 ? Light1 : (index == 1 ? Light2 : Light3);
                _pendingChainIndex = index;
                light = true;
            }

            if (_vitals != null && !_vitals.TrySpendCombatStamina(next.StaminaCost, RegenDelaySeconds))
            {
                OutOfStamina?.Invoke();
                return false;
            }

            StartSwing(next, light, yaw);
            return true;
        }

        private int _pendingChainIndex;

        private void StartSwing(SoulsAttack attack, bool light, float yawDegrees)
        {
            // A held block is dropped the moment you swing.
            SetBlockHeld(false);
            ReleaseHitStop();

            _current = attack;
            _currentIsLight = light;
            _chainIndex = light ? _pendingChainIndex : -1;
            _phase = SoulsPhase.Startup;
            _phaseTime = 0.0f;
            _hitThisSwing.Clear();
            _havePrevBlade = false;

            // Swing where the camera is looking, not wherever the body last
            // happened to face -- without this a swing goes off at an angle
            // from the crosshair whenever you stopped turning a moment ago.
            transform.rotation = Quaternion.Euler(0.0f, yawDegrees, 0.0f);

            // The clip's strike stretch, stretched to fit this attack's own
            // Startup+Active+Recovery -- announced to the network with the same
            // speed and offset so other players see the same paced swing.
            if (_driver != null)
            {
                _driver.PlayLocalState(attack.Animation, attack.AnimatorSpeed, attack.StartSeconds);
            }
            AttackStarted?.Invoke(attack);
        }

        /// <summary>Lets the swing's clip run again after a hit-stop (a no-op when none is running).</summary>
        private void ReleaseHitStop()
        {
            if (_hitStopRemaining <= 0.0f && !_clipHeld)
            {
                return;
            }
            _hitStopRemaining = 0.0f;
            if (_clipHeld && _driver != null && _current != null)
            {
                _driver.SetStateSpeed(_current.Animation, _current.AnimatorSpeed);
            }
            _clipHeld = false;
        }

        /// <summary>True while the animation clip is stopped for a hit-stop, so it is put back exactly once.</summary>
        private bool _clipHeld;

        private void EndSwing()
        {
            ReleaseHitStop();
            _phase = SoulsPhase.Idle;
            _phaseTime = 0.0f;
            _chainIndex = -1;
            _currentIsLight = false;

            // The swing's clip is cut off at ClipEnd, well before the clip's
            // own exit, so blend out by hand -- and put the animator speed
            // back to 1 (this same call carries speed 1 to other players).
            if (_driver != null)
            {
                _driver.PlayLocalState(IdleStateName);
            }
        }

        /// <summary>Drops the swing in progress and anything queued (a hit, death, unequip).</summary>
        public void Interrupt()
        {
            if (_phase != SoulsPhase.Idle)
            {
                EndSwing();
            }
            _buffered = Buffered.None;
            _bufferRemaining = 0.0f;
        }

        // --- hit detection --------------------------------------------------

        private void SweepBlade()
        {
            Vector3 bladeBase;
            Vector3 bladeTip;
            if (_sword != null)
            {
                bladeBase = _sword.BladeBaseWorld;
                bladeTip = _sword.BladeTipWorld;
            }
            else
            {
                // No sword object (a test, or one destroyed mid-swing): a blade
                // held out in front at chest height, so the rules still work.
                float height = 1.4f;
                bladeBase = transform.position + Vector3.up * height + transform.forward * 0.4f;
                bladeTip = bladeBase + transform.forward * 1.8f;
            }

            if (!_havePrevBlade)
            {
                _prevBase = bladeBase;
                _prevTip = bladeTip;
                _havePrevBlade = true;
            }

            int samples = Mathf.Max(1, SweepSamples);
            for (int i = 1; i <= samples; i++)
            {
                float f = i / (float)samples;
                Vector3 b = Vector3.Lerp(_prevBase, bladeBase, f);
                Vector3 t = Vector3.Lerp(_prevTip, bladeTip, f);
                int count = Physics.OverlapCapsuleNonAlloc(
                    b, t, BladeRadius, _overlap, TargetLayers, QueryTriggerInteraction.Ignore);
                LandOnOverlaps(count, t);
            }

            BreakGlassWithBlade(_prevBase, _prevTip, bladeBase, bladeTip);

            _prevBase = bladeBase;
            _prevTip = bladeTip;

            if (ReachMetres > 0.0f)
            {
                float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
                Vector3 center = transform.position
                    + Vector3.up * (height * 0.5f)
                    + transform.forward * (ReachMetres * 0.5f + 0.15f);
                var halfExtents = new Vector3(ReachHalfWidth, height * 0.5f, ReachMetres * 0.5f);
                int count = Physics.OverlapBoxNonAlloc(
                    center, halfExtents, _overlap, transform.rotation, TargetLayers, QueryTriggerInteraction.Ignore);
                LandOnOverlaps(count, transform.position + Vector3.up * (height * 0.6f));
            }
        }

        /// <summary>
        /// A blade through a window takes the pane out at once -- a HEAVY blow, unlike a fist, which only
        /// cracks glass (the melee-versus-thrown-brick difference in The Last of Us Part II's windows).
        /// Three points along the blade are each traced from where they were last frame to where they are
        /// now, so a fast swing cannot pass through a pane between two frames; only the active frames call
        /// this, so a wind-up brushing a wall breaks nothing.
        /// </summary>
        private void BreakGlassWithBlade(Vector3 prevBase, Vector3 prevTip, Vector3 nowBase, Vector3 nowTip)
        {
            for (int i = 1; i <= 3; i++)
            {
                float along = i / 3.0f;
                Vector3 from = Vector3.Lerp(prevBase, prevTip, along);
                Vector3 travel = Vector3.Lerp(nowBase, nowTip, along) - from;
                float length = travel.magnitude;
                if (length < 0.02f)
                {
                    continue;
                }
                Vector3 direction = travel / length;
                if (Physics.Raycast(from, direction, out RaycastHit hit, length, TargetLayers, QueryTriggerInteraction.Ignore))
                {
                    var breakable = hit.collider.GetComponentInParent<World.IBreakableSurface>();
                    breakable?.TakeBlow(hit.point, direction, World.BlowStrength.Heavy);
                }
            }
        }

        private void LandOnOverlaps(int count, Vector3 contactFrom)
        {
            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlap[i];
                if (collider == null)
                {
                    continue;
                }

                // Never our own body (or its ragdoll) or our own sword.
                if (collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || _hitThisSwing.Contains(target))
                {
                    continue;
                }
                if (ReferenceEquals(target, _vitals))
                {
                    continue;
                }

                _hitThisSwing.Add(target);
                Vector3 point = collider.ClosestPoint(contactFrom);
                LandOn(target, point);
            }
        }

        private void LandOn(IDamageable target, Vector3 point)
        {
            int strength = _vitals != null ? _vitals.Strength : 0;
            float baseDamage = _sword != null ? _sword.GetMeleeDamage() : CombatTuning.PunchDamage;
            float damage = CombatTuning.DamageFor(baseDamage * _current.DamageScale, strength) * (_melee != null ? _melee.BloodrageFactor : 1.0f);

            HitResponse response = _melee != null
                ? _melee.RegisterExternalHit(target, point, damage, _current.HitstunSeconds)
                : target.ApplyHit(damage, transform.position, _current.HitstunSeconds);

            if (response == HitResponse.Ignored)
            {
                return;
            }

            AttackHit?.Invoke(target);
            BeginHitStop();

            if (response == HitResponse.Blocked && _vitals != null)
            {
                // A TIMED block (the classic one): the blade bounces off and
                // you are the one left open. A merely held guard (Guarded)
                // costs the defender stamina instead and does not do this.
                _vitals.Stun(BlockedRecoilSeconds);
            }
        }

        private void BeginHitStop()
        {
            if (HitStopSeconds <= 0.0f || _current == null)
            {
                return;
            }
            float seconds = HitStopSeconds * (_currentIsLight ? 1.0f : HeavyHitStopMultiplier);
            if (seconds <= _hitStopRemaining)
            {
                return;   // several targets in one frame: the longest stop wins, they do not add up
            }
            _hitStopRemaining = seconds;
            if (_driver != null && !_clipHeld)
            {
                _driver.SetStateSpeed(_current.Animation, 0.0f);
                _clipHeld = true;
            }
        }

        private void ApplyLunge(float deltaTime)
        {
            if (_controller == null || _current == null || _current.LungeMetres <= 0.0f)
            {
                return;
            }
            float window = _current.Startup + _current.Active;
            if (window <= 0.0f)
            {
                return;
            }
            Vector3 step = transform.forward * (_current.LungeMetres / window) * deltaTime;
            _controller.Move(step);
        }

        // --- animation ---------------------------------------------------------

        private bool InState(string stateName)
        {
            return _driver != null && _driver.IsPlaying(stateName);
        }

        private void OnDisable()
        {
            Interrupt();
            SetBlockHeld(false);
        }

        // --- live tuning (/sword) -------------------------------------------

        /// <summary>Every tunable, one per line, for the console.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            foreach (FieldInfo field in typeof(SoulsSwordCombat).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float) || field.FieldType == typeof(int))
                {
                    lines.Add($"{field.Name} = {field.GetValue(this)}");
                }
                else if (field.FieldType == typeof(SoulsAttack))
                {
                    var attack = (SoulsAttack)field.GetValue(this);
                    lines.Add($"{field.Name}: startup {attack.Startup} active {attack.Active} recovery {attack.Recovery} " +
                              $"chainAt {attack.ChainAt} cost {attack.StaminaCost} damage {attack.DamageScale} " +
                              $"hitstun {attack.HitstunSeconds} lunge {attack.LungeMetres}");
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Sets a tunable by path: "BufferSeconds", or "Heavy.Startup" for a
        /// field of an attack. Case-insensitive.
        /// </summary>
        public bool TrySet(string path, float value, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(path))
            {
                message = "no name given";
                return false;
            }

            string[] parts = path.Split('.');
            object owner = this;
            FieldInfo field = FindField(owner.GetType(), parts[0]);
            if (field == null)
            {
                message = $"no tunable '{parts[0]}' -- /sword lists them";
                return false;
            }

            if (parts.Length == 2 && field.FieldType == typeof(SoulsAttack))
            {
                owner = field.GetValue(this);
                field = FindField(typeof(SoulsAttack), parts[1]);
                if (field == null)
                {
                    message = $"an attack has no '{parts[1]}'";
                    return false;
                }
            }
            else if (parts.Length != 1)
            {
                message = "use Name or Attack.Field";
                return false;
            }

            if (field.FieldType == typeof(float))
            {
                field.SetValue(owner, value);
            }
            else if (field.FieldType == typeof(int))
            {
                field.SetValue(owner, Mathf.RoundToInt(value));
            }
            else
            {
                message = $"'{field.Name}' is not a number";
                return false;
            }

            // Re-apply anything the vitals were told at equip time.
            if (_equipped && _vitals != null)
            {
                _vitals.StaminaRegenPerSecondOverride = StaminaRegenPerSecond;
                _vitals.GuardBreakStunSeconds = GuardBreakStunSeconds;
                _vitals.BlockStaminaPerDamage = BlockStaminaPerDamage;
            }
            message = $"{path} = {value.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        private static FieldInfo FindField(System.Type type, string name)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            return null;
        }
    }
}
