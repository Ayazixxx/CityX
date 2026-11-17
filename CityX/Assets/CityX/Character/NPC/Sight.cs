using UnityEngine;

namespace CityX.Character
{
    /// <summary>What an enemy's eyes are stopped by: walls, floors and glass, never a body or something loose.</summary>
    public static class Sight
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// Something solid between two points: not a collider under <paramref name="self"/> or <paramref name="alsoIgnore"/>, not a body that can be
        /// hurt, not a loose rigid body.
        /// </summary>
        public static bool SolidBetween(Vector3 from, Vector3 to, Transform self, Transform alsoIgnore)
        {
            Vector3 along = to - from;
            float distance = along.magnitude;
            if (distance < 1e-3f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(from, along / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || (self != null && c.transform.IsChildOf(self)) || (alsoIgnore != null && c.transform.IsChildOf(alsoIgnore)))
                {
                    continue;
                }
                if (c.GetComponentInParent<IDamageable>() != null || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic))
                {
                    continue;
                }
                return true;
            }
            return false;
        }
    }
}
