using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A cracked window, watching for somebody climbing through it.
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
    /// </summary>
    public sealed class CrackedPane : MonoBehaviour
    {
        private GlassDamage _damage;
        private PaneKey _key;

        public void Watch(GlassDamage damage, PaneKey key)
        {
            _damage = damage;
            _key = key;
        }

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
    }
}
