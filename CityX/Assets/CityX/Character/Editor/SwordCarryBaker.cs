using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// THE ONE-HANDED SWORD'S OWN WALK, RUN AND IDLE (owner, 2026-10-17: "make you own sword running and walking animations and make them look
    /// good"; "research how games do one handed sword running and walking animations and make ours work"). Each clip is the game's own Godot
    /// clip (idle, walk, run, walk backward, walk left, walk right: the legs, hips, chest, head and free arm everyone already has), baked again
    /// with the right arm carrying a sword the way <see cref="SwordCarryArm"/> says: hanging a little back and out, the blade forward, down and
    /// clear of the legs, swinging under a third as far as the clip's own arm, in its time, with the forearm and hand following a beat behind.
    /// The arm's swing is read off the clip's own right arm (its swing forward and back of its middle), so it is always in step with the legs,
    /// whatever the gait.
    ///
    /// Run by PlayerRigBuilder before it builds the controller (so a rebuild always has them), or from the menu.
    /// </summary>
    public static class SwordCarryBaker
    {
        public const string OutputDirectory = "Assets/CityX/Character/Animations/Sword1H";

        /// <summary>The Godot clips the carry is baked from, all looping.</summary>
        public static readonly string[] GodotClips = { "idle", "walk", "run", "walk_backward", "walk_left", "walk_right" };

        public static string PathOf(string godotName) => $"{OutputDirectory}/sword1h_{godotName}.anim";

        /// <summary>A baked carry clip, or null if it has not been baked.</summary>
        public static AnimationClip Load(string godotName) => AssetDatabase.LoadAssetAtPath<AnimationClip>(PathOf(godotName));

        [MenuItem("CityX/Bake One-Handed Sword Carry")]
        public static void BakeAll()
        {
            foreach (string name in GodotClips)
            {
                Bake(name);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>The carry pose a gait's clip is baked with.</summary>
        public static ArmAngles PoseFor(string godotName)
        {
            switch (godotName)
            {
                case "idle": return SwordCarryArm.Idle;
                case "run": return SwordCarryArm.Run;
                default: return SwordCarryArm.Walk;
            }
        }

        /// <summary>How a gait's own arm swing is handed to the sword arm.</summary>
        public static SwordCarryArm.SwingShares SwingFor(string godotName)
        {
            switch (godotName)
            {
                case "idle": return SwordCarryArm.IdleSwing;
                case "run": return SwordCarryArm.RunSwing;
                default: return SwordCarryArm.WalkSwing;
            }
        }

        public static AnimationClip Bake(string godotName)
        {
            ArmAngles pose = PoseFor(godotName);
            SwordCarryArm.SwingShares shares = SwingFor(godotName);

            // First every moment's own arm swing (the clip's right upper arm, forward or back, in the chest's frame) ...
            var times = new List<float>();
            var pitches = new List<float>();
            float[] sampleTimes = null;
            float[] swing = null;
            GodotMotionBaker.PoseHook measure = (bones, time, length) =>
            {
                times.Add(time);
                pitches.Add(ArmPitch(bones));
            };

            // ... then the sword arm in its place, swung by a share of it about its middle.
            GodotMotionBaker.PoseHook adjust = (bones, time, length) =>
            {
                if (swing == null)
                {
                    sampleTimes = times.ToArray();
                    float mean = 0.0f;
                    foreach (float p in pitches)
                    {
                        mean += p;
                    }
                    mean /= Mathf.Max(1, pitches.Count);
                    swing = new float[pitches.Count];
                    for (int i = 0; i < swing.Length; i++)
                    {
                        swing[i] = pitches[i] - mean;
                    }
                }
                float now = SwordCarryArm.SampleLooping(sampleTimes, swing, length, time);
                float elbow = SwordCarryArm.SampleLooping(sampleTimes, swing, length, time - shares.ElbowLag * length);
                float wrist = SwordCarryArm.SampleLooping(sampleTimes, swing, length, time - shares.WristLag * length);
                ArmAngles arm = SwordCarryArm.Swinging(pose, shares, now, elbow, wrist);
                SwordCarryArm.Solve(arm, bones["spine"].rotation, out Quaternion upperArm, out Quaternion forearm, out Quaternion hand);
                bones["arm_r"].rotation = upperArm;
                bones["forearm_r"].rotation = forearm;
                bones["hand_r"].rotation = hand;
            };

            AnimationClip clip = GodotMotionBaker.Bake(godotName, true, "sword1h_" + godotName, PathOf(godotName), measure, adjust);
            if (clip != null && swing != null)
            {
                float most = float.MinValue, least = float.MaxValue;
                foreach (float s in swing)
                {
                    most = Mathf.Max(most, s);
                    least = Mathf.Min(least, s);
                }
                Debug.Log($"CityX: sword carry {godotName}: the clip's own arm swings {least:0}..{most:0} deg, the sword arm {least * shares.Shoulder:0}..{most * shares.Shoulder:0}");
            }
            return clip;
        }

        /// <summary>The right upper arm's swing in a pose: degrees forward (+) or back (-) of hanging straight down, in the chest's own frame.</summary>
        public static float ArmPitch(Dictionary<string, Transform> bones)
        {
            Vector3 along = bones["spine"].InverseTransformDirection(bones["forearm_r"].position - bones["arm_r"].position);
            return Mathf.Atan2(along.z, -along.y) * Mathf.Rad2Deg;
        }
    }
}
