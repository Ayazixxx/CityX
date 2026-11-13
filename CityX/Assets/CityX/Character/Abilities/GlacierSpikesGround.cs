using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The real world asked for Glacier Spikes' line (<see cref="IGlacierGround"/>): rays against everything solid that is not a body (a body
    /// in the way is struck, not climbed, and the caster's own is under its feet) nor a small prop (a parking meter, a lamp post, a hydrant:
    /// the ice flows round it, as a wall it would end the whole spell). No garbage: one shared buffer.
    /// </summary>
    public sealed class GlacierSpikesGround : IGlacierGround
    {
        public static readonly GlacierSpikesGround World = new GlacierSpikesGround();

        /// <summary>A thing narrower than this both ways across, metres, is a prop the ice passes, not a wall or the ground.</summary>
        public const float PropWidth = 1.2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public bool TryGround(Vector3 above, float depth, out float y)
        {
            y = above.y;
            int count = Physics.RaycastNonAlloc(above, Vector3.down, Hits, depth, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.distance < nearest && !PassedThrough(hit.collider))
                {
                    nearest = hit.distance;
                    y = hit.point.y;
                }
            }
            return nearest < float.MaxValue;
        }

        public float ClearDistance(Vector3 from, Vector3 to)
        {
            Vector3 way = to - from;
            float distance = way.magnitude;
            if (distance < 1e-4f)
            {
                return distance;
            }
            float clear = distance;
            int count = Physics.RaycastNonAlloc(from, way / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance < clear && !PassedThrough(Hits[i].collider))
                {
                    clear = Hits[i].distance;
                }
            }
            return clear;
        }

        private static bool PassedThrough(Collider collider)
        {
            if (collider == null || collider.GetComponentInParent<IDamageable>() != null)
            {
                return true;
            }
            Vector3 size = collider.bounds.size;
            return size.x < PropWidth && size.z < PropWidth;
        }
    }
}
