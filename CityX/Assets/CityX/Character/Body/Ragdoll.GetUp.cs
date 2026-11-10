using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// FROM HOWEVER IT LIES TO THE GET-UP CLIP (owner, 2026-11-10: "make the standing up animation look good. the ragdoll to stand animation
    /// ... right now it looks glitchy"). The clip starts face down. The body is first brought onto its front as a body turns over: rolled
    /// about its own length by however far it lies from face down (half a turn from its back, a quarter from its side, none from its front),
    /// its hips rising over its shoulder width on the way, while every joint eases from the angle the ragdoll left it at to the clip's
    /// (<see cref="PoseFromLying"/>). Then the clip runs, at <see cref="GetUpPace"/>.
    /// Before, every bone was turned straight from where it lay to where the clip had it, in half a second: from its back the body went
    /// through itself.
    /// </summary>
    public sealed partial class Ragdoll
    {
        /// <summary>How much higher the hips are carried at the middle of a roll than lying flat, metres on a body of the player's size.</summary>
        public const float RollLift = 0.14f;

        /// <summary>The least time the joints take to ease from the ragdoll's angles to the clip's, seconds: a body already on its front.</summary>
        public const float SettleSeconds = 0.4f;

        private Transform[] _lyingBones;
        private Quaternion[] _lyingTurns;
        private Vector3 _lyingHips;
        private Quaternion _lyingHipsTurn;
        private Vector3 _lyingFront;
        private float _rollDegrees;
        private Vector3 _rollAxis;
        private Quaternion _rollRest;
        private GetUpPace _pace = new GetUpPace(StandUpPaceSeconds);

        /// <summary>How long this get-up's roll onto its front takes, seconds (0 lying on its front).</summary>
        public float RollSeconds => _pace.RollSeconds(_rollDegrees);

        /// <summary>How long the joints take to ease into the clip: the roll, or the least time.</summary>
        private float SettleFor => Mathf.Max(RollSeconds, SettleSeconds);

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
        /// After the Animator has posed the clip: the body between how it lay and that pose, rolled as far as it still has to go. Returns
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

        /// <summary>The clip lies on the ground until this second of it, and is on its feet from <see cref="OnItsFeetClipSeconds"/>.</summary>
        private const float LyingClipSeconds = 2.0f;

        private const float OnItsFeetClipSeconds = 4.0f;

        /// <summary>How far along and across the lying body the ground is looked at for its slope, metres on a body of the player's size.</summary>
        private const float SlopeAlong = 0.9f;

        private const float SlopeAcross = 0.35f;

        /// <summary>How far over a point the ground under it is looked for from, and how far down, metres.</summary>
        private const float FloorProbeAbove = 0.9f;

        private const float FloorProbeDown = 2.5f;

        private LimbFloorClamp.FloorUnder _floorUnder;

        /// <summary>
        /// After the pose is the clip's (as much of it as <paramref name="clipShare"/>): put on the ground the body is really on (owner,
        /// 2026-11-10: "use hand and foot ik to interact with the terrain it is standing on properly"). The clip lies, kneels and stands on a
        /// level floor at the root's feet. While it lies, the whole pose is tipped to the slope of the ground under it; as it stands, the hips
        /// come down to the lower foot's ground; and each hand and foot goes on the ground under itself (LimbFloorClamp).
        /// </summary>
        private void HoldOnTheGround(float clipShare)
        {
            if (Animator == null || !Animator.isHuman)
            {
                return;
            }
            _floorUnder ??= FloorUnder;
            float clip = _pace.ClipSecondsAt(_standingUpElapsed - RollSeconds);
            float lying = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(LyingClipSeconds, OnItsFeetClipSeconds, clip));
            LieAlongTheGround(lying * clipShare);
            StandOnTheLowerFoot((1.0f - lying) * clipShare);
            LimbFloorClamp.Apply(Animator, _floorUnder, transform.position.y, clipShare);
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
        private void StandOnTheLowerFoot(float share)
        {
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform left = Animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (share <= 0.0f || hips == null || left == null || right == null)
            {
                return;
            }
            float clipFloor = transform.position.y;
            float lower = Mathf.Min(FloorUnder(left.position), FloorUnder(right.position)) - clipFloor;
            hips.position += Vector3.up * (lower * share);
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

        /// <summary>The clip at where this get-up has got to: held on its first pose until the body is on its front.</summary>
        private void PlayGetUpClip()
        {
            if (Animator == null || !Animator.isActiveAndEnabled)
            {
                return;
            }
            Animator.speed = 0.0f;   // the pace is not the clip's own: each frame it is put where the get-up is
            Animator.Play("GettingUp", 0, _pace.ClipSecondsAt(_standingUpElapsed - RollSeconds) / GetUpPace.ClipSeconds);
        }
    }
}
