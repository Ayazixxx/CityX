using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// LIMP IN MID MOVE, ON ITS FEET (owner, 2026-11-10, of the body rolled off the end of the conveyor belt: "make it look natural like a
    /// person getting rolled off"; "looks very game engine like not like a cool cutscene"). <see cref="LetGoTurning"/> makes the body limp
    /// from the pose it is in already moving as it was, along and turning, so nothing about it jumps as physics takes it, with round ends
    /// on its legs to stand or land on. <see cref="Pin"/> holds it at one foot to a body (a snag) with a hinge, and it swings over that
    /// point; <see cref="Unpin"/> lets it go.
    /// Tried for the belt and dropped: one stiff body standing on its feet (it looked like a statue, a leg held out over nothing, and then
    /// went limp all at once), and springs on the joints (on a body this heavy they held at a hundredth of their strength).
    /// </summary>
    public sealed partial class Ragdoll
    {
        private readonly List<GameObject> _soles = new List<GameObject>();
        private ConfigurableJoint _pin;

        /// <summary>Held at a foot (<see cref="Pin"/>).</summary>
        public bool IsPinned => _pin != null;

        /// <summary>
        /// Limp from the pose it is in, every bone moving as one body would that runs at <paramref name="velocity"/> and turns at
        /// <paramref name="turning"/> (radians a second) about <paramref name="about"/>. False when it cannot go limp (see GoLimp).
        /// </summary>
        public bool LetGoTurning(Vector3 velocity, Vector3 turning, Vector3 about)
        {
            if (Bodies == null || !GoLimp(false))
            {
                return false;
            }
            // The joints take their limits from this pose, not the one the body was bound in.
            MeasureLimitsFromThisPose();
            SetKinematic(false);
            foreach (Rigidbody body in Bodies)
            {
                if (body != null)
                {
                    // Where it is drawn, not where physics last had it: smoothed from there, it was drawn a step behind as it went limp.
                    body.position = body.transform.position;
                    body.rotation = body.transform.rotation;
                    body.linearVelocity = velocity + Vector3.Cross(turning, body.worldCenterOfMass - about);
                    body.angularVelocity = turning;
                }
            }
            AddSoles();
            return true;
        }

        /// <summary>
        /// Holds the shin of <paramref name="foot"/> at its sole to <paramref name="to"/>, free to turn about <paramref name="axis"/> only, as a
        /// hinge. That sole is left out of it: the snag holds it.
        /// </summary>
        public void Pin(HumanBodyBones foot, Vector3 axis, Rigidbody to)
        {
            Transform bone = Animator != null && Animator.isHuman ? Animator.GetBoneTransform(foot) : null;
            Rigidbody shin = bone != null ? bone.GetComponentInParent<Rigidbody>() : null;
            if (shin == null || to == null)
            {
                return;
            }
            Unpin();
            _pin = shin.gameObject.AddComponent<ConfigurableJoint>();
            _pin.autoConfigureConnectedAnchor = true;
            _pin.anchor = shin.transform.InverseTransformPoint(bone.position);
            _pin.axis = shin.transform.InverseTransformDirection(axis);
            _pin.connectedBody = to;
            _pin.xMotion = ConfigurableJointMotion.Locked;
            _pin.yMotion = ConfigurableJointMotion.Locked;
            _pin.zMotion = ConfigurableJointMotion.Locked;
            _pin.angularYMotion = ConfigurableJointMotion.Locked;
            _pin.angularZMotion = ConfigurableJointMotion.Locked;
            _pin.enablePreprocessing = false;
            SoleSolid(bone, false);
        }

        public void Unpin()
        {
            if (_pin == null)
            {
                return;
            }
            SoleSolid(_pin.transform, true);
            Destroy(_pin);
            _pin = null;
        }

        /// <summary>Turns the round end under <paramref name="under"/> (a foot, or the shin it hangs from) on or off.</summary>
        private void SoleSolid(Transform under, bool solid)
        {
            foreach (GameObject sole in _soles)
            {
                if (sole != null && sole.transform.IsChildOf(under))
                {
                    sole.GetComponent<Collider>().enabled = solid;
                }
            }
        }

        /// <summary>The pin and the round ends are done with.</summary>
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
        }

        /// <summary>
        /// The round ends of the legs: the ragdoll has no feet of its own, and on the ends of its shins the feet sank into the floor by the
        /// height of the ankle, and the shins stuck in it were shoved out every step (TickLiveDepenetration) until the legs crossed.
        /// </summary>
        private void AddSoles()
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
                sole.transform.SetParent(bone, false);
                SphereCollider round = sole.AddComponent<SphereCollider>();
                round.radius = BodyRig.FootY;
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
