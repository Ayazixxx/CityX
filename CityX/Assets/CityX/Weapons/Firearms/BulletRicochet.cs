using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BULLET THE GUARD TURNS ASIDE FLIES OFF (owner, 2026-10-27: "I want it to work like in lego star wars when you hold block as a jedi it
    /// reflects all bullets ... into another direction"; docs/BULLET_DEFLECTION.md). When this body's guard turns a bullet aside
    /// (PlayerVitals.BulletTurnedAside: the Block Chance roll won, less the shooter's Armor Penetration, or the parry caught it), the bullet
    /// leaves the guard back roughly the way it came, in a random direction (Ricochet), and hits whatever is there with the damage it carried.
    ///
    /// NEVER AT AN ALLY: a way that would hit this body, a player other than whoever fired it, or anyone who is not an enemy (a civilian) is
    /// passed over for another; with no safe way it only glances off. An enemy's guard (the swordsman's) sends it at players instead -- the
    /// shooter's side. "Ally" is any other player until parties and factions exist.
    ///
    /// Decided on the machine whose body blocked it (where the roll is made); a hit on another machine's body or enemy goes there through
    /// its copy, as every blow does. Every other player sees the streak (IReplicatedAbility: the way and how far it went).
    /// </summary>
    public sealed class BulletRicochet : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "bullet-ricochet";

        /// <summary>A body this near where a bullet came from (horizontally, metres) is the one that fired it.</summary>
        public const float ShooterReach = 1.5f;

        /// <summary>Where on the body the bullet leaves the guard: this share of its height up, this far out toward the shooter (metres).</summary>
        public const float GuardHeightShare = 0.62f;
        public const float GuardReach = 0.6f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private bool _hostile;
        private System.Random _random;
        private Vector3 _shooterAt;
        private Collider _ended;
        private Vector3 _endedAt;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>How many bullets this guard has sent flying, and how many it could find no safe way for (for tests).</summary>
        public int Ricochets { get; private set; }
        public int Dropped { get; private set; }

        /// <summary>How many other players' streaks this copy has shown (for tests).</summary>
        public int RemoteShown { get; private set; }

        /// <summary>The last one's way and end (for tests).</summary>
        public Vector3 LastDirection { get; private set; }
        public Vector3 LastEnd { get; private set; }

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _hostile = GetComponent<IHostile>() != null;
            _random = new System.Random(GetInstanceID());
            if (_vitals != null)
            {
                _vitals.BulletTurnedAside += OnTurnedAside;
            }
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.BulletTurnedAside -= OnTurnedAside;
            }
        }

        private void OnTurnedAside(Vector3 from, float damage)
        {
            float height = _locomotion != null ? _locomotion.WorldHeight : 2.6f;
            Vector3 toShooter = from - transform.position;
            toShooter.y = 0.0f;
            toShooter = toShooter.sqrMagnitude > 1e-4f ? toShooter.normalized : transform.forward;
            Vector3 origin = transform.position + Vector3.up * (height * GuardHeightShare) + toShooter * (GuardReach * height / 2.6f);
            _shooterAt = from;

            if (!Ricochet.TryPick(origin, origin - from, _random, Safe, out Vector3 direction, out Vector3 end))
            {
                Dropped++;
                return;
            }
            Ricochets++;
            LastDirection = direction;
            LastEnd = end;
            IDamageable target = _ended != null ? _ended.GetComponentInParent<IDamageable>() : null;
            if (target != null && target.IsAlive)
            {
                DamageContext.Strike(target, damage, origin, 0.0f, DamageKind.Bullet, 0.0f, _endedAt, gameObject);
            }
            BulletTracer.Spawn(origin, end);
            Fired?.Invoke(new AbilityEvent(0, origin, direction, Vector3.Distance(origin, end)));
        }

        /// <summary>Traces a way (Ricochet.Probe): where the bullet would end, and whether what it ends on may be hit.</summary>
        private bool Safe(Vector3 origin, Vector3 direction, out Vector3 end)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, Ricochet.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            _ended = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance < nearest && !Hits[i].collider.transform.IsChildOf(transform))
                {
                    nearest = Hits[i].distance;
                    _ended = Hits[i].collider;
                    _endedAt = Hits[i].point;
                }
            }
            end = _ended != null ? _endedAt : origin + direction * Ricochet.Range;
            return _ended == null || MayHit(_ended);
        }

        /// <summary>
        /// Whether a turned-aside bullet may end on <paramref name="hit"/>: anything that is not a body (a wall, the street); whoever fired it;
        /// and the other side -- an enemy, if this is a player's guard; a player, if this is an enemy's. Never this body, another player on a
        /// player's guard, or anyone who is no one's enemy (a civilian).
        /// </summary>
        public bool MayHit(Collider hit)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                return true;
            }
            if (!(target is Component component) || component.gameObject == gameObject)
            {
                return false;
            }
            GameObject body = component.gameObject;
            Vector3 flat = body.transform.position - _shooterAt;
            flat.y = 0.0f;
            if (flat.magnitude <= ShooterReach && Mathf.Abs(body.transform.position.y - _shooterAt.y) < 4.0f)
            {
                return true;   // whoever fired it
            }
            return _hostile ? PlayerBodies.IsPlayer(body) : body.GetComponent<IHostile>() != null;
        }

        /// <summary>Another player's guard sent a bullet flying: the same streak, here. Nothing is hit by a copy's.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Direction.sqrMagnitude < 1e-6f || float.IsNaN(evt.A))
            {
                return;
            }
            float length = Mathf.Clamp(evt.A, 0.0f, Ricochet.Range);
            BulletTracer.Spawn(evt.Origin, evt.Origin + evt.Direction.normalized * length);
            RemoteShown++;
        }
    }
}
