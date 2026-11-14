using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Hostile "Crimson Claw Monster" enemy -- spawned via the dev console's
    /// own /spawn crimsonclaw. Ported from npc/crimson_claw_monster.gd.
    ///
    /// Like FlyingNpc, this has NO CharacterController -- it hovers (no legs
    /// to stand on) and moves by translating the transform directly, with
    /// NpcSteering (the same local obstacle avoidance Civilian/UniqueNpc use)
    /// keeping it from walking straight into a building. Unlike FlyingNpc, it
    /// is a real combatant: IDamageable, with a multi-phase claw swing attack
    /// that only lands if the target is still in reach DURING the swing
    /// phase, not a guaranteed hit on trigger -- same "distance-to-body, not
    /// per-claw swept tip" reasoning the source file's own class comment
    /// gives (the claws have no skeleton to derive an exact swept position
    /// from). Since 2026-10-24 the attack can be read and dashed: the claw
    /// holds drawn back, glinting (TellSeconds), before it swipes; it turns
    /// only slowly while winding up and not at all once it swipes; and the
    /// swipe reaches only what is in front of it (ClawHitHalfAngleDegrees).
    /// The swipe moves its whole body in one flowing motion (ClawStrikeMotion),
    /// and its eyes glow red until it dies, then go out (EyesGlow). Every fourth
    /// attack its swipe grabs and throws instead (CrimsonClaw.Grab.cs).
    ///
    /// HOVER HEIGHT IS GROUND-RELATIVE, NOT AN ABSOLUTE WORLD Y -- the one
    /// deliberate deviation from the source. Godot's own HOVER_HEIGHT (1.5)
    /// was a flat world Y, fine for the relatively flat area it was built
    /// against, but this world has genuine hillside terrain (see
    /// [[city_art_direction_chongqing]]) that a fixed world Y would clip
    /// through or float absurdly above depending on where it spawns. Ground
    /// height is found the same way FlyingNpcSteering already finds it for
    /// the OTHER hovering NPC in this project: a downward raycast, not a
    /// query into the procedural terrain field directly.
    ///
    /// NO SKELETON, SO NO SKELETAL RAGDOLL -- the visual body is 3 separate,
    /// pre-built mesh pieces (Body/LeftClaw/RightClaw, wired by
    /// CrimsonClawBuilder). Death detaches each into its own independent
    /// Rigidbody debris piece, keeping whatever pose it was actually in
    /// (mid-swing included) via Transform.SetParent's own keep-world-
    /// transform default -- the coarsest ragdoll granularity available, one
    /// rigid piece per mesh island, matching the source file's own
    /// _spawn_ragdoll_pieces exactly.
    ///
    /// NO LOOT -- Scope cut. The source's own LootComponent
    /// ("press E to loot a corpse") has no equivalent anywhere in this
    /// Unity port yet; building that whole interaction from nothing is a
    /// separate task from porting the creature itself.
    ///
    /// TICK IS PUBLIC, SEPARATE FROM UPDATE -- same reason WallStick.Tick is:
    /// an EditMode test can drive the whole state machine by calling this
    /// directly with an explicit deltaTime, without needing a real Unity
    /// Update loop pumping.
    /// </summary>
    public sealed partial class CrimsonClaw : MonoBehaviour, IDamageable, IHealthBar, IHostile
    {
        public const float MaxHealth = 150.0f;
        public const float DetectionRange = 16.0f;
        public const float ChaseSpeed = 5.0f;

        /// <summary>
        /// How far in front of its middle its claws reach when it swipes, metres: the claw's tip, 0.95 of the model forward, carried on by the
        /// swipe and the lunge of its body (ClawStrikeMotion), about 1.15.
        /// </summary>
        public const float ClawReachMetres = 1.15f * ModelScale;

        /// <summary>Half the width of what it swipes at across the shoulders, metres: a blow that reaches its side lands.</summary>
        public const float TargetBodyRadius = 0.45f;

        /// <summary>It swipes at what is within its claws' reach, so a blow lands unless the target steps away during the tell.</summary>
        public const float AttackRange = ClawReachMetres;

        public const float AttackCooldownSeconds = 1.6f;
        public const float AttackDamage = 15.0f;

        // --- attack phases: see Tick's own attack-state handling. ---
        public const float WindupSeconds = 0.18f;

        /// <summary>
        /// ITS TELL (owner, 2026-10-24: "the telegraph of the enemies attack is either very short or non existent"): after the wind-up the claw
        /// holds drawn back this long, glinting, before it swipes, seconds -- long enough to see and dash.
        /// </summary>
        public const float TellSeconds = 0.35f;

        public const float SwingSeconds = 0.22f;
        public const float RecoverSeconds = 0.28f;

        /// <summary>One whole attack, wind-up to recovered, seconds.</summary>
        public const float AttackSeconds = WindupSeconds + TellSeconds + SwingSeconds + RecoverSeconds;

        public const float WindupAngleDegrees = 35.0f;
        public const float SwingAngleDegrees = 75.0f;

        /// <summary>
        /// How close the TARGET's middle must be to this body's during the swing phase to actually land a hit -- see this class's own doc
        /// comment on why that is a distance-to-body check, not a per-claw swept-tip check: as far as its claws reach and the target's own
        /// width (owner, 2026-11-08: "he was swinging right next to me and should have been hitting but it wasnt reaching my hitbox"; it was
        /// 2.5 m while it swung from 3).
        /// </summary>
        public const float ClawHitRadius = ClawReachMetres + TargetBodyRadius;

        /// <summary>
        /// ...and in front of it: within this many degrees either side of where it faces. It used to reach all round, so a dash past it was hit
        /// anyway (owner, 2026-10-24: "the dash feels pretty useless ... because the enemies just auto lock onto you anyways"). Measured to the
        /// near edge of the target's body since 2026-11-08 (it was 70 to the middle), so a body beside it is reached and a dash round it still slips it.
        /// </summary>
        public const float ClawHitHalfAngleDegrees = 60.0f;

        /// <summary>Winding up and through its tell it turns toward its target only this fast, degrees per second; swiping and recovering not at all.</summary>
        public const float WindUpTurnDegreesPerSecond = 120.0f;

        /// <summary>The tell's glint shows this far in front of the claw's root, metres: at the claw.</summary>
        public const float ClawGlintReach = 0.7f;

        public const float HoverHeightAboveGround = 1.5f;
        public const float HoverCorrectionSpeed = 6.0f;
        public const float GroundProbeDistance = 300.0f;

        public const float NoticeGrowlIntervalSeconds = 5.0f;

        /// <summary>
        /// Its eyes glow this red (HDR: the bloom spreads it), as if lit from inside its head; only the eyes give off light (owner,
        /// 2026-11-07: "make it glow red from within the head only visible through the eyes").
        /// </summary>
        public static readonly Color EyesGlow = new Color(5.0f, 0.0f, 0.03f);

        /// <summary>Dead, the glow goes out over this many seconds ("the glow fades away upon death").</summary>
        public const float EyesFadeSeconds = 1.5f;

        /// <summary>
        /// Drawn at this size (owner, 2026-11-08: "make the crimson claw 10% bigger": it was 2, then 2.2; then, for its fist to hold a player
        /// with little of either showing through the other, "i dont mind increasing the size of the crimson claw as a whole slightly": the
        /// fit was measured at 2.2, 2.4 and 2.6, see <see cref="FistHollow"/>).
        /// </summary>
        public const float ModelScale = 2.4f;

        /// <summary>While chasing and waiting it turns to face its target at most this fast, degrees per second (it used to snap round).</summary>
        public const float TurnDegreesPerSecond = 360.0f;
        public const float RagdollTossSpeed = 1.8f;
        public const float RagdollLinearDamp = 0.4f;
        public const float RagdollAngularDamp = 0.4f;

        [Header("Wired by CrimsonClawBuilder")]
        public Transform LeftClawPivot;
        public Transform RightClawPivot;
        public Transform BodyVisual;
        [Tooltip("Which of the body's materials is its eyes; -1 if it has none.")]
        public int EyesMaterialSlot = -1;
        public BoxCollider MainCollision;
        public AudioSource GrowlAudio;
        public AudioSource AttackAudio;
        public AudioSource DeathAudio;
        public AudioClip[] NoticeSounds = System.Array.Empty<AudioClip>();
        public AudioClip AttackSound;
        public AudioClip DeathSound;

        [Tooltip("What counts as ground/a wall to steer around. World geometry by default.")]
        public LayerMask ObstacleMask = ~0;

        private enum AttackState
        {
            Idle,
            Windup,
            Tell,
            Swing,
            Recover,
            Holding,   // its swipe caught someone: CrimsonClaw.Grab.cs
        }

        private readonly NpcSteering _steering = new NpcSteering();
        private NpcWander _wander;

        /// <summary>How fast it drifts about with nothing to fight, metres per second, and how much room it wants ahead of it.</summary>
        public const float WanderSpeed = 1.6f;

        public const float WanderRoomMetres = 0.5f * ModelScale;

        /// <summary>
        /// IN A SESSION (owner, 2026-11-14: "monsters should appear in the same location between players when online"; NetworkedCrimsonClaw):
        /// true on the machines that do not run it. A copy is moved by its owner, decides nothing, sends the hits it takes to its owner
        /// (<see cref="Relay"/>), is told its health, and plays the swipes its owner starts.
        /// </summary>
        public bool IsCopy { get; set; }

        /// <summary>Where a hit on a copy goes instead of landing on it (the session's; null alone).</summary>
        public IRemoteDamageRelay Relay { get; set; }

        /// <summary>A swipe has started, with the left claw (true) or the right: the session shows it on the other machines.</summary>
        public event System.Action<bool> AttackStarted;

        /// <summary>A copy: its owner has started a swipe. The claws go through it here; it cuts nothing (the owner's does).</summary>
        public void ShowAttackFromNetwork(bool leftHand)
        {
            if (_isDead || !IsCopy)
            {
                return;
            }
            _nextAttackHandIsLeft = leftHand;
            _grabbing = false;
            _attackState = AttackState.Windup;
            _attackPhaseTimer = 0.0f;
            _attackPhaseDuration = WindupSeconds;
            _attackHitLanded = true;
            _activeClawPivot = leftHand ? LeftClawPivot : RightClawPivot;
            KnowRest();
            if (AttackAudio != null && AttackSound != null)
            {
                AttackAudio.clip = AttackSound;
                AttackAudio.Play();
            }
        }

        /// <summary>A copy: its owner's health. At nothing it dies here as it did there.</summary>
        public void SetHealthFromNetwork(float health)
        {
            if (_isDead)
            {
                return;
            }
            _health = Mathf.Clamp(health, 0.0f, MaxHealth);
            if (_health <= 0.0f)
            {
                Die();
            }
        }

        private float _health = MaxHealth;
        private bool _isDead;
        private IOpponent _target;
        private float _retargetIn;

        /// <summary>It looks again for the nearest of what it fights this often, seconds: a summon come nearer than its target is fought instead.</summary>
        public const float RetargetSeconds = 1.0f;

        private float _attackCooldownRemaining;
        private AttackState _attackState = AttackState.Idle;
        private float _attackPhaseTimer;
        private float _attackPhaseDuration;
        private bool _attackHitLanded;
        private bool _nextAttackHandIsLeft = true;
        private Transform _activeClawPivot;
        private Rest _leftRest;
        private Rest _rightRest;
        private Rest _bodyRest;
        private bool _restKnown;

        private Material _eyes;
        private float _deadSeconds;

        /// <summary>Where a moving part sits between attacks.</summary>
        private struct Rest
        {
            public Transform Part;
            public Vector3 Position;
            public Quaternion Rotation;

            public static Rest Of(Transform part) =>
                new Rest { Part = part, Position = part != null ? part.localPosition : Vector3.zero, Rotation = part != null ? part.localRotation : Quaternion.identity };

            /// <summary>Turned and shifted from rest (in its parent's frame: the model's).</summary>
            public void Place(Vector3 turn, Vector3 shift)
            {
                if (Part != null)
                {
                    Part.localPosition = Position + shift;
                    Part.localRotation = Rotation * Quaternion.Euler(turn);
                }
            }
        }

        private bool _hadTargetLastFrame;
        private float _growlTimerRemaining;

        private bool _bound;

        public bool IsAlive => !_isDead;
        public float Health => _health;

        float IHealthBar.MaxHealth => MaxHealth;

        /// <summary>Exposed for tests -- which hand the NEXT attack swings.</summary>
        public bool NextAttackHandIsLeft => _nextAttackHandIsLeft;

        /// <summary>Somewhere between winding up and recovered, swiping or grabbing.</summary>
        public bool IsAttacking => _attackState != AttackState.Idle;

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// The real setup Awake() normally does -- public and idempotent,
        /// matching Radio.Bind()/UniqueNpc.Bind()'s own established
        /// precedent, since Awake never runs on a bare AddComponent in an
        /// EditMode test.
        /// </summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            // Sized off the collision box CrimsonClawBuilder gives this body
            // (1.8 x 1.3 x 2.0 of the model, scaled) rather than a CharacterController
            // that does not exist here.
            _steering.Configure(width: 0.9f * ModelScale, topY: 0.65f * ModelScale, reach: 1.0f * ModelScale);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (_isDead)
            {
                FadeEyes(deltaTime);
                return;
            }
            if (IsCopy)
            {
                // Another machine's: its owner moves it and decides everything. Here it only plays the swipes it is told of.
                if (IsAttacking)
                {
                    TickAttack(deltaTime);
                }
                return;
            }

            HoverCorrect(deltaTime);
            _attackCooldownRemaining = Mathf.Max(0.0f, _attackCooldownRemaining - deltaTime);

            // Once a second, with a target or without (a search scans the scene: an idle one that asked every frame cost more than five
            // swordsmen); a target that has died or gone is replaced at once.
            _retargetIn -= deltaTime;
            bool lostIt = _target != null && !Opponents.Alive(_target);
            if (lostIt || (_retargetIn <= 0.0f && (!IsAttacking || _target == null)))
            {
                _target = FindTarget();
                _retargetIn = RetargetSeconds;
            }

            TickGrowl(deltaTime);

            if (IsAttacking)
            {
                if (Opponents.Alive(_target))
                {
                    EnemyPack.Turns.Ask(transform, _target, TurnKind.Melee, Time.time);   // keeps the turn through the swing
                }
                TickAttack(deltaTime);
                if (Opponents.Alive(_target) && (_attackState == AttackState.Windup || _attackState == AttackState.Tell))
                {
                    TurnTowards(_target.transform.position, WindUpTurnDegreesPerSecond * deltaTime);   // slowly; swiping, not at all
                }
                PressIn(deltaTime);
                return;
            }
            if (_turnUsed)
            {
                EnemyPack.Turns.GiveBack(transform, Time.time);   // the swing is over: the next of the pack has its go
                _turnUsed = false;
            }

            if (!Opponents.Alive(_target))
            {
                // Nothing to fight: it drifts about where it is (NpcWander), turning before it goes.
                _wander ??= new NpcWander(GetInstanceID());
                NpcWander.Step stroll = _wander.Tick(NpcWanderProbe.Sense(transform, 0.0f, WanderRoomMetres, walks: false), deltaTime);
                transform.rotation = Quaternion.Euler(0.0f, stroll.YawDegrees, 0.0f);
                transform.position += transform.forward * (stroll.Move > 0.0f ? WanderSpeed : 0.0f) * deltaTime;
                return;
            }
            _wander?.Reset();

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0.0f;
            float distance = toTarget.magnitude;
            // In a pack (EnemyPack) it closes in and swings only on its turn; waiting, it hovers out at WaitMetres, drifting round away from
            // the nearest packmate. Alone it always has the turn.
            bool turn = distance <= WaitMetres + 1.0f && EnemyPack.Turns.Ask(transform, _target, TurnKind.Melee, Time.time);
            if (distance > DetectionRange)
            {
                _target = null;
            }
            else if (distance > AttackRange && (turn || distance > WaitMetres))
            {
                // Steered round what is in its way, but always facing what it chases (facing where it steered, it looked one way and the other).
                Vector3 direction = _steering.Steer(transform, (toTarget / distance + EnemyPack.Separation(transform)).normalized, deltaTime,
                    _target.transform);
                TurnTowards(_target.transform.position, TurnDegreesPerSecond * deltaTime);
                transform.position += direction * ChaseSpeed * deltaTime;
            }
            else if (turn && !EnemyPack.AllyInTheWay(transform, transform.position, _target.transform.position))
            {
                TurnTowards(_target.transform.position, TurnDegreesPerSecond * deltaTime);
                if (_attackCooldownRemaining <= 0.0f)
                {
                    StartAttack(WantsToGrab());
                    _turnUsed = true;
                }
            }
            else
            {
                Vector3 toward = toTarget / Mathf.Max(distance, 1e-3f);
                Vector3 round = Vector3.Cross(Vector3.up, toward) * -EnemyPack.CrowdSide(transform, _target.transform.position);
                Vector3 inOut = distance < WaitMetres - 0.5f ? -toward : Vector3.zero;
                Vector3 drift = Vector3.ClampMagnitude(round * 0.5f + inOut + EnemyPack.Separation(transform), 1.0f);
                transform.position += drift * WaitSpeed * deltaTime;
                TurnTowards(_target.transform.position, TurnDegreesPerSecond * deltaTime);
            }
        }

        /// <summary>
        /// Through its wind-up and tell it edges forward this fast, metres per second, while what it swipes at is further off than
        /// <see cref="PressInWithinMetres"/>: backing off at a walk it no longer escaped every swipe (owner, 2026-11-08: "i feel like i can just
        /// hold s and not get hit by him as he is swinging"). Slower than a walk backward (3), so it is the lunge that catches up; a dash still
        /// gets clear.
        /// </summary>
        public const float TellCreepSpeed = 2.5f;

        /// <summary>... and swiping, it lunges this far forward over the swipe, metres.</summary>
        public const float SwingLungeMetres = 1.2f;

        /// <summary>It presses in only while its target is further off than this, metres: never into them.</summary>
        public const float PressInWithinMetres = TargetBodyRadius + 1.2f;

        /// <summary>Edging forward through its tell and lunging with the swipe, along where it faces.</summary>
        private void PressIn(float deltaTime)
        {
            float speed = _attackState == AttackState.Windup || _attackState == AttackState.Tell ? TellCreepSpeed
                : _attackState == AttackState.Swing ? SwingLungeMetres / SwingSeconds
                : 0.0f;
            if (speed <= 0.0f || !Opponents.Alive(_target))
            {
                return;
            }
            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0.0f;
            if (toTarget.magnitude <= PressInWithinMetres)
            {
                return;
            }
            Vector3 ahead = transform.forward;
            ahead.y = 0.0f;
            transform.position += ahead.normalized * (speed * deltaTime);
        }

        /// <summary>Waiting its turn (another of the pack is attacking), it hovers out about here, metres: just out of its reach.</summary>
        public const float WaitMetres = AttackRange + 2.5f;

        /// <summary>How fast it drifts while it waits, metres per second.</summary>
        public const float WaitSpeed = 2.0f;

        private bool _turnUsed;

        private void OnEnable()
        {
            EnemyPack.Join(transform);
        }

        private void OnDisable()
        {
            EnemyPack.Leave(transform);
            LetGoOfHeld(Vector3.zero);   // gone while holding someone: they drop
        }

        /// <summary>
        /// Eases toward HoverHeightAboveGround over the REAL ground beneath
        /// this body every frame -- see this class's own doc comment on why
        /// that is ground-relative rather than a flat world Y. Same
        /// exponential-ease-toward-target shape the source's own
        /// `(HOVER_HEIGHT - y) * HOVER_CORRECTION_SPEED` velocity produces
        /// once integrated over one physics step.
        /// </summary>
        private void HoverCorrect(float deltaTime)
        {
            float groundY = transform.position.y - HoverHeightAboveGround;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                    GroundProbeDistance, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
            float targetY = groundY + HoverHeightAboveGround;
            Vector3 position = transform.position;
            position.y += (targetY - position.y) * HoverCorrectionSpeed * deltaTime;
            transform.position = position;
        }

        /// <summary>
        /// What it fights: the nearest of this machine's player, in a session another player's copy here (owner, 2026-11-14: monsters are
        /// the same for every player online), and the summons on the players' side (<see cref="Opponents"/>).
        /// </summary>
        private IOpponent FindTarget()
        {
            IOpponent player = FindNearestPlayer();
            float reach = player != null ? Vector3.Distance(transform.position, player.transform.position) : DetectionRange;
            IOpponent best = player;
            PlayerVitals other = PlayerBodies.NearestAlive(transform.position, reach);
            if (other != null && !ReferenceEquals(other, player))
            {
                reach = Vector3.Distance(transform.position, other.transform.position);
                best = other;
            }
            foreach (IOpponent summon in Opponents.Summons)
            {
                if (!Opponents.Alive(summon))
                {
                    continue;
                }
                float distance = Vector3.Distance(transform.position, summon.transform.position);
                if (distance < reach)
                {
                    reach = distance;
                    best = summon;
                }
            }
            return best;
        }

        /// <summary>
        /// Simplified from the source's own "search every node in the
        /// players group, keep the nearest" -- this Unity port has no such
        /// group (NPCs are not networked at all yet, see
        /// [[multiplayer_sync_roadmap]]), and every other system here
        /// already treats "the player" as the one FindFirstObjectByType
        /// PlayerVitals singleton. A real multi-player scan would replace
        /// this the same day multiplayer NPCs become real.
        /// </summary>
        private PlayerVitals FindNearestPlayer()
        {
            // The player's body is the one with a working input reader: since the Shoota (2026-10-15) wears the player's body too, the first
            // PlayerVitals found could be him.
            PlayerVitals player = null;
            PlayerInputReader reader = Object.FindFirstObjectByType<PlayerInputReader>();
            if (reader != null && reader.isActiveAndEnabled)
            {
                player = reader.GetComponent<PlayerVitals>();
            }
            if (player == null)
            {
                player = Object.FindFirstObjectByType<PlayerVitals>();
            }
            if (player == null || !player.IsAlive)
            {
                return null;
            }
            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= DetectionRange ? player : null;
        }

        private void TickGrowl(float deltaTime)
        {
            bool hasTargetNow = Opponents.Alive(_target);
            if (hasTargetNow && !_hadTargetLastFrame)
            {
                PlayRandomGrowl();
                _growlTimerRemaining = NoticeGrowlIntervalSeconds;
            }
            else if (hasTargetNow)
            {
                _growlTimerRemaining -= deltaTime;
                if (_growlTimerRemaining <= 0.0f)
                {
                    PlayRandomGrowl();
                    _growlTimerRemaining = NoticeGrowlIntervalSeconds;
                }
            }
            _hadTargetLastFrame = hasTargetNow;
        }

        private void PlayRandomGrowl()
        {
            if (GrowlAudio == null || NoticeSounds.Length == 0)
            {
                return;
            }
            GrowlAudio.clip = NoticeSounds[Random.Range(0, NoticeSounds.Length)];
            GrowlAudio.Play();
        }

        /// <summary>Turns toward <paramref name="worldPoint"/> by at most <paramref name="maxDegrees"/>.</summary>
        private void TurnTowards(Vector3 worldPoint, float maxDegrees)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction.normalized, Vector3.up), maxDegrees);
        }

        // --- attack state machine ---

        /// <summary>A swipe; with <paramref name="grab"/>, one whose claw closes on what it reaches and throws it instead of cutting it.</summary>
        private void StartAttack(bool grab = false)
        {
            _attackCooldownRemaining = AttackCooldownSeconds;
            _grabbing = grab;
            // Only a swipe at a player on their feet counts toward the next grab: swiped at lying down after a throw, they were caught again
            // the moment they stood (owner, 2026-11-08: "the claw should not do the grab or the throw twice in a row").
            _swipesSinceGrab = grab ? 0 : _swipesSinceGrab + (_target is PlayerVitals standing && standing.CanBeSeized ? 1 : 0);
            _attackState = AttackState.Windup;
            _attackPhaseTimer = 0.0f;
            _attackPhaseDuration = WindupSeconds;
            _attackHitLanded = false;
            _activeClawPivot = _nextAttackHandIsLeft ? LeftClawPivot : RightClawPivot;
            KnowRest();

            if (AttackAudio != null && AttackSound != null)
            {
                AttackAudio.clip = AttackSound;
                AttackAudio.Play();
            }
            AttackStarted?.Invoke(_nextAttackHandIsLeft);
        }

        private void TickAttack(float deltaTime)
        {
            _attackPhaseTimer += deltaTime;
            if (_attackState == AttackState.Holding)
            {
                TickHolding();
                return;
            }
            ApplyStrikePose();

            if (_attackPhaseTimer < _attackPhaseDuration)
            {
                if (IsCopy)
                {
                    return;   // shown, not decided: the owner's claw is the one that cuts
                }
                if (_attackState == AttackState.Swing && _grabbing)
                {
                    TryCatch();
                }
                else if (_attackState == AttackState.Swing)
                {
                    CheckClawHit();
                }
                return;
            }

            switch (_attackState)
            {
                case AttackState.Windup:
                    // Drawn back: it holds there, glinting, before it swipes.
                    _attackState = AttackState.Tell;
                    _attackPhaseTimer = 0.0f;
                    _attackPhaseDuration = TellSeconds;
                    if (_activeClawPivot != null)
                    {
                        AttackGlint.Show(_activeClawPivot.position + transform.forward * ClawGlintReach);
                    }
                    break;

                case AttackState.Tell:
                    _attackState = AttackState.Swing;
                    _attackPhaseTimer = 0.0f;
                    _attackPhaseDuration = SwingSeconds;
                    break;

                case AttackState.Swing:
                    _attackState = AttackState.Recover;
                    _attackPhaseTimer = 0.0f;
                    _attackPhaseDuration = RecoverSeconds;
                    break;

                case AttackState.Recover:
                    EndAttack();
                    break;
            }
        }

        /// <summary>Recovered: at rest, the other hand next.</summary>
        private void EndAttack()
        {
            _attackState = AttackState.Idle;
            _activeClawPivot = null;
            PlaceAtRest();
            _nextAttackHandIsLeft = !_nextAttackHandIsLeft;
        }

        /// <summary>Claws and body where <see cref="ClawStrikeMotion"/> has them this far into the attack (the phases' own times).</summary>
        private void ApplyStrikePose()
        {
            float phaseStart = _attackState switch
            {
                AttackState.Tell => WindupSeconds,
                AttackState.Swing => WindupSeconds + TellSeconds,
                AttackState.Recover => WindupSeconds + TellSeconds + SwingSeconds,
                _ => 0.0f,
            };
            Place(ClawStrikeMotion.At(phaseStart + _attackPhaseTimer));
        }

        /// <summary>Claws and body at <paramref name="pose"/> (the -X claw's), swung by the claw striking now.</summary>
        private void Place(ClawStrikeMotion.Pose pose)
        {
            Rest strike = _nextAttackHandIsLeft ? _leftRest : _rightRest;
            Rest other = _nextAttackHandIsLeft ? _rightRest : _leftRest;
            if (strike.Position.x > 0.0f)
            {
                // By where the claw is, not its name: the model's LeftClaw sits on +X, and swung by its name it swiped out and back,
                // away from its target.
                pose = pose.Mirrored();
            }
            strike.Place(pose.StrikeTurn, pose.StrikeShift);
            other.Place(pose.OtherTurn, pose.OtherShift);
            _bodyRest.Place(pose.BodyTurn, pose.BodyShift);
        }

        /// <summary>Between attacks every part is back at rest, so the first attack finds where rest is.</summary>
        private void KnowRest()
        {
            if (_restKnown)
            {
                return;
            }
            _leftRest = Rest.Of(LeftClawPivot);
            _rightRest = Rest.Of(RightClawPivot);
            _bodyRest = Rest.Of(BodyVisual != null ? BodyVisual.parent : null);
            _restKnown = true;
        }

        private void PlaceAtRest()
        {
            _leftRest.Place(Vector3.zero, Vector3.zero);
            _rightRest.Place(Vector3.zero, Vector3.zero);
            _bodyRest.Place(Vector3.zero, Vector3.zero);
        }

        /// <summary>How bright its eyes glow, a share of <see cref="EyesGlow"/>, <paramref name="seconds"/> after it died.</summary>
        public static float EyesGlowAfterDeath(float seconds) => Mathf.SmoothStep(1.0f, 0.0f, seconds / EyesFadeSeconds);

        /// <summary>How bright its eyes glow now, a share of <see cref="EyesGlow"/>.</summary>
        public float EyesBrightness => _isDead ? EyesGlowAfterDeath(_deadSeconds) : 1.0f;

        /// <summary>Dead, its eyes go out.</summary>
        private void FadeEyes(float deltaTime)
        {
            if (_deadSeconds > EyesFadeSeconds)
            {
                return;   // out
            }
            _deadSeconds += deltaTime;
            if (_eyes != null || TakeOwnEyes())
            {
                _eyes.SetColor("_EmissionColor", EyesGlow * EyesBrightness);
            }
        }

        /// <summary>A copy of the eyes' material of its own, so only this one's go out.</summary>
        private bool TakeOwnEyes()
        {
            if (BodyVisual == null || EyesMaterialSlot < 0 || !BodyVisual.TryGetComponent(out MeshRenderer body))
            {
                return false;
            }
            Material[] materials = body.sharedMaterials;
            if (EyesMaterialSlot >= materials.Length || materials[EyesMaterialSlot] == null)
            {
                return false;
            }
            _eyes = new Material(materials[EyesMaterialSlot]) { name = materials[EyesMaterialSlot].name + " (going out)" };
            materials[EyesMaterialSlot] = _eyes;
            body.sharedMaterials = materials;
            return true;
        }

        private void OnDestroy()
        {
            if (_eyes == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(_eyes);
            }
            else
            {
                DestroyImmediate(_eyes);
            }
        }

        /// <summary>
        /// Only ever lands ONE hit per attack (_attackHitLanded), so staying
        /// close for several frames in a row cannot multi-hit off a single
        /// swing -- matches the source exactly.
        /// </summary>
        private void CheckClawHit()
        {
            if (_attackHitLanded || !Opponents.Alive(_target))
            {
                return;
            }
            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0.0f;
            if (InFront(toTarget, ClawHitRadius, ClawHitHalfAngleDegrees))
            {
                _attackHitLanded = true;
                _target.ApplyHit(AttackDamage, transform.position, CombatTuning.HitstunSeconds);
            }
        }

        /// <summary>
        /// A target <paramref name="toTarget"/> away (flat) is within <paramref name="reach"/> and within <paramref name="halfAngle"/> of where
        /// it faces: the angle to the nearer edge of its body, not its middle, so one right beside it is not missed by a hair.
        /// </summary>
        private bool InFront(Vector3 toTarget, float reach, float halfAngle)
        {
            float distance = toTarget.magnitude;
            if (distance > reach)
            {
                return false;
            }
            Vector3 facing = transform.forward;
            facing.y = 0.0f;
            float edge = distance > TargetBodyRadius ? Mathf.Asin(TargetBodyRadius / distance) * Mathf.Rad2Deg : 90.0f;
            return Vector3.Angle(facing, toTarget) <= halfAngle + edge;
        }

        // --- IDamageable ---

        bool IDamageable.IsAlive => IsAlive;

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (_isDead || amount <= 0.0f)
            {
                return HitResponse.Ignored;
            }
            if (Relay != null && Relay.TryRelayHit(amount, fromPosition, hitstunSeconds, 0.0f))
            {
                return HitResponse.Landed;   // a copy: the hit goes to the machine that runs it, and its health comes back from there
            }
            _health = Mathf.Max(0.0f, _health - amount);
            if (_health <= 0.0f)
            {
                Die();
            }
            return HitResponse.Landed;
        }

        private void Die()
        {
            EnemyPack.Leave(transform);
            _isDead = true;
            LetGoOfHeld(Vector3.zero);
            _target = null;
            _attackState = AttackState.Idle;
            _activeClawPivot = null;
            if (MainCollision != null)
            {
                MainCollision.enabled = false;
            }
            if (DeathAudio != null && DeathSound != null)
            {
                DeathAudio.clip = DeathSound;
                DeathAudio.Play();
            }
            SpawnRagdollPieces();
        }

        /// <summary>
        /// Detaches Body and both claw pivots into 3 independent Rigidbody
        /// debris pieces -- Transform.SetParent's own keepWorldTransform (true) is
        /// what makes this simple: each piece keeps its exact current world
        /// pose without any manual transform math, mid-swing pose included.
        /// Matches the source's own _spawn_ragdoll_pieces.
        /// </summary>
        private void SpawnRagdollPieces()
        {
            if (BodyVisual != null)
            {
                GameObject headBody = MakeRagdollBody(BodyVisual.position, BodyVisual.rotation);
                BodyVisual.SetParent(headBody.transform, true);
                AddRagdollCollision(headBody, new Vector3(1.3f, 1.0f, 1.4f) * ModelScale * 0.5f);
            }

            if (LeftClawPivot != null)
            {
                GameObject leftBody = MakeRagdollBody(LeftClawPivot.position, LeftClawPivot.rotation);
                LeftClawPivot.SetParent(leftBody.transform, true);
                AddRagdollCollision(leftBody, Vector3.one * ModelScale * 0.4f);
            }

            if (RightClawPivot != null)
            {
                GameObject rightBody = MakeRagdollBody(RightClawPivot.position, RightClawPivot.rotation);
                RightClawPivot.SetParent(rightBody.transform, true);
                AddRagdollCollision(rightBody, Vector3.one * ModelScale * 0.4f);
            }
        }

        /// <summary>
        /// A small random toss/tumble rather than a limp drop straight down
        /// -- reads as a genuine collapse, matching the source's own
        /// RAGDOLL_TOSS_SPEED comment. Grabbable so GrabCarry's own hold-E
        /// pickup works on the debris, same as any other loose Rigidbody in
        /// this project.
        /// </summary>
        private static GameObject MakeRagdollBody(Vector3 position, Quaternion rotation)
        {
            var body = new GameObject("CrimsonClawDebris");
            body.transform.SetPositionAndRotation(position, rotation);
            Rigidbody rb = body.AddComponent<Rigidbody>();
            rb.linearDamping = RagdollLinearDamp;
            rb.angularDamping = RagdollAngularDamp;
            rb.linearVelocity = new Vector3(
                Random.Range(-1.0f, 1.0f), Random.Range(0.6f, 1.4f), Random.Range(-1.0f, 1.0f)) * RagdollTossSpeed;
            rb.angularVelocity = new Vector3(
                Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f));
            var physicsMaterial = new PhysicsMaterial("CrimsonClawDebris")
            {
                bounciness = 0.25f,
                dynamicFriction = 0.7f,
                staticFriction = 0.7f,
            };
            BoxCollider collider = body.AddComponent<BoxCollider>();
            collider.material = physicsMaterial;
            collider.enabled = false; // re-sized and enabled by AddRagdollCollision right after
            body.AddComponent<Grabbable>();
            return body;
        }

        /// <summary>A single box centred on the piece's own origin -- close enough for a few seconds of tumbling debris, matching the source's own comment on why this isn't a precise per-mesh fit.</summary>
        private static void AddRagdollCollision(GameObject body, Vector3 boxSize)
        {
            BoxCollider collider = body.GetComponent<BoxCollider>();
            collider.size = boxSize;
            collider.enabled = true;
        }
    }
}
