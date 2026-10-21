using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE SWORDSMAN (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him. make him utilize dashes as
    /// well as blocking and make him take advantage of the stagger if i attack their block", then "make the npc have all the movement animations
    /// that i do"). /spawn swordsman. The player's own body (Swordsman.prefab, built from Player.prefab by SwordsmanBuilder: no input reader),
    /// with the real combat sword equipped as the player equips it, so he fights by the player's own rules -- SoulsSwordCombat's cuts, chain,
    /// heavy, guard and parry, the dash with its moment of invulnerability, the guard rebound (SoulsSwordCombat.GuardReboundSeconds) -- and moves with the
    /// player's own animation: the one-handed sword's walk, run, side-steps and back-step, the sprint, the dash and the guarded walk, locked
    /// on to whoever he fights (LocomotionInput.LockedOn: he faces you and steps round you, the way Tab makes the player). What to press is
    /// DuelistMind's; this hands it to the body each frame, as PlayerInputReader does for the player.
    ///
    /// LOCAL FOR NOW, as the Shoota and the Crimson Claw are: NPCs are not networked yet (docs/GAME_DESIGN.md, "Enemy/NPC sync"), so he exists only
    /// on the machine that spawned him and only fights that machine's own player. What happens to the PLAYER in the fight -- a rebound off his
    /// guard, a hit reaction, a parry -- is the player's own and reaches other players as always.
    /// </summary>
    public sealed class SwordsmanBrain : MonoBehaviour
    {
        [Tooltip("The sword he fights with: the combat sword, equipped at spawn as the player equips it.")]
        public GameObject SwordPrefab;

        [Tooltip("What he wears on his head (the Knight Helmet), put on at spawn as the player puts one on.")]
        public GameObject HelmetPrefab;

        [Tooltip("XP for beating him.")]
        public int XpReward = 80;

        [Tooltip("His dice: 0 picks new ones each time he is spawned; a test sets one to replay the same fight.")]
        public int Seed;

        /// <summary>How long his body lies there once beaten, seconds.</summary>
        public const float CorpseSeconds = 25.0f;

        /// <summary>How often he looks round for who to fight, seconds.</summary>
        public const float TargetSearchSeconds = 1.0f;

        /// <summary>He only takes on someone within this, metres.</summary>
        public const float NoticeMetres = 40.0f;

        /// <summary>What he is doing (the mind's state).</summary>
        public DuelistState State => Mind.State;

        /// <summary>The sword combat he fights with (added when the sword is equipped).</summary>
        public SoulsSwordCombat Combat => _combat;

        /// <summary>How many times he has dashed (for the tests).</summary>
        public int Dashes { get; private set; }

        /// <summary>How many times he has raised his guard (for the tests).</summary>
        public int GuardsRaised { get; private set; }

        /// <summary>Who he is fighting (null when nobody): found by the search, or set by a test.</summary>
        public PlayerVitals Target
        {
            get => _target;
            set
            {
                SetTarget(value);
                _pinned = value != null;
            }
        }

        private DuelistMind _mind;

        /// <summary>His mind, made on first use so a test can set <see cref="Seed"/> after spawning him.</summary>
        private DuelistMind Mind => _mind ?? (_mind = new DuelistMind(Seed != 0 ? Seed : System.Environment.TickCount));
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Ragdoll _ragdoll;
        private PlayerAudio _audio;
        private SoulsSwordCombat _combat;
        private SwordWeapon _sword;
        private PlayerVitals _target;
        private SoulsSwordCombat _targetCombat;
        private MeleeAttacker _targetMelee;
        private PlayerLocomotion _targetBody;
        private bool _pinned;
        private float _nextSearch;

        /// <summary>Until when the target is OPEN to a punish: its blade rebounded off his guard or a parry, or its guard broke (Time.time).</summary>
        private float _targetOpenUntil;
        private SoulsSwordCombat _watchedCombat;
        private PlayerVitals _watchedVitals;
        private bool _hurt;
        private bool _wasBlocking;
        private float _deadFor;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _ragdoll = GetComponent<Ragdoll>();
            _audio = GetComponent<PlayerAudio>();
            if (_vitals != null)
            {
                _vitals.Damaged += OnDamaged;
                _vitals.Died += OnDied;
                _vitals.XpRewardOnDeath = XpReward;
            }
            // The clash of a blow on his guard (sparks and the ting), as on the player's body (PlayerInputReader adds it there).
            if (GetComponent<BlockDeflectEffect>() == null)
            {
                gameObject.AddComponent<BlockDeflectEffect>();
            }
            if (GetComponent<HitImpactEffects>() == null)
            {
                gameObject.AddComponent<HitImpactEffects>();
            }
            EquipSword();
            EquipHelmet();
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.Damaged -= OnDamaged;
                _vitals.Died -= OnDied;
            }
            WatchTarget(null, null);
        }

        /// <summary>
        /// The openings he punishes are the target's own: its blade thrown back off a guard (SoulsSwordCombat.Rebounded: his guard, or his
        /// parry) and its guard broken (PlayerVitals.GuardBroken) -- not a flinch from his own last cut.
        /// </summary>
        private void WatchTarget(SoulsSwordCombat combat, PlayerVitals vitals)
        {
            if (combat != _watchedCombat)
            {
                if (_watchedCombat != null) _watchedCombat.Rebounded -= OnTargetOpened;
                _watchedCombat = combat;
                if (_watchedCombat != null) _watchedCombat.Rebounded += OnTargetOpened;
            }
            if (vitals != _watchedVitals)
            {
                if (_watchedVitals != null) _watchedVitals.GuardBroken -= OnTargetOpened;
                _watchedVitals = vitals;
                if (_watchedVitals != null) _watchedVitals.GuardBroken += OnTargetOpened;
            }
        }

        private void OnTargetOpened()
        {
            float stun = _target != null ? _target.HitstunRemaining : 0.0f;
            _targetOpenUntil = Time.time + Mathf.Max(0.3f, stun);
        }

        private void OnDamaged(float amount)
        {
            _hurt = true;
        }

        /// <summary>
        /// Beaten: his sword falls from his hand for the player to pick up (owner, 2026-10-17). His drop stays on this machine, as he does
        /// (an NPC exists only on the machine that spawned it).
        /// </summary>
        private void OnDied()
        {
            if (_sword == null)
            {
                return;
            }
            var pose = new Pose(_sword.transform.position, _sword.transform.rotation);
            Vector3 bodyVelocity = _locomotion != null ? _locomotion.Velocity : Vector3.zero;
            DroppedItems.Shared.Drop(DroppedItem.LetGo(_sword.ItemId, ItemInstanceData.None, pose, transform, bodyVelocity, _vitals.DeathImpulse),
                shared: false, gameObject, SwordPrefab);
            _sword.OnUnequip(gameObject);
            _sword = null;
        }

        /// <summary>His helmet on his head, worn as the player wears one; it stays on his body when he falls.</summary>
        private void EquipHelmet()
        {
            if (HelmetPrefab == null)
            {
                return;
            }
            GameObject helmet = Instantiate(HelmetPrefab);
            helmet.name = HelmetPrefab.name;
            helmet.GetComponent<IEquippableItem>()?.OnEquip(gameObject, ItemInstanceData.None);
        }

        /// <summary>The combat sword in his right hand, equipped exactly as the player equips one (the hold, the sword combat, the posing).</summary>
        private void EquipSword()
        {
            if (SwordPrefab == null)
            {
                return;
            }
            GameObject sword = Instantiate(SwordPrefab);
            sword.name = SwordPrefab.name;
            _sword = sword.GetComponent<SwordWeapon>();
            if (_sword == null)
            {
                Destroy(sword);
                return;
            }
            _sword.OnEquip(gameObject, ItemInstanceData.None);
            _combat = GetComponent<SoulsSwordCombat>();
        }

        private void Update()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            if (_vitals == null || _locomotion == null)
            {
                return;
            }
            _vitals.TickHitstun(deltaTime);
            _vitals.TickHealthRegen(deltaTime);
            if (_vitals.IsDead)
            {
                Mind.Tick(new DuelistMind.Senses { Dead = true }, deltaTime);
                _combat?.SetBlockHeld(false);
                _deadFor += deltaTime;
                if (_deadFor >= CorpseSeconds)
                {
                    Destroy(gameObject);
                }
                return;
            }

            // Knocked down (a finisher, Self Smite's burst): the ragdoll has him until he gets up, as the player's body does.
            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                _ragdoll.Tick(deltaTime);
                if (_ragdoll.CanStandUp)
                {
                    _ragdoll.BeginStandingUp();
                }
                return;
            }

            FindTarget();
            WatchTarget(_targetCombat, _target);
            _combat?.Tick(deltaTime);

            DuelistMind.Intent intent = Mind.Tick(Sense(), deltaTime);
            _hurt = false;
            Act(intent, deltaTime);
        }

        private DuelistMind.Senses Sense()
        {
            bool has = _target != null && _target.IsAlive;
            var senses = new DuelistMind.Senses
            {
                HasTarget = has,
                Stamina = _vitals.MaxStamina > 0.0f ? _vitals.Stamina / _vitals.MaxStamina : 0.0f,
                Committed = _combat != null && _combat.IsCommitted,
                CanDash = _combat != null && _combat.CanRoll && !_locomotion.IsDashing && _vitals.Stamina > 0.0f && !_vitals.IsExhausted,
                Stunned = _vitals.IsStunned,
                TookHit = _hurt,
            };
            if (!has)
            {
                return senses;
            }
            Vector3 apart = _target.transform.position - transform.position;
            apart.y = 0.0f;
            senses.Distance = apart.magnitude;
            senses.TargetAttacking = (_targetCombat != null && _targetCombat.IsEquipped
                                         && (_targetCombat.Phase == SoulsPhase.Startup || _targetCombat.Phase == SoulsPhase.Active))
                                     || (_targetMelee != null && _targetMelee.IsSwinging);
            senses.TargetBlocking = _target.IsBlocking || (_targetCombat != null && _targetCombat.IsBlocking);
            senses.TargetStaggered = _target.IsStunned && Time.time < _targetOpenUntil;
            senses.TargetDashing = (_targetBody != null && _targetBody.IsDashing) || _target.IsInvulnerable;
            return senses;
        }

        /// <summary>What a player would press, handed to the same combat, dash and locomotion the player's keys reach.</summary>
        private void Act(DuelistMind.Intent intent, float deltaTime)
        {
            float yaw = _target != null ? YawTo(_target.transform.position) : transform.eulerAngles.y;
            bool stunned = _vitals.IsStunned;

            if (_combat != null && _combat.IsEquipped)
            {
                _combat.Locked = _locomotion.IsDashing;
                _combat.SetBlockHeld(intent.Guard && !stunned);
                if (_combat.IsBlocking && !_wasBlocking)
                {
                    GuardsRaised++;
                }
                _wasBlocking = _combat.IsBlocking;
                if (!stunned)
                {
                    if (intent.Heavy)
                    {
                        _combat.RequestHeavy(yaw);
                    }
                    else if (intent.Light)
                    {
                        _combat.RequestLight(yaw);
                    }
                }
            }
            if (intent.Dash && !stunned)
            {
                TryDash(intent.DashDirection, yaw);
            }

            Vector2 move = Vector2.ClampMagnitude(intent.Move, 1.0f);
            bool committed = _combat != null && _combat.IsCommitted;
            bool blocking = _combat != null && _combat.IsBlocking;
            bool wantsSprint = intent.Sprint && !committed && !blocking && move.y > 0.0f;
            bool sprinting = _vitals.TickStamina(wantsSprint, deltaTime);
            if (committed)
            {
                move *= _combat.CommittedMoveSpeedFraction;
            }
            else if (blocking)
            {
                move *= 0.5f;
            }
            if (stunned)
            {
                move = Vector2.zero;
            }
            _locomotion.Tick(new LocomotionInput
            {
                Move = move,
                YawDegrees = yaw,
                Sprint = sprinting,
                // The sword's own walk, run, side-steps and back-step, facing whoever he fights as the player does locked on (Tab).
                Sword1HEquipped = _combat != null && _combat.IsEquipped && _combat.IsOneHanded,
                SwordEquipped = _combat != null && _combat.IsEquipped && !_combat.IsOneHanded,
                LockedOn = _target != null,
            }, deltaTime);
        }

        /// <summary>The dash with a sword in hand, exactly the player's double-tap of Shift (PlayerInputReader.TickDash, which the sword combat calls its roll): its stamina, cancelling a swing's tail, the moment of invulnerability.</summary>
        private void TryDash(Vector2 direction, float yaw)
        {
            if (_combat == null || !_combat.CanRoll || _locomotion.IsDashing)
            {
                return;
            }
            if (!_vitals.TrySpendCombatStamina(_combat.RollStaminaCost, _combat.RollRegenDelaySeconds))
            {
                return;
            }
            _combat.BeginRoll();
            var heading = new LocomotionInput { Move = direction.sqrMagnitude > 0.01f ? direction : new Vector2(0.0f, -1.0f), YawDegrees = yaw };
            _locomotion.StartDash(heading.WorldDirection());
            _vitals.GrantInvulnerability(_combat.RollInvulnerabilitySeconds);
            _audio?.PlayDash();
            Dashes++;
        }

        private float YawTo(Vector3 point)
        {
            Vector3 toward = point - transform.position;
            toward.y = 0.0f;
            return toward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toward).eulerAngles.y : transform.eulerAngles.y;
        }

        private void SetTarget(PlayerVitals target)
        {
            _target = target;
            _targetCombat = target != null ? target.GetComponent<SoulsSwordCombat>() : null;
            _targetMelee = target != null ? target.GetComponent<MeleeAttacker>() : null;
            _targetBody = target != null ? target.GetComponent<PlayerLocomotion>() : null;
        }

        /// <summary>This machine's own player, the nearest alive within <see cref="NoticeMetres"/> (never another player's copy: see the class comment).</summary>
        private void FindTarget()
        {
            if (_pinned)
            {
                if (_target == null)
                {
                    _pinned = false;
                }
                else if (_targetCombat == null)
                {
                    _targetCombat = _target.GetComponent<SoulsSwordCombat>();   // added when a sword is first equipped
                }
                return;
            }
            if (_targetCombat == null && _target != null)
            {
                _targetCombat = _target.GetComponent<SoulsSwordCombat>();
            }
            if (Time.time < _nextSearch && _target != null && _target.IsAlive)
            {
                return;
            }
            _nextSearch = Time.time + TargetSearchSeconds;
            PlayerVitals best = null;
            float nearest = NoticeMetres;
            foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
            {
                if (!reader.isActiveAndEnabled)
                {
                    continue;
                }
                var vitals = reader.GetComponent<PlayerVitals>();
                if (vitals == null || !vitals.IsAlive)
                {
                    continue;
                }
                float distance = Vector3.Distance(transform.position, vitals.transform.position);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = vitals;
                }
            }
            if (best != _target)
            {
                SetTarget(best);
            }
        }
    }
}
