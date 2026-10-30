using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FLAME STAFF'S STREAM (owner, 2026-10-28: "make a fire staff that functions like a flamethrower but costs mana"; docs/STAFFS.md). Held,
    /// a stream of fire from the staff's head along the crosshair (<see cref="FlameStreamRules"/>): everyone in its cone burns, a tick every
    /// 0.15 s for as long as it is held on them, a spell (Spell Power and Armor Penetration apply, magic resistance stops a share), with no
    /// stagger (the flamethrower's own choice: a stream that locked its target every frame would never let it move) and no blood (fire does
    /// not cut: DamageContext.WithoutBlood). It drinks mana while held and stops when the mana is gone.
    /// Rain halves it and shortens it, as it does the flamethrower. The fire is the flamethrower's (<see cref="FlameStreamLook"/>, a prefab).
    ///
    /// Other players see it (<see cref="IReplicatedAbility"/>): the stream starting on this body's staff, where it points (sent ten times a
    /// second), and stopping. Only this machine's stream burns.
    /// </summary>
    public sealed class FlameStreamCaster : MonoBehaviour, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "flame-stream";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): Origin the staff's head, Direction the stream's.</summary>
        public const int StreamStarted = 0;
        public const int StreamAimed = 1;
        public const int StreamStopped = 2;

        /// <summary>A copy's stream runs on this long past the last word of it, seconds: a missed aim event is not a stop.</summary>
        public const float RemoteGraceSeconds = 0.4f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("The fire (FlameStreamLook, built by FlamethrowerBuilder.BuildStaffFlame). Set by PlayerAbilityWiring.")]
        public GameObject FlamePrefab;

        private readonly List<IDamageable> _inStream = new List<IDamageable>();
        private readonly Dictionary<IDamageable, float> _nextTick = new Dictionary<IDamageable, float>();
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private FlameStreamLook _flame;
        private FlameStreamLook _remoteFlame;
        private float _aimSendIn;
        private float _remoteLeft;
        private float _held;
        private float _igniteDue;

        /// <summary>This body's stream is on.</summary>
        public bool IsBurning { get; private set; }

        /// <summary>How long the stream has been held, seconds.</summary>
        public float HeldSeconds => IsBurning ? _held : 0.0f;

        /// <summary>The bodies the stream is on this frame.</summary>
        public IReadOnlyList<IDamageable> InStream => _inStream;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs (the casters' idiom).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            bool alive = _vitals == null || !_vitals.IsDead;
            bool fed = _vitals == null || _vitals.TrySpendMana(held ? FlameStreamRules.ManaPerSecond * deltaTime : 0.0f);
            bool burning = held && alive && fed;
            Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            Vector3 direction = PlayerInputReader.CrosshairDirection(from, eye, look, transform);
            if (burning && !IsBurning)
            {
                Start(tip, from, direction);
            }
            else if (!burning && IsBurning)
            {
                Stop();
            }
            if (!IsBurning)
            {
                return;
            }
            _held += deltaTime;
            _locomotion?.HoldStaffOut();
            Burn(from, direction, deltaTime);
            _aimSendIn -= deltaTime;
            if (_aimSendIn <= 0.0f)
            {
                _aimSendIn = FlameStreamRules.AimSendSeconds;
                Fired?.Invoke(new AbilityEvent(StreamAimed, from, direction));
            }
        }

        public void CancelStaffCharge()
        {
            Stop();
        }

        private void Start(Transform tip, Vector3 from, Vector3 direction)
        {
            IsBurning = true;
            _held = 0.0f;
            _aimSendIn = FlameStreamRules.AimSendSeconds;
            _nextTick.Clear();
            if (_flame == null)
            {
                _flame = MakeFlame(tip);
            }
            _flame?.Run();
            Fired?.Invoke(new AbilityEvent(StreamStarted, from, direction));
        }

        private void Stop()
        {
            if (!IsBurning)
            {
                return;
            }
            IsBurning = false;
            _inStream.Clear();
            _flame?.Stop();
            Fired?.Invoke(new AbilityEvent(StreamStopped, transform.position, Vector3.zero));
        }

        /// <summary>One frame of the stream: everyone in its cone is in the fire, and burns a tick when their next is due.</summary>
        private void Burn(Vector3 from, Vector3 direction, float deltaTime)
        {
            float flame = Weather.FlameStrength(CurrentWeather.RainOn(from));
            float reach = FlameStreamRules.ReachFor(flame);
            _flame?.Aim(from, direction, deltaTime);
            FireScene.IgniteAlongStream(from, direction, reach, transform, gameObject, ref _igniteDue, deltaTime);   // the ground catches (docs/FIRE.md)

            // A spell: Spell Power makes it hurt more (2% a point), and the caster's Armor Penetration goes with it.
            float spellPower = PlayerVitals.SpellPowerOf(_vitals);
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float tick = CombatTuning.DamageFor(FlameStreamRules.DamagePerTick, spellPower) * flame;

            _inStream.Clear();
            Vector3 centre = from + direction * (reach * 0.5f);
            foreach (Collider collider in Physics.OverlapSphere(centre, reach * 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || _inStream.Contains(target) || !(target is Component body) || body.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (!FlameStreamRules.InStream(from, direction, collider.bounds.center, reach))
                {
                    continue;
                }
                _inStream.Add(target);
                if (!_nextTick.TryGetValue(target, out float next) || Time.time >= next)
                {
                    _nextTick[target] = Time.time + FlameStreamRules.TickSeconds;
                    using (DamageContext.WithoutBlood())   // fire does not cut
                    {
                        DamageContext.Strike(target, tick, from, 0.0f, DamageKind.Magic, penetration, collider.bounds.center, gameObject);
                    }
                }
            }
        }

        /// <summary>The fire at a staff's head, from the prefab, or nothing without one (a test body with no wiring).</summary>
        private FlameStreamLook MakeFlame(Transform tip)
        {
            if (FlamePrefab == null || tip == null)
            {
                return null;
            }
            GameObject flame = Instantiate(FlamePrefab, tip.position, tip.rotation, tip);
            flame.name = "StaffFlame";
            return flame.GetComponent<FlameStreamLook>();
        }

        // --- what other players see ---

        private void Update()
        {
            if (_remoteLeft <= 0.0f)
            {
                return;
            }
            _remoteLeft -= Time.deltaTime;
            _locomotion?.HoldStaffOut();
            if (_remoteLeft <= 0.0f)
            {
                _remoteFlame?.Stop();
            }
        }

        /// <summary>Another player's stream, on this copy of their staff: burning and pointing where they point it, their staff held out, stopping with theirs.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case StreamStarted:
                case StreamAimed:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    Transform tip = StaffItem.TipOf(_locomotion);
                    if (_remoteFlame == null)
                    {
                        _remoteFlame = MakeFlame(tip);
                    }
                    if (_remoteFlame == null)
                    {
                        return;
                    }
                    _remoteFlame.Run();
                    _remoteFlame.Aim(tip != null ? tip.position : evt.Origin, evt.Direction.normalized, FlameStreamRules.AimSendSeconds);
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case StreamStopped:
                    _remoteLeft = 0.0f;
                    _remoteFlame?.Stop();
                    break;
            }
        }
    }
}
