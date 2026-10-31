using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// AN ITEM LYING IN THE WORLD after a body let go of it: the item's own model with nothing of the item acting (InertModel), falling under
    /// gravity on a rigidbody with a box round it, tumbling and coming to rest where it lands, until a player picks it up (DroppedItemPicker).
    /// Made and taken away by DroppedItems.
    /// </summary>
    public sealed class DroppedItem : MonoBehaviour
    {
        /// <summary>What every dropped item weighs: a sword's weight (they only fall, tumble and lie there).</summary>
        public const float MassKg = 3.0f;

        /// <summary>The box round the model is never thinner than this: a blade's own few millimetres would slip through the street.</summary>
        public const float MinThicknessMetres = 0.05f;

        /// <summary>
        /// With nothing to land on this far below (the street not built yet where another player dropped it, far away), it waits in the air
        /// where it was dropped until there is; and one that falls this far past where it was dropped has lost its ground and goes back to wait.
        /// </summary>
        public const float GroundSearchMetres = 60.0f;

        public const float GroundCheckSeconds = 0.5f;

        /// <summary>How fast a dying hand throws what it held: up, and out from the body, on top of the body's own speed.</summary>
        public const float LetGoUpSpeed = 1.5f;

        public const float LetGoOutSpeed = 1.2f;

        /// <summary>How much of a killing blow's fling (PlayerVitals.DeathImpulse) the item takes with it.</summary>
        public const float LetGoFlingShare = 0.5f;

        /// <summary>How fast it turns over as it falls, radians a second.</summary>
        public const float LetGoSpinSpeed = 4.0f;

        public long Id { get; private set; }

        public string ItemId { get; private set; }

        public ItemInstanceData Data { get; private set; }

        public Rigidbody Body { get; private set; }

        public BoxCollider Solid { get; private set; }

        /// <summary>The middle of the item (its box's), where a player looks to pick it up: a sword's root is at its guard, not its middle.</summary>
        public Vector3 Centre => Solid != null ? Solid.transform.TransformPoint(Solid.center) : transform.position;

        /// <summary>True while it hangs where it was dropped, waiting for ground to land on.</summary>
        public bool WaitingForGround { get; private set; }

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private DroppedItems _record;
        private Vector3 _droppedAt;
        private Quaternion _droppedRotation;
        private Vector3 _launchVelocity;
        private Vector3 _launchSpin;
        private float _nextGroundCheck;

        /// <summary>
        /// How a dying hand lets go of what it held at <paramref name="pose"/>: thrown a little up and out from <paramref name="body"/>, with the
        /// body's own <paramref name="bodyVelocity"/> and part of the killing blow's <paramref name="fling"/>, turning over as it goes.
        /// </summary>
        public static ItemDrop LetGo(string itemId, ItemInstanceData data, Pose pose, Transform body, Vector3 bodyVelocity, Vector3 fling)
        {
            Vector3 outward = pose.position - body.position;
            outward.y = 0.0f;
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : body.right;
            Vector3 spinAxis = Vector3.Cross(Vector3.up, outward);
            spinAxis = (spinAxis.sqrMagnitude > 1e-4f ? spinAxis.normalized : Vector3.right) + Random.insideUnitSphere * 0.3f;
            return new ItemDrop
            {
                ItemId = itemId,
                Data = data,
                Position = pose.position,
                Rotation = pose.rotation,
                Velocity = bodyVelocity + fling * LetGoFlingShare + outward * LetGoOutSpeed + Vector3.up * LetGoUpSpeed,
                Spin = spinAxis.normalized * LetGoSpinSpeed,
            };
        }

        internal static DroppedItem Make(GameObject prefab, ItemDrop drop, DroppedItems record, GameObject dropper)
        {
            GameObject model = Instantiate(prefab, drop.Position, drop.Rotation);
            model.name = prefab.name + " (dropped)";
            InertModel.Strip(model);
            var item = model.AddComponent<DroppedItem>();
            item.Id = drop.Id;
            item.ItemId = drop.ItemId;
            item.Data = drop.Data;
            item._record = record;
            item._droppedAt = drop.Position;
            item._droppedRotation = drop.Rotation;
            item.MakeSolid(dropper);
            if (drop.Placed)
            {
                item.Body.isKinematic = true;
            }
            else
            {
                item.Launch(drop.Velocity, drop.Spin);
            }
            return item;
        }

        /// <summary>A box round what can be seen of the model, on a rigidbody of its own; never colliding with the body it fell from.</summary>
        private void MakeSolid(GameObject dropper)
        {
            Bounds bounds = VisibleBounds();
            var solidObject = new GameObject("DropSolid");
            solidObject.transform.SetParent(transform, false);
            Solid = solidObject.AddComponent<BoxCollider>();
            Solid.center = bounds.center;
            Solid.size = Vector3.Max(bounds.size, Vector3.one * MinThicknessMetres);

            Body = GetComponent<Rigidbody>();
            if (Body == null)
            {
                Body = gameObject.AddComponent<Rigidbody>();
            }
            Body.isKinematic = false;
            Body.mass = MassKg;
            Body.angularDamping = 0.4f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // It starts inside the hand that held it: on the dropper's machine that body is known; on another it is a copy, whichever body the
            // item starts inside.
            if (dropper != null)
            {
                foreach (Collider c in dropper.GetComponentsInChildren<Collider>(true))
                {
                    Physics.IgnoreCollision(Solid, c, true);
                }
            }
            Vector3 half = Vector3.Scale(Solid.size * 0.5f, transform.lossyScale);
            foreach (Collider c in Physics.OverlapBox(transform.TransformPoint(Solid.center), half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c != Solid && c.GetComponentInParent<PlayerVitals>() != null)
                {
                    Physics.IgnoreCollision(Solid, c, true);
                }
            }
        }

        /// <summary>The model's visible meshes, in its own space.</summary>
        private Bounds VisibleBounds()
        {
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
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
                    Vector3 point = transform.InverseTransformPoint(filter.transform.TransformPoint(local));
                    if (any)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                }
            }
            return any ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
        }

        private void Launch(Vector3 velocity, Vector3 spin)
        {
            _launchVelocity = velocity;
            _launchSpin = spin;
            if (!GroundBelow())
            {
                Wait();
                return;
            }
            Body.linearVelocity = velocity;
            Body.angularVelocity = spin;
        }

        private void Wait()
        {
            WaitingForGround = true;
            Body.isKinematic = true;
            _nextGroundCheck = Time.time + GroundCheckSeconds;
        }

        private void Update()
        {
            if (WaitingForGround)
            {
                if (Time.time >= _nextGroundCheck)
                {
                    _nextGroundCheck = Time.time + GroundCheckSeconds;
                    if (GroundBelow())
                    {
                        WaitingForGround = false;
                        Body.isKinematic = false;
                        Body.linearVelocity = _launchVelocity;
                        Body.angularVelocity = _launchSpin;
                    }
                }
                return;
            }
            // The ground it was falling to went away (the street unloaded under it): back to where it was dropped, to wait for it.
            if (transform.position.y < _droppedAt.y - GroundSearchMetres)
            {
                Body.isKinematic = true;
                transform.SetPositionAndRotation(_droppedAt, _droppedRotation);
                Wait();
            }
        }

        /// <summary>Something to land on below: the street, a roof, a floor (not a body, not this item).</summary>
        private bool GroundBelow()
        {
            int count = Physics.RaycastNonAlloc(_droppedAt + Vector3.up * 0.5f, Vector3.down, Hits, GroundSearchMetres, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i].collider;
                if (hit != null && !hit.transform.IsChildOf(transform) && hit.GetComponentInParent<PlayerVitals>() == null)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            _record?.Forget(Id, this);
        }
    }
}
