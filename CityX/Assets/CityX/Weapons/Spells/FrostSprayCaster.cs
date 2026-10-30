using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The Frost Staff's spray (owner, 2026-10-29: "a frost staff, it basically works like mei's freeze gun in overwatch, you hold click to spray
    /// a cloud of frost and it does a low amount of consecutive damage and slows them down then eventually freezes them"; docs/FROST.md). Held, a
    /// short cone of frost from the staff's head along the crosshair (<see cref="FrostRules"/>): whoever is in it takes a little magic damage a
    /// tick at a time, with no blood, and each tick chills them (<see cref="DamageContext.Chilling"/>, taken on the body's own machine by its
    /// <see cref="Chill"/>): slowed the colder they are, frozen solid once full. It drinks mana while held. Drawn in code (<see cref="FrostSpray"/>).
    ///
    /// Other players see it (<see cref="IReplicatedAbility"/>): the spray starting on this body's staff, where it points (ten times a second),
    /// and stopping. Only this machine's spray hurts and chills.
    /// </summary>
    public sealed class FrostSprayCaster : MonoBehaviour, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "frost-spray";

        public const int SprayStarted = 0;
        public const int SprayAimed = 1;
        public const int SprayStopped = 2;

        /// <summary>A copy's spray runs on this long past the last word of it, seconds.</summary>
        public const float RemoteGraceSeconds = 0.4f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private readonly List<IDamageable> _inSpray = new List<IDamageable>();
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private FrostSpray _spray;
        private FrostSpray _remoteSpray;
        private float _sinceTick;
        private float _aimSendIn;
        private float _remoteLeft;
        private Vector3 _remoteDirection;

        public bool IsSpraying { get; private set; }

        /// <summary>The bodies in the spray this frame.</summary>
        public IReadOnlyList<IDamageable> InSpray => _inSpray;

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
            bool fed = _vitals == null || _vitals.TrySpendMana(held ? FrostRules.ManaPerSecond * deltaTime : 0.0f);
            bool spraying = held && alive && fed;
            Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            Vector3 direction = PlayerInputReader.CrosshairDirection(from, eye, look, transform);
            if (spraying && !IsSpraying)
            {
                Begin(tip, from, direction);
            }
            else if (!spraying && IsSpraying)
            {
                End();
            }
            if (!IsSpraying)
            {
                return;
            }
            _locomotion?.HoldStaffOut();
            _spray?.Spray(from, direction, deltaTime);
            Freeze(from, direction, deltaTime);
            _aimSendIn -= deltaTime;
            if (_aimSendIn <= 0.0f)
            {
                _aimSendIn = FrostRules.AimSendSeconds;
                Fired?.Invoke(new AbilityEvent(SprayAimed, from, direction));
            }
        }

        public void CancelStaffCharge()
        {
            End();
        }

        private void Begin(Transform tip, Vector3 from, Vector3 direction)
        {
            IsSpraying = true;
            _sinceTick = FrostRules.TickSeconds;   // the first tick lands at once
            _aimSendIn = FrostRules.AimSendSeconds;
            if (_spray == null && tip != null)
            {
                _spray = FrostSpray.Make(tip);
            }
            _spray?.Run();
            Fired?.Invoke(new AbilityEvent(SprayStarted, from, direction));
        }

        private void End()
        {
            if (!IsSpraying)
            {
                return;
            }
            IsSpraying = false;
            _inSpray.Clear();
            _spray?.Stop();
            Fired?.Invoke(new AbilityEvent(SprayStopped, transform.position, Vector3.zero));
        }

        /// <summary>One frame of the spray: who is in it, and the ticks due on them (a little damage, and colder).</summary>
        private void Freeze(Vector3 from, Vector3 direction, float deltaTime)
        {
            _inSpray.Clear();
            Vector3 end = from + direction * FrostRules.Range;
            foreach (Collider collider in Physics.OverlapCapsule(from, end, FrostRules.Range * Mathf.Tan(FrostRules.ConeHalfAngleDegrees * Mathf.Deg2Rad) + 0.5f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || _inSpray.Contains(target) || !(target is Component body) || body.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (!FrostRules.InCone(from, direction, collider.bounds.center))
                {
                    continue;
                }
                _inSpray.Add(target);
            }
            int due = SpellTicks.Due(ref _sinceTick, deltaTime, FrostRules.TickSeconds);
            if (due == 0 || _inSpray.Count == 0)
            {
                return;
            }
            float spellPower = PlayerVitals.SpellPowerOf(_vitals);
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float tick = CombatTuning.DamageFor(FrostRules.DamagePerTick, spellPower);
            foreach (IDamageable target in _inSpray)
            {
                Vector3 at = ((Component)target).transform.position + Vector3.up * 1.3f;
                for (int i = 0; i < due; i++)
                {
                    using (DamageContext.WithoutBlood())   // frost does not cut
                    using (DamageContext.Chilling(FrostRules.ChillPerTick))
                    {
                        DamageContext.Strike(target, tick, from, 0.0f, DamageKind.Magic, penetration, at, gameObject);
                    }
                }
            }
        }

        // --- what other players see ---

        private void Update()
        {
            if (_remoteLeft <= 0.0f)
            {
                return;
            }
            _remoteLeft -= Time.deltaTime;
            Transform tip = StaffItem.TipOf(_locomotion);
            if (_remoteLeft <= 0.0f || tip == null)
            {
                _remoteLeft = 0.0f;
                _remoteSpray?.Stop();
                return;
            }
            _locomotion?.HoldStaffOut();
            _remoteSpray?.Spray(tip.position, _remoteDirection, Time.deltaTime);
        }

        /// <summary>Another player's spray, on this copy of their staff: running where they point it, stopping with theirs.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case SprayStarted:
                case SprayAimed:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    Transform tip = StaffItem.TipOf(_locomotion);
                    if (_remoteSpray == null && tip != null)
                    {
                        _remoteSpray = FrostSpray.Make(tip);
                    }
                    if (_remoteSpray == null)
                    {
                        return;
                    }
                    _remoteDirection = evt.Direction.normalized;
                    _remoteSpray.Run();
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case SprayStopped:
                    _remoteLeft = 0.0f;
                    _remoteSpray?.Stop();
                    break;
            }
        }
    }
}
