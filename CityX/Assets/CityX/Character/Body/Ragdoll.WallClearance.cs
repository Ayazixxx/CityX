using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE LIVING BODY'S OWN SHAPE AGAINST WALLS (owner, 2026-11-10: "make it so body parts cant clip through walls and other objects, make
    /// the hitbox the exact size of the body and stop at contact with the wall"). The capsule that moves the body is 0.5 m round: the trunk,
    /// head and legs are inside it, but the arms reach past it, further as they swing, and walking along a wall put an arm and a shoulder into
    /// the bricks.
    /// The arms' and the head's own colliders (the ragdoll's, kinematic and on the bones while the body is alive) are asked how far they are
    /// into the world (<see cref="WallContacts"/>), and the body's movement keeps it out (PlayerLocomotion.WallClearance, WallStandoff). Only the
    /// world itself counts: other bodies and loose things do not push.
    /// </summary>
    public sealed partial class Ragdoll
    {
        /// <summary>The parts held out of walls: those the moving capsule does not already cover.</summary>
        private static readonly HumanBodyBones[] WallParts =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.Head,
        };

        /// <summary>A contact whose way out is steeper than this (its share up or down) is a floor or a ceiling, not a wall.</summary>
        private const float WallSteepest = 0.7f;

        /// <summary>How far clear of the wall a part is left, metres: touching it, not held off it.</summary>
        private const float WallSkin = 0.005f;

        private Collider[] _wallParts;
        private static readonly Collider[] WallHits = new Collider[16];

        /// <summary>
        /// Each wall the arms and head reach into: the way out of it along the ground and how far, written into <paramref name="into"/>;
        /// returns how many.
        /// </summary>
        public int WallContacts(Vector3[] into)
        {
            if (_limp || Animator == null)
            {
                return 0;
            }
            _wallParts ??= FindWallParts();
            int outs = 0;
            foreach (Collider part in _wallParts)
            {
                if (part == null || !part.enabled)
                {
                    continue;
                }
                Transform bone = part.transform;
                int hits = Physics.OverlapSphereNonAlloc(bone.TransformPoint(CentreOf(part)), ReachOf(part), WallHits, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits && outs < into.Length; i++)
                {
                    Collider other = WallHits[i];
                    if (!IsWorld(other) || !Physics.ComputePenetration(part, bone.position, bone.rotation, other, other.transform.position,
                            other.transform.rotation, out Vector3 direction, out float distance))
                    {
                        continue;
                    }
                    if (Mathf.Abs(direction.y) > WallSteepest)
                    {
                        continue;
                    }
                    Vector3 along = new Vector3(direction.x, 0.0f, direction.z).normalized;
                    into[outs++] = along * (distance / Mathf.Max(0.3f, new Vector2(direction.x, direction.z).magnitude) + WallSkin);
                }
            }
            return outs;
        }

        /// <summary>The world's own colliders: not this body's, not another body's capsule, nothing with a rigid body of its own.</summary>
        private bool IsWorld(Collider other)
        {
            return other != null && other.attachedRigidbody == null && !(other is CharacterController) && !other.transform.IsChildOf(transform)
                && !IsOwnCollider(other);
        }

        private Collider[] FindWallParts()
        {
            var parts = new Collider[WallParts.Length];
            for (int i = 0; i < WallParts.Length; i++)
            {
                Transform bone = Animator.GetBoneTransform(WallParts[i]);
                parts[i] = bone != null ? bone.GetComponent<Collider>() : null;
            }
            return parts;
        }

        private static Vector3 CentreOf(Collider part)
        {
            return part switch
            {
                CapsuleCollider capsule => capsule.center,
                SphereCollider sphere => sphere.center,
                BoxCollider box => box.center,
                _ => Vector3.zero,
            };
        }

        /// <summary>How far from its centre a part reaches, world metres, and a little more.</summary>
        private static float ReachOf(Collider part)
        {
            Vector3 s = part.transform.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));   // (the three-argument Max allocates)
            float local = part switch
            {
                CapsuleCollider capsule => Mathf.Max(0.5f * capsule.height, capsule.radius),
                SphereCollider sphere => sphere.radius,
                BoxCollider box => 0.5f * box.size.magnitude,
                _ => 0.5f,
            };
            return local * scale + 0.05f;
        }
    }
}
