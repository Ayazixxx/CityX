using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What <see cref="GrabCarry"/> can lift: a limp body's bones and loose props (<see cref="Grabbable"/>), a dropped item, and any item lying in
    /// the world (a sword, a hat, a folded shirt). Most items lie as still props with no physics; one is made a real body the moment it is first
    /// lifted (<see cref="Physical"/>), and stays one, so it falls and tumbles where it is dropped or thrown.
    /// </summary>
    public static class Carryable
    {
        /// <summary>The weight a lifted item is given: light enough to fling, heavy enough not to skate.</summary>
        public const float ItemMassKg = 2.0f;

        /// <summary>
        /// The thing <paramref name="hit"/> belongs to, if it can be lifted by <paramref name="player"/>: its root and the point to aim at. Never the
        /// player's own body, and never an item someone is wearing or holding (it hangs under their body).
        /// </summary>
        public static bool TryFind(Collider hit, Transform player, out GameObject thing, out Vector3 centre)
        {
            thing = null;
            centre = Vector3.zero;
            if (hit == null || hit.transform.IsChildOf(player))
            {
                return false;
            }
            Rigidbody body = hit.attachedRigidbody;
            if (body != null && !body.isKinematic && body.GetComponent<Grabbable>() != null)
            {
                thing = body.gameObject;
                centre = body.worldCenterOfMass;
                return true;
            }
            var dropped = hit.GetComponentInParent<DroppedItem>();
            if (dropped != null && dropped.Body != null)
            {
                thing = dropped.gameObject;
                centre = dropped.Body.worldCenterOfMass;
                return true;
            }
            var item = hit.GetComponentInParent<IEquippableItem>() as Component;
            if (item != null && item.GetComponentInParent<PlayerVitals>() == null)
            {
                thing = item.gameObject;
                centre = Bounds(thing).center;
                return true;
            }
            return false;
        }

        /// <summary><paramref name="thing"/>'s body, made a real one if it is an item lying as a still prop: a rigidbody and a solid box around its model.</summary>
        public static Rigidbody Physical(GameObject thing)
        {
            var body = thing.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                return body;
            }
            if (!HasSolidCollider(thing))
            {
                Bounds local = LocalBounds(thing);
                var box = thing.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = Vector3.Max(local.size, Vector3.one * 0.05f);
            }
            body = thing.AddComponent<Rigidbody>();
            body.mass = ItemMassKg;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return body;
        }

        private static bool HasSolidCollider(GameObject thing)
        {
            foreach (Collider c in thing.GetComponentsInChildren<Collider>())
            {
                if (!c.isTrigger && c.enabled)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The world box around the thing's shown model.</summary>
        public static Bounds Bounds(GameObject thing)
        {
            var bounds = new Bounds(thing.transform.position, Vector3.zero);
            foreach (Renderer r in thing.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>The box around the thing's shown meshes in its own space, so the collider turns with it.</summary>
        private static Bounds LocalBounds(GameObject thing)
        {
            Transform root = thing.transform;
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in thing.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Bounds mesh = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(local));
                    if (!any)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return any ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
        }
    }
}
