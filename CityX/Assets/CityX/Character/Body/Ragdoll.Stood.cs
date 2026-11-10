using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// STOOD UP (owner, 2026-11-10, of the body rolled off the end of the conveyor belt: "make it have ragdoll physics but just be in a stood
    /// up position ... then we shouldnt need to add any artificial tilt it should just do it on its own as it slowly rolls off"). The body is
    /// the ragdoll's, every bone live and free to be pushed, but each joint is sprung back toward the pose it was in when it was stood
    /// (<see cref="Stand"/>): it stands as a body does, held up by nothing but its feet, and a support taken from under them tips it over by
    /// itself. <see cref="Slacken"/> lets the springs go and it is plain limp. The springs are drives on joints of their own beside the
    /// CharacterJoints, angular only: the solver holds them, so they can be as stiff as a standing leg needs without shaking.
    /// </summary>
    public sealed partial class Ragdoll
    {
        private readonly List<ConfigurableJoint> _standing = new List<ConfigurableJoint>();

        /// <summary>Stood up and sprung toward its pose, not yet slack.</summary>
        public bool IsStood => _standing.Count > 0;

        /// <summary>
        /// Live, in the pose it is in, every bone moving at <paramref name="velocity"/>, each joint sprung back toward that pose this hard
        /// (newton-metres a radian) and damped this much (per radian a second). False when it cannot go limp (see GoLimp).
        /// </summary>
        public bool Stand(Vector3 velocity, float stiffness, float damping)
        {
            if (!GoLimp(false))
            {
                return false;
            }
            // A joint takes the pose it is connected in as the one it is drawn to, and its limits from it.
            MeasureLimitsFromThisPose();
            var drive = new JointDrive { positionSpring = stiffness, positionDamper = damping, maximumForce = float.MaxValue };
            foreach (CharacterJoint limb in GetComponentsInChildren<CharacterJoint>(true))
            {
                ConfigurableJoint spring = limb.gameObject.AddComponent<ConfigurableJoint>();
                spring.connectedBody = limb.connectedBody;
                spring.xMotion = ConfigurableJointMotion.Free;
                spring.yMotion = ConfigurableJointMotion.Free;
                spring.zMotion = ConfigurableJointMotion.Free;
                spring.angularXMotion = ConfigurableJointMotion.Free;
                spring.angularYMotion = ConfigurableJointMotion.Free;
                spring.angularZMotion = ConfigurableJointMotion.Free;
                spring.rotationDriveMode = RotationDriveMode.Slerp;
                spring.slerpDrive = drive;
                spring.targetRotation = Quaternion.identity;
                spring.enablePreprocessing = false;
                _standing.Add(spring);
            }
            DepenetrateFromWorld();
            SetKinematic(false);
            foreach (Rigidbody body in Bodies)
            {
                if (body != null)
                {
                    body.linearVelocity = velocity;
                }
            }
            return true;
        }

        /// <summary>The springs let go: plain limp from here, from whatever pose it has been pushed into.</summary>
        public void Slacken()
        {
            foreach (ConfigurableJoint spring in _standing)
            {
                if (spring != null)
                {
                    Destroy(spring);
                }
            }
            _standing.Clear();
        }
    }
}
