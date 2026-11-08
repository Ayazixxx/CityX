using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HELD IN A FIST (the Crimson Claw's grab; owner, 2026-11-08, with a drawing: "make the body fit into the claw like make him grip it with
    /// his thumb", "you are ragdolled when thrown"). Seized, the body is the ragdoll's as a knockdown's is (the walker, its controller and its
    /// Animator off, no keys), but not floppy: every bone stays kinematic, stiff in the pose it was caught in with its arms let down, and the
    /// whole body is carried upright by its root, the middle of its torso where the hand holds it (<see cref="HoldMiddleAt"/>). Let go, it goes
    /// limp for real and flies (<see cref="LetGo"/>), and gets up as from any knockdown. The owner's machine runs it; a copy mirrors the pose
    /// (RagdollMirror), so everyone sees it held and thrown.
    /// </summary>
    public sealed partial class Ragdoll
    {
        private bool _held;

        /// <summary>The middle of the torso, between the hips and the neck, in the root's frame, in the pose it was caught in.</summary>
        private Vector3 _heldMiddle;

        /// <summary>Seized and held stiff, not yet let go.</summary>
        public bool IsHeld => _held && _limp && !IsStandingUp;

        /// <summary>Where the middle of the torso is now.</summary>
        public Vector3 TorsoMiddle => transform.TransformPoint(_heldMiddle);

        /// <summary>Seized: limp but stiff, to be carried (<see cref="HoldMiddleAt"/>). False when it cannot be (see GoLimp).</summary>
        public bool Seize()
        {
            if (!GoLimp(false))
            {
                return false;
            }
            _held = true;
            StillPose.ArmsDown(transform);
            Transform hips = Animator != null ? Animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform neck = Animator != null ? Animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            Vector3 middle = hips != null && neck != null ? (hips.position + neck.position) * 0.5f : Torso != null ? Torso.position : transform.position;
            _heldMiddle = transform.InverseTransformPoint(middle);
            return true;
        }

        /// <summary>Held: upright, turned to face along <paramref name="facing"/>, the middle of its torso at <paramref name="point"/>.</summary>
        public void HoldMiddleAt(Vector3 point, Vector3 facing)
        {
            if (!IsHeld)
            {
                return;
            }
            facing.y = 0.0f;
            if (facing.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }
            transform.position += point - TorsoMiddle;
        }

        /// <summary>Thrown, each limb flies up to this much off the throw, metres per second ...</summary>
        public const float ThrownLimbScatter = 2.5f;

        /// <summary>... and the body tumbles head over heels this fast, degrees per second: thrown all at one speed, it sailed off stiff.</summary>
        public const float ThrownTumbleDegreesPerSecond = 540.0f;

        /// <summary>Let go: limp for real now, every bone flying at <paramref name="velocity"/> (zero: dropped), flailing.</summary>
        public void LetGo(Vector3 velocity)
        {
            if (!IsHeld)
            {
                return;
            }
            _held = false;
            Fly(velocity);
            if (velocity.sqrMagnitude < 0.01f || Torso == null)
            {
                return;
            }
            // The whole body turns as one about its torso (each bone moving as a point of one spinning body would), and each limb a little
            // off it: spun alone, the torso was held back by every bone hanging off it, and the body flew off upright.
            Vector3 spin = Vector3.Cross(Vector3.up, velocity).normalized * (ThrownTumbleDegreesPerSecond * Mathf.Deg2Rad);
            Vector3 centre = Torso.worldCenterOfMass;
            foreach (Rigidbody body in Bodies)
            {
                if (body == null)
                {
                    continue;
                }
                body.angularVelocity = spin;
                body.linearVelocity += Vector3.Cross(spin, body.worldCenterOfMass - centre);
                if (body != Torso)
                {
                    body.linearVelocity += Random.insideUnitSphere * ThrownLimbScatter;
                }
            }
        }
    }
}
