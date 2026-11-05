using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE WATER STAFF'S JET (owner, 2026-10-28; docs/STAFFS.md): GTA V's fire truck hose. Held, a jet of water from the staff's head along the
    /// crosshair (<see cref="WaterJetRules"/>): whoever it is held on is shoved along it, thrown limp (a knockdown, up again after a moment),
    /// kept pushed along the ground while down, and ticks 3 damage every third of a second. It drinks mana while held and stops when the mana
    /// is gone. The jet is drawn in code (<see cref="WaterJet"/>), its hiss with it.
    ///
    /// Other players see it (<see cref="IReplicatedAbility"/>): the jet starting on this body's staff, where it points (sent ten times a
    /// second), and stopping; a knockdown on another player's body goes to their machine as every knockdown does (NetworkedDamage), so they
    /// fall on their own screen too. Only this machine's jet hurts and pushes.
    /// </summary>
    public sealed class WaterJetCaster : MonoBehaviour, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "water-jet";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): Origin the staff's head, Direction the jet's.</summary>
        public const int JetStarted = 0;
        public const int JetAimed = 1;
        public const int JetStopped = 2;

        /// <summary>A copy's jet runs on this long past the last word of it, seconds: a missed aim event is not a stop.</summary>
        public const float RemoteGraceSeconds = 0.4f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private readonly StreamTicks _ticks = new StreamTicks();
        private StreamFeed _feed;
        private readonly List<IDamageable> _inStream = new List<IDamageable>();
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private WaterJet _jet;
        private WaterJet _remoteJet;
        private float _aimSendIn;
        private float _remoteLeft;
        private float _sprayed;

        /// <summary>This body's jet is on.</summary>
        public bool IsSpraying { get; private set; }

        /// <summary>How long the jet has been held, seconds.</summary>
        public float SprayedSeconds => IsSpraying ? _sprayed : 0.0f;

        /// <summary>The bodies the jet is on this frame.</summary>
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
            bool spraying = held && alive && _feed.Draw(_vitals, WaterJetRules.ManaPerSecond, deltaTime);
            Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            Vector3 direction = PlayerInputReader.CrosshairDirection(from, eye, look, transform);
            if (spraying && !IsSpraying)
            {
                StartTheStream(tip, from, direction);
            }
            else if (!spraying && IsSpraying)
            {
                Stop();
            }
            if (!IsSpraying)
            {
                return;
            }
            _sprayed += deltaTime;
            _locomotion?.HoldStaffOut();
            Spray(from, direction, deltaTime);
            _aimSendIn -= deltaTime;
            if (_aimSendIn <= 0.0f)
            {
                _aimSendIn = WaterJetRules.AimSendSeconds;
                Fired?.Invoke(new AbilityEvent(JetAimed, from, direction));
            }
        }

        public void CancelStaffCharge()
        {
            Stop();
        }

        private void StartTheStream(Transform tip, Vector3 from, Vector3 direction)
        {
            IsSpraying = true;
            _sprayed = 0.0f;
            _aimSendIn = WaterJetRules.AimSendSeconds;
            _ticks.Forget(Time.time);
            if (_jet == null && tip != null)
            {
                _jet = WaterJet.Make(tip);
            }
            _jet?.Run();
            Fired?.Invoke(new AbilityEvent(JetStarted, from, direction));
        }

        private void Stop()
        {
            if (!IsSpraying)
            {
                return;
            }
            IsSpraying = false;
            _inStream.Clear();
            _jet?.Stop();
            Fired?.Invoke(new AbilityEvent(JetStopped, transform.position, Vector3.zero));
        }

        /// <summary>One frame of the jet: where it lands, who is in it, and what it does to them.</summary>
        private void Spray(Vector3 from, Vector3 direction, float deltaTime)
        {
            bool lands = Landing(from, direction, out Vector3 landing, out _);
            _jet?.Aim(from, direction, transform, deltaTime);
            if (lands)
            {
                FireScene.Douse(landing, CityX.World.FireRules.DouseRadius);   // water puts fire out (docs/FIRE.md)
            }
            float reach = lands ? Vector3.Distance(from, landing) + WaterJetRules.StreamRadius : WaterJetRules.Range;

            // A spell: Spell Power makes each tick hurt more (2% a point), and the caster's Armor Penetration goes with it.
            float spellPower = PlayerVitals.SpellPowerOf(_vitals);
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float tick = CombatTuning.DamageFor(WaterJetRules.DamagePerTick, spellPower);

            _inStream.Clear();
            Vector3 end = from + direction * Mathf.Min(reach, WaterJetRules.Range);
            foreach (Collider collider in Physics.OverlapCapsule(from, end, WaterJetRules.StreamRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || _inStream.Contains(target) || !(target is Component body) || body.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (!WaterJetRules.InStream(from, direction, collider.bounds.center) || Vector3.Distance(from, collider.bounds.center) > reach)
                {
                    continue;
                }
                _inStream.Add(target);
                FireScene.DouseBody(target);
                if (_ticks.Due(target, Time.time, WaterJetRules.TickSeconds))
                {
                    using (DamageContext.WithoutBlood())   // water does not cut
                    {
                        DamageContext.Strike(target, tick, from, 0.0f, DamageKind.Magic, penetration, collider.bounds.center, gameObject);
                    }
                }
                Shove(body, direction);
            }
        }

        /// <summary>The jet on a body: thrown limp along it if it is on its feet, pushed on along it while it is down, shoved if it cannot be thrown.</summary>
        private static void Shove(Component body, Vector3 direction)
        {
            var ragdoll = body.GetComponent<Ragdoll>();
            if (ragdoll != null && ragdoll.IsLimp)
            {
                if (ragdoll.Torso != null)
                {
                    ragdoll.Torso.AddForce(direction * WaterJetRules.PushAcceleration, ForceMode.Acceleration);
                }
                return;
            }
            var vitals = body.GetComponent<PlayerVitals>();
            if (vitals != null && ragdoll != null && !vitals.IsInvulnerable)
            {
                vitals.TriggerLaunchKnockdown(WaterJetRules.KnockdownVelocity(direction));
                return;
            }
            var locomotion = body.GetComponent<PlayerLocomotion>();
            if (locomotion != null)
            {
                locomotion.SetResidualVelocity(direction * WaterJetRules.ShoveSpeed);
            }
        }

        /// <summary>Where the jet first meets the world (not this body, not a trigger), within its reach.</summary>
        private bool Landing(Vector3 from, Vector3 direction, out Vector3 point, out Vector3 normal)
        {
            int count = Physics.RaycastNonAlloc(from, direction, HitBuffer, WaterJetRules.Range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            point = from + direction * WaterJetRules.Range;
            normal = Vector3.up;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= best)
                {
                    continue;
                }
                best = hit.distance;
                point = hit.point;
                normal = hit.normal;
                found = true;
            }
            return found;
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
                _remoteJet?.Stop();
            }
        }

        /// <summary>Another player's jet, on this copy of their staff: running and pointing where they point it, their staff held out, stopping with theirs.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case JetStarted:
                case JetAimed:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    Transform tip = StaffItem.TipOf(_locomotion);
                    if (_remoteJet == null && tip != null)
                    {
                        _remoteJet = WaterJet.Make(tip);
                    }
                    if (_remoteJet == null)
                    {
                        return;
                    }
                    _remoteJet.Run();
                    Vector3 from = tip != null ? tip.position : evt.Origin;
                    _remoteJet.Aim(from, evt.Direction.normalized, transform, WaterJetRules.AimSendSeconds);
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case JetStopped:
                    _remoteLeft = 0.0f;
                    _remoteJet?.Stop();
                    break;
            }
        }
    }
}
