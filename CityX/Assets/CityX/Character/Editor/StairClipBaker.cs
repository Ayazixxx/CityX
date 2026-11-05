using System.Collections.Generic;
using System.IO;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The recorded stair clips as humanoid clips (docs/STAIRS.md). CityX/SourceArt/StairClips/stair_clips.py copies the upper body of the owner's four Mixamo files onto this
    /// skeleton, one JSON each, and each frame is put on a throwaway skeleton and read back as muscles, the way GodotMotionBaker does the walks. Imported as a humanoid FBX
    /// instead, the same clips came out with the arms held far too high and the hips 15 cm too high.
    /// </summary>
    public static class StairClipBaker
    {
        public const string OutputDirectory = "Assets/CityX/Character/Animations/Stairs";

        /// <summary>Relative to the Unity project folder.</summary>
        private const string SourceDirectory = "SourceArt/StairClips";

        public const string UpWalk = "stairs_up_walk";
        public const string UpRun = "stairs_up_run";
        public const string DownWalk = "stairs_down_walk";
        public const string DownRun = "stairs_down_run";

        private static readonly string[] Names = { UpWalk, UpRun, DownWalk, DownRun };

        public static string PathOf(string name) => $"{OutputDirectory}/{name}.anim";

        /// <summary>A baked clip, or null if it has not been baked.</summary>
        public static AnimationClip Load(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(PathOf(name));

        [MenuItem("CityX/Bake Stair Clips")]
        public static void BakeAll()
        {
            foreach (string name in Names)
            {
                Bake(name);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Bake(string name)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string file = Path.Combine(projectRoot, SourceDirectory, name + ".json");
            if (!File.Exists(file))
            {
                Debug.LogWarning($"CityX: no stair clip at {file}");
                return;
            }
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/CityX/Character/CityXAvatar.asset");
            if (avatar == null || !avatar.isValid)
            {
                Debug.LogError("CityX: the humanoid avatar is missing or invalid -- rebuild the character first.");
                return;
            }

            BakedMotion motion = BakedMotion.FromJson(File.ReadAllText(file));
            GameObject root = BodyRigBuilder.BuildSkeleton("StairBake", out Dictionary<string, Transform> bones);
            try
            {
                int frames = motion.FrameCount;
                int muscleCount = HumanTrait.MuscleCount;
                var times = new float[frames];
                var muscles = new float[muscleCount][];
                for (int i = 0; i < muscleCount; i++)
                {
                    muscles[i] = new float[frames];
                }
                var rootT = new[] { new float[frames], new float[frames], new float[frames] };
                var rootQ = new[] { new float[frames], new float[frames], new float[frames], new float[frames] };

                var handler = new HumanPoseHandler(avatar, root.transform);
                var pose = new HumanPose();
                for (int f = 0; f < frames; f++)
                {
                    times[f] = f / (float)motion.FramesPerSecond;
                    for (int b = 0; b < motion.Bones.Count; b++)
                    {
                        if (bones.TryGetValue(motion.Bones[b], out Transform bone))
                        {
                            bone.localRotation = motion.RotationAt(f, b);
                        }
                    }
                    bones[BakedMotion.RootBone].position = motion.HipsAt(f);

                    handler.GetHumanPose(ref pose);
                    for (int i = 0; i < muscleCount; i++)
                    {
                        muscles[i][f] = pose.muscles[i];
                    }
                    rootT[0][f] = pose.bodyPosition.x;
                    rootT[1][f] = pose.bodyPosition.y;
                    rootT[2][f] = pose.bodyPosition.z;
                    rootQ[0][f] = pose.bodyRotation.x;
                    rootQ[1][f] = pose.bodyRotation.y;
                    rootQ[2][f] = pose.bodyRotation.z;
                    rootQ[3][f] = pose.bodyRotation.w;
                }
                handler.Dispose();
                GodotMotionBaker.StoreHumanoidClip(name, PathOf(name), times, muscles, rootT, rootQ, true);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
