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
    /// IN A SESSION (2026-10-27, NetworkedSwordsman.prefab): the machine that spawned him runs this; every other player's machine shows a copy
    /// with this switched off (NetworkedEnemy), which plays his swings, guard and tell as he does (by name, PlayerAnimationDriver). He fights
    /// whichever player is nearest, another player's copy too (PlayerBodies): his blows on a copy go to that player's own body. Offline he is
    /// the plain Swordsman.prefab, as before.
    /// </summary>
    public sealed class SwordsmanBrain : MonoBehaviour, IHostile
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

        /// <summary>
        /// HIS TELL (owner, 2026-10-24: "the telegraph of the enemies attack is either very short or non existent"): seconds an opening cut holds
        /// at the top of its wind-up, the blade drawn back and glinting, before it strikes (SoulsSwordCombat.TelegraphLightSeconds). A player's
        /// own swings have none: in a duel between players nothing changes. Shorter since every swing's own wind-up grew (2026-11-13,
        /// SwordStrikes.TimingOf): his cuts land as long after they start as they did, 0.68 s an opening one.
        /// </summary>
        public const float TelegraphLightSeconds = 0.28f;

        /// <summary>The tell of a cut chained on from another: shorter, a follow-up comes quicker.</summary>
        public const float TelegraphChainedSeconds = 0.12f;

        /// <summary>The heavy's tell, on top of its own pause at the top.</summary>
        public const float TelegraphHeavySeconds = 0.18f;

        /// <summary>
        /// HIS SWINGS COMMIT (owner, 2026-10-24: "the dash feels pretty useless ... because the enemies just auto lock onto you anyways"). Out of
        /// a swing he turns to whoever he fights at this rate, degrees per second...
        /// </summary>
        public const float TurnDegreesPerSecond = 540.0f;

        /// <summary>
        /// ...while winding up (and through his tell) only this fast, so a dash at the last moment leaves the cut behind; and from the strike to
        /// the end of his recovery not at all: the blade goes where it was aimed, and a dash round him leaves him open.
        /// </summary>
        public const float WindUpTurnDegreesPerSecond = 90.0f;

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

        /// <summary>Who he is fighting (null when nobody): a player or a summon on their side, found by the search, or set by a test.</summary>
        public IOpponent Target
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
        private PlayerAnimationDriver _driver;
        private SoulsSwordCombat _combat;
        private SwordWeapon _sword;
        private IOpponent _target;
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
        private bool _removed;
        private float _facingYaw;
        private bool _facingSet;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _ragdoll = GetComponent<Ragdoll>();
            _audio = GetComponent<PlayerAudio>();
            _driver = GetComponent<PlayerAnimationDriver>();
            if (_vitals != null)
            {
                _vitals.Damaged += OnDamaged;
                _vitals.Died += OnDied;
                _vitals.XpRewardOnDeath = XpReward;
            }
            if (_driver != null)
            {
                _driver.RemoteGlinted += ShowGlint;   // on another player's copy of him: his tell, as the owner's machine shows it
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
            if (_combat != null)
            {
                _combat.Telegraphed -= OnTelegraphed;
            }
            if (_driver != null)
            {
                _driver.RemoteGlinted -= ShowGlint;
            }
            WatchTarget(null, null);
        }

        /// <summary>His swing holds its tell: the blade glints, and the ting -- here, and on every other player's copy of him.</summary>
        private void OnTelegraphed(SoulsAttack attack, float seconds)
        {
            ShowGlint();
            _driver?.AnnounceState(AttackGlint.StateName);
        }

        private void ShowGlint()
        {
            AttackGlint.Show(_sword != null ? _sword.BladeTipWorld : transform.position + Vector3.up * 2.0f);
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
            float stun = Opponents.Alive(_target) ? _target.HitstunRemaining : 0.0f;
            _targetOpenUntil = Time.time + Mathf.Max(0.3f, stun);
        }

        private void OnDamaged(float amount)
        {
            _hurt = true;
        }

        /// <summary>
        /// Beaten: his sword falls from his hand for the player to pick up (owner, 2026-10-17). In a session the machine that runs him drops it
        /// for everyone (one sword, the first to ask gets it: DroppedItemsNetwork); another player's copy of him only lets go of its own.
        /// </summary>
        private void OnDied()
        {
            EnemyPack.Leave(transform);   // a body on the ground takes no turn and is no one to step round
            if (_sword == null)
            {
                return;
            }
            if (!EnemySession.IsCopy(gameObject))
            {
                var pose = new Pose(_sword.transform.position, _sword.transform.rotation);
                Vector3 bodyVelocity = _locomotion != null ? _locomotion.Velocity : Vector3.zero;
                DroppedItems.Shared.Drop(DroppedItem.LetGo(_sword.ItemId, ItemInstanceData.None, pose, transform, bodyVelocity, _vitals.DeathImpulse),
                    shared: EnemySession.IsShared(gameObject), gameObject, SwordPrefab);
            }
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
            if (_combat != null)
            {
                _combat.TelegraphLightSeconds = TelegraphLightSeconds;
                _combat.TelegraphChainedSeconds = TelegraphChainedSeconds;
                _combat.TelegraphHeavySeconds = TelegraphHeavySeconds;
                _combat.Telegraphed += OnTelegraphed;
            }
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
                if (_deadFor >= CorpseSeconds && !_removed)
                {
                    _removed = true;
                    EnemySession.Remove(gameObject);   // in a session the server takes him out on every machine
                }
                return;
            }

            // Knocked down (a finisher, Self Smite's burst): the ragdoll has him until he gets up, as the player's body does.
            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                _combat?.Interrupt();   // the cut he was in is gone: its clock stops with his mind, and it would go on from there once he was up
                _ragdoll.Tick(deltaTime);
                if (_ragdoll.CanStandUp)
                {
                    _ragdoll.BeginStandingUp();
                }
                _facingSet = false;   // he gets up facing wherever he fell
                return;
            }

            FindTarget();
            WatchTarget(_targetCombat, _target as PlayerVitals);
            _combat?.Tick(deltaTime);

            DuelistMind.Senses senses = Sense();
            ShareTheFight(ref senses, deltaTime);
            DuelistMind.Intent intent = Mind.Tick(senses, deltaTime);
            _hurt = false;
            Act(intent, deltaTime);
        }

        private void OnEnable()
        {
            EnemyPack.Join(transform);
        }

        private void OnDisable()
        {
            EnemyPack.Leave(transform);
        }

        /// <summary>Close to the target by this much more than his fighting distance, he asks for his turn to attack, metres.</summary>
        public const float AskTurnWithinMetres = DuelistMind.EngageMetres + 1.5f;

        /// <summary>A turn he holds this long without attacking (the target turtles behind its guard) he hands on, seconds.</summary>
        public const float UnusedTurnSeconds = 1.5f;

        /// <summary>How hard he steps away from a packmate too close, as a share of a full step.</summary>
        public const float SeparationWeight = 1.0f;

        private bool _turnUsed;
        private float _turnHeldFor;

        /// <summary>He holds the turn to attack his target now (EnemyPack.Turns). For tests.</summary>
        public bool HasTurn => EnemyPack.Turns.Holds(transform);

        /// <summary>
        /// FIGHTING IN A PACK (EnemyPack): he asks for his turn as he comes into reach and attacks only while it is his and no packmate stands in
        /// the line; he hands it on once his attack is over (or when he has not used it for a while); and he circles away from his nearest
        /// packmate. Alone he always has the turn, and fights as he always did.
        /// </summary>
        private void ShareTheFight(ref DuelistMind.Senses senses, float deltaTime)
        {
            float now = Time.time;
            if (!senses.HasTarget)
            {
                EnemyPack.Turns.GiveBack(transform, now);
                _turnUsed = false;
                return;
            }
            bool attacking = Mind.State == DuelistState.Attack || Mind.State == DuelistState.Punish || senses.Committed;
            bool wants = attacking || senses.Distance <= AskTurnWithinMetres;
            bool turn = wants && EnemyPack.Turns.Ask(transform, _target, TurnKind.Melee, now);
            if (turn)
            {
                _turnHeldFor += deltaTime;
                if (attacking)
                {
                    _turnUsed = true;
                }
                else if (_turnUsed || _turnHeldFor > UnusedTurnSeconds)
                {
                    EnemyPack.Turns.GiveBack(transform, now);   // his attack is over (or he waited it out): the next one's go
                    turn = false;
                    _turnUsed = false;
                }
            }
            if (!turn)
            {
                _turnHeldFor = 0.0f;
            }
            bool blocked = EnemyPack.AllyInTheWay(transform, transform.position, _target.transform.position);
            senses.Waiting = !attacking && wants && (!turn || blocked);   // further off he comes in as ever, to ask
            senses.CrowdSide = EnemyPack.CrowdSide(transform, _target.transform.position);
        }

        private DuelistMind.Senses Sense()
        {
            bool has = Opponents.Alive(_target);
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
            senses.TargetWindingUp = _targetCombat != null && _targetCombat.IsEquipped && _targetCombat.Phase == SoulsPhase.Startup;
            senses.TargetBlocking = _target.IsBlocking || (_targetCombat != null && _targetCombat.IsBlocking);
            senses.TargetStaggered = _target.IsStunned && Time.time < _targetOpenUntil;
            senses.TargetDashing = (_targetBody != null && _targetBody.IsDashing) || _target.IsInvulnerable;
            return senses;
        }

        /// <summary>What a player would press, handed to the same combat, dash and locomotion the player's keys reach.</summary>
        private void Act(DuelistMind.Intent intent, float deltaTime)
        {
            float yaw = Facing(Opponents.Alive(_target) ? YawTo(_target.transform.position) : transform.eulerAngles.y, deltaTime);
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
                _combat.AimSwing(yaw);   // a wind-up follows where he turns; the blade goes where he faces when it goes live
            }
            if (intent.Dash && !stunned)
            {
                TryDash(intent.DashDirection, yaw);
            }

            Vector2 move = Vector2.ClampMagnitude(intent.Move + EnemyPack.SeparationMove(transform, yaw) * SeparationWeight, 1.0f);
            bool slowed = _combat != null && _combat.SlowsMovement;
            bool blocking = _combat != null && _combat.IsBlocking;
            bool wantsSprint = intent.Sprint && !slowed && !blocking && move.y > 0.0f;
            bool sprinting = _vitals.TickStamina(wantsSprint, deltaTime);
            if (slowed)
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
                LockedOn = Opponents.Alive(_target),
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

        /// <summary>
        /// Where he faces this frame, turned toward <paramref name="targetYaw"/> at his rate for what he is doing: quick out of a swing, slow
        /// through a wind-up, not at all from the strike to the end of the recovery (<see cref="WindUpTurnDegreesPerSecond"/>).
        /// </summary>
        private float Facing(float targetYaw, float deltaTime)
        {
            if (!_facingSet)
            {
                _facingYaw = transform.eulerAngles.y;
                _facingSet = true;
            }
            float rate = TurnDegreesPerSecond;
            if (_combat != null && _combat.IsCommitted)
            {
                rate = _combat.Phase == SoulsPhase.Startup ? WindUpTurnDegreesPerSecond : 0.0f;
            }
            _facingYaw = Mathf.MoveTowardsAngle(_facingYaw, targetYaw, rate * deltaTime);
            return _facingYaw;
        }

        private float YawTo(Vector3 point)
        {
            Vector3 toward = point - transform.position;
            toward.y = 0.0f;
            return toward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toward).eulerAngles.y : transform.eulerAngles.y;
        }

        /// <summary>Whom he fights, and how he reads them: a player's sword, fists and dash; a summon has none of them.</summary>
        private void SetTarget(IOpponent target)
        {
            bool here = Opponents.Here(target);
            _target = target;
            _targetCombat = here ? target.gameObject.GetComponent<SoulsSwordCombat>() : null;
            _targetMelee = here ? target.gameObject.GetComponent<MeleeAttacker>() : null;
            _targetBody = here ? target.gameObject.GetComponent<PlayerLocomotion>() : null;
        }

        /// <summary>
        /// The nearest living opponent within <see cref="NoticeMetres"/>: this machine's player, in a session another player's copy, or a summon
        /// on their side (Opponents).
        /// </summary>
        private void FindTarget()
        {
            if (_pinned)
            {
                if (!Opponents.Here(_target))
                {
                    _pinned = false;
                }
                else if (_targetCombat == null)
                {
                    _targetCombat = _target.gameObject.GetComponent<SoulsSwordCombat>();   // added when a sword is first equipped
                }
                return;
            }
            if (_targetCombat == null && Opponents.Here(_target))
            {
                _targetCombat = _target.gameObject.GetComponent<SoulsSwordCombat>();
            }
            // Once a second, with or without a target (every enemy used to scan the whole scene every frame while nobody was near: 0.33 ms
            // each, docs/PERFORMANCE.md); a target that has died is replaced at once.
            if (Time.time < _nextSearch && (_target == null || Opponents.Alive(_target)))
            {
                return;
            }
            _nextSearch = Time.time + TargetSearchSeconds;
            IOpponent best = Opponents.NearestAlive(transform.position, NoticeMetres);
            if (best != _target)
            {
                SetTarget(best);
            }
        }
    }
}
