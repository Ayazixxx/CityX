using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The Arcane Beam Staff's beam (owner, 2026-10-29: "a staff called "arcane beam staff" that has a beam like attack ... a magenta arcane
    /// beam that consecutively does damage to whatever its targeting and costs mana"; docs/STAFFS.md). Held, a straight beam from the staff's
    /// head along the crosshair (<see cref="ArcaneBeamRules"/>) to the first thing it meets: that, if it can be hurt, takes a tick of magic
    /// damage every fifth of a second (a corpse still with something to lose is taken apart by them). It drinks mana while held and stops when
    /// the mana is gone. Drawn in code (<see cref="StaffBeam"/>, its arcane style).
    ///
    /// Other players see it (<see cref="IReplicatedAbility"/>): the beam starting on this body's staff, where it points (sent ten times a
    /// second), and stopping; their copy of the beam runs to what it meets on their own screen. Only this machine's beam hurts.
    /// </summary>
    public sealed class ArcaneBeamCaster : MonoBehaviour, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "arcane-beam";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): Origin the staff's head, Direction the beam's.</summary>
        public const int BeamStarted = 0;

        public const int BeamAimed = 1;
        public const int BeamStopped = 2;

        /// <summary>A copy's beam runs on this long past the last word of it, seconds: a missed aim event is not a stop.</summary>
        public const float RemoteGraceSeconds = 0.4f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private StaffBeam _beam;
        private StaffBeam _remoteBeam;
        private float _aimSendIn;
        private float _sinceTick;
        private float _remoteLeft;
        private Vector3 _remoteDirection;

        /// <summary>This body's beam is on.</summary>
        public bool IsBeaming { get; private set; }

        /// <summary>What the beam is burning this frame (null when it meets nothing that can be hurt).</summary>
        public IDamageable Target { get; private set; }

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
            bool fed = _vitals == null || _vitals.TrySpendMana(held ? ArcaneBeamRules.ManaPerSecond * deltaTime : 0.0f);
            bool beaming = held && alive && fed;
            Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            Vector3 direction = PlayerInputReader.ConvergedDirection(from, eye, look);
            if (beaming && !IsBeaming)
            {
                Begin(tip, from, direction);
            }
            else if (!beaming && IsBeaming)
            {
                End();
            }
            if (!IsBeaming)
            {
                return;
            }
            _locomotion?.HoldStaffOut();
            Burn(from, direction, deltaTime);
            _aimSendIn -= deltaTime;
            if (_aimSendIn <= 0.0f)
            {
                _aimSendIn = ArcaneBeamRules.AimSendSeconds;
                Fired?.Invoke(new AbilityEvent(BeamAimed, from, direction));
            }
        }

        public void CancelStaffCharge()
        {
            End();
        }

        private void Begin(Transform tip, Vector3 from, Vector3 direction)
        {
            IsBeaming = true;
            _aimSendIn = ArcaneBeamRules.AimSendSeconds;
            _sinceTick = ArcaneBeamRules.TickSeconds;   // the first tick lands at once
            if (_beam == null && tip != null)
            {
                _beam = StaffBeam.Make(tip, StaffBeam.Arcane);
            }
            _beam?.Run();
            Fired?.Invoke(new AbilityEvent(BeamStarted, from, direction));
        }

        private void End()
        {
            if (!IsBeaming)
            {
                return;
            }
            IsBeaming = false;
            Target = null;
            _beam?.Stop();
            Fired?.Invoke(new AbilityEvent(BeamStopped, transform.position, Vector3.zero));
        }

        /// <summary>One frame of the beam: where it ends, what it burns, and the ticks due on it.</summary>
        private void Burn(Vector3 from, Vector3 direction, float deltaTime)
        {
            bool lands = FirstHit(from, direction, transform, out Vector3 end, out Collider what);
            _beam?.Aim(from, end, lands, deltaTime);
            IDamageable target = what != null ? what.GetComponentInParent<IDamageable>() : null;
            if (target != null && !target.IsAlive && !Dismemberer.TakesCorpseHits(target))
            {
                target = null;
            }
            Target = target;
            int due = SpellTicks.Due(ref _sinceTick, deltaTime, ArcaneBeamRules.TickSeconds);
            if (target == null)
            {
                return;
            }
            // A spell: Spell Power makes each tick hurt more (2% a point), and the caster's Armor Penetration goes with it.
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float tick = CombatTuning.DamageFor(ArcaneBeamRules.DamagePerTick, spellPower);
            for (int i = 0; i < due; i++)
            {
                DamageContext.Strike(target, tick, from, 0.0f, DamageKind.Magic, penetration, end, gameObject);
            }
        }

        /// <summary>
        /// Where a beam from <paramref name="from"/> along <paramref name="direction"/> first meets the world (not <paramref name="self"/>, not
        /// a trigger), within its reach, and what it meets there; false (and the end of its reach) when it meets nothing.
        /// </summary>
        public static bool FirstHit(Vector3 from, Vector3 direction, Transform self, out Vector3 end, out Collider what)
        {
            int count = Physics.SphereCastNonAlloc(from, ArcaneBeamRules.HitRadius, direction, HitBuffer, ArcaneBeamRules.Range, ~0,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            end = from + direction * ArcaneBeamRules.Range;
            what = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == null || (self != null && hit.collider.transform.IsChildOf(self)) || hit.distance >= best)
                {
                    continue;
                }
                best = hit.distance;
                end = hit.distance > 0.0f ? hit.point : from;   // a cast that starts inside a collider says nothing of where
                what = hit.collider;
            }
            return what != null;
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
                _remoteBeam?.Stop();
                return;
            }
            _locomotion?.HoldStaffOut();
            bool lands = FirstHit(tip.position, _remoteDirection, transform, out Vector3 end, out _);
            _remoteBeam?.Aim(tip.position, end, lands, Time.deltaTime);
        }

        /// <summary>Another player's beam, on this copy of their staff: running and pointing where they point it, their staff held out, stopping with theirs.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case BeamStarted:
                case BeamAimed:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    Transform tip = StaffItem.TipOf(_locomotion);
                    if (_remoteBeam == null && tip != null)
                    {
                        _remoteBeam = StaffBeam.Make(tip, StaffBeam.Arcane);
                    }
                    if (_remoteBeam == null)
                    {
                        return;
                    }
                    _remoteDirection = evt.Direction.normalized;
                    _remoteBeam.Run();
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case BeamStopped:
                    _remoteLeft = 0.0f;
                    _remoteBeam?.Stop();
                    break;
            }
        }
    }
}
