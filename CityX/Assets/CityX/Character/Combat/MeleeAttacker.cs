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
    public sealed partial class MeleeAttacker : MonoBehaviour, IReplicatedAbility
    {
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
        /// neither Godot figure -- Bug report: "make the animations
        /// exactly the same they feel different in unity," combat poses/
        /// timing specifically.
        /// </summary>
        public float HeavySwingSeconds = 0.6f;

        [Tooltip("Fraction into the swing at which the blow actually lands.")]
        public float ContactFraction = 0.45f;

        [Header("Feedback (ported from player.gd)")]
        [Tooltip("player.gd PunchImpactSounds -- one is picked at random per LANDED hit.")]
        public AudioClip[] ImpactSounds;

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
        /// swing's HalfArcDegrees would let it reach. <paramref name="halfArc"/>
        /// narrows or widens the arc for one blow (a thrust's is narrow).
        /// </summary>
        private List<Contact> ContactsInReach(bool fullCircle = false, float reach = -1.0f, float halfArc = -1.0f)
        {
            var found = new List<Contact>();
            Vector3 origin = SwingOrigin();

            Collider[] hits = Physics.OverlapSphere(origin, reach > 0.0f ? reach : Range, TargetLayers,
                QueryTriggerInteraction.Ignore);
            float cosLimit = Mathf.Cos((halfArc > 0.0f ? halfArc : HalfArcDegrees) * Mathf.Deg2Rad);

            foreach (Collider collider in hits)
            {
                var target = collider.GetComponentInParent<IDamageable>();
                // A corpse is still struck while it has something left to take off (docs/DISMEMBERMENT.md); it answers Ignored, so nothing else follows.
                if (target == null || (!target.IsAlive && !Dismemberer.TakesCorpseHits(target)))
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
                HitResponse response = Strike(contact.Target, damage, hitstun, contact.Point);
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

                // A target with vitals of its own (a player, a swordsman) shows its own wound (HitImpactEffects, on every machine); anything
                // else bleeds here, out of the wound away from the fist. Nothing bleeds off a blocked hit, or out of a ghost.
                if (!(contact.Target is PlayerVitals) && !(contact.Target is IBloodless))
                {
                    BloodBurst.Spawn(contact.Point, contact.Point - origin, 1.0f);
                }

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

            HitResponse response = Strike(target, damage, hitstunSeconds, contactPoint);
            if (response == HitResponse.Ignored)
            {
                return response;
            }

            Landed?.Invoke(target, damage);

            if (!target.IsAlive && target is IXpReward xpSource)
            {
                _vitals?.AddXp(xpSource.XpReward);
            }

            if (response != HitResponse.Blocked && response != HitResponse.Guarded && !(target is PlayerVitals) && !(target is IBloodless))
            {
                BloodBurst.Spawn(contactPoint, contactPoint - SwingOrigin(), 1.0f);
            }
            PlayImpact();
            EquippedWeapon?.RegisterLandedMeleeHit();
            return response;
        }

        /// <summary>A sword is in hand: what the sword's own abilities (the spin, Octaslash) need.</summary>
        public bool HoldsSword => EquippedWeapon != null && EquippedWeapon.MovesetId == ComboChain.Sword;

        /// <summary>The weapon in hand has an edge: its blows cut (docs/DISMEMBERMENT.md). The bat and the crowbar hold like a sword but do not.</summary>
        public bool WieldsEdge => EquippedWeapon != null && EquippedWeapon.IsEdged;

        /// <summary>One blow of this body's on <paramref name="target"/>: physical, with its penetration, where it landed, and its weapon's edge.</summary>
        private HitResponse Strike(IDamageable target, float damage, float hitstunSeconds, Vector3 contactPoint)
        {
            using (DamageContext.Cutting(WieldsEdge))
            {
                return DamageContext.Strike(target, damage, transform.position, hitstunSeconds, DamageKind.Physical, Penetration, contactPoint, gameObject);
            }
        }

        /// <summary>The sword in hand, for a pose that lays the blade (null when it is not one).</summary>
        public ISwordBlade SwordBlade => EquippedWeapon as ISwordBlade;

        /// <summary>
        /// The sword in the right hand, for light to trail from: the equipped one on the player's own body, or, on another player's copy
        /// (which equips nothing), the mounted model hanging from the hand bone. Null without one.
        /// </summary>
        public ISwordBlade BladeInHand()
        {
            if (SwordBlade is Object held && held != null)
            {
                return SwordBlade;
            }
            Animator animator = GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            return hand != null ? hand.GetComponentInChildren<ISwordBlade>() : null;
        }

        /// <summary>
        /// One cut of a sword ability that is not a swing (Octaslash, the stab dash): everything within <paramref name="reach"/> all round the
        /// body, or within <paramref name="halfArcDegrees"/> of straight ahead when that is under 180, takes <paramref name="scale"/> times the
        /// sword's damage (Strength and Bloodrage included, as every sword blow), and, if <paramref name="launch"/> is not zero, whatever can be
        /// launched is sent that way. A target already in <paramref name="once"/> is spared, and one hit is added to it (a move that hurts each
        /// target once). Returns how many it hit. Nothing without a sword.
        /// </summary>
        public int LandSwordCut(float scale, float reach, Vector3 launch, float halfArcDegrees = 180.0f, HashSet<IDamageable> once = null)
        {
            if (!HoldsSword || scale <= 0.0f)
            {
                return 0;
            }
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage = CombatTuning.DamageFor(EquippedWeapon.GetMeleeDamage() * scale, strength) * BloodrageFactor;
            Vector3 origin = SwingOrigin();
            int hits = 0;
            foreach (Contact contact in ContactsInReach(fullCircle: halfArcDegrees >= 180.0f, reach: reach, halfArc: halfArcDegrees))
            {
                if (once != null && once.Contains(contact.Target))
                {
                    continue;
                }
                HitResponse response = Strike(contact.Target, damage, CombatTuning.HitstunSeconds, contact.Point);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }
                hits++;
                once?.Add(contact.Target);
                Landed?.Invoke(contact.Target, damage);
                if (launch.sqrMagnitude > 0.0f && contact.Target.IsAlive && contact.Target is ILaunchable launchable)
                {
                    launchable.Launch(launch);
                }
                if (!contact.Target.IsAlive && contact.Target is IXpReward xpSource)
                {
                    _vitals?.AddXp(xpSource.XpReward);
                }
                if (response != HitResponse.Blocked && !(contact.Target is PlayerVitals) && !(contact.Target is IBloodless))
                {
                    BloodBurst.Spawn(contact.Point, contact.Point - origin, 1.0f);
                }
            }
            if (hits > 0)
            {
                PlayImpact();
                EquippedWeapon?.RegisterLandedMeleeHit();
            }
            return hits;
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
            _impactSource.pitch = Random.Range(0.92f, 1.08f);   // no two hits quite the same
            _impactSource.PlayOneShot(clip);
        }
    }
}
