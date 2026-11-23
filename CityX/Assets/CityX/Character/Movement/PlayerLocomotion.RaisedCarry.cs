using UnityEngine;

namespace CityX.Character
{
    /// <summary>A held thing that is carried raised (a bottle by its neck): the body holds its right arm in <see cref="RaisedCarry"/>'s pose.</summary>
    public interface IRaisedCarry
    {
    }

    /// <summary>
    /// THE BOTTLE'S CARRY (owner, 2026-11-23: "make him carry it like the demoman"; docs/GLASS_BOTTLE.md): the right forearm up before the
    /// chest and the bottle standing up out of the fist, ready to be swung. The pose is the first frame of the bottle's own moves, posed in
    /// Blender (CityX/SourceArt/Bottle/bottle_moves.py), so a throw begun from the carry begins where the arm already is.
    /// </summary>
    public static class RaisedCarry
    {
        /// <summary>The bottle's moves in Resources, and the clip whose first frame is the carry.</summary>
        public const string MotionPath = "Bottle/BottleMotion";
        public const string Clip = "bottle_ready";

        /// <summary>The right arm's bones, shoulder to wrist: the carry is theirs alone.</summary>
        public static readonly string[] ArmBones = { "arm_r", "forearm_r", "hand_r" };

        private static Quaternion[] _arm;
        private static bool _looked;

        /// <summary>The arm bones' local rotations in the carry (<see cref="ArmBones"/>' order), or null if the moves are not there.</summary>
        public static Quaternion[] Arm
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    BakedMotion motion = BakedMotion.Load(MotionPath);
                    if (motion != null && motion.HasSegment(Clip))
                    {
                        var arm = new Quaternion[ArmBones.Length];
                        int found = 0;
                        for (int b = 0; b < motion.Bones.Count; b++)
                        {
                            int which = System.Array.IndexOf(ArmBones, motion.Bones[b]);
                            if (which >= 0)
                            {
                                arm[which] = motion.RotationAt(motion.SegmentStart(Clip), b);
                                found++;
                            }
                        }
                        _arm = found == ArmBones.Length ? arm : null;
                    }
                }
                return _arm;
            }
        }
    }

    /// <summary>The right arm in the raised carry while a bottle is in the hand, after the clips and before the fingers (as the knife's turn is).</summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the arm takes to come up when a bottle is taken in hand, and to come back to it after a swing, seconds.</summary>
        public const float RaisedCarrySeconds = 0.22f;

        private float _raisedCarry;

        /// <summary>How much of the right arm the raised carry holds, 0 to 1 (for tests).</summary>
        public float RaisedCarryWeight { get; private set; }

        private void ApplyRaisedCarry()
        {
            Quaternion[] arm = RaisedCarry.Arm;
            bool carried = arm != null && HeldSword is IRaisedCarry && _rightUpperArm != null && _rightLowerArm != null && _handR != null;
            // A swing, a guard or a baked move (the aim, the throw) has the arm: the carry gives way as they come in.
            float wanted = carried
                ? (1.0f - (_swordFrame.Active ? Mathf.Clamp01(_swordFrame.Weight) : 0.0f)) * (1.0f - BakedArmsWeight)
                : 0.0f;
            _raisedCarry = Mathf.MoveTowards(_raisedCarry, carried ? 1.0f : 0.0f, Mathf.Max(0.0f, Time.deltaTime) / RaisedCarrySeconds);
            float weight = Mathf.Min(_raisedCarry, wanted);
            RaisedCarryWeight = weight;
            if (weight <= 0.001f)
            {
                return;
            }
            float eased = weight * weight * (3.0f - 2.0f * weight);
            _rightUpperArm.localRotation = Quaternion.Slerp(_rightUpperArm.localRotation, arm[0], eased);
            _rightLowerArm.localRotation = Quaternion.Slerp(_rightLowerArm.localRotation, arm[1], eased);
            _handR.localRotation = Quaternion.Slerp(_handR.localRotation, arm[2], eased);
        }
    }
}
