using CityX.Character;
using CityX.Weapons;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>What one of the crowd test's players does: runs about, fights, or casts.</summary>
    public enum CrowdRole
    {
        Runner,
        Fighter,
        Caster,
    }

    /// <summary>
    /// One player of the crowd test (CrowdCapture), driven the way PlayerInputReader drives the player's own body: it picks a spot on the street
    /// near the crowd's middle and runs or walks to it, then another; a fighter stops now and then and attacks with what it holds (a sword's
    /// cut, a bat or fists, a gun's shot); a caster throws a spell (a fireball, ice shards, a poison cloud, now and then Self Smite). Every
    /// body is fully simulated, as the owner's own body is, so the crowd costs a little more than other players' copies would.
    /// </summary>
    public sealed class CrowdMember : MonoBehaviour
    {
        /// <summary>How far from the crowd's middle a member wanders, metres.</summary>
        public float Radius = 18.0f;

        public CrowdRole Role;
        public Vector3 Home;

        /// <summary>Seconds between a fighter's attacks and a caster's spells, at random within this range.</summary>
        public Vector2 ActionEvery = new Vector2(1.5f, 4.0f);

        /// <summary>The share of casts that are Self Smite (a big one: lightning from the sky and everyone near knocked down).</summary>
        public float SmiteShare = 0.04f;

        private System.Random _random;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Ragdoll _ragdoll;
        private MeleeAttacker _melee;
        private Inventory _inventory;
        private FireballCaster _fireball;
        private IceShardCaster _ice;
        private PoisonBreathCaster _poison;
        private SelfSmiteAbility _smite;
        private Collider[] _own;
        private Vector3 _goal;
        private float _goalFor;
        private float _nextAction;
        private float _standStill;
        private bool _sprint;

        public void Begin(int seed, CrowdRole role, Vector3 home)
        {
            _random = new System.Random(seed);
            Role = role;
            Home = home;
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _ragdoll = GetComponent<Ragdoll>();
            _melee = GetComponent<MeleeAttacker>();
            _inventory = GetComponent<Inventory>();
            _fireball = GetComponent<FireballCaster>();
            _ice = GetComponent<IceShardCaster>();
            _poison = GetComponent<PoisonBreathCaster>();
            _smite = GetComponent<SelfSmiteAbility>();
            _own = GetComponentsInChildren<Collider>(true);
            if (_vitals != null)
            {
                _vitals.InfiniteHealth = true;   // a test crowd: blows and spells land, nobody dies
            }
            _nextAction = Range(0.0f, ActionEvery.y);
            PickGoal();
        }

        private float Range(float from, float to) => from + (float)_random.NextDouble() * (to - from);

        private void PickGoal()
        {
            for (int tries = 0; tries < 8; tries++)
            {
                float angle = Range(0.0f, Mathf.PI * 2.0f);
                float distance = Radius * Mathf.Sqrt(Range(0.05f, 1.0f));
                Vector3 spot = Home + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * distance;
                // On the street, not on a roof or in a hole: ground near the crowd's own level, open sky over it.
                if (Physics.Raycast(spot + Vector3.up * 40.0f, Vector3.down, out RaycastHit hit, 80.0f, ~0, QueryTriggerInteraction.Ignore)
                    && Mathf.Abs(hit.point.y - Home.y) < 1.5f)
                {
                    _goal = hit.point;
                    break;
                }
            }
            _goalFor = Range(5.0f, 10.0f);
            _sprint = _random.NextDouble() < 0.5;
        }

        private void Update()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            if (_locomotion == null || deltaTime <= 0.0f)
            {
                return;
            }
            if (_vitals != null)
            {
                _vitals.TickHitstun(deltaTime);
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

            Vector3 toGoal = _goal - transform.position;
            toGoal.y = 0.0f;
            _goalFor -= deltaTime;
            if (toGoal.magnitude < 1.5f || _goalFor <= 0.0f)
            {
                PickGoal();
                toGoal = _goal - transform.position;
                toGoal.y = 0.0f;
            }
            float yaw = toGoal.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toGoal).eulerAngles.y : transform.eulerAngles.y;

            _nextAction -= deltaTime;
            if (_nextAction <= 0.0f)
            {
                _nextAction = Range(ActionEvery.x, ActionEvery.y);
                if (Role == CrowdRole.Fighter)
                {
                    Attack(yaw);
                }
                else if (Role == CrowdRole.Caster)
                {
                    Cast();
                }
            }
            _standStill = Mathf.Max(0.0f, _standStill - deltaTime);

            var input = new LocomotionInput
            {
                Move = _standStill > 0.0f ? Vector2.zero : Vector2.up,
                YawDegrees = yaw,
                Sprint = _sprint && _standStill <= 0.0f,
            };
            _locomotion.Tick(input, deltaTime);
            _melee?.Tick(deltaTime);
            Sword?.Tick(deltaTime);
            _fireball?.Tick(deltaTime);
            _ice?.Tick(deltaTime);
            _poison?.Tick(deltaTime);
        }

        private SoulsSwordCombat Sword
        {
            get
            {
                var sword = GetComponent<SoulsSwordCombat>();
                return sword != null && sword.IsEquipped ? sword : null;
            }
        }

        /// <summary>A fighter stands and strikes with what it holds.</summary>
        private void Attack(float yaw)
        {
            _standStill = 0.8f;
            GameObject held = _inventory != null ? _inventory.EquippedInstance(_inventory.ActiveWeaponSlot) : null;
            var gun = held != null ? held.GetComponent<PistolWeapon>() : null;
            if (gun != null)
            {
                Vector3 origin = transform.position + Vector3.up * 2.2f;
                gun.Fire(origin, Quaternion.Euler(0.0f, yaw + Range(-30.0f, 30.0f), 0.0f) * Vector3.forward, true, gameObject);
                return;
            }
            if (Sword != null)
            {
                Sword.RequestLight(yaw);
                return;
            }
            _melee?.TryAttack(_random.NextDouble() < 0.2);
        }

        /// <summary>A caster throws a spell ahead of it.</summary>
        private void Cast()
        {
            Vector3 origin = transform.position + Vector3.up * 2.0f + transform.forward * 0.6f;
            Vector3 direction = Quaternion.Euler(Range(-8.0f, 2.0f), transform.eulerAngles.y + Range(-25.0f, 25.0f), 0.0f) * Vector3.forward;
            double pick = _random.NextDouble();
            if (_smite != null && pick < SmiteShare)
            {
                _smite.TryCast();
                return;
            }
            pick = _random.NextDouble();
            if (pick < 0.45 && _fireball != null)
            {
                _fireball.TryCast(origin, direction, _own);
            }
            else if (pick < 0.8 && _ice != null)
            {
                _ice.TryCast(origin, direction, _own);
            }
            else if (_poison != null)
            {
                _poison.TryCast(origin, direction, _own);
            }
        }
    }
}
