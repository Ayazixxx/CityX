using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A cracked window, watching for somebody climbing through it -- and shootable.
    ///
    /// city_chunk.gd's building_glass has TWO ways to finish a window off: hit
    /// it again, or put a body through the opening. The second is the one that
    /// makes a cracked pane feel like a hole rather than a decal -- you break
    /// it, you step through, and the last of it goes with you.
    ///
    /// ONLY ON CRACKED PANES, and only ever briefly. An intact window is solid
    /// so nothing can be inside it, and a shattered one is not there. So this
    /// exists for exactly the state in between, on exactly the panes in it --
    /// which in a whole streamed city is usually none of them.
    ///
    /// SHOOTABLE. A cracked pane is drawn but has no solid collider (it has to be walkable at once), which
    /// used to mean the second bullet at a window simply flew through the first bullet's hole: it never found
    /// the pane, so no second hole ever appeared -- "only 1 bullet hole appears even if you shoot more than
    /// 1". The trigger box this object owns is the one thing standing where the glass is, so it answers for
    /// the glass: a bullet that lands in it is passed on to the window breaker as a bullet on this pane.
    /// </summary>
    public sealed class CrackedPane : MonoBehaviour, IBreakableSurface, IShootableTrigger
    {
        private GlassDamage _damage;
        private PaneKey _key;

        public void Watch(GlassDamage damage, PaneKey key)
        {
            _damage = damage;
            _key = key;
        }

        /// <summary>The pane this object stands for.</summary>
        public PaneKey Key => _key;

        /// <summary>
        /// How fast, in metres per second, something has to be moving THROUGH the pane's plane to count as
        /// knocking into it. Walking into a cracked window is a knock; standing beside one, or aiming a gun at
        /// it from a hand's width away, is not.
        /// </summary>
        public const float MinKnockSpeed = 1.5f;

        private void OnTriggerEnter(Collider other) => Consider(other);

        // Something already inside when the chunk is rebuilt (a shot rebuilds it, and the opening's box is made
        // around whoever is standing there) gets no Enter of its own -- and something that walks on into the
        // pane after standing in the box does not either. Stay covers both.
        private void OnTriggerStay(Collider other) => Consider(other);

        private void Consider(Collider other)
        {
            if (_damage == null || !IsAKnock(other, _key.Wall))
            {
                return;
            }

            // This shatters the pane, which rebuilds the chunk, which destroys
            // this object. Nothing may touch anything on it afterwards -- and
            // nothing does, because Hit is the last thing here.
            _damage.Hit(_key);
        }

        /// <summary>
        /// Whether something is really knocking into a cracked pane on a wall facing <paramref name="wall"/>.
        ///
        /// A cracked window used to shatter the instant ANY rigid body overlapped its half-metre-deep box: the
        /// kinematic limb colliders of a character standing or aiming beside the glass, a glass shard from the
        /// window next door (which then shattered ITS neighbours in a chain), anything resting nearby. That was
        /// "the windows shatter by themselves after being shot". A knock is now:
        ///   * the character's own body (its CharacterController) or a body that is really being simulated -- a
        ///     thrown bottle, a ragdoll -- never a kinematic limb collider and never debris (IgnoredByShots);
        ///   * moving THROUGH the pane's plane at <see cref="MinKnockSpeed"/> or more.
        /// </summary>
        public static bool IsAKnock(Collider other, WindowGrid.Wall wall)
        {
            if (other == null || IgnoredByShots.Covers(other))
            {
                return false;
            }

            Vector3 velocity;
            if (other is CharacterController controller)
            {
                velocity = controller.velocity;
            }
            else
            {
                Rigidbody body = other.attachedRigidbody;
                if (body == null || body.isKinematic)
                {
                    return false;   // a limb of a standing character, a static thing: not a knock
                }
                velocity = body.linearVelocity;
            }

            return Mathf.Abs(Vector3.Dot(velocity, WindowGrid.OutwardNormal(wall))) >= MinKnockSpeed;
        }

        public bool TakePunch(Vector3 point) => TakeBlow(point, Vector3.zero, BlowStrength.Light);

        /// <summary>
        /// A blow that found this pane's box. The point is moved onto the pane's own plane along the way the
        /// blow was travelling (the box is half a metre deep, so the hit is on its face, up to a quarter of a
        /// metre in front of the glass -- at a steep angle that would put the hole a hand's width off), and
        /// the streamer's window breaker does the rest.
        /// </summary>
        public bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength)
        {
            GlassBreaker breaker = GetComponentInParent<GlassBreaker>();
            if (breaker == null)
            {
                return false;
            }
            return breaker.TakeBlow(OnPanePlane(point, direction), direction, strength);
        }

        private Vector3 OnPanePlane(Vector3 point, Vector3 direction)
        {
            Vector3 normal = WindowGrid.OutwardNormal(_key.Wall);
            float along = Vector3.Dot(direction, normal);
            if (Mathf.Abs(along) < 0.05f)
            {
                return point;   // grazing the pane's plane: the point is as good as it gets
            }
            float t = Vector3.Dot(transform.position - point, normal) / along;
            return point + direction * t;
        }
    }
}
