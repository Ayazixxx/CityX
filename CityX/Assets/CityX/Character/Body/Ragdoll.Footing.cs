using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// STOOD UP, THEN LIMP (owner, 2026-11-10, of the body rolled off the end of the conveyor belt: "make it have ragdoll physics but just be
    /// in a stood up position"; "make it look natural like a person getting rolled off", with a drawing of it pitched out over the drop, its
    /// back foot still on the roller; "make him fully loose after frame 19"). <see cref="Stand"/> makes the body physics' as one solid body
    /// in the pose it is in, with the mass and middle of all its bones, on the round ends of its legs. <see cref="Slacken"/> makes it the
    /// plain limp ragdoll, every bone moving as it was moving as part of the whole. <see cref="Pin"/> holds it, stood or limp, at one
    /// foot to a body (a snag) with a hinge, and it swings over that point; <see cref="Unpin"/> lets it go.
    /// Springs on the joints were tried first: on a body this heavy they held at a hundredth of their strength, the knees folded, and it
    /// turned round as it went over.
    /// </summary>
    public sealed partial class Ragdoll
    {
        /// <summary>How little the stood body is slowed turning: it tips as a free body would.</summary>
        private const float StoodAngularDamping = 0.05f;

        private readonly List<GameObject> _soles = new List<GameObject>();
        private Rigidbody _stood;
        private ConfigurableJoint _pin;
        private Vector3[] _placesInStood;
        private Quaternion[] _turnsInStood;

        /// <summary>Stood up as one body, not yet slack.</summary>
        public bool IsStood => _stood != null;

        /// <summary>Held at a foot (<see cref="Pin"/>).</summary>
        public bool IsPinned => _pin != null;

        /// <summary>
        /// Physics' from here, standing as one solid body in the pose it is in, moving at <paramref name="velocity"/>. False when it cannot
        /// go limp (see GoLimp).
        /// </summary>
        public bool Stand(Vector3 velocity)
        {
            if (Bodies == null || !GoLimp(false))
            {
                return false;
            }
            // The joints take their limits from this pose, for when it goes slack.
            MeasureLimitsFromThisPose();
            DepenetrateFromWorld();

            var stood = new GameObject("Stood") { layer = gameObject.layer };
            stood.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _stood = stood.AddComponent<Rigidbody>();
            float mass = 0.0f;
            Vector3 middle = Vector3.zero;
            Vector3 low = Vector3.positiveInfinity, high = Vector3.negativeInfinity;
            _placesInStood = new Vector3[Bodies.Length];
            _turnsInStood = new Quaternion[Bodies.Length];
            for (int i = 0; i < Bodies.Length; i++)
            {
                Rigidbody body = Bodies[i];
                if (body == null)
                {
                    continue;
                }
                mass += body.mass;
                middle += body.worldCenterOfMass * body.mass;
                Vector3 local = stood.transform.InverseTransformPoint(body.worldCenterOfMass);
                low = Vector3.Min(low, local);
                high = Vector3.Max(high, local);
                _placesInStood[i] = stood.transform.InverseTransformPoint(body.transform.position);
                _turnsInStood[i] = Quaternion.Inverse(stood.transform.rotation) * body.transform.rotation;
            }
            AddSoles(foot => stood.transform);

            _stood.mass = mass;
            _stood.centerOfMass = stood.transform.InverseTransformPoint(middle / mass);
            // A box the size of the body, its own way up: what it turns like as it tips.
            Vector3 size = Vector3.Max(high - low, Vector3.one * 0.2f);
            _stood.inertiaTensor = new Vector3(
                mass * (size.y * size.y + size.z * size.z) / 12.0f,
                mass * (size.x * size.x + size.z * size.z) / 12.0f,
                mass * (size.x * size.x + size.y * size.y) / 12.0f);
            _stood.inertiaTensorRotation = Quaternion.identity;
            _stood.linearDamping = LinearDamping;
            _stood.angularDamping = StoodAngularDamping;
            _stood.interpolation = RigidbodyInterpolation.Interpolate;
            _stood.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _stood.linearVelocity = velocity;
            return true;
        }

        /// <summary>
        /// Holds the body at the sole of <paramref name="foot"/> to <paramref name="to"/>, free to turn about <paramref name="axis"/> only, as a
        /// hinge: the stood body while it stands, the shin once it is limp. The round ends are left out of it: the snag holds it, and a sole
        /// left solid propped it on the side of the roller, halfway over.
        /// </summary>
        public void Pin(HumanBodyBones foot, Vector3 axis, Rigidbody to)
        {
            Transform bone = Animator != null && Animator.isHuman ? Animator.GetBoneTransform(foot) : null;
            Rigidbody held = _stood != null ? _stood : bone != null ? bone.GetComponentInParent<Rigidbody>() : null;
            if (bone == null || held == null || to == null)
            {
                return;
            }
            Unpin();
            _pin = held.gameObject.AddComponent<ConfigurableJoint>();
            _pin.autoConfigureConnectedAnchor = true;
            _pin.anchor = held.transform.InverseTransformPoint(bone.position);
            _pin.axis = held.transform.InverseTransformDirection(axis);
            _pin.connectedBody = to;
            _pin.xMotion = ConfigurableJointMotion.Locked;
            _pin.yMotion = ConfigurableJointMotion.Locked;
            _pin.zMotion = ConfigurableJointMotion.Locked;
            _pin.angularYMotion = ConfigurableJointMotion.Locked;
            _pin.angularZMotion = ConfigurableJointMotion.Locked;
            _pin.enablePreprocessing = false;
            // Stood, both: the front one propped it. Limp, the held one only: the other leg goes over the roller as a leg would.
            SoleSolid(false, _stood != null ? null : bone);
        }

        public void Unpin()
        {
            if (_pin != null)
            {
                Destroy(_pin);
            }
            _pin = null;
            SoleSolid(true, null);
        }

        /// <summary>Limp from here: every bone live, moving as it was moving as part of the stood body, the round ends of its legs on its shins.</summary>
        public void Slacken()
        {
            if (_stood == null)
            {
                return;
            }
            Rigidbody stood = _stood;
            Follow(stood.position, stood.rotation);
            ForgetFooting();
            Physics.SyncTransforms();
            SetKinematic(false);
            foreach (Rigidbody body in Bodies)
            {
                if (body != null)
                {
                    body.linearVelocity = stood.GetPointVelocity(body.worldCenterOfMass);
                    body.angularVelocity = stood.angularVelocity;
                }
            }
            AddSoles(foot => foot);
        }

        /// <summary>Turns the round ends on or off: all, or the one on <paramref name="only"/>.</summary>
        private void SoleSolid(bool solid, Transform only)
        {
            foreach (GameObject sole in _soles)
            {
                if (sole != null && (only == null || sole.transform.parent == only))
                {
                    sole.GetComponent<Collider>().enabled = solid;
                }
            }
        }

        /// <summary>The pin, the stood body and every round end are done with, the bones left as they are.</summary>
        private void ForgetFooting()
        {
            Unpin();
            foreach (GameObject sole in _soles)
            {
                if (sole != null)
                {
                    Destroy(sole);
                }
            }
            _soles.Clear();
            if (_stood != null)
            {
                Destroy(_stood.gameObject);
            }
            _stood = null;
        }

        /// <summary>The bones, still kinematic, go where the stood body has carried them. LateUpdate, from its smoothed place.</summary>
        private void FollowTheStand()
        {
            if (_stood != null)
            {
                Follow(_stood.transform.position, _stood.transform.rotation);
            }
        }

        private void Follow(Vector3 position, Quaternion rotation)
        {
            // Parents come before their children in Bodies, so a bone set here is not moved again by its parent.
            for (int i = 0; i < Bodies.Length; i++)
            {
                if (Bodies[i] != null)
                {
                    Bodies[i].transform.SetPositionAndRotation(position + rotation * _placesInStood[i], rotation * _turnsInStood[i]);
                }
            }
        }

        /// <summary>
        /// The round ends of the legs, on whatever <paramref name="holder"/> gives for each foot: the ragdoll has no feet of its own, and on
        /// the ends of its shins the feet sank into the floor by the height of the ankle, and the shins stuck in it were shoved out every step
        /// (TickLiveDepenetration) until the legs crossed.
        /// </summary>
        private void AddSoles(System.Func<Transform, Transform> holder)
        {
            if (Animator == null || !Animator.isHuman)
            {
                return;
            }
            foreach (HumanBodyBones foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Transform bone = Animator.GetBoneTransform(foot);
                if (bone == null)
                {
                    continue;
                }
                var sole = new GameObject("Sole") { layer = gameObject.layer };
                sole.transform.SetParent(holder(bone), false);
                sole.transform.position = bone.position;
                SphereCollider round = sole.AddComponent<SphereCollider>();
                round.radius = BodyRig.FootY * bone.lossyScale.y / Mathf.Max(sole.transform.lossyScale.y, 1e-4f);
                foreach (Rigidbody body in Bodies)
                {
                    if (body != null && body.TryGetComponent(out Collider limb))
                    {
                        Physics.IgnoreCollision(round, limb, true);
                    }
                }
                foreach (GameObject other in _soles)
                {
                    Physics.IgnoreCollision(round, other.GetComponent<Collider>(), true);
                }
                _soles.Add(sole);
            }
        }
    }
}
