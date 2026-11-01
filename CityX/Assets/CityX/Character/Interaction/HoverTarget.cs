using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// SOMETHING IN THE WORLD THAT NAMES ITSELF WHEN LOOKED AT AND IS USED WITH E (docs/ITEM_INSPECT.md): the fuse box's lever, and whatever
    /// comes next. It takes part in the same look as the dropped items and the containers (ItemInspector): the one looked at most directly in
    /// reach is the one the prompt names and E works, so a lever beside a drawer never throws when the drawer was meant. A thing derives from
    /// this and says where it is, what to call it, which key does what, and what E does.
    /// </summary>
    public abstract class HoverTarget : MonoBehaviour
    {
        public static readonly HashSet<HoverTarget> All = new HashSet<HoverTarget>();

        /// <summary>The point the player looks at to use it.</summary>
        public abstract Vector3 Centre { get; }

        /// <summary>What the prompt calls it, and the keys line under the name.</summary>
        public abstract string Label { get; }

        public abstract string Keys { get; }

        /// <summary>How far from the feet, and how directly looked at (the cosine of the look cone's half-angle).</summary>
        public virtual float ReachMetres => 3.0f;

        public virtual float LookDot => 0.8f;

        /// <summary>False once it has nothing left to do (a lever already thrown): it no longer names itself.</summary>
        public virtual bool Available => true;

        public abstract void Use(GameObject player);

        protected virtual void OnEnable()
        {
            All.Add(this);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>The target <paramref name="player"/> looks at most directly in reach, and how directly; null with none.</summary>
        public static HoverTarget Best(Transform player, out float bestDot)
        {
            HoverTarget best = null;
            bestDot = float.MinValue;
            foreach (HoverTarget target in All)
            {
                if (target != null && target.Available && InteractionReach.CanReach(target.Centre, player, target.ReachMetres, target.LookDot, out float dot) && dot > bestDot)
                {
                    best = target;
                    bestDot = dot;
                }
            }
            return best;
        }
    }
}
