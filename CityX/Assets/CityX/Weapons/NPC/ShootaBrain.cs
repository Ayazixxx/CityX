using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE SHOOTA (owner, 2026-10-15: "create an npc called "shoota" and he is just a white playermodel and he wields the ak47 and make him a
    /// smart hostile npc so if you are nearby and in sight of him he starts shooting at you but also runs to cover if he is getting damaged and
    /// when he needs to reload"). /spawn shoota. The player's own body (Shoota.prefab, built from Player.prefab by ShootaBuilder: white, no input
    /// reader), driven like a player: this decides what a player would press each frame and hands it to PlayerLocomotion, so he walks, runs,
    /// crouches and shoulders the rifle with the player's own animation and hands-on-the-gun posing. What to do is ShootaMind's; where to hide,
    /// CoverPicker's.
    ///
    /// THE AK-47 is the real one (AK47.prefab), mounted in his hands the way another player's copy holds it (PistolWeapon.MountCosmetic: the
    /// two-handed rifle hold, the reload motion). His rounds are hitscan from the muzzle like the player's, with a spread that grows with range,
    /// your speed and the length of the burst; a hit hurts through DamageContext as a bullet (Armor counts), cracks a window it meets, and
    /// draws its tracer along the true path. They hurt a good deal less than the player's own AK (<see cref="DamageScale"/>).
    ///
    /// IN A SESSION (2026-10-27, NetworkedShoota.prefab): the machine that spawned him runs this; every other player's machine shows a copy with
    /// this switched off (NetworkedEnemy), whose rifle flashes, cracks and throws brass at each of his shots and plays his reloads (both
    /// announced by name, as a player's are). He shoots whichever player is nearest, another player's copy too (PlayerBodies). Offline he is the
    /// plain Shoota.prefab, as before.
    /// </summary>
    public sealed class ShootaBrain : MonoBehaviour, IHostile
    {
        [Tooltip("The gun he carries: the AK-47 prefab, mounted in his hands at spawn.")]
        public GameObject GunPrefab;

        [Tooltip("His rounds hurt this share of what the player's own AK does: a fair fight, not three hits and dead.")]
        public float DamageScale = 0.35f;

        [Tooltip("XP for killing him.")]
        public int XpReward = 60;

        /// <summary>How long his body lies there once dead, seconds.</summary>
        public const float CorpseSeconds = 25.0f;

        /// <summary>Close enough to his cover spot to count as in it, metres.</summary>
        public const float AtCoverMetres = 0.7f;

        /// <summary>How often he looks round for who to shoot, seconds (the search is not free).</summary>
        public const float TargetSearchSeconds = 1.0f;

        /// <summary>How often he re-checks that his cover still hides him from where the target is now, seconds.</summary>
        public const float CoverCheckSeconds = 0.5f;

        public ShootaState State => _mind.State;

        public int Magazine => _magazine;

        public int RoundsFired { get; private set; }

        public int Hits { get; private set; }

        public bool HasCover => _hasCover;

        public Vector3 CoverSpot => _cover;

        /// <summary>The longest one frame of the last cover search took, milliseconds (for the cost test).</summary>
        public float LastCoverSearchMs { get; private set; }

        /// <summary>How many rays the last cover search has cast.</summary>
        public int CoverSearchQueries => _search.Queries;

        /// <summary>The last cover search in a line (for the capture's log).</summary>
        public string CoverSearchSummary => $"{_search.Queries} rays, {_search.OpenSpots} open spots, {_search.DetourSpots} ways round tried";

        /// <summary>Who he is after (null when nobody): set by the search, or by a test.</summary>
        public IOpponent Target
        {
            get => _target;
            set
            {
                _target = value;
                _targetPinned = value != null;
            }
        }

        private readonly ShootaMind _mind = new ShootaMind(System.Environment.TickCount);
        private readonly NpcSteering _steering = new NpcSteering();
        private static readonly RaycastHit[] Hits16 = new RaycastHit[16];

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private PlayerAnimationDriver _driver;
        private CharacterController _controller;
        private Animator _animator;
        private Ragdoll _ragdoll;
        private Transform _head;
        private PistolWeapon _gun;
        private FirearmProfile _profile;
        private Vector3 _actionRest;
        private int _magazine;
        private bool _reloadStarted;
        private float _reloadTimer;
        private IOpponent _target;
        private bool _targetPinned;
        private float _nextSearch;
        private Vector3 _lastSeen;
        private bool _hasLastSeen;
        private bool _alerted;
        private bool _tookDamage;
        private Vector3 _cover;
        private Vector3 _via;
        private bool _viaReached = true;
        private readonly CoverSearch _search = new CoverSearch();
        private CoverPicker.Blocked _blocked;
        private Transform _searchThreat;
        private float _wantCoverUntil;

        /// <summary>The rays one frame of a cover search may cast: about half a millisecond in the city.</summary>
        public const int CoverQueriesPerFrame = 48;
        private bool _hasCover;
        private float _nextCoverCheck;
        private float _fireCooldown;
        private int _roundInBurst;
        private float _sinceShot = 999.0f;
        private float _deadFor;
        private bool _removed;
        private Vector3 _targetLastPosition;
        private float _targetSpeed;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            // What a blow does to him: the freeze, the shove, the blood (as on the player's body, where PlayerInputReader adds it).
            if (GetComponent<HitImpactEffects>() == null)
            {
                gameObject.AddComponent<HitImpactEffects>();
            }
            _driver = GetComponent<PlayerAnimationDriver>();
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _ragdoll = GetComponent<Ragdoll>();
            _head = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (_controller != null)
            {
                _steering.ConfigureFrom(_controller);
            }
            _blocked = (a, b) => SolidBetween(a, b, _searchThreat != null ? _searchThreat : (Opponents.Alive(_target) ? _target.transform : null));
            if (_vitals != null)
            {
                _vitals.Damaged += OnDamaged;
                _vitals.Died += OnDied;
                _vitals.XpRewardOnDeath = XpReward;
            }
            MountGun();
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.Damaged -= OnDamaged;
                _vitals.Died -= OnDied;
            }
        }

        private void OnEnable()
        {
            EnemyPack.Join(transform);
        }

        private void OnDisable()
        {
            EnemyPack.Leave(transform);
        }

        /// <summary>
        /// Killed: his rifle falls from his hands for the player to pick up (owner, 2026-10-17). In a session the machine that runs him drops it
        /// for everyone; another player's copy of him only lets go of its own.
        /// </summary>
        private void OnDied()
        {
            EnemyPack.Leave(transform);
            if (_gun == null)
            {
                return;
            }
            if (!EnemySession.IsCopy(gameObject))
            {
                var pose = new Pose(_gun.transform.position, _gun.transform.rotation);
                Vector3 bodyVelocity = _locomotion != null ? _locomotion.Velocity : Vector3.zero;
                DroppedItems.Shared.Drop(DroppedItem.LetGo(_gun.ItemId, ItemInstanceData.None, pose, transform, bodyVelocity, _vitals.DeathImpulse),
                    shared: EnemySession.IsShared(gameObject), gameObject, GunPrefab);
            }
            _gun.UnmountCosmetic(gameObject);
            if (_locomotion != null)
            {
                _locomotion.GunMuzzle = null;
            }
            Destroy(_gun.gameObject);
            _gun = null;
        }

        private void OnDamaged(float amount)
        {
            _tookDamage = true;
            _alerted = true;
        }

        private float Height => _locomotion != null ? _locomotion.StandingHeight : 2.6f;

        private Vector3 Eye => _head != null ? _head.position : transform.position + Vector3.up * (Height * 0.92f);

        /// <summary>The AK-47 in his hands, held as another player's copy holds one (two hands, the reload motion).</summary>
        private void MountGun()
        {
            if (GunPrefab == null)
            {
                return;
            }
            GameObject gun = Instantiate(GunPrefab);
            gun.name = GunPrefab.name;
            _gun = gun.GetComponent<PistolWeapon>();
            if (_gun == null)
            {
                Destroy(gun);
                return;
            }
            _profile = _gun.Profile;
            _gun.MountCosmetic(gameObject);
            _actionRest = _gun.ActionPart != null ? _gun.ActionPart.localPosition : Vector3.zero;
            if (_locomotion != null && _gun.MuzzleFlashObject != null)
            {
                _locomotion.GunMuzzle = _gun.MuzzleFlashObject.transform;
            }
            _magazine = _profile.MagazineSize;
            foreach (Collider c in gun.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;   // a prop in his hands, not something to trip over
            }
        }

        private void Update()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            if (_vitals != null)
            {
                _vitals.TickHitstun(deltaTime);
            }
            bool dead = _vitals != null && _vitals.IsDead;
            if (dead)
            {
                _mind.Tick(new ShootaMind.Senses { Dead = true }, deltaTime);
                _deadFor += deltaTime;
                if (_deadFor >= CorpseSeconds && !_removed)
                {
                    _removed = true;
                    EnemySession.Remove(gameObject);   // in a session the server takes him out on every machine
                }
                return;
            }

            // Knocked down (Self Smite's burst, a finisher): the ragdoll has him until he gets up, as the player's body does.
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
            TrackTargetSpeed(deltaTime);
            bool sees = Opponents.Alive(_target) && CanSee(_target);
            if (sees)
            {
                _alerted = true;
                _lastSeen = _target.transform.position;
                _hasLastSeen = true;
            }
            bool reloading = IsReloading();
            if (_tookDamage)
            {
                _wantCoverUntil = Time.time + ShootaMind.HurtMemorySeconds;
            }
            bool wantsCover = Time.time < _wantCoverUntil || _mind.State == ShootaState.TakeCover || _mind.State == ShootaState.Reload
                              || _magazine <= ShootaMind.LowAmmo;
            RefreshCover(wantsCover);

            var senses = new ShootaMind.Senses
            {
                HasTarget = Opponents.Alive(_target),
                SeesTarget = sees,
                TargetDistance = Opponents.Alive(_target) ? Vector3.Distance(transform.position, _target.transform.position) : float.MaxValue,
                Magazine = _magazine,
                MagazineSize = _profile != null ? _profile.MagazineSize : 30,
                Reloading = reloading,
                TookDamage = _tookDamage,
                HasCover = _hasCover,
                AtCover = _hasCover && Flat(transform.position - _cover).magnitude <= AtCoverMetres,
            };
            _tookDamage = false;
            ShootaState state = _mind.Tick(senses, deltaTime);

            bool stunned = _vitals != null && _vitals.IsStunned;
            if (_locomotion == null || !_locomotion.enabled)
            {
                return;
            }
            LocomotionInput input = stunned ? Stand() : Act(state, sees, reloading, deltaTime);
            _sinceShot += deltaTime;
            _locomotion.Tick(input, deltaTime);
        }

        // --- what he does ---

        private NpcWander _wander;

        /// <summary>Wandering, the way he is steered (the body comes round to it as it walks).</summary>
        private float _strollYaw;

        private LocomotionInput Act(ShootaState state, bool sees, bool reloading, float deltaTime)
        {
            _fireCooldown -= deltaTime;
            if (state != ShootaState.Idle)
            {
                _wander?.Reset();   // he wanders again from wherever the fight leaves him
            }
            switch (state)
            {
                case ShootaState.Engage:
                {
                    LocomotionInput input = AimAt(TargetAimPoint());
                    // In a pack (EnemyPack): he shoots on his turn (two at a time), never through a packmate -- he steps aside to clear the
                    // line instead -- and keeps a step from the others.
                    bool turn = Opponents.Alive(_target) && EnemyPack.Turns.Ask(transform, _target, TurnKind.Ranged, Time.time);
                    bool blocked = Opponents.Alive(_target) && EnemyPack.AllyInTheWay(transform, transform.position, _target.transform.position);
                    if (blocked)
                    {
                        float side = Opponents.Alive(_target) ? -EnemyPack.CrowdSide(transform, _target.transform.position) : 0.0f;
                        input.Move = new Vector2(side != 0.0f ? side : 1.0f, 0.0f);
                    }
                    else
                    {
                        input.Move = EnemyPack.SeparationMove(transform, input.YawDegrees);
                    }
                    if (_mind.ShouldFire && sees && turn && !blocked && _fireCooldown <= 0.0f && _magazine > 0 && !reloading)
                    {
                        Fire();
                    }
                    return input;
                }
                case ShootaState.TakeCover:
                    return _hasCover ? MoveTo(NextCoverStep(), run: true, deltaTime) : Stand();
                case ShootaState.Reload:
                {
                    TickReload();
                    // In cover: crouched behind it; caught in the open: still facing the threat.
                    bool atCover = _hasCover && Flat(transform.position - _cover).magnitude <= AtCoverMetres * 2.0f;
                    LocomotionInput input = Stand();
                    input.Crouch = atCover;
                    input.GunEquipped = true;
                    if (Opponents.Alive(_target))
                    {
                        input.YawDegrees = YawTo(_target.transform.position);
                    }
                    return input;
                }
                case ShootaState.Chase:
                    return _hasLastSeen ? MoveTo(_lastSeen, run: false, deltaTime) : Stand();
                default:
                {
                    // Nobody seen: he wanders (NpcWander), his rifle carried, as the swordsman does.
                    _wander ??= new NpcWander(GetInstanceID(), turnsOnTheSpot: false);
                    // (Standing, he is steered the way he faces: a stroll sets off from there and comes round.)
                    NpcWander.Step stroll = _wander.Tick(NpcWanderProbe.SenseWalker(transform, _wander.Walking ? _strollYaw : transform.eulerAngles.y), deltaTime);
                    _strollYaw = stroll.YawDegrees;
                    return new LocomotionInput { Move = new Vector2(0.0f, stroll.Move), YawDegrees = stroll.YawDegrees, GunEquipped = true };
                }
            }
        }

        /// <summary>Where to run next on the way to cover: the corner to turn at first (CoverPicker's way round a building), then the spot.</summary>
        private Vector3 NextCoverStep()
        {
            if (!_viaReached && Flat(transform.position - _via).magnitude <= AtCoverMetres * 1.5f)
            {
                _viaReached = true;
            }
            return _viaReached ? _cover : _via;
        }

        private LocomotionInput Stand()
        {
            return new LocomotionInput { YawDegrees = transform.eulerAngles.y, GunEquipped = true };
        }

        /// <summary>Facing the target with the rifle shouldered, the barrel on the point (the player's own aiming, from his eye).</summary>
        private LocomotionInput AimAt(Vector3 point)
        {
            Vector3 eye = Eye;
            Vector3 toPoint = point - eye;
            return new LocomotionInput
            {
                YawDegrees = YawTo(point),
                GunEquipped = true,
                Aiming = true,
                AimCameraPosition = eye,
                AimCameraForward = toPoint.sqrMagnitude > 1e-4f ? toPoint.normalized : transform.forward,
                GunAimPoint = point,
                HasGunAimPoint = true,
            };
        }

        /// <summary>Runs (or walks) for a spot, round whatever is in the way (NpcSteering, the civilians' own).</summary>
        private LocomotionInput MoveTo(Vector3 spot, bool run, float deltaTime)
        {
            Vector3 toSpot = Flat(spot - transform.position);
            if (toSpot.magnitude <= AtCoverMetres * 0.6f)
            {
                LocomotionInput still = Stand();
                still.Crouch = run;
                return still;
            }
            Vector3 steer = _steering.Steer(transform, toSpot.normalized, deltaTime);
            if (steer.sqrMagnitude < 1e-4f)
            {
                steer = toSpot.normalized;
            }
            float yaw = Mathf.Atan2(steer.x, steer.z) * Mathf.Rad2Deg;
            return new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = yaw, Sprint = run, GunEquipped = true };
        }

        // --- the gun ---

        private void Fire()
        {
            if (_gun == null || _profile == null)
            {
                return;
            }
            _fireCooldown = 1.0f / Mathf.Max(1.0f, _profile.FireRatePerSecond);
            _roundInBurst = _sinceShot <= _profile.RecoilResetSeconds ? _roundInBurst + 1 : 0;
            _sinceShot = 0.0f;
            _magazine--;
            RoundsFired++;
            _mind.Fired();

            Transform muzzle = _gun.MuzzleFlashObject != null ? _gun.MuzzleFlashObject.transform : _gun.transform;
            Vector3 origin = muzzle.position;
            Vector3 aim = TargetAimPoint() - origin;
            float distance = aim.magnitude;
            float spread = ShootaMind.SpreadDegrees(distance, _targetSpeed, _roundInBurst);
            Vector3 direction = Spread(aim.normalized, spread);

            Vector3 end = origin + direction * _profile.Range;
            if (Trace(origin, direction, _profile.Range, out RaycastHit hit))
            {
                // On through any windows in the way, as a player's bullet goes (GlassPenetration).
                if (GlassPenetration.Follow(ref hit, direction, _profile.Range - hit.distance, true,
                        (Vector3 from, float reach, out RaycastHit next) => Trace(from, direction, reach, out next),
                        out int panes, out end))
                {
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    float damage = _profile.Damage * DamageScale * GlassPenetration.DamageShare(panes);
                    if (target != null && target.IsAlive && !ReferenceEquals(target, _vitals)
                        && DamageContext.Strike(target, damage, origin, 0.0f, DamageKind.Bullet, 0.0f, hit.point, gameObject)
                        != HitResponse.Ignored)
                    {
                        Hits++;
                        if (!(target is PlayerVitals))
                        {
                            BloodBurst.Spawn(hit.point, hit.point - origin, 0.6f);
                        }
                    }
                }
            }

            // What the player sees and hears of it: the flash, the crack, the tracer down the true path, the brass, the bolt.
            FirearmEffects.Flash(_gun.MuzzleFlashObject, 0.05f);
            if (_gun.MuzzleSparks != null)
            {
                _gun.MuzzleSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _gun.MuzzleSparks.Play();
            }
            if (_gun.ShotPlayer != null && _gun.ShotPlayer.clip != null)
            {
                AudioSource.PlayClipAtPoint(_gun.ShotPlayer.clip, origin, 1.0f);
            }
            if (_gun.ShowsTracer(_roundInBurst))
            {
                BulletTracer.Spawn(origin, end, muzzle);
            }
            FirearmEffects.EjectCasing(_gun.transform.TransformPoint(_profile.EjectionPortLocal), _gun.transform.rotation,
                _gun.transform.TransformDirection(_profile.EjectionDirectionLocal), _profile.CasingLength, _profile.CasingRadius, gameObject);
            FirearmEffects.Cycle(_gun.ActionPart, _actionRest, _profile.ActionTravel, _profile.ActionCycleSeconds);
            if (_locomotion != null)
            {
                _locomotion.NotifyGunFired();
            }
            _driver?.AnnounceState(GunReload.FireName);   // every other player's copy of him shows the shot
        }

        private bool IsReloading()
        {
            if (!_reloadStarted)
            {
                return false;
            }
            GunReloadDriver reloader = _driver != null ? _driver.GunReloader : null;
            return (reloader != null && reloader.IsPlaying) || _reloadTimer > 0.0f;
        }

        /// <summary>Starts the magazine change (the AK's own reload motion, empty or tactical), and fills the magazine when it is done.</summary>
        private void TickReload()
        {
            if (_profile == null)
            {
                return;
            }
            if (!_reloadStarted)
            {
                if (_magazine >= _profile.MagazineSize)
                {
                    return;
                }
                _reloadStarted = true;
                bool empty = _magazine <= 0;
                _reloadTimer = empty ? _profile.EmptyReloadSeconds : _profile.ReloadSeconds;
                if (_driver != null)
                {
                    _driver.PlayRemoteState(GunReload.NameFor(empty));
                    _driver.AnnounceState(GunReload.NameFor(empty));   // and on every other player's copy of him
                }
                return;
            }
            _reloadTimer -= Time.deltaTime;
            if (!IsReloading())
            {
                _reloadStarted = false;
                _magazine = _profile.MagazineSize;
            }
        }

        /// <summary>The first solid thing (not himself) along a shot.</summary>
        private bool Trace(Vector3 origin, Vector3 direction, float range, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, Hits16, range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits16[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= best)
                {
                    continue;
                }
                best = hit.distance;
                nearest = hit;
                found = true;
            }
            return found;
        }

        private static Vector3 Spread(Vector3 direction, float degrees)
        {
            if (degrees <= 0.0f)
            {
                return direction;
            }
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.right;
            }
            side.Normalize();
            Vector3 up = Vector3.Cross(side, direction).normalized;
            Vector2 offset = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (direction + side * offset.x + up * offset.y).normalized;
        }

        // --- what he sees ---

        /// <summary>
        /// Whom to shoot: the nearest living player -- this machine's own, or in a session another player's copy (PlayerBodies), whose own body
        /// his rounds then reach through it. Searched once a second.
        /// </summary>
        private void FindTarget()
        {
            if (_targetPinned)
            {
                if (!Opponents.Here(_target))
                {
                    _targetPinned = false;
                }
                return;
            }
            if (Time.time < _nextSearch && (_target == null || Opponents.Alive(_target)))   // once a second, target or none (SwordsmanBrain.FindTarget)
            {
                return;
            }
            _nextSearch = Time.time + TargetSearchSeconds;
            _target = Opponents.NearestAlive(transform.position, float.MaxValue);   // a player or a summon on their side
        }

        private void TrackTargetSpeed(float deltaTime)
        {
            if (!Opponents.Alive(_target) || deltaTime <= 0.0f)
            {
                _targetSpeed = 0.0f;
                return;
            }
            Vector3 now = _target.transform.position;
            float speed = Flat(now - _targetLastPosition).magnitude / deltaTime;
            _targetSpeed = Mathf.Lerp(_targetSpeed, Mathf.Min(speed, 20.0f), 0.2f);
            _targetLastPosition = now;
        }

        /// <summary>Within sight range, in front of him unless he is already alerted, and nothing solid between his eye and the target's.</summary>
        private bool CanSee(IOpponent target)
        {
            Vector3 eye = Eye;
            Vector3 chest = TargetAimPoint();
            Vector3 toTarget = chest - eye;
            float distance = toTarget.magnitude;
            if (distance > ShootaMind.SightRange)
            {
                return false;
            }
            if (!_alerted && Vector3.Angle(Flat(transform.forward), Flat(toTarget)) > ShootaMind.SightHalfAngle)
            {
                return false;
            }
            Vector3 targetHead = target.transform.position + Vector3.up * (Height * 0.9f);
            return !SolidBetween(eye, chest, target.transform) || !SolidBetween(eye, targetHead, target.transform);
        }

        /// <summary>The middle of the target's chest: what he aims at.</summary>
        private Vector3 TargetAimPoint()
        {
            if (!Opponents.Alive(_target))
            {
                return transform.position + transform.forward * 10.0f + Vector3.up * (Height * 0.6f);
            }
            return _target.transform.position + Vector3.up * (Height * 0.62f);
        }

        /// <summary>Something solid between two points: not either body (his, the target's), not glass breaking under fire.</summary>
        private bool SolidBetween(Vector3 from, Vector3 to, Transform alsoIgnore)
        {
            Vector3 along = to - from;
            float distance = along.magnitude;
            if (distance < 1e-3f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(from, along / distance, Hits16, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits16[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || (alsoIgnore != null && c.transform.IsChildOf(alsoIgnore)))
                {
                    continue;
                }
                if (c.GetComponentInParent<IDamageable>() != null || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic))
                {
                    continue;   // bodies and loose things are not cover
                }
                return true;
            }
            return false;
        }

        // --- cover ---

        /// <summary>
        /// Keeps his cover current while he wants some (hurt, running for it, reloading, low on rounds): the spot he has is checked every half
        /// second against where the target is now, and when it no longer hides him a new search is started and stepped a few dozen rays a
        /// frame (CoverSearch), so looking round never costs a frame more than about half a millisecond.
        /// </summary>
        private void RefreshCover(bool wanted)
        {
            if (!Opponents.Alive(_target))
            {
                _search.Cancel();
                return;
            }
            Vector3 threatEye = _target.transform.position + Vector3.up * (Height * 0.9f);
            if (_search.Running)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                bool done = _search.Step(CoverQueriesPerFrame);
                LastCoverSearchMs = Mathf.Max(LastCoverSearchMs, (float)watch.Elapsed.TotalMilliseconds);
                if (done)
                {
                    _hasCover = _search.Found;
                    _cover = _search.Cover;
                    _via = _search.Via;
                    _viaReached = !_hasCover || Flat(_via - _cover).sqrMagnitude < 0.01f;
                }
                return;
            }
            if (!wanted || Time.time < _nextCoverCheck)
            {
                return;
            }
            _nextCoverCheck = Time.time + CoverCheckSeconds;
            if (_hasCover && CoverPicker.IsHidden(_cover, Height, threatEye, _blocked))
            {
                return;   // still good
            }
            LastCoverSearchMs = 0.0f;
            _searchThreat = _target.transform;
            _search.Begin(transform.position, Height, threatEye, _blocked, GroundBelow);
        }

        private bool GroundBelow(Vector3 above, out Vector3 point)
        {
            point = above;
            int count = Physics.RaycastNonAlloc(above, Vector3.down, Hits16, 6.0f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits16[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= best
                    || hit.collider.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                best = hit.distance;
                point = hit.point;
                found = true;
            }
            return found;
        }

        private float YawTo(Vector3 point)
        {
            Vector3 to = Flat(point - transform.position);
            return to.sqrMagnitude > 1e-4f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
