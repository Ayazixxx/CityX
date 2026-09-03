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
    public sealed class MeleeAttacker : MonoBehaviour
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
        /// </summary>
        public float HeavySwingSeconds = 0.55f;

        [Tooltip("Fraction into the swing at which the blow actually lands.")]
        public float ContactFraction = 0.45f;

        [Header("Sword Spin Attack")]
        [Tooltip("How long the whole spin lasts, in seconds.")]
        public float SpinAttackSeconds = 1.2f;

        [Tooltip("How fast the body spins during the attack, in degrees per second.")]
        public float SpinAttackDegreesPerSecond = 720.0f;

        [Tooltip("How often, in seconds, a spinning blade actually lands a hit -- several landings per use, not one.")]
        public float SpinAttackDamageIntervalSeconds = 0.3f;

        [Tooltip("Cooldown, counted from the moment a spin starts, before another can begin.")]
        public float SpinAttackCooldownSeconds = 4.0f;

        /// <summary>
        /// Scaled down from an ordinary sword swing -- a full spin lands
        /// several hits (SpinAttackSeconds / SpinAttackDamageIntervalSeconds
        /// worth), so per-hit damage has to give something back for that or
        /// spamming 2 would strictly outclass the ordinary combo.
        /// </summary>
        public float SpinAttackDamageScale = 0.6f;

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

        private float _spinRemaining;
        private float _spinCooldownRemaining;
        private float _spinDamageTimer;

        /// <summary>
        /// Reuses sword_attack's own clip rather than inventing a new one --
        /// there is no dedicated spin animation asset, and retriggering a
        /// real swing clip every SpinAttackDamageIntervalSeconds while the
        /// body itself physically rotates (see Tick) is what reads as
        /// "spinning with the sword" using only what already exists.
        /// </summary>
        private static readonly ComboStage SpinAttackStage =
            new ComboStage("sword_attack", 1.0f, 1.0f, 0.0f, false);

        /// <summary>
        /// The swing duration actually in effect for the swing in progress.
        /// A weapon supplies its own cooldown; jab and heavy both use
        /// SwingSeconds even though heavy's own duration is HeavySwingSeconds
        /// -- that mismatch predates this field and isn't this change's to
        /// fix, so it's preserved bit-for-bit.
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
        public bool IsSwinging => _swingRemaining > 0.0f || _spinRemaining > 0.0f;

        /// <summary>True while the sword's own spin attack is spinning.</summary>
        public bool IsSpinAttacking => _spinRemaining > 0.0f;

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
            if (IsSwinging || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }

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
                _activeSwingSeconds = SwingSeconds;
                _swingRemaining = HeavySwingSeconds;
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
            _activeSwingSeconds = SwingSeconds;
            _swingRemaining = SwingSeconds;
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
                _windowRemaining = SwingSeconds + _currentStage.WindowSeconds;
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
        public bool TrySpinAttack()
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

            _spinRemaining = SpinAttackSeconds;
            _spinDamageTimer = SpinAttackDamageIntervalSeconds;
            // Counted from NOW, covering the spin's own duration plus the
            // cooldown after it -- so the very next TrySpinAttack call
            // (which IsSwinging above already refuses on its own while
            // spinning) still finds a real cooldown waiting once the spin
            // ends, rather than being free to restart the instant
            // _spinRemaining reaches zero.
            _spinCooldownRemaining = SpinAttackSeconds + SpinAttackCooldownSeconds;
            _currentStage = SpinAttackStage;
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

            if (_spinRemaining > 0.0f)
            {
                // WORLD SPACE, not local -- Space.Self would spin around
                // whatever local up the body currently has, which does not
                // matter here since the character always stands upright,
                // but World is the same axis PlayerLocomotion's own facing
                // turn (Quaternion.Euler(0, yaw, 0)) already uses, so a spin
                // reads as the same kind of turn, just much faster and not
                // player-steered.
                transform.Rotate(Vector3.up, SpinAttackDegreesPerSecond * deltaTime, Space.World);

                _spinRemaining -= deltaTime;
                _spinDamageTimer -= deltaTime;
                if (_spinDamageTimer <= 0.0f)
                {
                    _spinDamageTimer += SpinAttackDamageIntervalSeconds;
                    LandSpinBlow();
                    // RETRIGGERS THE SAME SWING CLIP -- a single CrossFade
                    // at the start of a 1.2s spin would play one short swing
                    // and then sit idle in whatever state follows it for
                    // the rest of the rotation. Firing SwingStarted again on
                    // every damage tick keeps a real swing animation
                    // playing in sync with each landed hit for the whole
                    // spin, using the exact same "told by name" listener
                    // PlayerAnimationDriver already has -- no new wiring
                    // needed there at all.
                    SwingStarted?.Invoke(_currentStage);
                }
                if (_spinRemaining <= 0.0f)
                {
                    _spinRemaining = 0.0f;
                }
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

            if (_swingRemaining <= 0.0f)
            {
                return;
            }

            float before = _swingRemaining;
            _swingRemaining -= deltaTime;

            // Contact is a moment inside the swing, not its start or end. Fire
            // it when the timer crosses that point, so a long frame cannot skip
            // it -- a dropped frame swallowing a punch is a miserable bug.
            float contactAt = _activeSwingSeconds * (1.0f - ContactFraction);
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
        private List<Contact> ContactsInReach(bool fullCircle = false)
        {
            var found = new List<Contact>();
            Vector3 origin = SwingOrigin();

            Collider[] hits = Physics.OverlapSphere(origin, Range, TargetLayers,
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

            bool connected = false;
            bool blocked = false;
            Vector3 origin = SwingOrigin();
            foreach (Contact contact in ContactsInReach())
            {
                HitResponse response =
                    contact.Target.ApplyHit(damage, transform.position, hitstun);
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
        /// One landed pulse of the spin attack -- FULL CIRCLE, not the
        /// forward arc LandBlow's own ContactsInReach uses, which is the
        /// entire point of a spin. Deliberately much simpler than LandBlow:
        /// no combo/chain/finisher/window bookkeeping, no glass-punching,
        /// no block handling -- TrySpinAttack's own gate already guarantees
        /// a real EquippedWeapon.MovesetId == ComboChain.Sword every time
        /// this runs, so there is no unarmed/bottle path to branch on
        /// either, unlike LandBlow.
        /// </summary>
        private void LandSpinBlow()
        {
            // BELT AND BRACES: TrySpinAttack already refuses without a
            // sword equipped, but nothing stops the sword being unequipped
            // WHILE the spin it started is still running -- this guard is
            // what stands between that and a null EquippedWeapon.GetMeleeDamage()
            // crash mid-spin.
            if (EquippedWeapon == null)
            {
                return;
            }
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage = CombatTuning.DamageFor(EquippedWeapon.GetMeleeDamage() * SpinAttackDamageScale, strength);

            bool connected = false;
            Vector3 origin = SwingOrigin();
            foreach (Contact contact in ContactsInReach(fullCircle: true))
            {
                HitResponse response = contact.Target.ApplyHit(damage, transform.position, CombatTuning.HitstunSeconds);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }

                Landed?.Invoke(contact.Target, damage);
                connected = true;

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
