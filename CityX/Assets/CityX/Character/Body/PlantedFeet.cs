using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A standing body's soles held flat on one spot, whatever its Animator does to the legs (owner, 2026-11-10, of the body idling on the
    /// conveyor belt: "it appears as if his feet are gliding back and forth on the ground"). The idle clip sways the feet along with the
    /// hips; this takes where they are once, in the body's own space so they ride and turn with it, and after the Animator bends each leg
    /// back onto that spot. Geometry on Transforms, like <see cref="TwoBoneIK"/>: the caller says when.
    /// </summary>
    public sealed class PlantedFeet
    {
        /// <summary>How much a knee is drawn toward the body's front, metres: a nearly straight leg has no bend of its own to keep.</summary>
        private const float KneeForward = 0.05f;

        private sealed class Leg
        {
            public Transform Thigh;
            public Transform Shin;
            public Transform Foot;
            public Vector3 Place;
            public Quaternion Turn;
            public Vector3 Ankle;
            public Quaternion Sole;
        }

        /// <summary>How many poses of the clip the middle of the sway is taken from.</summary>
        private const int SwaySamples = 24;

        /// <summary>The share of a leg's full length it stands at: a little short of straight, so the knee never locks.</summary>
        private const float LegReach = 0.985f;

        private readonly Animator _animator;
        private readonly Transform _root;
        private readonly Transform _hips;
        private readonly Leg _left;
        private readonly Leg _right;

        public PlantedFeet(Animator animator)
        {
            _animator = animator;
            _root = animator.transform;
            _hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _left = Bones(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            _right = Bones(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        }

        /// <summary>The feet have been set down (<see cref="SetDown"/>).</summary>
        public bool IsDown { get; private set; }

        /// <summary>
        /// Finds where the feet stay by playing the Animator's current clip once round, here and now (it ends where it began): each foot
        /// goes on the middle of its sway, on the body's floor, sole flat. On one end of the sway the far leg would run out of reach.
        /// </summary>
        public void SetDown()
        {
            if (_left == null || _right == null)
            {
                return;
            }
            float step = _animator.GetCurrentAnimatorStateInfo(0).length / SwaySamples;
            Vector3 left = Vector3.zero, right = Vector3.zero;
            for (int i = 0; i < SwaySamples; i++)
            {
                _animator.Update(step);
                left += _root.InverseTransformPoint(_left.Foot.position);
                right += _root.InverseTransformPoint(_right.Foot.position);
            }
            SetDown(_left, left / SwaySamples);
            SetDown(_right, right / SwaySamples);
            IsDown = true;
        }

        /// <summary>After the Animator: the legs bent back so that the feet are on their spots. <paramref name="weight"/> 0 leaves the pose as it is.</summary>
        public void Hold(float weight)
        {
            if (!IsDown || weight <= 0.0f)
            {
                return;
            }
            // The idle stands on its toes: with the sole flat the ankle is lower, further than the straight leg goes. The hips come down for it.
            PutOnFloor(_left);
            PutOnFloor(_right);
            float drop = Mathf.Max(0.0f, HipsTooHighBy(_left), HipsTooHighBy(_right));
            _hips.position -= _root.up * (drop * weight);
            Hold(_left, weight);
            Hold(_right, weight);
        }

        private static Leg Bones(Animator animator, HumanBodyBones thigh, HumanBodyBones shin, HumanBodyBones foot)
        {
            if (!animator.isHuman)
            {
                return null;
            }
            return new Leg
            {
                Thigh = animator.GetBoneTransform(thigh),
                Shin = animator.GetBoneTransform(shin),
                Foot = animator.GetBoneTransform(foot),
            };
        }

        private void SetDown(Leg leg, Vector3 place)
        {
            place.y = BodyRig.FootY * leg.Foot.lossyScale.y / _root.lossyScale.y;
            leg.Place = place;

            // The clip stands on its toes (FlatFeetPlayTests): the sole's own up is put on the floor's, its heading kept.
            Quaternion turn = Quaternion.Inverse(_root.rotation) * leg.Foot.rotation;
            leg.Turn = Quaternion.FromToRotation(turn * Vector3.up, Vector3.up) * turn;
        }

        /// <summary>How far above the height this leg's hip can reach its spot from, the knee left its <see cref="LegReach"/>; below 0 it reaches.</summary>
        private float HipsTooHighBy(Leg leg)
        {
            Vector3 fromSpot = leg.Thigh.position - leg.Ankle;
            float above = Vector3.Dot(fromSpot, _root.up);
            float asideSquared = fromSpot.sqrMagnitude - above * above;
            float length = (Vector3.Distance(leg.Thigh.position, leg.Shin.position) + Vector3.Distance(leg.Shin.position, leg.Foot.position)) * LegReach;
            return above - Mathf.Sqrt(Mathf.Max(length * length - asideSquared, 0.0f));
        }

        private void Hold(Leg leg, float weight)
        {
            Vector3 hip = leg.Thigh.position;
            Vector3 along = leg.Foot.position - hip;
            Vector3 bow = leg.Shin.position - hip;
            if (along.sqrMagnitude > 1e-6f)
            {
                bow -= along * (Vector3.Dot(bow, along) / along.sqrMagnitude);
            }
            Quaternion footAsItWas = leg.Foot.rotation;
            TwoBoneIK.Solve(leg.Thigh, leg.Shin, leg.Foot, leg.Ankle, bow + _root.forward * KneeForward, weight);
            leg.Foot.rotation = Quaternion.Slerp(footAsItWas, leg.Sole, weight);
        }

        /// <summary>Where this frame's ankle and sole go: on the spot.</summary>
        private void PutOnFloor(Leg leg)
        {
            leg.Ankle = _root.TransformPoint(leg.Place);
            leg.Sole = _root.rotation * leg.Turn;
        }
    }
}
