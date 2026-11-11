using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// FROM HOWEVER IT LIES TO STANDING, on the get-up clips made for this body (<see cref="GetUpMotion"/>; owner, 2026-11-10: "the limbs
    /// bend all awkwardly clearly in an unintented way", "its like the knees bend the wrong way"). The clip nearer how the body lies is
    /// played on its bones after the Animator (<see cref="PoseGetUp"/>), at <see cref="GetUpPace"/>. The body is first brought to the clip's
    /// first pose as a body turns over: rolled about its own length by however far it lies from it (none on its back or front, a quarter
    /// turn on its side), every joint easing from the angle the ragdoll left it at to the clip's (<see cref="PoseFromLying"/>). Then it is
    /// held on the ground it is really on (<see cref="HoldOnTheGround"/>).
    /// Before, a clip made for another body, face down only, was played through the Animator, and a floor clamp bowed every knee near the
    /// ground upward: on a kneeling leg that bent the knee backward.
    /// </summary>
    public sealed partial class Ragdoll
    {
        /// <summary>How much higher the hips are carried at the middle of a roll than lying flat, metres on a body of the player's size.</summary>
        public const float RollLift = 0.14f;

        /// <summary>The least time the joints take to ease from the ragdoll's angles to the clip's, seconds.</summary>
        public const float SettleSeconds = 0.4f;

        private Transform[] _lyingBones;
        private Quaternion[] _lyingTurns;
        private Vector3 _lyingHips;
        private Quaternion _lyingHipsTurn;
        private Vector3 _lyingFront;
        private float _rollDegrees;
        private Vector3 _rollAxis;
        private Quaternion _rollRest;
        private GetUpPace _pace = new GetUpPace(GetUpPace.QuickSpeed, 0.0f);
        private string _getUpSegment = GetUpMotion.FromTheBack;
        private BakedPose _getUpPose;
        private Transform[] _getUpBones;

        /// <summary>The clip this get-up plays (<see cref="GetUpMotion.FromTheBack"/> or <see cref="GetUpMotion.FromTheFront"/>).</summary>
        public string GetUpSegment => _getUpSegment;

        /// <summary>How long this get-up's roll to the clip's first pose takes, seconds (0 lying as the clip starts).</summary>
        public float RollSeconds => _pace.RollSeconds(_rollDegrees);

        /// <summary>How long the joints take to ease into the clip: the roll, or the least time.</summary>
        private float SettleFor => Mathf.Max(RollSeconds, SettleSeconds);

        /// <summary>Where in its clip the get-up is, seconds at the clip's own speed: on its first pose until the roll is done.</summary>
        public float GetUpClipSeconds => _pace.ClipSecondsAt(_standingUpElapsed - RollSeconds);

        /// <summary>The clip's pose where the get-up has got to, on the bones; the hips placed in the body's own frame.</summary>
        private void PoseGetUp()
        {
            BakedMotion motion = GetUpMotion.Motion;
            if (motion == null || Animator == null || !motion.HasSegment(_getUpSegment))
            {
                return;
            }
            if (_getUpPose == null)
            {
                _getUpPose = motion.NewPose();
                _getUpBones = new Transform[_getUpPose.Bones.Count];
                for (int i = 0; i < _getUpBones.Length; i++)
                {
                    _getUpBones[i] = TransformSearch.FindDeep(Animator.transform, _getUpPose.Bones[i]);
                }
            }
            motion.Sample(_getUpSegment, GetUpClipSeconds, false, _getUpPose);
            for (int i = 0; i < _getUpBones.Length; i++)
            {
                Transform bone = _getUpBones[i];
                if (bone == null)
                {
                    continue;
                }
                if (_getUpPose.Bones[i] == BakedMotion.RootBone)
                {
                    bone.localPosition = _getUpPose.Hips;
                }
                bone.localRotation = _getUpPose.Rotations[i];
            }
        }

        /// <summary>The pose the ragdoll lies in: each joint's own angle, where the hips are and how they are turned, which way the chest faces.</summary>
        private void CaptureLying()
        {
            _lyingBones = null;
            _rollDegrees = 0.0f;
            if (Animator == null || !Animator.isHuman)
            {
                return;
            }
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = Animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
            {
                return;
            }
            var bones = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = Animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null && bone != hips)
                {
                    bones.Add(bone);
                }
            }
            _lyingBones = bones.ToArray();
            _lyingTurns = new Quaternion[_lyingBones.Length];
            for (int i = 0; i < _lyingBones.Length; i++)
            {
                _lyingTurns[i] = _lyingBones[i].localRotation;
            }
            _lyingHips = hips.position;
            _lyingHipsTurn = hips.rotation;
            _lyingFront = ChestFront();
            _snapshotHipsPosition = hips.position;
            _headWorldAtCapture = head.position;
        }

        /// <summary>Which way the chest faces now (FallFlail.Front); zero without the bones to tell.</summary>
        private Vector3 ChestFront()
        {
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips), neck = Animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform left = Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (hips == null || neck == null || left == null || right == null)
            {
                return Vector3.zero;
            }
            return FallFlail.Front(hips.position, neck.position, left.position, right.position);
        }

        /// <summary>
        /// With the clip's first pose on the bones and laid where the body lies: how far round its own length the body lies from that pose
        /// (<see cref="_rollDegrees"/> about <see cref="_rollAxis"/>), and what is left of its hips' turn that a roll does not explain.
        /// </summary>
        private void MeasureRoll()
        {
            if (_lyingBones == null)
            {
                return;
            }
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips), head = Animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 length = head.position - hips.position;
            length.y = 0.0f;
            _rollAxis = length.sqrMagnitude > 1e-4f ? length.normalized : transform.forward;
            Vector3 clipFront = Vector3.ProjectOnPlane(ChestFront(), _rollAxis), lyingFront = Vector3.ProjectOnPlane(_lyingFront, _rollAxis);
            _rollDegrees = clipFront.sqrMagnitude > 1e-4f && lyingFront.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(clipFront, lyingFront, _rollAxis) : 0.0f;
            _rollRest = _lyingHipsTurn * Quaternion.Inverse(Quaternion.AngleAxis(_rollDegrees, _rollAxis) * hips.rotation);
        }

        /// <summary>
        /// After <see cref="PoseGetUp"/>: the body between how it lay and that pose, rolled as far as it still has to go. Returns
        /// how much of the pose is the clip's (1 once it is all the clip's).
        /// </summary>
        private float PoseFromLying()
        {
            if (_lyingBones == null)
            {
                return 1.0f;
            }
            float share = Mathf.Clamp01(_standingUpElapsed / SettleFor);
            float eased = share * share * (3.0f - 2.0f * share);
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            float left = _rollDegrees * (1.0f - eased);
            // Over its shoulder on the way: highest on its side, and no higher than it lay at the start or lies at the end.
            float over = Mathf.Abs(Mathf.Sin(left * Mathf.Deg2Rad)) - (1.0f - eased) * Mathf.Abs(Mathf.Sin(_rollDegrees * Mathf.Deg2Rad));
            hips.SetPositionAndRotation(
                Vector3.Lerp(_lyingHips, hips.position, eased) + Vector3.up * (RollLift * transform.lossyScale.y * Mathf.Max(0.0f, over)),
                Quaternion.Slerp(_rollRest, Quaternion.identity, eased) * Quaternion.AngleAxis(left, _rollAxis) * hips.rotation);
            for (int i = 0; i < _lyingBones.Length; i++)
            {
                if (_lyingBones[i] != null)
                {
                    _lyingBones[i].localRotation = Quaternion.Slerp(_lyingTurns[i], _lyingBones[i].localRotation, eased);
                }
            }
            if (share >= 1.0f)
            {
                _lyingBones = null;
            }
            return eased;
        }

        // --- on the ground it lies on ---

        /// <summary>How far along and across the lying body the ground is looked at for its slope, metres on a body of the player's size.</summary>
        private const float SlopeAlong = 0.9f;

        private const float SlopeAcross = 0.35f;

        /// <summary>How far over a point the ground under it is looked for from, and how far down, metres.</summary>
        private const float FloorProbeAbove = 0.9f;

        private const float FloorProbeDown = 2.5f;

        /// <summary>
        /// A hand or foot the clip has within <see cref="OnTheFloor"/> of its own floor is on it and goes with the ground under it in full;
        /// above <see cref="OffTheFloor"/> it is in the air and is left where the clip has it; between, part of the way. Metres.
        /// </summary>
        public const float OnTheFloor = 0.3f;

        public const float OffTheFloor = 0.7f;

        /// <summary>A hand or foot the ground moves less than this is left alone, metres.</summary>
        private const float FollowsAtLeast = 0.01f;

        /// <summary>How many times a hand or foot is put on the ground under where it has got to.</summary>
        private const int FollowPasses = 2;

        /// <summary>Each limb's upper bone, lower bone and hand or foot: the left arm, the right arm, the left leg, the right leg.</summary>
        private static readonly HumanBodyBones[] Limbs =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        private readonly float[] _overTheClipFloor = new float[4];

        /// <summary>How much of the way a hand or foot the clip has <paramref name="overItsFloor"/> metres over its floor goes with the ground under it.</summary>
        public static float OnTheGroundShare(float overItsFloor) =>
            1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(OnTheFloor, OffTheFloor, overItsFloor));

        /// <summary>
        /// After the pose is the clip's (as much of it as <paramref name="clipShare"/>): put on the ground the body is really on (owner,
        /// 2026-11-10: "use hand and foot ik to interact with the terrain it is standing on properly"). The clips lie, kneel and stand on a
        /// level floor at the root's feet. While it lies, the whole pose is tipped to the slope of the ground under it; as it stands, the hips
        /// come down to the lower foot's ground; and each hand and foot the clip has on its floor is put as high over the ground under it as
        /// the clip has it over its floor, its elbow or knee bending the way the clip bends it.
        /// </summary>
        private void HoldOnTheGround(float clipShare)
        {
            if (Animator == null || !Animator.isHuman)
            {
                return;
            }
            float clipFloor = transform.position.y;
            for (int limb = 0; limb < _overTheClipFloor.Length; limb++)
            {
                Transform end = Animator.GetBoneTransform(Limbs[limb * 3 + 2]);
                _overTheClipFloor[limb] = end != null ? end.position.y - clipFloor : float.MaxValue;
            }
            float last = GetUpMotion.Seconds(_getUpSegment);
            float lying = 1.0f - Mathf.SmoothStep(0.0f, 1.0f,
                Mathf.InverseLerp(last - GetUpMotion.KneelingBeforeTheEnd, last - GetUpMotion.StandingBeforeTheEnd, GetUpClipSeconds));
            LieAlongTheGround(lying * clipShare);
            StandOnTheLowerFoot((1.0f - lying) * clipShare, clipFloor);
            for (int limb = 0; limb < _overTheClipFloor.Length; limb++)
            {
                FollowTheGround(limb, clipShare);
            }
        }

        /// <summary>Puts one hand or foot (<paramref name="limb"/>: see <see cref="Limbs"/>) as high over the ground under it as the clip had it over its floor.</summary>
        private void FollowTheGround(int limb, float share)
        {
            Transform upper = Animator.GetBoneTransform(Limbs[limb * 3]), lower = Animator.GetBoneTransform(Limbs[limb * 3 + 1]);
            Transform end = Animator.GetBoneTransform(Limbs[limb * 3 + 2]);
            float over = _overTheClipFloor[limb];
            if (upper == null || lower == null || end == null || over == float.MaxValue)
            {
                return;
            }
            float follows = OnTheGroundShare(over / transform.lossyScale.y) * share;
            Quaternion held = end.rotation;
            // Reaching down a step can swing a foot over the step above it, into its riser: the ground is looked at again where it went, and the
            // hand or foot lifted out of whatever it went into.
            for (int pass = 0; pass < FollowPasses; pass++)
            {
                float rise = (FloorUnder(end.position) + over - end.position.y) * follows;
                if (Mathf.Abs(rise) < FollowsAtLeast * transform.lossyScale.y || (pass > 0 && rise < 0.0f))
                {
                    break;
                }
                // The joint bows the way the clip bows it, never turned about: on a kneeling leg that bends the knee backward.
                Vector3 bow = lower.position - 0.5f * (upper.position + end.position);
                TwoBoneIK.Solve(upper, lower, end, end.position + Vector3.up * rise, bow.sqrMagnitude > 1e-8f ? bow.normalized : transform.forward);
            }
            end.rotation = held;
        }

        /// <summary>Tips the pose, about the ground under its hips, to the slope of the ground along and across the lying body.</summary>
        private void LieAlongTheGround(float share)
        {
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips), head = Animator.GetBoneTransform(HumanBodyBones.Head);
            if (share <= 0.0f || hips == null || head == null)
            {
                return;
            }
            Vector3 along = head.position - hips.position;
            along.y = 0.0f;
            if (along.sqrMagnitude < 1e-4f)
            {
                return;
            }
            along.Normalize();
            Vector3 across = Vector3.Cross(Vector3.up, along);
            float scale = transform.lossyScale.y;
            Vector3 at = hips.position;
            float rise = FloorUnder(at + along * (SlopeAlong * scale)) - FloorUnder(at - along * (SlopeAlong * scale));
            float bank = FloorUnder(at + across * (SlopeAcross * scale)) - FloorUnder(at - across * (SlopeAcross * scale));
            Quaternion tip = Quaternion.FromToRotation(along, (along * (2.0f * SlopeAlong * scale) + Vector3.up * rise).normalized)
                * Quaternion.FromToRotation(across, (across * (2.0f * SlopeAcross * scale) + Vector3.up * bank).normalized);
            tip = Quaternion.Slerp(Quaternion.identity, tip, share);
            var pivot = new Vector3(at.x, FloorUnder(at), at.z);
            hips.SetPositionAndRotation(pivot + tip * (at - pivot), tip * hips.rotation);
        }

        /// <summary>On its feet: the hips go down (or up) with the ground under the lower foot, so that foot's leg still reaches it.</summary>
        private void StandOnTheLowerFoot(float share, float clipFloor)
        {
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform left = Animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (share <= 0.0f || hips == null || left == null || right == null)
            {
                return;
            }
            float lower = Mathf.Min(FloorUnder(left.position), FloorUnder(right.position)) - clipFloor;
            hips.position += Vector3.up * (lower * share);
        }

        /// <summary>
        /// Up: the root goes onto the ground under the lower foot, where <see cref="StandOnTheLowerFoot"/> has been holding the hips, so that
        /// nothing moves and the feet stay on that ground once the get-up lets go. The root was put on the ground under the hips as they lay:
        /// lying across a kerb that was the road, and the body stood out the intro's look-around with its feet in the sidewalk.
        /// </summary>
        private void StandTheRootOnTheGround()
        {
            if (Animator == null || !Animator.isHuman)
            {
                return;
            }
            Transform left = Animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (left == null || right == null)
            {
                return;
            }
            Vector3 root = transform.position;
            root.y = Mathf.Min(FloorUnder(left.position), FloorUnder(right.position));
            transform.position = root;
        }

        /// <summary>The height of the ground under <paramref name="at"/>, the body itself looked through; the root's own height where there is none.</summary>
        private float FloorUnder(Vector3 at)
        {
            float scale = transform.lossyScale.y;
            var from = new Vector3(at.x, Mathf.Max(at.y, transform.position.y) + FloorProbeAbove * scale, at.z);
            int hits = Physics.RaycastNonAlloc(from, Vector3.down, GroundHits, (FloorProbeAbove + FloorProbeDown) * scale, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                if (GroundHits[i].distance < nearest && !GroundHits[i].transform.IsChildOf(transform))
                {
                    nearest = GroundHits[i].distance;
                }
            }
            return nearest < float.MaxValue ? from.y - nearest : transform.position.y;
        }
    }
}
