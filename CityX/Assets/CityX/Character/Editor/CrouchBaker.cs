using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// THE CROUCH CLIPS (owner, 2026-10-27: "the crouch animation looks bad ... make ours more like [Source's] with a more suitable
    /// animation"; docs/CROUCH.md). Each is the game's own Godot clip (the idle, the walk and its two side-steps: the legs, arms and
    /// timing everyone already has) baked again crouched, the way <see cref="CrouchPose"/> says: the hips down and back and the upper body
    /// leaned forward on them, the head turned back up, the spine straight (the game holds it so, PlayerLocomotion.LocomotionSpineLeans), and
    /// each leg folded under it by a two-bone solve onto where the clip puts that foot, lifted less, standing still a little wider.
    ///
    /// THE STEPS FIT THE SPEED: each walk's own pace (how fast a planted foot passes under the body) is measured, and its steps are drawn in or
    /// out so the clip covers Source's crouching speed (a third of the walk) at its own rate -- so the clip neither hurries nor slides. A
    /// planted foot's sole is laid flat along the way its leg points; a lifted one keeps the leg's tilt.
    ///
    /// The Animator's "Crouch" layer plays them (legs, hips and head only), at the duck's weight, over whatever the arms do. Run by
    /// PlayerRigBuilder before it builds the controller (so a rebuild always has them), or from the menu.
    /// </summary>
    public static class CrouchBaker
    {
        public const string OutputDirectory = "Assets/CityX/Character/Animations/Crouch";

        /// <summary>The Godot clips the crouch is baked from, all looping. Backward is the crouched walk played the other way (<see cref="Reversed"/>).</summary>
        public static readonly string[] GodotClips = { "idle", "walk", "walk_left", "walk_right" };

        /// <summary>The crouched walk backward's name, as a Godot clip's would be.</summary>
        public const string Backward = "walk_backward";

        /// <summary>How high above the ground a foot must be to keep its leg's tilt instead of lying flat, metres.</summary>
        private const float FlatBelow = 0.08f;

        /// <summary>A foot this close to its lowest counts as planted, for measuring a walk's pace, metres.</summary>
        private const float PlantedWithin = 0.02f;

        /// <summary>The most a walk's steps are drawn in or stretched out to fit the crouching speed.</summary>
        private const float LeastStride = 0.5f;
        private const float MostStride = 2.0f;

        public static string PathOf(string godotName) => $"{OutputDirectory}/crouch_{godotName}.anim";

        /// <summary>A baked crouch clip, or null if it has not been baked.</summary>
        public static AnimationClip Load(string godotName) => AssetDatabase.LoadAssetAtPath<AnimationClip>(PathOf(godotName));

        [MenuItem("CityX/Bake Crouch")]
        public static void BakeAll()
        {
            foreach (string name in GodotClips)
            {
                Bake(name);
            }
            // BACKWARD IS FORWARD REVERSED: the Godot walk backward is slow (0.69 m/s), and fitting it to the crouching speed nearly doubled its
            // steps, the legs kicking out off the ground. The crouched walk run the other way has the same steps and pace, feet on the ground.
            AnimationClip forward = Load("walk");
            if (forward != null)
            {
                ClipAsset.Store(Reversed(forward, "crouch_" + Backward), PathOf(Backward));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static AnimationClip Bake(string godotName)
        {
            bool still = godotName == "idle";
            var times = new List<float>();
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            Vector3 leftCentre = Vector3.zero, rightCentre = Vector3.zero;
            float leftGround = 0.0f, rightGround = 0.0f;
            float stride = 1.0f;
            bool measured = false;

            // First where the clip puts each foot through the whole cycle ...
            GodotMotionBaker.PoseHook measure = (bones, time, length) =>
            {
                times.Add(time);
                left.Add(bones["foot_l"].position);
                right.Add(bones["foot_r"].position);
            };

            // ... then each moment crouched over feet drawn from those.
            GodotMotionBaker.PoseHook adjust = (bones, time, length) =>
            {
                if (!measured)
                {
                    Middle(left, out leftCentre, out leftGround);
                    Middle(right, out rightCentre, out rightGround);
                    if (!still)
                    {
                        float pace = Pace(times, left, leftGround, right, rightGround);
                        stride = pace > 0.01f ? Mathf.Clamp(PlayerRigBuilder.CrouchSpeed / pace, LeastStride, MostStride) : 1.0f;
                        Debug.Log($"CityX: crouch {godotName}: the walk's own pace {pace:0.00} m/s, steps drawn to {stride:0.00} of it for {PlayerRigBuilder.CrouchSpeed:0.00} m/s");
                    }
                    measured = true;
                }
                Transform leftFoot = bones["foot_l"], rightFoot = bones["foot_r"];
                Vector3 leftAt = leftFoot.position, rightAt = rightFoot.position;

                Transform hips = bones["hips"];
                hips.position += new Vector3(0.0f, -CrouchPose.HipsDrop, -CrouchPose.HipsBack);
                hips.rotation = Quaternion.AngleAxis(CrouchPose.LeanDegrees, Vector3.right) * hips.rotation;
                bones["spine"].localRotation = Quaternion.identity;
                Transform neck = bones["neck"];
                neck.rotation = Quaternion.AngleAxis(-CrouchPose.LeanDegrees * CrouchPose.HeadLevelShare, Vector3.right) * neck.rotation;

                float leftSide = Mathf.Sign(leftCentre.x), rightSide = Mathf.Sign(rightCentre.x);
                Fold(bones["thigh_l"], bones["shin_l"], leftFoot, CrouchPose.FootTarget(leftAt, leftCentre, leftGround, still, leftSide, stride),
                    CrouchPose.KneeHint(leftSide), leftAt.y - leftGround);
                Fold(bones["thigh_r"], bones["shin_r"], rightFoot, CrouchPose.FootTarget(rightAt, rightCentre, rightGround, still, rightSide, stride),
                    CrouchPose.KneeHint(rightSide), rightAt.y - rightGround);
            };

            return GodotMotionBaker.Bake(godotName, true, "crouch_" + godotName, PathOf(godotName), measure, adjust);
        }

        /// <summary>
        /// <paramref name="clip"/> played backward, as a new clip called <paramref name="name"/>: every key at the clip's length less its time, its
        /// slopes turned round (the curve's mirror in time), the clip's settings kept.
        /// </summary>
        public static AnimationClip Reversed(AnimationClip clip, string name)
        {
            float length = clip.length;
            var reversed = new AnimationClip { name = name, frameRate = clip.frameRate };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                var turned = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    Keyframe key = keys[keys.Length - 1 - i];
                    turned[i] = new Keyframe(length - key.time, key.value, -key.outTangent, -key.inTangent);
                }
                AnimationUtility.SetEditorCurve(reversed, binding, new AnimationCurve(turned));
            }
            AnimationUtility.SetAnimationClipSettings(reversed, AnimationUtility.GetAnimationClipSettings(clip));
            return reversed;
        }

        /// <summary>A foot's middle over the cycle (on the ground plane) and the lowest it goes (its planted height).</summary>
        private static void Middle(List<Vector3> feet, out Vector3 centre, out float ground)
        {
            centre = Vector3.zero;
            ground = float.MaxValue;
            foreach (Vector3 foot in feet)
            {
                centre += foot;
                ground = Mathf.Min(ground, foot.y);
            }
            centre /= Mathf.Max(1, feet.Count);
            centre.y = ground;
        }

        /// <summary>How fast a planted foot passes under the body in the clip, metres a second: the speed the clip walks at (the middle value of both feet's planted moments).</summary>
        private static float Pace(List<float> times, List<Vector3> left, float leftGround, List<Vector3> right, float rightGround)
        {
            var speeds = new List<float>();
            for (int i = 1; i < times.Count; i++)
            {
                float dt = times[i] - times[i - 1];
                if (dt <= 1e-4f)
                {
                    continue;
                }
                AddPlanted(speeds, left[i - 1], left[i], leftGround, dt);
                AddPlanted(speeds, right[i - 1], right[i], rightGround, dt);
            }
            if (speeds.Count == 0)
            {
                return 0.0f;
            }
            speeds.Sort();
            return speeds[speeds.Count / 2];
        }

        private static void AddPlanted(List<float> speeds, Vector3 from, Vector3 to, float ground, float dt)
        {
            if (from.y - ground < PlantedWithin && to.y - ground < PlantedWithin)
            {
                Vector3 moved = to - from;
                moved.y = 0.0f;
                speeds.Add(moved.magnitude / dt);
            }
        }

        /// <summary>One leg folded onto <paramref name="target"/>, the knee bowed toward <paramref name="knee"/>; the sole flat along the leg while planted.</summary>
        private static void Fold(Transform thigh, Transform shin, Transform foot, Vector3 target, Vector3 knee, float lift)
        {
            TwoBoneIK.Solve(thigh, shin, foot, target, knee);
            foot.localRotation = Quaternion.identity;
            Vector3 along = Vector3.ProjectOnPlane(foot.forward, Vector3.up);
            if (along.sqrMagnitude < 1e-6f)
            {
                along = Vector3.forward;
            }
            Quaternion flat = Quaternion.LookRotation(along, Vector3.up);
            foot.rotation = Quaternion.Slerp(flat, foot.rotation, Mathf.Clamp01(lift / FlatBelow));
        }
    }
}
