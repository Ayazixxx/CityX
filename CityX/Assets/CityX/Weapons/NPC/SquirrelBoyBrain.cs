using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// SQUIRREL BOY (owner, 2026-11-25: "make an npc called squirrel boy that uses my squirrel boy character i made and the outfit he is wearing
    /// now ... he is non hostile until struck and then he fights unarmed with fists only for now. he is also able to crawl up walls and will run
    /// away and crawl up a wall when low health"; docs/SQUIRREL_BOY.md). The owner's character "squirrel boy" on the player's own body
    /// (SquirrelBoyBuilder), in his outfit: the T-shirt with the owner's SB design printed on it (the design carried in the prefab, so every
    /// machine has it), the Squirrel Mask, the brown cape and gloves, khakis and black-and-white sneakers.
    ///
    /// What he does is SquirrelBoyMind's; this body does it with the player's own moves: the player's fists (MeleeAttacker), the player's
    /// walk and run (PlayerLocomotion), and the player's climb (PlayerClimbing: run into a wall and he goes up it, hand over hand, and over the
    /// top). He never tires on a wall. In a session the machine that brought him in runs him (NetworkedEnemy) and everyone sees him.
    /// </summary>
    public sealed class SquirrelBoyBrain : MonoBehaviour, IHostile
    {
        /// <summary>How long his body lies before it is taken away, seconds.</summary>
        public const float CorpseSeconds = 25.0f;

        /// <summary>How far he looks for whoever is nearest once struck, metres, and how often, seconds.</summary>
        public const float NoticeMetres = 40.0f;
        public const float FoeSearchSeconds = 1.0f;

        /// <summary>How far he looks for a wall to climb, metres, in how many directions, how often, seconds.</summary>
        public const float WallSearchMetres = 25.0f;
        public const int WallDirections = 16;
        public const float WallSearchSeconds = 0.5f;

        /// <summary>A wall worth climbing goes on up at least this far above the ground, metres: lower is no escape.</summary>
        public const float TallEnoughMetres = 6.0f;

        /// <summary>How long off the ground still counts as on it for starting a climb, seconds.</summary>
        public const float FootingGraceSeconds = 0.15f;

        /// <summary>How fast he turns, degrees a second.</summary>
        public const float TurnDegreesPerSecond = 540.0f;

        [Tooltip("What he wears, each worn as the player wears it (set by SquirrelBoyBuilder from the owner's character).")]
        public GameObject[] Wearing = new GameObject[0];

        [Tooltip("The design printed on his shirt (the owner's SB), as the heat press keeps it (Core.ShirtDesignBlob).")]
        public TextAsset ShirtDesign;

        public int XpReward = 80;

        public SquirrelBoyMind Mind { get; } = new SquirrelBoyMind();

        /// <summary>
        /// Who he goes for once struck, set from outside (a test, a scripted scene: as SwordsmanBrain.Target); unset, the nearest opponent. A
        /// body with its controls off is no player to PlayerBodies, so a test's player has to be named.
        /// </summary>
        public IOpponent Target
        {
            get => _foe;
            set
            {
                _foe = value;
                _pinned = value != null;
            }
        }

        private bool _pinned;
        private float _lastGrounded = -10.0f;
        private readonly RaycastHit[] _wallHits = new RaycastHit[16];
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Ragdoll _ragdoll;
        private MeleeAttacker _fists;
        private PlayerClimbing _climbing;
        private IOpponent _foe;
        private float _nextFoeSearch;
        private bool _struck;
        private bool _climbedOver;
        private bool _hasWall;
        private Vector3 _wall;
        private float _nextWallSearch;
        private float _yaw;
        private float _deadFor;
        private bool _removed;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _ragdoll = GetComponent<Ragdoll>();
            _fists = GetComponent<MeleeAttacker>();
            _climbing = GetComponent<PlayerClimbing>();
            _yaw = transform.eulerAngles.y;
            if (_vitals != null)
            {
                _vitals.Damaged += OnDamaged;
                _vitals.XpRewardOnDeath = XpReward;
            }
            if (_climbing != null)
            {
                _climbing.MantleStarted += OnOverTheTop;
            }
            PutOnClothes();
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.Damaged -= OnDamaged;
            }
            if (_climbing != null)
            {
                _climbing.MantleStarted -= OnOverTheTop;
            }
        }

        private void OnDamaged(float amount)
        {
            if (!_struck)
            {
                _struck = true;
                _nextFoeSearch = 0.0f;   // he looks round for who did it at once
            }
        }

        private void OnOverTheTop() => _climbedOver = true;

        /// <summary>
        /// His outfit, each piece worn as the player wears it, the shirt with the owner's design on it. On every machine (Awake), so a copy wears
        /// it too; the design is handed to this machine's design library from the prefab, never fetched.
        /// </summary>
        private void PutOnClothes()
        {
            string designId = null;
            if (ShirtDesign != null)
            {
                byte[] blob = ShirtDesign.bytes;
                designId = DesignStore.IdOf(blob);
                DesignLibrary.Arrived(designId, blob);
            }
            foreach (GameObject piece in Wearing)
            {
                if (piece == null)
                {
                    continue;
                }
                GameObject worn = Instantiate(piece, transform, false);
                worn.name = piece.name;
                ItemInstanceData data = designId != null && worn.GetComponent<IPrintable>() != null
                    ? new ItemInstanceData { DesignId = designId }
                    : ItemInstanceData.None;
                worn.GetComponent<IEquippableItem>()?.OnEquip(gameObject, data);
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
                _deadFor += deltaTime;
                if (_deadFor >= CorpseSeconds && !_removed)
                {
                    _removed = true;
                    EnemySession.Remove(gameObject);   // in a session the server takes him out on every machine
                }
                return;
            }
            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                _ragdoll.Tick(deltaTime);
                if (_ragdoll.CanStandUp)
                {
                    _ragdoll.BeginStandingUp();
                }
                return;
            }
            if (_climbing != null && _climbing.IsToppingOut)
            {
                _climbing.TickTopOut();   // over the top: the pull-up has the body
                return;
            }

            FindFoe();
            LookForAWall();
            bool climbing = _climbing != null && _climbing.IsClimbing;
            _vitals.InfiniteStamina = climbing;   // a squirrel never tires on a wall
            SquirrelBoyMind.Intent intent = Mind.Tick(new SquirrelBoyMind.Senses
            {
                Struck = _struck,
                HasFoe = Opponents.Alive(_foe),
                FoeDistance = Opponents.Alive(_foe) ? Vector3.Distance(transform.position, _foe.transform.position) : float.MaxValue,
                Health = _vitals.MaxHealth > 0.0f ? _vitals.Health / _vitals.MaxHealth : 0.0f,
                Helpless = _vitals.IsStunned,
                Punching = _fists != null && _fists.IsSwinging,
                Climbing = climbing,
                ClimbedOver = _climbedOver,
                HasWall = _hasWall,
                WallDistance = _hasWall ? Flat(_wall - transform.position).magnitude : float.MaxValue,
            }, deltaTime);
            Act(intent, deltaTime);
        }

        private void Act(SquirrelBoyMind.Intent intent, float deltaTime)
        {
            if (_climbing != null && _climbing.IsClimbing)
            {
                _climbing.Tick(false, intent.ClimbUp, false, _vitals, deltaTime);
                return;
            }
            Vector3 way = WayTo(intent.Goal);
            if (way.sqrMagnitude > 1e-4f)
            {
                _yaw = Mathf.MoveTowardsAngle(_yaw, Quaternion.LookRotation(way).eulerAngles.y, TurnDegreesPerSecond * deltaTime);
            }
            else if (Mind.State == SquirrelBoyState.Fight && Opponents.Alive(_foe))
            {
                Vector3 toFoe = Flat(_foe.transform.position - transform.position);
                if (toFoe.sqrMagnitude > 1e-4f)
                {
                    _yaw = Mathf.MoveTowardsAngle(_yaw, Quaternion.LookRotation(toFoe).eulerAngles.y, TurnDegreesPerSecond * deltaTime);
                }
            }
            bool moving = intent.Goal != SquirrelBoyMind.Goal.Stay && way.sqrMagnitude > 1e-4f;
            if (_climbing != null)
            {
                // Run into the wall a moment and he is on it (the player's own climb starts the same way).
                // Pushed into a wall, the body's footing flickers off a frame now and then, and each flicker started the press over; a moment
                // off the ground still counts.
                if (_locomotion.IsGrounded)
                {
                    _lastGrounded = Time.time;
                }
                bool grounded = Time.time - _lastGrounded <= FootingGraceSeconds;
                bool into = intent.StartClimb && moving;
                if (_climbing.TryStartAuto(into, into, grounded, _vitals.Stamina, deltaTime, way.normalized))
                {
                    _climbedOver = false;
                    _climbing.Tick(false, true, false, _vitals, deltaTime);
                    return;
                }
            }
            _locomotion.Tick(new LocomotionInput
            {
                Move = moving ? Vector2.up : Vector2.zero,
                YawDegrees = _yaw,
                Sprint = intent.Run && moving,
            }, deltaTime);
            if (_fists != null)
            {
                _fists.Tick(deltaTime);
                if (intent.Punch)
                {
                    _fists.TryAttack(intent.Heavy);
                }
            }
        }

        /// <summary>Which way his goal is, flat: his foe, the wall, away from his foe, or nowhere.</summary>
        private Vector3 WayTo(SquirrelBoyMind.Goal goal)
        {
            switch (goal)
            {
                case SquirrelBoyMind.Goal.Foe:
                    return Opponents.Alive(_foe) ? Flat(_foe.transform.position - transform.position) : Vector3.zero;
                case SquirrelBoyMind.Goal.Wall:
                    return _hasWall ? Flat(_wall - transform.position) : Vector3.zero;
                case SquirrelBoyMind.Goal.AwayFromFoe:
                    return Opponents.Alive(_foe) ? Flat(transform.position - _foe.transform.position) : Vector3.zero;
                default:
                    return Vector3.zero;
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        /// <summary>Once struck, the nearest living opponent (this machine's player, another player's copy, a summon), looked for once a second.</summary>
        private void FindFoe()
        {
            if (!_struck || (Time.time < _nextFoeSearch && Opponents.Alive(_foe)) || (_pinned && Opponents.Alive(_foe)))
            {
                return;
            }
            _nextFoeSearch = Time.time + FoeSearchSeconds;
            _foe = Opponents.NearestAlive(transform.position, NoticeMetres);
        }

        /// <summary>
        /// While he would run for one, the nearest wall he can climb: looked for round him at his chest's height, a wall standing up (not a
        /// slope) that goes on up at least <see cref="TallEnoughMetres"/>, nearer counting for more and one past his foe for less.
        /// </summary>
        private void LookForAWall()
        {
            if (Mind.State != SquirrelBoyState.Flee || Time.time < _nextWallSearch)
            {
                return;
            }
            _nextWallSearch = Time.time + WallSearchSeconds;
            Vector3 chest = transform.position + Vector3.up * (BodyRig.StandingHeight * 0.5f);
            Vector3 away = Opponents.Alive(_foe) ? Flat(transform.position - _foe.transform.position).normalized : Vector3.zero;
            float best = float.MaxValue;
            _hasWall = false;
            for (int i = 0; i < WallDirections; i++)
            {
                Vector3 way = Quaternion.Euler(0.0f, i * 360.0f / WallDirections, 0.0f) * Vector3.forward;
                if (!NearestNotHim(chest, way, WallSearchMetres, out RaycastHit hit) || Mathf.Abs(hit.normal.y) > 0.3f)
                {
                    continue;
                }
                Vector3 high = hit.point + hit.normal * 0.4f + Vector3.up * TallEnoughMetres;
                if (!Physics.Raycast(high, -hit.normal, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;   // the wall does not go on up: a low wall or a fence is no escape
                }
                float cost = hit.distance * (1.5f - 0.5f * Vector3.Dot(way, away));   // towards his foe counts half again
                if (cost < best)
                {
                    best = cost;
                    _wall = hit.point;
                    _hasWall = true;
                }
            }
        }

        /// <summary>The nearest thing a ray meets that is not part of him (his own limbs' hit shapes stand round his chest).</summary>
        private bool NearestNotHim(Vector3 from, Vector3 way, float reach, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(from, way, _wallHits, reach, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_wallHits[i].distance < best && !_wallHits[i].collider.transform.IsChildOf(transform))
                {
                    best = _wallHits[i].distance;
                    nearest = _wallHits[i];
                }
            }
            return best < float.MaxValue;
        }
    }
}
