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

        private void OnTriggerEnter(Collider other)
        {
            if (_damage == null)
            {
                return;
            }

            // SOMETHING THAT MOVES UNDER ITS OWN POWER, not anything that
            // happens to overlap. A window frame is full of other geometry's
            // trigger volumes and a dropped bottle rolling past the sill is not
            // a person climbing through.
            bool aBody = other.attachedRigidbody != null ||
                         other.GetComponentInParent<CharacterController>() != null;
            if (!aBody)
            {
                return;
            }

            // This shatters the pane, which rebuilds the chunk, which destroys
            // this object. Nothing may touch anything on it afterwards -- and
            // nothing does, because Hit is the last thing here.
            _damage.Hit(_key);
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
