using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Hangs rigid bodies, colliders and joints off a humanoid skeleton.
    ///
    /// WHY IN CODE RATHER THAN UNITY'S RAGDOLL WIZARD. The wizard is a modal
    /// dialog: it cannot run headlessly, it cannot be re-run as part of a
    /// rebuild, and what it produced is only recoverable by remembering which
    /// bones somebody dragged into which slots. Every number below is instead
    /// derived from the skeleton itself, so this works on any humanoid rig and
    /// survives the character changing size.
    ///
    /// EVERYTHING IS A FRACTION OF THE RIG, NEVER A LENGTH IN METRES. A radius
    /// written as "0.05" means one thing on the 2.6m CITY X body and another on
    /// anything else it gets pointed at, and the body has already changed
    /// height once. Measuring the skeleton and working in fractions of it
    /// removes the question.
    /// </summary>
    public static class RagdollBuilder
    {
        // --- proportions, as fractions of the rig's own standing height ---

        private const float HeadRadius = 0.065f;
        private const float UpperArmRadius = 0.030f;
        private const float LowerArmRadius = 0.026f;
        private const float UpperLegRadius = 0.048f;
        private const float LowerLegRadius = 0.038f;
        private const float TorsoDepth = 0.075f;

        /// <summary>
        /// character.gd RAGDOLL_DENSITY, in the same spirit: mass comes from
        /// real capsule volume so that every piece weighs what it represents.
        ///
        /// Unity's default of 1kg per body would make a torso and a forearm
        /// equally heavy, which gives the joint solver no reason to think the
        /// torso should dominate a limb's motion rather than the reverse. That
        /// is precisely what makes a rig look like it is flailing around its
        /// own light extremities instead of a heavy body falling and dragging
        /// light limbs with it. Only the RATIOS matter -- the absolute value is
        /// arbitrary, since these proportions are stylised rather than
        /// anatomical.
        /// </summary>
        private const float Density = 700.0f;

        /// <summary>
        /// character.gd RAGDOLL_JOINT_LIMITS, in degrees. Swing is how far the
        /// joint bends, twist is rotation about the bone.
        /// </summary>
        private struct Limits
        {
            public float Swing;
            public float Twist;

            public Limits(float swing, float twist)
            {
                Swing = swing;
                Twist = twist;
            }
        }

        private static readonly Limits SpineLimits = new Limits(30.0f, 15.0f);
        private static readonly Limits NeckLimits = new Limits(40.0f, 30.0f);
        private static readonly Limits UpperArmLimits = new Limits(70.0f, 30.0f);
        private static readonly Limits LowerArmLimits = new Limits(80.0f, 5.0f);
        private static readonly Limits UpperLegLimits = new Limits(50.0f, 20.0f);
        private static readonly Limits LowerLegLimits = new Limits(40.0f, 10.0f);

        /// <summary>
        /// Builds the rig onto <paramref name="animator"/>'s bones and adds the
        /// Ragdoll component to <paramref name="root"/>. Returns null, having
        /// logged, if the skeleton is not usable.
        /// </summary>
        public static Ragdoll Build(GameObject root, Animator animator)
        {
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("CityX: no humanoid animator -- cannot build a ragdoll.");
                return null;
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

            // WHERE THE CHEST BOX STOPS: the shoulder line, not the middle of
            // the skull. Measuring chest-to-head was right on a rig with a
            // chest bone up at the collarbones, and wrong on this one, where
            // the "chest" falls back to a spine bone halfway up the torso -- it
            // built a 0.85m slab that swallowed the neck and half the head
            // sphere, and the rig thrashed itself apart trying to resolve it.
            Transform shoulderLine = animator.GetBoneTransform(HumanBodyBones.Neck) ?? head;

            if (hips == null || chest == null || head == null || leftFoot == null)
            {
                Debug.LogError("CityX: the skeleton is missing hips, chest, head or feet.");
                return null;
            }

            // Measured in the SKELETON'S OWN space, so it is unaffected by
            // whatever scale the model is displayed at.
            float rigHeight = Mathf.Abs(
                hips.InverseTransformPoint(head.position).magnitude +
                hips.InverseTransformPoint(leftFoot.position).magnitude);
            if (rigHeight <= 0.0f)
            {
                Debug.LogError("CityX: the skeleton measures zero height.");
                return null;
            }

            var bodies = new List<Rigidbody>();

            // --- torso. Two boxes rather than capsules: a torso is not round,
            // --- and a capsule chest rolls when the body lands on its side.
            float shoulderSpan = Distance(
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
            float hipSpan = Distance(
                animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.RightUpperLeg));

            Rigidbody hipBody = MakeBox(hips, chest, Mathf.Max(hipSpan, rigHeight * 0.14f),
                rigHeight * TorsoDepth);
            bodies.Add(hipBody);

            Rigidbody chestBody = MakeBox(chest, shoulderLine,
                Mathf.Max(shoulderSpan, rigHeight * 0.18f), rigHeight * TorsoDepth);
            bodies.Add(chestBody);
            Join(chestBody, hipBody, chest, SpineLimits);

            // --- head. A sphere, because a head is the one part that really is
            // --- round, and a sphere cannot catch an edge and stand the corpse
            // --- back up on its neck.
            Rigidbody headBody = MakeSphere(head, rigHeight * HeadRadius);
            bodies.Add(headBody);
            Join(headBody, chestBody, head, NeckLimits);

            // --- limbs.
            AddLimb(animator, bodies, chestBody, rigHeight,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                UpperArmRadius, LowerArmRadius, UpperArmLimits, LowerArmLimits);
            AddLimb(animator, bodies, chestBody, rigHeight,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                UpperArmRadius, LowerArmRadius, UpperArmLimits, LowerArmLimits);
            AddLimb(animator, bodies, hipBody, rigHeight,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                UpperLegRadius, LowerLegRadius, UpperLegLimits, LowerLegLimits);
            AddLimb(animator, bodies, hipBody, rigHeight,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                UpperLegRadius, LowerLegRadius, UpperLegLimits, LowerLegLimits);

            Ragdoll ragdoll = root.GetComponent<Ragdoll>();
            if (ragdoll == null)
            {
                ragdoll = root.AddComponent<Ragdoll>();
            }
            ragdoll.Bodies = bodies.ToArray();
            ragdoll.Torso = hipBody;
            ragdoll.Animator = animator;

            Debug.Log($"CityX: ragdoll built from {bodies.Count} bones " +
                      $"(rig measures {rigHeight:0.00} in its own units)");
            return ragdoll;
        }

        private static void AddLimb(Animator animator, List<Rigidbody> bodies, Rigidbody parent,
            float rigHeight, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end,
            float upperRadius, float lowerRadius, Limits upperLimits, Limits lowerLimits)
        {
            Transform upperBone = animator.GetBoneTransform(upper);
            Transform lowerBone = animator.GetBoneTransform(lower);
            Transform endBone = animator.GetBoneTransform(end);

            if (upperBone == null || lowerBone == null || endBone == null)
            {
                Debug.LogWarning($"CityX: {upper} is incomplete -- that limb will not ragdoll.");
                return;
            }

            Rigidbody upperBody = MakeCapsule(upperBone, lowerBone, rigHeight * upperRadius);
            bodies.Add(upperBody);
            Join(upperBody, parent, upperBone, upperLimits);

            Rigidbody lowerBody = MakeCapsule(lowerBone, endBone, rigHeight * lowerRadius);
            bodies.Add(lowerBody);
            Join(lowerBody, upperBody, lowerBone, lowerLimits);
        }

        /// <summary>
        /// A capsule running from <paramref name="bone"/> to <paramref name="child"/>.
        /// Its axis is chosen by measuring where the child actually is, because
        /// which local axis a bone points down is an exporter's decision and
        /// differs between rigs.
        /// </summary>
        private static Rigidbody MakeCapsule(Transform bone, Transform child, float radius)
        {
            Vector3 local = bone.InverseTransformPoint(child.position);
            float length = local.magnitude;

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = DominantAxis(local);
            capsule.radius = radius;
            // Never shorter than it is round, or Unity produces a sphere with a
            // negative cylinder in the middle.
            capsule.height = Mathf.Max(length, radius * 2.0f);
            capsule.center = local * 0.5f;

            float volume = CapsuleVolume(radius, capsule.height);
            return MakeBody(bone, volume);
        }

        private static Rigidbody MakeSphere(Transform bone, float radius)
        {
            var sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius;

            float volume = 4.0f / 3.0f * Mathf.PI * radius * radius * radius;
            return MakeBody(bone, volume);
        }

        private static Rigidbody MakeBox(Transform bone, Transform toward, float width, float depth)
        {
            Vector3 local = bone.InverseTransformPoint(toward.position);
            float length = Mathf.Max(local.magnitude, depth);

            var box = bone.gameObject.AddComponent<BoxCollider>();
            Vector3 size = Vector3.one * depth;
            size[DominantAxis(local)] = length;
            // Widened across whichever axis is neither the bone's own direction
            // nor its thickness, so the torso is a slab rather than a post.
            size[WidestAxis(DominantAxis(local))] = width;
            box.size = size;
            box.center = local * 0.5f;

            return MakeBody(bone, size.x * size.y * size.z);
        }

        private static Rigidbody MakeBody(Transform bone, float volume)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            // See GrabCarry.cs -- every ragdoll bone can be picked up once
            // it is actually limp (GrabCarry also requires !isKinematic).
            bone.gameObject.AddComponent<Grabbable>();
            body.mass = Mathf.Max(volume * Density, 0.5f);
            body.linearDamping = Ragdoll.LinearDamping;
            body.angularDamping = Ragdoll.AngularDamping;
            // Continuous, because a limb thrown by a finisher moves further in
            // one step than it is thick, and would otherwise pass through the
            // road it is supposed to land on.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // The default lets a badly overlapping pair fire itself into orbit
            // while separating. The rig starts with overlaps by construction.
            body.maxDepenetrationVelocity = 3.0f;
            body.isKinematic = true;
            return body;
        }

        /// <summary>
        /// Connects a bone to its parent bone with a limited character joint.
        ///
        /// The twist axis is the BONE'S OWN direction, so twist means what it
        /// means anatomically -- rotation along the limb -- and the two swing
        /// limits are the bending. Unity's default axes are world-relative and
        /// would give a limb a twist limit about some arbitrary direction.
        /// </summary>
        private static void Join(Rigidbody body, Rigidbody parent, Transform bone, Limits limits)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = parent.transform.InverseTransformPoint(bone.position);

            Vector3 along = AlongBone(bone);
            joint.axis = along;
            joint.swingAxis = Perpendicular(along);

            joint.lowTwistLimit = new SoftJointLimit { limit = -limits.Twist };
            joint.highTwistLimit = new SoftJointLimit { limit = limits.Twist };
            joint.swing1Limit = new SoftJointLimit { limit = limits.Swing };
            joint.swing2Limit = new SoftJointLimit { limit = limits.Swing };

            joint.enableProjection = true;
            joint.projectionDistance = 0.1f;
            joint.projectionAngle = 20.0f;
            // Preprocessing tries to resolve joints it considers over-stretched
            // by teleporting bodies, which on a rig that starts in a real pose
            // reads as the corpse snapping into a different shape on frame one.
            joint.enablePreprocessing = false;
        }

        /// <summary>
        /// The bone's own direction in its local space, taken from its first
        /// child. A leaf bone falls back to +Y, which is the usual convention
        /// and only ever affects a twist axis on something with no children.
        /// </summary>
        private static Vector3 AlongBone(Transform bone)
        {
            if (bone.childCount == 0)
            {
                return Vector3.up;
            }
            Vector3 local = bone.InverseTransformPoint(bone.GetChild(0).position);
            return local.sqrMagnitude < 1e-8f ? Vector3.up : local.normalized;
        }

        private static Vector3 Perpendicular(Vector3 v)
        {
            // Crossing with whichever world axis this is least aligned to, so
            // the result is never a near-zero vector.
            Vector3 reference = Mathf.Abs(v.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(v, reference).normalized;
        }

        private static int DominantAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            if (x >= y && x >= z) return 0;
            return y >= z ? 1 : 2;
        }

        /// <summary>The axis a torso is widest across, given the way it runs.</summary>
        private static int WidestAxis(int boneAxis)
        {
            return boneAxis == 0 ? 1 : 0;
        }

        private static float CapsuleVolume(float radius, float height)
        {
            float cylinder = Mathf.PI * radius * radius * Mathf.Max(height - 2.0f * radius, 0.0f);
            float caps = 4.0f / 3.0f * Mathf.PI * radius * radius * radius;
            return cylinder + caps;
        }

        private static float Distance(Transform a, Transform b)
        {
            return a == null || b == null ? 0.0f : Vector3.Distance(a.position, b.position);
        }
    }
}
