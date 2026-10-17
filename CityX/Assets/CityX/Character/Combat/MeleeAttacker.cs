using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Swings the attack chain and works out what it hits.
    ///
    /// Ported from player.gd's punch handling plus character.gd's combo state,
    /// which were split across two files in the original because one owned
    /// animation and the other owned damage. Here the chain lives in
    /// <see cref="ComboChain"/> and this drives it, so there is one place that
    /// knows where in a combo you are.
    ///
    /// EVERY RULE IS A FUNCTION OF A DELTA. No coroutines, no invoke, no
    /// waiting on animation events. The chain is the part that made the Godot
    /// build feel good, and the failure modes -- a window that never closes, a
    /// finisher that loops, a swing that never releases (task #292 in the
    /// Godot build, still open) -- are all timing bugs that only show up after
    /// a specific sequence. Those are exactly the bugs a test can catch and a
    /// playthrough usually will not.
    /// </summary>
    public sealed class MeleeAttacker : MonoBehaviour, IReplicatedAbility
    {
        /// <summary>The sword spin (key 2) as an ability other players can see; see IReplicatedAbility. (The ordinary swings go over the animation sync.)</summary>
        public const string SpinAbilityId = "sword-spin";

        string IReplicatedAbility.AbilityId => SpinAbilityId;

        /// <summary>The kinds of event the spin raises: charging began, the turn began (A is 1 for an air spin, B the charge in seconds), it ended.</summary>
        public const int SpinEventCharging = 1;

        public const int SpinEventTurning = 2;
        public const int SpinEventEnded = 3;

        /// <summary>Raised on the owner's copy as the sword spin charges, turns and ends. See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        [Header("Reach (ported from player.gd)")]
        [Tooltip("player.gd PUNCH_RANGE")]
        public float Range = 2.5f;

        [Tooltip("player.gd PUNCH_ORIGIN_HEIGHT, as a fraction of standing height.")]
        public float OriginHeightFraction = 0.68f;

        [Tooltip("Half-angle of the arc in front that a swing can reach, in degrees.")]
        public float HalfArcDegrees = 60.0f;

        [Header("Timing (ported from player.gd)")]
        [Tooltip("player.gd PUNCH_COOLDOWN_SECONDS -- how long a swing occupies the body.")]
        public float SwingSeconds = 0.35f;

        /// <summary>
        /// A haymaker takes longer to throw. That is the trade for 1.5x damage:
        /// without it, holding for a heavy punch would be strictly better than
        /// tapping and there would be no reason ever to jab.
        ///
        /// 0.6s, MATCHING player.gd's OWN HEAVY PUNCH LOCKOUT EXACTLY --
        /// character.gd's _is_punching blocks all movement for the
        /// animation's full length there, and heavy_punch.tres's own baked
        /// length (scratch_mixamo_bake.gd's HEAVY_PUNCH_WINDOW, [0.95,1.55])
        /// is 0.6s, not the 0.35s jab/PUNCH_COOLDOWN_SECONDS figure. The old
        /// 0.55s here was an independently-guessed number that matched
        /// neither Godot figure -- EXPLICIT BUG REPORT: "make the animations
        /// exactly the same they feel different in unity," combat poses/
        /// timing specifically.
        /// </summary>
        public float HeavySwingSeconds = 0.6f;

        [Tooltip("Fraction into the swing at which the blow actually lands.")]
        public float ContactFraction = 0.45f;

        [Header("Sword Spin Attack (Link's Up Special -- see SwordSpinMove and docs/SWORD_SPIN.md)")]
        [Tooltip("How far the blade reaches from the body's middle: the sword (2.1 m) and the arm holding it out.")]
        public float SpinReachMetres = 2.8f;

        [Tooltip("Seconds after one spin before another can begin. Smash has none (the move's own lag is the cost); a short one stops a held key chaining them.")]
        public float SpinAttackCooldownSeconds = 0.4f;

        [Tooltip("Multiplier on every spin blow, on top of SwordSpinMove's own table.")]
        public float SpinAttackDamageScale = 1.0f;

        [Tooltip("How fast the aerial spin starts climbing, in metres per second. With its thinner gravity this carries the body about 4 m up.")]
        public float AirSpinRiseSpeed = 7.0f;

        [Tooltip("Gravity while the aerial spin turns, as a fraction of normal (Smash: 0.55).")]
        public float AirSpinGravityScale = 0.55f;

        [Tooltip("How much of its normal pace the body can steer at while the aerial spin turns (Smash: 0.5).")]
        public float AirSpinMoveFraction = 0.5f;

        [Tooltip("What the aerial spin's last blow throws its target at: upward speed, and the speed away from the spinner.")]
        public float AirSpinLaunchUp = 9.0f;

        public float AirSpinLaunchAway = 3.0f;

        [Tooltip("The glowing swoosh drawn round the body while it spins. Built by SwordSpinTrailBuilder; empty draws none.")]
        public GameObject SpinTrailPrefab;

        [Header("Feedback (ported from player.gd)")]
        [Tooltip("player.gd PunchImpactSounds -- one is picked at random per LANDED hit.")]
        public AudioClip[] ImpactSounds;

        [Tooltip("weapons/blood_splatter.tscn -- one burst per landed hit, at the impact point.")]
        public GameObject BloodPrefab;

        private AudioSource _impactSource;

        [Header("Targets")]
        public LayerMask TargetLayers = ~0;

        /// <summary>
        /// Set by Inventory.EquipItemId (via the weapon's own OnEquip) and
        /// cleared by OnUnequip. Non-null routes TryAttack/LandBlow through
        /// the weapon's own damage and cooldown instead of the unarmed
        /// combo chain -- MeleeAttacker never references a concrete weapon
        /// class, only this interface.
        /// </summary>
        public IEquippableWeapon EquippedWeapon;

        private float _swingRemaining;
        private float _windowRemaining;
        private int _nextStage;
        private bool _contactDone;
        private ComboStage _currentStage;

        private enum SpinState
        {
            None,
            Charging,   // ground only: the key is held, the sword drawn back
            Turning,    // the spin itself, through to when the body is handed back
            Landing,    // came down out of an aerial spin before it was over
        }

        private SpinState _spinState;
        private SpinKind _spinKind;
        private float _spinTime;
        private float _spinChargeSeconds;
        private float _spinChargeMultiplier = 1.0f;
        private float _spinAngleDone;
        private int _nextPulse;
        private float _landingLagRemaining;
        private float _spinCooldownRemaining;
        private bool _airSpinUsed;
        private readonly HashSet<IDamageable> _spinHit = new HashSet<IDamageable>();
        private SwordSpinTrail _trail;

        /// <summary>
        /// THE ANIMATOR IS TOLD TO PLAY "UpperEmpty" when a spin starts, which shows nothing new: the spin's arms are placed by
        /// PlayerLocomotion.ApplySwordSpinPose, not by a clip. What the call is for is PlayerAnimationDriver.PlayState's forced
        /// zero-delta evaluation (an Animator that has never run sits in its bind pose -- see SpinningTheInstantAfterSpawn...) and
        /// for letting go of any swing still playing on the upper body.
        /// </summary>
        private static readonly ComboStage SpinAttackStage =
            new ComboStage(PlayerAnimationDriver.UpperEmptyStateName, 1.0f, 1.0f, 0.0f, false);

        /// <summary>
        /// The swing duration actually in effect for the swing in progress --
        /// what ContactFraction is measured against. FIXED, as part of the
        /// same combat-timing pass HeavySwingSeconds' own doc comment
        /// describes: a heavy punch used to set this to SwingSeconds (0.35)
        /// while _swingRemaining actually counted down from HeavySwingSeconds
        /// (0.55), so contactAt (below) measured 45% of the WRONG, shorter
        /// duration -- the blow actually landed at roughly 65% of the real
        /// swing, not the intended 45%. Now set to whichever duration the
        /// swing in progress is actually using.
        /// </summary>
        private float _activeSwingSeconds;

        /// <summary>Which stage of the chain the next swing would play.</summary>
        public int NextStageIndex => _nextStage;

        /// <summary>
        /// True while a swing OR a spin attack is occupying the body --
        /// PlayerInputReader's own movement gate ("Swinging roots the
        /// body") already reads this one flag, so folding the spin into it
        /// here is what makes a spin attack root you in place too, for
        /// free, matching Smash's own up-special commitment rather than
        /// needing a second gate wired through separately.
        /// </summary>
        public bool IsSwinging => _swingRemaining > 0.0f || _spinState != SpinState.None;

        /// <summary>True from the moment 2 is pressed until the body is handed back: charging, turning or landing.</summary>
        public bool IsSpinAttacking => _spinState != SpinState.None;

        /// <summary>True while a ground spin is being charged (the key held).</summary>
        public bool IsChargingSpin => _spinState == SpinState.Charging;

        /// <summary>True while the spin is actually turning (not charging, not landing).</summary>
        public bool IsSpinTurning => _spinState == SpinState.Turning;

        /// <summary>Which spin is under way (meaningful while <see cref="IsSpinAttacking"/>).</summary>
        public SpinKind CurrentSpinKind => _spinKind;

        /// <summary>How long the spin about to start (or in progress) keeps the body: the ground one's 74 frames, the air one's 91.</summary>
        public float SpinAttackSeconds => SwordSpinMove.CommitSeconds(_spinKind);

        /// <summary>Seconds into the spin that is turning.</summary>
        public float SpinElapsedSeconds => _spinTime;

        /// <summary>
        /// How much of its normal pace the body may move at while this holds it: nothing for an ordinary swing or a ground spin,
        /// half for the whole of an aerial spin (it can be steered a little, as in Smash -- on top of the momentum it keeps).
        /// </summary>
        public float SwingMoveFraction =>
            _spinState == SpinState.Turning && _spinKind == SpinKind.Air
                ? AirSpinMoveFraction
                : UnarmedMoveFraction;

        /// <summary>Where in the swing in progress the blow lands (the contact moment): a weapon's is <see cref="ContactFraction"/>, a fist strike's is its own.</summary>
        private float _activeContact = 0.45f;

        private bool UnarmedSwingInProgress => EquippedWeapon == null && _swingRemaining > 0.0f && _spinState == SpinState.None && FistStrikes.Has(_currentStage.Animation);

        /// <summary>How far through the swing in progress it is, 0 to 1.</summary>
        public float SwingProgress => _activeSwingSeconds > 0.0f && _swingRemaining > 0.0f ? Mathf.Clamp01(1.0f - _swingRemaining / _activeSwingSeconds) : 0.0f;

        /// <summary>
        /// Walking pace kept during an unarmed strike (the legs go on walking under it; only the aerial spin ever kept any before): a fist strike
        /// slows the body, it no longer nails it to the floor.
        /// </summary>
        public float UnarmedMoveFraction =>
            UnarmedSwingInProgress && FistStrikes.TryGet(_currentStage.Animation, out FistKind k) ? FistStrikes.MoveFraction(k) : 0.0f;

        /// <summary>The step INTO the blow, as a share of walking pace, while the fist is still going out; zero after the contact.</summary>
        public float LungeFraction
        {
            get
            {
                if (!UnarmedSwingInProgress || !FistStrikes.TryGet(_currentStage.Animation, out FistKind k))
                {
                    return 0.0f;
                }
                return SwingProgress < FistStrikes.ContactFraction(k) ? FistStrikes.LungeFraction(k) : 0.0f;
            }
        }

        /// <summary>The share of the swing that is only recovery, during which the next punch of a chain may already be thrown (cancelling the rest).</summary>
        public const float ChainCancelFraction = 0.35f;

        /// <summary>A punch pressed during a swing is remembered this long, seconds, and thrown the moment the swing allows.</summary>
        public const float InputBufferSeconds = 0.25f;

        private float _bufferRemaining;
        private bool _bufferedHeavy;

        private bool CanChainNow =>
            EquippedWeapon == null && _spinState == SpinState.None && _contactDone && _swingRemaining > 0.0f
            && _swingRemaining <= _activeSwingSeconds * ChainCancelFraction;

        /// <summary>The stage currently being swung. Meaningless when not swinging.</summary>
        public ComboStage CurrentStage => _currentStage;

        /// <summary>Strength stat, read from vitals when present.</summary>
        private PlayerVitals _vitals;

        private PlayerLocomotion _locomotion;

        /// <summary>Raised when a swing actually connects with something.</summary>
        public event System.Action<IDamageable, float> Landed;

        /// <summary>Raised when a swing starts, with the stage being played.</summary>
        public event System.Action<ComboStage> SwingStarted;

        /// <summary>Raised when a block is thrown, so the animator can show it.</summary>
        public event System.Action BlockThrown;

        /// <summary>
        /// Plays the block pose. No state of its own -- the block WINDOW lives
        /// on the vitals, because that is what has to consult it when a hit
        /// actually arrives. This is only what it looks like.
        /// </summary>
        public void PlayBlock()
        {
            BlockThrown?.Invoke();
        }

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        /// <summary>
        /// Asks to swing. Returns false when the body is busy.
        ///
        /// Advancing versus restarting is decided here rather than by the
        /// caller: the window is this component's state, and letting anyone
        /// else decide is how the two ends of a combo drift apart.
        /// </summary>
        public bool TryAttack(bool heavy = false)
        {
            if (_vitals != null && _vitals.IsDead)
            {
                return false;
            }
            if (IsSwinging && !CanChainNow)
            {
                // Busy: remember the press so it fires the moment the swing lets it (input buffering), instead of being lost.
                if (EquippedWeapon == null && _spinState == SpinState.None)
                {
                    _bufferRemaining = InputBufferSeconds;
                    _bufferedHeavy = heavy;
                }
                return false;
            }
            _activeContact = ContactFraction;

            if (EquippedWeapon != null)
            {
                // A HELD SWING PLAYS THE WEAPON'S OWN HEAVY VARIANT, if it
                // has one -- same "commit, don't advance" shape
                // CombatTuning.HeavyPunch already uses for unarmed.
                if (heavy && EquippedWeapon.HeavyStage.HasValue)
                {
                    _currentStage = EquippedWeapon.HeavyStage.Value;
                    _activeSwingSeconds = EquippedWeapon.MeleeCooldownSeconds;
                    _swingRemaining = _activeSwingSeconds;
                    _contactDone = false;
                    _nextStage = 0;
                    _windowRemaining = 0.0f;
                    SwingStarted?.Invoke(_currentStage);
                    return true;
                }

                string moveset = EquippedWeapon.MovesetId;
                ComboStage[] weaponChain = string.IsNullOrEmpty(moveset)
                    ? System.Array.Empty<ComboStage>()
                    : ComboChain.For(moveset);

                if (weaponChain.Length > 0)
                {
                    // A WEAPON'S OWN CHAIN, same window/advance/finisher
                    // rules the unarmed chain below uses -- see
                    // ComboChain.Sword's own doc comment for why a sword
                    // gets this instead of a flat single swing.
                    if (_windowRemaining <= 0.0f)
                    {
                        _nextStage = 0;
                    }

                    _currentStage = weaponChain[_nextStage];
                    _activeSwingSeconds = EquippedWeapon.MeleeCooldownSeconds;
                    _swingRemaining = _activeSwingSeconds;
                    _contactDone = false;

                    if (_currentStage.IsFinisher)
                    {
                        _nextStage = 0;
                        _windowRemaining = 0.0f;
                    }
                    else
                    {
                        _nextStage++;
                        _windowRemaining = _activeSwingSeconds + _currentStage.WindowSeconds;
                    }

                    SwingStarted?.Invoke(_currentStage);
                    return true;
                }

                // THE FLAT PATH, unchanged from before MovesetId/HeavyStage
                // existed -- one cooldown, one silent swing, no stages. What
                // the bottle still uses.
                _currentStage = default;
                _activeSwingSeconds = EquippedWeapon.MeleeCooldownSeconds;
                _swingRemaining = _activeSwingSeconds;
                _contactDone = false;
                _nextStage = 0;
                _windowRemaining = 0.0f;
                SwingStarted?.Invoke(_currentStage);
                return true;
            }

            // A HAYMAKER IS NOT PART OF THE CHAIN. It is its own committed
            // swing: slower, harder, and it starts the chain over rather than
            // advancing it -- which is what makes holding for one a decision
            // rather than a free upgrade to whatever the next jab was going to
            // be. Godot's own heavy_punch resets _combo_stage_index for the
            // same reason.
            if (heavy)
            {
                _currentStage = CombatTuning.HeavyPunch;
                _activeSwingSeconds = FistStrikes.Seconds(FistKind.Haymaker);
                _swingRemaining = _activeSwingSeconds;
                _activeContact = FistStrikes.ContactFraction(FistKind.Haymaker);
                _contactDone = false;
                _nextStage = 0;
                _windowRemaining = 0.0f;
                SwingStarted?.Invoke(_currentStage);
                return true;
            }

            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            if (chain.Length == 0)
            {
                return false;
            }

            // Out of the window means starting again from the first stage, not
            // continuing where the chain left off.
            if (_windowRemaining <= 0.0f)
            {
                _nextStage = 0;
            }

            _currentStage = chain[_nextStage];
            float chainSeconds = SwingSeconds;
            if (FistStrikes.TryGet(_currentStage.Animation, out FistKind chainKind))
            {
                chainSeconds = FistStrikes.Seconds(chainKind);
                _activeContact = FistStrikes.ContactFraction(chainKind);
            }
            _activeSwingSeconds = chainSeconds;
            _swingRemaining = chainSeconds;
            _contactDone = false;

            // A finisher closes the chain; anything else opens the window for
            // the following stage. Advancing here rather than on contact means
            // a whiffed punch still moves the combo along, which is what the
            // original did and what makes the chain feel continuous.
            if (_currentStage.IsFinisher)
            {
                _nextStage = 0;
                _windowRemaining = 0.0f;
            }
            else
            {
                _nextStage++;
                _windowRemaining = chainSeconds + _currentStage.WindowSeconds;
            }

            SwingStarted?.Invoke(_currentStage);
            return true;
        }

        /// <summary>
        /// Asks to spin. Sword only -- explicit ask: "press 2 to use it.
        /// its like links up special in smash bros he just spins around
        /// with the sword for a bit." Refuses while already swinging/
        /// spinning (IsSwinging covers both) or on cooldown, same shape
        /// TryAttack's own busy-body guard uses.
        /// </summary>
        public bool TrySpinAttack(bool charge = false)
        {
            if (IsSwinging || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }
            if (_spinCooldownRemaining > 0.0f)
            {
                return false;
            }
            if (EquippedWeapon == null || EquippedWeapon.MovesetId != ComboChain.Sword)
            {
                return false;
            }

            // IN THE AIR it goes straight into the rising spin (nothing to charge), once per time off the ground -- a move that climbs,
            // pressed again at the top, would otherwise be a way to fly. On the ground it is the slow strong spin, which waits to be
            // CHARGED when the key is being held (charge: true, then ReleaseSpinAttack) and goes at once when it is only a tap.
            if (IsAirborne())
            {
                if (_airSpinUsed)
                {
                    return false;
                }
                BeginTurning(SpinKind.Air, 0.0f);
                return true;
            }

            _currentStage = SpinAttackStage;
            if (charge)
            {
                _spinState = SpinState.Charging;
                _spinKind = SpinKind.Ground;
                _spinChargeSeconds = 0.0f;
                _spinTime = 0.0f;
                SwingStarted?.Invoke(_currentStage);
                Fired?.Invoke(new AbilityEvent(SpinEventCharging, transform.position, transform.forward));
                return true;
            }
            BeginTurning(SpinKind.Ground, 0.0f);
            return true;
        }

        /// <summary>
        /// The key was let go: a ground spin that was being charged goes now, with whatever it charged. (It also goes by itself when
        /// the charge is full, as Link's does.) Does nothing when no charge is under way.
        /// </summary>
        public void ReleaseSpinAttack()
        {
            if (_spinState != SpinState.Charging)
            {
                return;
            }
            BeginTurning(SpinKind.Ground, _spinChargeSeconds);
        }

        private void BeginTurning(SpinKind kind, float chargeSeconds)
        {
            bool wasCharging = _spinState == SpinState.Charging;
            _spinState = SpinState.Turning;
            _spinKind = kind;
            _spinTime = 0.0f;
            _spinAngleDone = 0.0f;
            _nextPulse = 0;
            _spinHit.Clear();
            _spinChargeMultiplier = SwordSpinMove.ChargeMultiplier(chargeSeconds);
            _currentStage = SpinAttackStage;
            if (!wasCharging)
            {
                SwingStarted?.Invoke(_currentStage);
            }

            if (kind == SpinKind.Air)
            {
                _airSpinUsed = true;
                // Starts climbing at once, from whatever the body was doing (a fall is cut short, as in Smash), under thinner gravity.
                if (_locomotion != null)
                {
                    _locomotion.SetVerticalVelocity(AirSpinRiseSpeed);
                    _locomotion.GravityScale = AirSpinGravityScale;
                    // And keeps going the way it was going, through the spin and the rest of the fall (until it lands -- see Tick).
                    _locomotion.KeepsAirMomentum = true;
                }
            }
            SpinTrail?.SetTarget(1.0f);
            Fired?.Invoke(new AbilityEvent(SpinEventTurning, transform.position, transform.forward,
                kind == SpinKind.Air ? 1.0f : 0.0f, chargeSeconds));
        }

        /// <summary>
        /// Feet off the ground: the controller says so AND there is no ground within a step below. The controller alone flickers
        /// to "not grounded" on stairs and kerbs, which would turn an ordinary spin into a leap.
        /// </summary>
        private bool IsAirborne()
        {
            if (_locomotion == null || _locomotion.IsGrounded)
            {
                return false;
            }
            Vector3 from = transform.position + Vector3.up * 0.4f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, GroundProbe, 0.4f + AirborneClearanceMetres, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!GroundProbe[i].collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>How far above the ground the body must be for a press of 2 to count as an aerial spin.</summary>
        private const float AirborneClearanceMetres = 0.35f;

        private static readonly RaycastHit[] GroundProbe = new RaycastHit[8];

        private SwordSpinTrail SpinTrail
        {
            get
            {
                if (_trail == null && SpinTrailPrefab != null)
                {
                    GameObject made = Instantiate(SpinTrailPrefab, transform);
                    made.name = "SwordSpinTrail";
                    _trail = made.GetComponent<SwordSpinTrail>();
                    if (_trail != null)
                    {
                        _trail.Follow(_locomotion != null ? _locomotion.SwordArmShoulder : null);
                    }
                }
                return _trail;
            }
        }

        /// <summary>
        /// Advances swing and window timers, and lands the blow at the contact
        /// moment. Call once per frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_spinCooldownRemaining > 0.0f)
            {
                _spinCooldownRemaining -= deltaTime;
            }

            if (_locomotion != null && _locomotion.IsGrounded && _spinState != SpinState.Turning)
            {
                _airSpinUsed = false;
                _locomotion.KeepsAirMomentum = false;   // landed: the ordinary ground movement takes back over
            }

            if (_spinState != SpinState.None)
            {
                TickSpin(deltaTime);
                return;
            }

            if (_windowRemaining > 0.0f)
            {
                _windowRemaining -= deltaTime;
                if (_windowRemaining <= 0.0f)
                {
                    _windowRemaining = 0.0f;
                    _nextStage = 0;
                }
            }

            // A punch pressed while busy (remembered by TryAttack) is thrown the moment the swing allows: when it ends, or once it is only recovery.
            if (_bufferRemaining > 0.0f)
            {
                _bufferRemaining -= deltaTime;
                if (_swingRemaining <= 0.0f || CanChainNow)
                {
                    bool heavyBuffered = _bufferedHeavy;
                    _bufferRemaining = 0.0f;
                    TryAttack(heavyBuffered);
                }
            }

            if (_swingRemaining <= 0.0f)
            {
                return;
            }

            float before = _swingRemaining;
            _swingRemaining -= deltaTime * SwingSpeed;   // Melee Speed runs the swing faster (2026-10-14)

            // Contact is a moment inside the swing, not its start or end. Fire
            // it when the timer crosses that point, so a long frame cannot skip
            // it -- a dropped frame swallowing a punch is a miserable bug.
            float contactAt = _activeSwingSeconds * (1.0f - _activeContact);
            if (!_contactDone && before > contactAt && _swingRemaining <= contactAt)
            {
                _contactDone = true;
                LandBlow();
            }

            if (_swingRemaining <= 0.0f)
            {
                _swingRemaining = 0.0f;
                // Belt and braces: if the contact moment was somehow never
                // crossed, land it as the swing ends rather than losing it.
                if (!_contactDone)
                {
                    _contactDone = true;
                    LandBlow();
                }
            }
        }

        /// <summary>After the last blow of an aerial spin, how long gravity takes to come back to normal.</summary>
        private const float AirGravityReturnSeconds = 0.4f;

        /// <summary>How far back the blade is drawn while a ground spin charges, from straight out to the right.</summary>
        private const float ChargeBackDegrees = 70.0f;

        /// <summary>How much longer the arm stays out after the body stops turning, before the pose lets go.</summary>
        private static readonly float GroundPoseLingerSeconds = SwordSpinMove.Frames(8.0f);

        private static readonly float AirPoseLingerSeconds = SwordSpinMove.Frames(12.0f);

        private void SetSpinPose(float target, float backDegrees)
        {
            if (_locomotion != null)
            {
                _locomotion.SetSwordSpinPose(target, backDegrees, 0.0f, EquippedWeapon as ISwordBlade);
            }
        }

        private void TickSpin(float deltaTime)
        {
            if (EquippedWeapon == null || (_vitals != null && _vitals.IsDead))
            {
                EndSpin();
                return;
            }

            if (_spinState == SpinState.Charging)
            {
                _spinChargeSeconds += deltaTime;
                SetSpinPose(1.0f, ChargeBackDegrees);
                if (_spinChargeSeconds >= SwordSpinMove.MaxChargeSeconds || IsAirborne())
                {
                    // Full charge goes by itself, and so does a charge whose ground was lost (an edge, a shove): no standing in the air.
                    BeginTurning(SpinKind.Ground, _spinChargeSeconds);
                }
                return;
            }

            if (_spinState == SpinState.Landing)
            {
                _landingLagRemaining -= deltaTime;
                if (_landingLagRemaining <= 0.0f)
                {
                    EndSpin();
                }
                return;
            }

            float before = _spinTime;
            _spinTime += deltaTime;

            // THE TURN is an exact function of the time (SwordSpinMove.AngleAt), so it ends on a whole number of revolutions facing
            // where it began. WORLD SPACE about up -- the axis PlayerLocomotion's own facing turn uses -- and NEGATIVE, so the sword
            // (out to the right) sweeps forward across the front of the body rather than back across it.
            float angle = SwordSpinMove.AngleAt(_spinKind, _spinTime);
            transform.Rotate(Vector3.up, -(angle - _spinAngleDone), Space.World);
            _spinAngleDone = angle;

            if (_spinKind == SpinKind.Air)
            {
                if (_locomotion != null)
                {
                    // Thin gravity while it turns, then back to normal OVER A MOMENT: snapping it back at the last blow, with the body
                    // still rising, stalled the climb at its top, which read as the spin stopping the body in mid-air.
                    float sinceTurn = _spinTime - SwordSpinMove.AirActiveSeconds;
                    float back = sinceTurn <= 0.0f ? 0.0f : Mathf.SmoothStep(0.0f, 1.0f, sinceTurn / AirGravityReturnSeconds);
                    _locomotion.GravityScale = Mathf.Lerp(AirSpinGravityScale, 1.0f, back);
                    // A ceiling stops the climb outright; otherwise it would keep its speed and press on the roof.
                    if ((_locomotion.LastMoveCollisionFlags & CollisionFlags.Above) != 0 && _locomotion.VerticalVelocity > 0.0f)
                    {
                        _locomotion.SetVerticalVelocity(0.0f);
                    }
                }

                var pulses = SwordSpinMove.AirPulses;
                while (_nextPulse < pulses.Length && _spinTime >= pulses[_nextPulse].Time)
                {
                    LandSpinBlow(pulses[_nextPulse].DamageScale, pulses[_nextPulse].Final, oncePerTarget: false);
                    _nextPulse++;
                }

                // Coming down before the animation is over costs the landing lag.
                if (_locomotion != null && _spinTime > 0.15f && _locomotion.IsGrounded && _locomotion.VerticalVelocity <= 0.0f
                    && _spinTime < SwordSpinMove.AirCommitSeconds)
                {
                    _spinState = SpinState.Landing;
                    _landingLagRemaining = SwordSpinMove.AirLandingLagSeconds;
                    _locomotion.GravityScale = 1.0f;
                    _locomotion.KeepsAirMomentum = false;
                    _locomotion.SetSwordSpinPose(0.0f);
                    _trail?.SetTarget(0.0f);
                    Fired?.Invoke(new AbilityEvent(SpinEventEnded, transform.position, transform.forward));
                    return;
                }
            }
            else if (_spinTime >= SwordSpinMove.GroundHitStart && before <= SwordSpinMove.GroundHitEnd)
            {
                // Every frame of the window: each target is hit once, by whatever the blow is worth at the moment it first connects.
                float sampled = Mathf.Clamp(_spinTime, SwordSpinMove.GroundHitStart, SwordSpinMove.GroundHitEnd);
                LandSpinBlow(SwordSpinMove.GroundDamageScale(sampled) * _spinChargeMultiplier, false, oncePerTarget: true);
            }

            float turnSeconds = SwordSpinMove.TurnSeconds(_spinKind);
            float poseSeconds = turnSeconds + (_spinKind == SpinKind.Air ? AirPoseLingerSeconds : GroundPoseLingerSeconds);
            SetSpinPose(_spinTime < poseSeconds ? 1.0f : 0.0f, 0.0f);
            _trail?.SetTarget(_spinTime < turnSeconds ? 1.0f : 0.0f);

            if (_spinTime >= SwordSpinMove.CommitSeconds(_spinKind))
            {
                EndSpin();
            }
        }

        /// <summary>
        /// A stagger cuts a spin's charge short (2026-10-14: the Stability stat's "harder to interrupt"): it ends without spinning, and other
        /// players see it end. A spin already turning carries on (it is the blow, not the wind-up).
        /// </summary>
        public void InterruptSpinCharge()
        {
            if (_spinState == SpinState.Charging)
            {
                EndSpin();
            }
        }

        private void EndSpin()
        {
            bool wasSpinning = _spinState != SpinState.None;
            _spinState = SpinState.None;
            _landingLagRemaining = 0.0f;
            _spinCooldownRemaining = SpinAttackCooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);   // Cooldown Reduction shortens it
            if (_locomotion != null)
            {
                _locomotion.GravityScale = 1.0f;
                _locomotion.SetSwordSpinPose(0.0f);
            }
            _trail?.SetTarget(0.0f);
            if (wasSpinning)
            {
                Fired?.Invoke(new AbilityEvent(SpinEventEnded, transform.position, transform.forward));
            }
        }

        // ---- the spin on a copy of the body someone else owns ----------------------------------------------------------------
        // The turn and the rise are ordinary movement and arrive with the body's own transform. What does not is the look of it: the arm out
        // to the side and the swoosh. A remote copy runs no input and no Tick, so it plays the same timeline here from the events the owner
        // raised, with no hitting, no cooldown and nothing sent back.

        private SpinState _remoteSpin;
        private SpinKind _remoteKind;
        private float _remoteTime;

        /// <summary>True while this copy of a remote player's body is showing their spin.</summary>
        public bool IsShowingRemoteSpin => _remoteSpin != SpinState.None;

        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case SpinEventCharging:
                    _remoteSpin = SpinState.Charging;
                    _remoteKind = SpinKind.Ground;
                    _remoteTime = 0.0f;
                    break;
                case SpinEventTurning:
                    _remoteSpin = SpinState.Turning;
                    _remoteKind = evt.A > 0.5f ? SpinKind.Air : SpinKind.Ground;
                    _remoteTime = 0.0f;
                    SpinTrail?.SetTarget(1.0f);
                    break;
                case SpinEventEnded:
                    EndRemoteSpin();
                    break;
            }
        }

        private void Update()
        {
            if (_remoteSpin != SpinState.None)
            {
                TickRemoteSpin(Time.deltaTime);
            }
        }

        /// <summary>Advances the remote spin's look by a step; public so a test can drive it without frames.</summary>
        public void TickRemoteSpin(float deltaTime)
        {
            if (_remoteSpin == SpinState.None)
            {
                return;
            }
            _remoteTime += deltaTime;
            if (_remoteSpin == SpinState.Charging)
            {
                // Held up for as long as the owner holds the key (at most a full charge, then it goes by itself); a lost "ended" event cannot strand the pose.
                _locomotion?.SetSwordSpinPose(1.0f, ChargeBackDegrees, 0.0f, null);
                if (_remoteTime > SwordSpinMove.MaxChargeSeconds + 1.0f)
                {
                    EndRemoteSpin();
                }
                return;
            }

            float turnSeconds = SwordSpinMove.TurnSeconds(_remoteKind);
            float poseSeconds = turnSeconds + (_remoteKind == SpinKind.Air ? AirPoseLingerSeconds : GroundPoseLingerSeconds);
            _locomotion?.SetSwordSpinPose(_remoteTime < poseSeconds ? 1.0f : 0.0f, 0.0f, 0.0f, null);
            _trail?.SetTarget(_remoteTime < turnSeconds ? 1.0f : 0.0f);
            if (_remoteTime >= SwordSpinMove.CommitSeconds(_remoteKind))
            {
                EndRemoteSpin();
            }
        }

        private void EndRemoteSpin()
        {
            _remoteSpin = SpinState.None;
            _remoteTime = 0.0f;
            _locomotion?.SetSwordSpinPose(0.0f);
            _trail?.SetTarget(0.0f);
        }

        private void OnDisable()
        {
            if (_spinState != SpinState.None)
            {
                EndSpin();
            }
            if (_locomotion != null)
            {
                _locomotion.KeepsAirMomentum = false;
            }
        }

        /// <summary>
        /// Everything within reach and in front. Public so a test can check
        /// what a swing WOULD hit without swinging.
        /// </summary>
        public List<IDamageable> TargetsInReach()
        {
            var found = new List<IDamageable>();
            foreach (Contact contact in ContactsInReach())
            {
                found.Add(contact.Target);
            }
            return found;
        }

        /// <summary>How far a punch will pull toward a target near where the camera looks, metres.</summary>
        public const float MagnetRadiusMetres = 4.5f;

        /// <summary>A target must be within this many degrees of where the camera looks to be pulled toward.</summary>
        public const float MagnetHalfArcDegrees = 70.0f;

        /// <summary>
        /// PUNCH MAGNETISM, as in the Arkham and Yakuza games and every third-person brawler: a blow is thrown at the enemy NEAR where the camera looks,
        /// not into the empty air a few degrees to one side of him. Finds the living target closest to the camera's aim (within
        /// <see cref="MagnetRadiusMetres"/> and <see cref="MagnetHalfArcDegrees"/>) and gives the yaw (degrees) that faces it; false when there is none.
        /// </summary>
        public bool TryFindMagnetYaw(float cameraYawDegrees, out float yawDegrees)
        {
            yawDegrees = cameraYawDegrees;
            Vector3 origin = SwingOrigin();
            Collider[] hits = Physics.OverlapSphere(origin, MagnetRadiusMetres, TargetLayers, QueryTriggerInteraction.Ignore);
            Vector3 aim = Quaternion.Euler(0.0f, cameraYawDegrees, 0.0f) * Vector3.forward;
            IDamageable self = GetComponent<IDamageable>();
            float bestAngle = MagnetHalfArcDegrees;
            bool found = false;
            foreach (Collider collider in hits)
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || ReferenceEquals(target, self))
                {
                    continue;
                }
                Vector3 toTarget = collider.bounds.center - origin;
                toTarget.y = 0.0f;
                if (toTarget.sqrMagnitude < 0.0004f)
                {
                    continue;
                }
                float angle = Vector3.Angle(aim, toTarget);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    yawDegrees = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Who a swing would hit, and where on them it would land.</summary>
        public readonly struct Contact
        {
            public readonly IDamageable Target;

            /// <summary>
            /// The point on the target's own surface nearest the swing, which
            /// is where blood belongs. The target's centre would put every
            /// spurt inside the body it came out of.
            /// </summary>
            public readonly Vector3 Point;

            public Contact(IDamageable target, Vector3 point)
            {
                Target = target;
                Point = point;
            }
        }

        /// <summary>
        /// <paramref name="fullCircle"/> skips the forward-arc check
        /// entirely -- the spin attack's own whole point, matching Smash's
        /// up-special hitting all around rather than only what an ordinary
        /// swing's HalfArcDegrees would let it reach.
        /// </summary>
        private List<Contact> ContactsInReach(bool fullCircle = false, float reach = -1.0f)
        {
            var found = new List<Contact>();
            Vector3 origin = SwingOrigin();

            Collider[] hits = Physics.OverlapSphere(origin, reach > 0.0f ? reach : Range, TargetLayers,
                QueryTriggerInteraction.Ignore);
            float cosLimit = Mathf.Cos(HalfArcDegrees * Mathf.Deg2Rad);

            foreach (Collider collider in hits)
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive)
                {
                    continue;
                }
                // Do not punch yourself.
                if (ReferenceEquals(target, GetComponent<IDamageable>()))
                {
                    continue;
                }
                // One body can own several colliders; the first one reached is
                // the nearest, so later ones are the same target again.
                if (found.Exists(seen => ReferenceEquals(seen.Target, target)))
                {
                    continue;
                }

                if (!fullCircle)
                {
                    Vector3 toTarget = collider.transform.position - origin;
                    toTarget.y = 0.0f;
                    if (toTarget.sqrMagnitude > 0.0001f &&
                        Vector3.Dot(toTarget.normalized, transform.forward) < cosLimit)
                    {
                        continue;   // behind, or too far round the side
                    }
                }
                found.Add(new Contact(target, collider.ClosestPoint(origin)));
            }

            return found;
        }

        private Vector3 SwingOrigin()
        {
            float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
            return transform.position + Vector3.up * (height * OriginHeightFraction);
        }

        private BloodrageAbility _bloodrage;
        private bool _bloodrageLooked;

        /// <summary>How fast this body's swings run: Melee Speed (1 plus its share; 1 with no vitals). The poses and other players' copies follow it.</summary>
        public float SwingSpeed => PlayerVitals.SpeedOf(_vitals, StatId.MeleeSpeed);

        /// <summary>How much of a target's armour and block chance this body's blows ignore (its Armor Penetration stat, 2026-10-14).</summary>
        private float Penetration => _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;

        /// <summary>The melee damage multiplier from Bloodrage (1 when it is not running or this body has none).</summary>
        public float BloodrageFactor
        {
            get
            {
                if (!_bloodrageLooked)
                {
                    _bloodrage = GetComponent<BloodrageAbility>();
                    _bloodrageLooked = true;
                }
                return _bloodrage != null ? _bloodrage.DamageFactor : 1.0f;
            }
        }

        private void LandBlow()
        {
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage;
            float hitstun;
            if (EquippedWeapon != null)
            {
                // A CHAINED OR HEAVY WEAPON SWING carries a real stage --
                // TryAttack only ever leaves _currentStage at its default
                // (null Animation) for the flat, no-chain path a weapon
                // like the bottle still uses. Checking the stage itself
                // rather than re-deriving "was this chained" from
                // EquippedWeapon again keeps this in exact lockstep with
                // whatever TryAttack actually decided.
                if (!string.IsNullOrEmpty(_currentStage.Animation))
                {
                    damage = CombatTuning.DamageFor(
                        EquippedWeapon.GetMeleeDamage() * _currentStage.DamageScale, strength);
                    hitstun = CombatTuning.HitstunSeconds * _currentStage.HitstunScale;
                }
                else
                {
                    damage = CombatTuning.DamageFor(EquippedWeapon.GetMeleeDamage(), strength);
                    // No weapon-specific figure in the ground truth -- reuses
                    // the fist's own base hitstun rather than inventing one.
                    hitstun = CombatTuning.HitstunSeconds;
                }
            }
            else
            {
                damage = CombatTuning.DamageFor(_currentStage, strength);
                hitstun = CombatTuning.HitstunFor(_currentStage);
            }
            damage *= BloodrageFactor;

            bool connected = false;
            bool blocked = false;
            Vector3 origin = SwingOrigin();
            foreach (Contact contact in ContactsInReach())
            {
                HitResponse response =
                    DamageContext.Strike(contact.Target, damage, transform.position, hitstun, DamageKind.Physical, Penetration);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }

                Landed?.Invoke(contact.Target, damage);
                connected = true;

                // THE KILLING BLOW, DETECTED AFTER THE FACT rather than off
                // the response value -- Civilian.ApplyHit returns Landed
                // whether or not that hit was fatal, only Ignored tells you
                // it was already dead beforehand. So IsAlive flipping false
                // here can only mean this exact swing did it, matching
                // civilian.gd's own _die() granting XP to whoever's attack
                // parameter it was called with.
                if (!contact.Target.IsAlive && contact.Target is IXpReward xpSource)
                {
                    _vitals?.AddXp(xpSource.XpReward);
                }

                if (response == HitResponse.Blocked)
                {
                    blocked = true;
                    continue;
                }

                // Spraying back towards whoever threw the punch, which is what
                // "away from the surface that got hit" means when the surface
                // is a body facing you. Nothing bleeds off a blocked hit.
                BloodSplatter.Spawn(BloodPrefab, contact.Point, origin - contact.Point);

                // THE FINISHER RAGDOLLS INSTEAD OF STUNNING -- see
                // PlayerVitals.TriggerFinisherKnockdown's own doc comment
                // for why this is a direct call rather than threaded
                // through ApplyHit's own hitstunSeconds parameter. A no-op
                // against anything that isn't a real PlayerVitals (a
                // Civilian already ragdolls on any hit at all, unconditionally).
                if (_currentStage.IsFinisher && contact.Target is PlayerVitals targetVitals)
                {
                    targetVitals.TriggerFinisherKnockdown(contact.Point - origin);
                }
            }

            // THE PUNISH. Whoever threw a punch into a timed block is locked out
            // for a second -- which is the actual reward for blocking. Without
            // it a block would just be a worse dodge, still eating a quarter of
            // the damage and giving nothing back.
            if (blocked && _vitals != null)
            {
                _vitals.Stun(CombatTuning.BlockStunSeconds);
                _swingRemaining = 0.0f;
                _windowRemaining = 0.0f;
                _nextStage = 0;
            }

            // ONLY BARE FISTS BREAK WINDOWS -- the ground truth's own bottle-
            // melee description never mentions glass, so a weapon swing
            // skips this rather than punching through a wall it's holding
            // a bottle up to.
            if (EquippedWeapon == null)
            {
                connected |= PunchGlass(origin);
            }

            if (connected)
            {
                PlayImpact();
                EquippedWeapon?.RegisterLandedMeleeHit();
            }
        }

        /// <summary>
        /// Lands ONE hit decided by something other than this component's own
        /// timing -- the soulslike sword combat sweeps a real blade and calls
        /// this per target it touches. Does exactly what a swing's own contact
        /// does for each target (apply the hit, raise Landed, award XP for a
        /// kill, blood, impact sound) and none of the chain/window/block-punish
        /// bookkeeping, so the old swings' behaviour is untouched.
        /// </summary>
        public HitResponse RegisterExternalHit(IDamageable target, Vector3 contactPoint, float damage, float hitstunSeconds)
        {
            if (target == null)
            {
                return HitResponse.Ignored;
            }

            HitResponse response = DamageContext.Strike(target, damage, transform.position, hitstunSeconds, DamageKind.Physical, Penetration);
            if (response == HitResponse.Ignored)
            {
                return response;
            }

            Landed?.Invoke(target, damage);

            if (!target.IsAlive && target is IXpReward xpSource)
            {
                _vitals?.AddXp(xpSource.XpReward);
            }

            if (response != HitResponse.Blocked && response != HitResponse.Guarded)
            {
                BloodSplatter.Spawn(BloodPrefab, contactPoint, SwingOrigin() - contactPoint);
            }
            PlayImpact();
            EquippedWeapon?.RegisterLandedMeleeHit();
            return response;
        }

        /// <summary>
        /// One blow of the spin attack -- FULL CIRCLE, not the forward arc LandBlow's own ContactsInReach uses, which is the entire
        /// point of a spin, and out to the blade's own reach (SpinReachMetres). Deliberately much simpler than LandBlow: no
        /// combo/chain/finisher/window bookkeeping, no glass-punching, no block handling -- TrySpinAttack's own gate already
        /// guarantees a real EquippedWeapon.MovesetId == ComboChain.Sword every time this runs.
        /// </summary>
        /// <param name="scale">Multiplier on the sword's damage for this blow (SwordSpinMove's table, with the charge folded in).</param>
        /// <param name="final">The aerial spin's last blow: launches whatever can be launched.</param>
        /// <param name="oncePerTarget">The ground spin hits each target only once (the first frame it connects is what sets the damage).</param>
        /// <summary>A sword is in hand: what the sword's own abilities (the spin, Octaslash) need.</summary>
        public bool HoldsSword => EquippedWeapon != null && EquippedWeapon.MovesetId == ComboChain.Sword;

        /// <summary>The sword in hand, for a pose that lays the blade (null when it is not one).</summary>
        public ISwordBlade SwordBlade => EquippedWeapon as ISwordBlade;

        /// <summary>
        /// One cut of a sword ability that is not a swing (Octaslash): everything within <paramref name="reach"/> all round the body takes
        /// <paramref name="scale"/> times the sword's damage (Strength and Bloodrage included, as every sword blow), and, if
        /// <paramref name="launch"/> is not zero, whatever can be launched is sent that way. Returns how many it hit. Nothing without a sword.
        /// </summary>
        public int LandSwordCut(float scale, float reach, Vector3 launch)
        {
            if (!HoldsSword || scale <= 0.0f)
            {
                return 0;
            }
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage = CombatTuning.DamageFor(EquippedWeapon.GetMeleeDamage() * scale, strength) * BloodrageFactor;
            Vector3 origin = SwingOrigin();
            int hits = 0;
            foreach (Contact contact in ContactsInReach(fullCircle: true, reach: reach))
            {
                HitResponse response = DamageContext.Strike(contact.Target, damage, transform.position, CombatTuning.HitstunSeconds, DamageKind.Physical, Penetration);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }
                hits++;
                Landed?.Invoke(contact.Target, damage);
                if (launch.sqrMagnitude > 0.0f && contact.Target.IsAlive && contact.Target is ILaunchable launchable)
                {
                    launchable.Launch(launch);
                }
                if (!contact.Target.IsAlive && contact.Target is IXpReward xpSource)
                {
                    _vitals?.AddXp(xpSource.XpReward);
                }
                if (response != HitResponse.Blocked)
                {
                    BloodSplatter.Spawn(BloodPrefab, contact.Point, origin - contact.Point);
                }
            }
            if (hits > 0)
            {
                PlayImpact();
                EquippedWeapon?.RegisterLandedMeleeHit();
            }
            return hits;
        }

        private void LandSpinBlow(float scale, bool final, bool oncePerTarget)
        {
            // BELT AND BRACES: TrySpinAttack already refuses without a
            // sword equipped, but nothing stops the sword being unequipped
            // WHILE the spin it started is still running -- this guard is
            // what stands between that and a null EquippedWeapon.GetMeleeDamage()
            // crash mid-spin.
            if (EquippedWeapon == null || scale <= 0.0f)
            {
                return;
            }
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage = CombatTuning.DamageFor(EquippedWeapon.GetMeleeDamage() * SpinAttackDamageScale * scale, strength) * BloodrageFactor;

            bool connected = false;
            Vector3 origin = SwingOrigin();
            foreach (Contact contact in ContactsInReach(fullCircle: true, reach: SpinReachMetres))
            {
                if (oncePerTarget && _spinHit.Contains(contact.Target))
                {
                    continue;
                }
                HitResponse response = DamageContext.Strike(contact.Target, damage, transform.position, CombatTuning.HitstunSeconds, DamageKind.Physical, Penetration);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }
                if (oncePerTarget)
                {
                    _spinHit.Add(contact.Target);
                }

                Landed?.Invoke(contact.Target, damage);
                connected = true;

                if (final && contact.Target.IsAlive && contact.Target is ILaunchable launchable)
                {
                    Vector3 away = contact.Point - transform.position;
                    away.y = 0.0f;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
                    launchable.Launch(away * AirSpinLaunchAway + Vector3.up * AirSpinLaunchUp);
                }

                if (!contact.Target.IsAlive && contact.Target is IXpReward xpSource)
                {
                    _vitals?.AddXp(xpSource.XpReward);
                }

                if (response == HitResponse.Blocked)
                {
                    continue;
                }

                BloodSplatter.Spawn(BloodPrefab, contact.Point, origin - contact.Point);
            }

            if (connected)
            {
                PlayImpact();
                EquippedWeapon?.RegisterLandedMeleeHit();
            }
        }

        /// <summary>
        /// A window in front of the swing, if there is one.
        ///
        /// A RAY RATHER THAN THE ARC SWEEP the bodies use, and the difference is
        /// the point. A body is a thing you turn towards and hit; a window is
        /// one of a dozen in a wall you are standing at, and the one you meant
        /// is the one you are looking at. A sweep would break whichever pane
        /// the physics query happened to return first.
        ///
        /// The chunk is one merged collider for the road, the roofs and every
        /// wall, so the ray only says WHERE the wall was touched -- what is at
        /// that point is the city's own question to answer.
        /// </summary>
        private bool PunchGlass(Vector3 origin)
        {
            if (!Physics.Raycast(origin, transform.forward, out RaycastHit hit, Range,
                    TargetLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            var breakable = hit.collider.GetComponentInParent<World.IBreakableSurface>();
            return breakable != null && breakable.TakePunch(hit.point);
        }

        /// <summary>
        /// One of player.gd's three punch impact samples, picked fresh per hit.
        ///
        /// ONLY WHEN THE SWING CONNECTS. A whiff stays silent, which is
        /// player.gd's own rule and is most of what makes the sound read as
        /// impact rather than as effort -- a swing that always clicks tells
        /// you nothing about whether you hit anything.
        ///
        /// Picked at random each time rather than cycling, so spamming punches
        /// does not loop the same sample.
        /// </summary>
        private void PlayImpact()
        {
            if (ImpactSounds == null || ImpactSounds.Length == 0)
            {
                return;
            }

            AudioClip clip = ImpactSounds[Random.Range(0, ImpactSounds.Length)];
            if (clip == null)
            {
                return;
            }

            if (_impactSource == null)
            {
                _impactSource = gameObject.AddComponent<AudioSource>();
                // Flat rather than positional: it is the player's own hit
                // landing, not a thing happening somewhere in the world.
                _impactSource.spatialBlend = 0.0f;
                _impactSource.playOnAwake = false;
            }
            _impactSource.PlayOneShot(clip);
        }
    }
}
