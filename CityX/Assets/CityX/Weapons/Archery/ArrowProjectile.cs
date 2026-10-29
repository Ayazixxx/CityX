using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// AN ARROW OR A BOLT (docs/ARCHERY.md): the hit over its flat range is instant (a hitscan, decided by whoever loosed it: the bow, or the
    /// crossbow's gun code), and this is what is SEEN -- the arrow crossing its flat range at <see cref="ArcheryRules.VisibleSpeed"/> to where
    /// the hitscan ended, sticking there if it met something; and where it met nothing, carrying on from the end of its flat range as a real
    /// falling projectile (<see cref="ArcheryRules.Gravity"/>), swept each frame, hurting whatever it lands in and sticking in it. Stuck, it
    /// stays a while (longer in the world than in someone) and then is gone. A cosmetic one (another player's arrow, the flat part of every
    /// arrow) hurts nothing.
    /// </summary>
    public sealed class ArrowProjectile : MonoBehaviour
    {
        public enum Phase
        {
            /// <summary>Crossing the flat range to where the hitscan ended.</summary>
            Flat,
            /// <summary>Past the flat range: a real projectile, falling.</summary>
            Falling,
            Stuck,
        }

        public Phase CurrentPhase { get; private set; }

        public bool Cosmetic { get; private set; }

        /// <summary>What it is stuck in, or null.</summary>
        public Transform StuckTo { get; private set; }

        public float StuckSecondsRemaining { get; private set; }

        private Vector3 _flatEnd;
        private Collider _flatHit;
        private Vector3 _direction;
        private float _fallSpeed;
        private float _damage;
        private float _penetration;
        private GameObject _striker;
        private Collider[] _ignore;
        private Vector3 _velocity;
        private float _age;
        private TrailRenderer _trail;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// Looses an arrow: a hitscan from <paramref name="traceFrom"/> (the camera, as a gun's: what the crosshair is on is hit) along
        /// <paramref name="direction"/> over <paramref name="flatRange"/> -- the first thing it meets that is not the archer's own takes
        /// <paramref name="damage"/>, a physical blow with a short stagger, unless <paramref name="cosmetic"/> -- then the arrow shown flying there
        /// from <paramref name="shownFrom"/> (the bow's nock), and on past the flat range falling at <paramref name="fallSpeed"/> if it met nothing.
        /// Says whether and where it hit, for the bow's own feedback.
        /// </summary>
        public static ArrowProjectile Loose(GameObject prefab, Vector3 traceFrom, Vector3 shownFrom, Vector3 direction, float flatRange, float fallSpeed,
            float damage, float penetration, GameObject striker, Collider[] ignore, bool cosmetic, out bool hitSomething, out Vector3 hitPoint)
        {
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            hitSomething = Sweep(traceFrom, direction, flatRange, ignore, out RaycastHit hit);
            hitPoint = hitSomething ? hit.point : traceFrom + direction * flatRange;
            if (hitSomething && !cosmetic)
            {
                Strike(hit.collider, damage, traceFrom, penetration, striker, hit.point);
            }
            Vector3 shown = hitPoint - shownFrom;
            return Show(prefab, shownFrom, hitPoint, hitSomething ? hit.collider : null, shown.sqrMagnitude > 1e-4f ? shown.normalized : direction,
                fallSpeed, damage, penetration, striker, ignore, cosmetic);
        }

        /// <summary>
        /// The arrow as seen, after the hitscan was decided elsewhere (the crossbow's gun code): flying from <paramref name="from"/> to
        /// <paramref name="flatEnd"/>, sticking in <paramref name="flatHit"/> there (null: it met nothing, and falls on from there).
        /// </summary>
        public static ArrowProjectile Show(GameObject prefab, Vector3 from, Vector3 flatEnd, Collider flatHit, Vector3 direction, float fallSpeed,
            float damage, float penetration, GameObject striker, Collider[] ignore, bool cosmetic)
        {
            if (prefab == null)
            {
                return null;
            }
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : (flatEnd - from).normalized;
            GameObject instance = Instantiate(prefab, from, Quaternion.LookRotation(direction));
            var arrow = instance.GetComponent<ArrowProjectile>();
            if (arrow == null)
            {
                arrow = instance.AddComponent<ArrowProjectile>();
            }
            arrow._flatEnd = flatEnd;
            arrow._flatHit = flatHit;
            arrow._direction = direction;
            arrow._fallSpeed = fallSpeed;
            arrow._damage = damage;
            arrow._penetration = Mathf.Clamp01(penetration);
            arrow._striker = striker;
            arrow._ignore = ignore;
            arrow.Cosmetic = cosmetic;
            arrow.CurrentPhase = Phase.Flat;
            arrow._trail = instance.GetComponentInChildren<TrailRenderer>();
            foreach (Collider c in instance.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            return arrow;
        }

        private void Update()
        {
            Tick(Mathf.Min(Time.deltaTime, 0.05f));
        }

        public void Tick(float deltaTime)
        {
            switch (CurrentPhase)
            {
                case Phase.Flat:
                    FlyFlat(deltaTime);
                    break;
                case Phase.Falling:
                    Fall(deltaTime);
                    break;
                default:
                    Age(deltaTime);
                    break;
            }
        }

        /// <summary>The arrow's head (its origin is its nock end; the model runs up +Z).</summary>
        private Vector3 Head => transform.position + transform.forward * ArcheryRules.ArrowLength;

        /// <summary>Across the flat range at the visible speed: when its head gets there it sticks where the hitscan hit, or starts to fall.</summary>
        private void FlyFlat(float deltaTime)
        {
            float left = Vector3.Distance(Head, _flatEnd);
            float step = ArcheryRules.VisibleSpeed * deltaTime;
            if (step < left)
            {
                transform.position += _direction * step;
                return;
            }
            transform.position = _flatEnd - _direction * ArcheryRules.ArrowLength;
            if (_flatHit != null)
            {
                Stick(_flatEnd, _direction, _flatHit);
                return;
            }
            CurrentPhase = Phase.Falling;
            _velocity = _direction * _fallSpeed;
            _age = 0.0f;
        }

        /// <summary>A real projectile now: gravity, the nose along the velocity, a sweep each frame; it hurts what it lands in (unless cosmetic).</summary>
        private void Fall(float deltaTime)
        {
            _age += deltaTime;
            if (_age > ArcheryRules.FallSeconds)
            {
                Destroy(gameObject);
                return;
            }
            _velocity += Vector3.down * (ArcheryRules.Gravity * deltaTime);
            Vector3 direction = _velocity.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            Vector3 from = Head;
            float distance = _velocity.magnitude * deltaTime;
            if (Sweep(from, direction, distance, _ignore, out RaycastHit hit))
            {
                if (!Cosmetic)
                {
                    Strike(hit.collider, _damage, from, _penetration, _striker, hit.point);
                }
                Stick(hit.point, direction, hit.collider);
                return;
            }
            transform.position += direction * distance;
        }

        private static bool Sweep(Vector3 from, Vector3 direction, float distance, Collider[] ignore, out RaycastHit nearest)
        {
            RaycastHit[] hits = Hits;
            int count = Physics.SphereCastNonAlloc(from, ArcheryRules.Radius, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length)
            {
                // A sweep that starts inside the archer overlaps every collider of their body at once (a ragdoll's limbs, the controller): more
                // than the buffer holds, and the one hit that matters, far down the line, can be the one dropped. Ask for all of them.
                hits = Physics.SphereCastAll(from, ArcheryRules.Radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            nearest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.isTrigger || hits[i].distance >= best || (ignore != null && System.Array.IndexOf(ignore, c) >= 0))
                {
                    continue;
                }
                best = hits[i].distance;
                nearest = hits[i];
                found = true;
            }
            return found;
        }

        /// <summary>The blow: physical (armour stops a share, a guard can turn it), a short stagger, Firearm Damage already in <paramref name="damage"/>.</summary>
        private static void Strike(Collider what, float damage, Vector3 from, float penetration, GameObject striker, Vector3 point)
        {
            IDamageable target = what.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive)
            {
                return;
            }
            bool wasAlive = target.IsAlive;
            DamageContext.Strike(target, damage, from, CombatTuning.HitstunSeconds * 0.5f, DamageKind.Physical, penetration, point, striker);
            PlayerVitals shooter = striker != null ? striker.GetComponent<PlayerVitals>() : null;
            if (wasAlive && !target.IsAlive && shooter != null && target is IXpReward reward)
            {
                shooter.AddXp(reward.XpReward);
            }
        }

        /// <summary>Head in past the surface, pointing the way it came, riding whatever it hit.</summary>
        private void Stick(Vector3 point, Vector3 direction, Collider what)
        {
            bool character = what.GetComponentInParent<IDamageable>() != null;
            transform.rotation = Quaternion.LookRotation(direction);
            transform.position = point + direction * (ArcheryRules.EmbedMetres - ArcheryRules.ArrowLength);
            transform.SetParent(what.transform, true);
            StuckTo = what.transform;
            CurrentPhase = Phase.Stuck;
            StuckSecondsRemaining = character ? ArcheryRules.CharacterStickSeconds : ArcheryRules.WorldStickSeconds;
            if (_trail != null)
            {
                _trail.emitting = false;
            }
            ArcherySounds.PlayImpact(point, character);
        }

        private void Age(float deltaTime)
        {
            StuckSecondsRemaining -= deltaTime;
            if (StuckSecondsRemaining <= 0.0f || StuckTo == null)
            {
                Destroy(gameObject);
            }
        }
    }
}
