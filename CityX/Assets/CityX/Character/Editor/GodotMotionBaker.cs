using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Turns the Godot character's own baked animations (character/animations/*.tres) into Unity humanoid clips, so the Unity character MOVES the way the Godot
    /// one does.
    ///
    /// WHY NOT JUST RETARGET THE MIXAMO FILES AGAIN. Both games start from the same Mixamo files, and the Godot clips are what the owner means by "the Godot
    /// character looks better". Retargeting them through Unity's humanoid system onto this body gave different motion from the same source: a sprint that swings
    /// a thigh through 130 degrees where the Godot clip swings it 66, a walk with half the knee bend, elbows bent forward in idle. (An earlier fix widened the
    /// leg swing by 1.8x to cure a stride that looked short, which only made the extremes worse.) The Godot clips are already retargeted to THIS skeleton's joints,
    /// so playing them as they are is exact, not an approximation.
    ///
    /// HOW. A Godot track is the rotation of one of the body's fourteen bones, in its parent's frame, from a rest where every bone's rotation is the identity
    /// (the arms hang down, since the body was modelled that way). The Unity skeleton is a T-pose (see <see cref="BodyRig"/>), so the arm chain is turned by the
    /// shoulder's hang angle S: the shoulder's rotation is q*S and the forearm's and hand's are S^-1*q*S. Skinned against the arms-down bind the body already has
    /// (<see cref="BodyRig.RestBindMatrix"/>) that is exactly Godot's deformation. Every other bone has the same rest in both, and takes the Godot rotation as it is.
    /// Each frame is applied to a throwaway skeleton and read back as humanoid muscle values with a <see cref="HumanPoseHandler"/>, which is what a humanoid clip is.
    ///
    /// The Godot and Unity coordinate frames agree (the same joint numbers, the body facing +Z), and a quaternion's components mean the same thing in both, so no
    /// conversion is needed beyond that.
    /// </summary>
    public static class GodotMotionBaker
    {
        /// <summary>The Godot project's own animations, relative to the Unity project folder.</summary>
        private const string GodotDirectory = "../character/animations";

        public const string OutputDirectory = "Assets/CityX/Character/Animations/Godot";

        /// <summary>fused_body.gd BONES, which is the index every track's path uses.</summary>
        private static readonly string[] GodotBones =
        {
            "hips", "spine", "neck", "head",
            "arm_l", "forearm_l", "hand_l",
            "arm_r", "forearm_r", "hand_r",
            "thigh_l", "shin_l",
            "thigh_r", "shin_r",
        };

        /// <summary>The clips baked by default, and whether each one loops. The rest of the Godot set can be added here as the Unity states that use them are moved over.</summary>
        private static readonly (string Name, bool Loop)[] DefaultClips =
        {
            ("idle", true), ("walk", true), ("run", true),
            ("walk_backward", true), ("walk_left", true), ("walk_right", true),
        };

        private const float SampleRate = 30.0f;

        private sealed class Track
        {
            public string Bone;
            public bool IsPosition;
            public float[] Times;
            public Vector4[] Values;   // a quaternion's x,y,z,w, or a position's x,y,z,0
        }

        private sealed class Motion
        {
            public float Length;
            public readonly List<Track> Tracks = new List<Track>();
        }

        [MenuItem("CityX/Bake Godot Motion")]
        public static void BakeAll()
        {
            foreach ((string name, bool loop) in DefaultClips)
            {
                Bake(name, loop);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>The path a baked clip is saved at.</summary>
        public static string PathOf(string godotName) => $"{OutputDirectory}/godot_{godotName}.anim";

        /// <summary>A baked clip, or null if it has not been baked.</summary>
        public static AnimationClip Load(string godotName)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(PathOf(godotName));
        }

        /// <summary>Bakes one Godot animation by file name (without .tres). Returns the saved clip, or null if the file is missing.</summary>
        public static AnimationClip Bake(string godotName, bool loop)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string file = Path.GetFullPath(Path.Combine(projectRoot, GodotDirectory, godotName + ".tres"));
            if (!File.Exists(file))
            {
                Debug.LogWarning($"CityX: no Godot animation at {file}");
                return null;
            }

            Motion motion = Parse(File.ReadAllText(file));
            if (motion.Tracks.Count == 0)
            {
                Debug.LogWarning($"CityX: {godotName}.tres has no bone tracks");
                return null;
            }

            GameObject root = BodyRigBuilder.BuildSkeleton("GodotBake", out Dictionary<string, Transform> bones);
            try
            {
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/CityX/Character/CityXAvatar.asset");
                if (avatar == null || !avatar.isValid)
                {
                    Debug.LogError("CityX: the humanoid avatar is missing or invalid -- rebuild the character first.");
                    return null;
                }

                var rest = new Dictionary<string, (Vector3 position, Quaternion rotation)>();
                foreach (KeyValuePair<string, Transform> entry in bones)
                {
                    rest[entry.Key] = (entry.Value.localPosition, entry.Value.localRotation);
                }

                // Every key time of every track, so no track is sampled between its own keys when it did not have to be.
                var timeSet = new SortedSet<float>();
                foreach (Track track in motion.Tracks)
                {
                    foreach (float t in track.Times)
                    {
                        timeSet.Add((float)Math.Round(t, 4));
                    }
                }
                var times = new List<float>(timeSet);
                if (times.Count < 2)
                {
                    times.Add(motion.Length);
                }

                var handler = new HumanPoseHandler(avatar, root.transform);
                var pose = new HumanPose();
                int muscleCount = HumanTrait.MuscleCount;
                // Dense samples first (one per key time), then each channel is simplified into a few well-tangented keys: a 10 second idle at 30 fps is otherwise 6 MB.
                var muscleValues = new float[muscleCount][];
                for (int i = 0; i < muscleCount; i++)
                {
                    muscleValues[i] = new float[times.Count];
                }
                var rootTValues = new[] { new float[times.Count], new float[times.Count], new float[times.Count] };
                var rootQValues = new[] { new float[times.Count], new float[times.Count], new float[times.Count], new float[times.Count] };
                int frame = -1;

                float hangDegrees = BodyRig.ArmDownDegrees;
                Quaternion hangLeft = Quaternion.Euler(0.0f, 0.0f, hangDegrees);
                Quaternion hangRight = Quaternion.Euler(0.0f, 0.0f, -hangDegrees);

                foreach (float time in times)
                {
                    frame++;
                    foreach (KeyValuePair<string, (Vector3 position, Quaternion rotation)> entry in rest)
                    {
                        bones[entry.Key].localPosition = entry.Value.position;
                        bones[entry.Key].localRotation = entry.Value.rotation;
                    }

                    foreach (Track track in motion.Tracks)
                    {
                        Vector4 value = Evaluate(track, time);
                        if (track.IsPosition)
                        {
                            // Only the hips carry a position; it is the hips' own rest position replaced, in the body's own space.
                            if (track.Bone == "hips")
                            {
                                bones["hips"].position = new Vector3(value.x, value.y, value.z);
                            }
                            continue;
                        }

                        var q = new Quaternion(value.x, value.y, value.z, value.w);
                        q = Normalize(q);
                        bool left = track.Bone.EndsWith("_l", StringComparison.Ordinal);
                        bool armChain = track.Bone.StartsWith("arm_", StringComparison.Ordinal) ||
                                        track.Bone.StartsWith("forearm_", StringComparison.Ordinal) ||
                                        track.Bone.StartsWith("hand_", StringComparison.Ordinal);
                        if (armChain)
                        {
                            Quaternion hang = left ? hangLeft : hangRight;
                            q = track.Bone.StartsWith("arm_", StringComparison.Ordinal)
                                ? q * hang
                                : Quaternion.Inverse(hang) * q * hang;
                        }
                        bones[track.Bone].localRotation = q;
                    }

                    handler.GetHumanPose(ref pose);
                    for (int i = 0; i < muscleCount; i++)
                    {
                        muscleValues[i][frame] = pose.muscles[i];
                    }
                    rootTValues[0][frame] = pose.bodyPosition.x;
                    rootTValues[1][frame] = pose.bodyPosition.y;
                    rootTValues[2][frame] = pose.bodyPosition.z;
                    rootQValues[0][frame] = pose.bodyRotation.x;
                    rootQValues[1][frame] = pose.bodyRotation.y;
                    rootQValues[2][frame] = pose.bodyRotation.z;
                    rootQValues[3][frame] = pose.bodyRotation.w;
                }
                handler.Dispose();

                if (godotName == "run")
                {
                    var dump = new System.Text.StringBuilder("RUNMUSCLES\n");
                    foreach (string muscle in new[] { "Left Lower Leg Stretch", "Left Lower Leg Twist In-Out", "Left Upper Leg Front-Back", "Left Upper Leg In-Out", "Left Upper Leg Twist In-Out", "Left Foot Up-Down", "Left Foot Twist In-Out", "Left Forearm Stretch", "Left Forearm Twist In-Out" })
                    {
                        int index = System.Array.IndexOf(HumanTrait.MuscleName, muscle);
                        dump.Append(muscle.PadRight(30));
                        for (int f = 0; f < times.Count; f++) dump.Append($" {muscleValues[index][f]:F2}");
                        dump.Append('\n');
                    }
                    Debug.Log(dump.ToString());
                }

                // A muscle that jumps between two consecutive frames is not motion, it is the conversion flipping (an Euler-style decomposition crossing a branch): it shows
                // as a limb snapping through 70 degrees in one frame. Say so loudly rather than ship it.
                for (int i = 0; i < muscleCount; i++)
                {
                    float worstStep = 0.0f;
                    int worstAt = 0;
                    for (int f = 1; f < times.Count; f++)
                    {
                        float step = Mathf.Abs(muscleValues[i][f] - muscleValues[i][f - 1]);
                        if (step > worstStep)
                        {
                            worstStep = step;
                            worstAt = f;
                        }
                    }
                    if (worstStep > 0.45f)
                    {
                        Debug.LogWarning($"CityX: {godotName}: muscle '{HumanTrait.MuscleName[i]}' jumps {worstStep:F2} between frames {worstAt - 1} and {worstAt} ({muscleValues[i][worstAt - 1]:F2} -> {muscleValues[i][worstAt]:F2})");
                    }
                }

                var clip = new AnimationClip { name = "godot_" + godotName, frameRate = SampleRate };
                float[] timeArray = times.ToArray();
                for (int i = 0; i < muscleCount; i++)
                {
                    clip.SetCurve("", typeof(Animator), HumanTrait.MuscleName[i], Simplify(timeArray, muscleValues[i], MuscleTolerance, loop));
                }
                string[] axes = { "x", "y", "z", "w" };
                for (int i = 0; i < 3; i++)
                {
                    clip.SetCurve("", typeof(Animator), "RootT." + axes[i], Simplify(timeArray, rootTValues[i], PositionTolerance, loop));
                }
                for (int i = 0; i < 4; i++)
                {
                    clip.SetCurve("", typeof(Animator), "RootQ." + axes[i], Simplify(timeArray, rootQValues[i], RotationTolerance, loop));
                }
                clip.EnsureQuaternionContinuity();

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                settings.loopBlend = false;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                settings.startTime = 0.0f;
                settings.stopTime = times[times.Count - 1];
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                Directory.CreateDirectory(Path.Combine(projectRoot, OutputDirectory));
                string path = PathOf(godotName);
                // UPDATED IN PLACE, not deleted and recreated: a recreated asset gets a new GUID, and the controller built against the old one would silently lose its clip.
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing != null)
                {
                    existing.ClearCurves();
                    EditorUtility.CopySerialized(clip, existing);
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(clip);
                    clip = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(clip, path);
                }
                Debug.Log($"CityX: baked {godotName} -- {times.Count} frames, {motion.Length:F2} s, {(loop ? "looping" : "once")} -> {path}");
                return clip;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>How far a simplified muscle curve may stray from the dense samples. A muscle runs -1..1 over a joint's whole range, so this is well under a degree.</summary>
        private const float MuscleTolerance = 0.002f;

        private const float PositionTolerance = 0.0005f;

        private const float RotationTolerance = 0.0004f;

        /// <summary>
        /// A curve through the keys of <paramref name="values"/> that matter: dense samples are kept only where dropping them would move the curve more than
        /// <paramref name="tolerance"/> (Ramer-Douglas-Peucker), and every kept key takes the slope of the dense samples around it, so the curve between keys is smooth
        /// instead of easing to a stop at each one. A channel that never moves is two keys. A looping clip's ends take their slope across the wrap.
        /// </summary>
        private static AnimationCurve Simplify(float[] times, float[] values, float tolerance, bool loop)
        {
            int n = times.Length;
            var curve = new AnimationCurve();
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in values)
            {
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            if (n < 3 || max - min <= tolerance)
            {
                curve.AddKey(new Keyframe(times[0], values[0], 0.0f, 0.0f));
                curve.AddKey(new Keyframe(times[n - 1], values[0], 0.0f, 0.0f));
                return curve;
            }

            var keep = new bool[n];
            keep[0] = true;
            keep[n - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, n - 1));
            while (stack.Count > 0)
            {
                (int a, int b) = stack.Pop();
                float worst = 0.0f;
                int worstIndex = -1;
                for (int i = a + 1; i < b; i++)
                {
                    float u = (times[i] - times[a]) / (times[b] - times[a]);
                    float error = Mathf.Abs(values[i] - Mathf.Lerp(values[a], values[b], u));
                    if (error > worst)
                    {
                        worst = error;
                        worstIndex = i;
                    }
                }
                if (worstIndex >= 0 && worst > tolerance)
                {
                    keep[worstIndex] = true;
                    stack.Push((a, worstIndex));
                    stack.Push((worstIndex, b));
                }
            }

            // STRAIGHT SEGMENTS, EXACTLY: each kept key's tangents are the slopes of the segment on either side of it, so the curve is the piecewise-linear one the
            // simplification measured its error against (and the linear interpolation the Godot clips themselves use). Smooth tangents taken from the dense data were tried
            // first and overshot wildly across a long segment after a fast move, snapping a knee through 130 degrees in one frame.
            var kept = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (keep[i])
                {
                    kept.Add(i);
                }
            }
            for (int k = 0; k < kept.Count; k++)
            {
                int i = kept[k];
                float inSlope = k > 0 ? (values[i] - values[kept[k - 1]]) / (times[i] - times[kept[k - 1]]) : 0.0f;
                float outSlope = k < kept.Count - 1 ? (values[kept[k + 1]] - values[i]) / (times[kept[k + 1]] - times[i]) : 0.0f;
                if (k == 0) inSlope = outSlope;
                if (k == kept.Count - 1) outSlope = inSlope;
                curve.AddKey(new Keyframe(times[i], values[i], inSlope, outSlope));
            }
            return curve;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length < 1e-6f ? Quaternion.identity : new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
        }

        private static Vector4 Evaluate(Track track, float time)
        {
            float[] times = track.Times;
            if (time <= times[0])
            {
                return track.Values[0];
            }
            int last = times.Length - 1;
            if (time >= times[last])
            {
                return track.Values[last];
            }
            int hi = 1;
            while (hi < last && times[hi] < time)
            {
                hi++;
            }
            int lo = hi - 1;
            float u = Mathf.InverseLerp(times[lo], times[hi], time);
            Vector4 a = track.Values[lo], b = track.Values[hi];
            if (track.IsPosition)
            {
                return Vector4.Lerp(a, b, u);
            }
            Quaternion q = Quaternion.Slerp(new Quaternion(a.x, a.y, a.z, a.w), new Quaternion(b.x, b.y, b.z, b.w), u);
            return new Vector4(q.x, q.y, q.z, q.w);
        }

        private static readonly Regex PathPattern = new Regex(@"^tracks/(\d+)/path = NodePath\(""BodySkeleton:bones/(\d+)/(rotation|position)""\)", RegexOptions.Compiled);
        private static readonly Regex TimesPattern = new Regex(@"""times"": PackedFloat32Array\(([^)]*)\)", RegexOptions.Compiled);
        private static readonly Regex ValuesPattern = new Regex(@"""values"": \[(.*)\]\s*$", RegexOptions.Compiled);
        private static readonly Regex VectorPattern = new Regex(@"(?:Quaternion|Vector3)\(([^)]*)\)", RegexOptions.Compiled);
        private static readonly Regex LengthPattern = new Regex(@"^length = ([0-9eE+\-.]+)", RegexOptions.Compiled);

        private static float Number(string text) => float.Parse(text.Trim(), CultureInfo.InvariantCulture);

        /// <summary>Reads the bone tracks out of a Godot .tres animation. Anything that is not a bone's rotation or position (there is nothing else in these files) is skipped.</summary>
        private static Motion Parse(string text)
        {
            var motion = new Motion();
            Track current = null;
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                Match length = LengthPattern.Match(line);
                if (length.Success)
                {
                    motion.Length = Number(length.Groups[1].Value);
                    continue;
                }

                Match path = PathPattern.Match(line);
                if (path.Success)
                {
                    int bone = int.Parse(path.Groups[2].Value, CultureInfo.InvariantCulture);
                    current = bone >= 0 && bone < GodotBones.Length
                        ? new Track { Bone = GodotBones[bone], IsPosition = path.Groups[3].Value == "position" }
                        : null;
                    if (current != null)
                    {
                        motion.Tracks.Add(current);
                    }
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                Match times = TimesPattern.Match(line);
                if (times.Success)
                {
                    string[] parts = times.Groups[1].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    current.Times = new float[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        current.Times[i] = Number(parts[i]);
                    }
                    continue;
                }

                Match values = ValuesPattern.Match(line);
                if (values.Success)
                {
                    MatchCollection vectors = VectorPattern.Matches(values.Groups[1].Value);
                    current.Values = new Vector4[vectors.Count];
                    for (int i = 0; i < vectors.Count; i++)
                    {
                        string[] c = vectors[i].Groups[1].Value.Split(',');
                        current.Values[i] = new Vector4(Number(c[0]), Number(c[1]), Number(c[2]), c.Length > 3 ? Number(c[3]) : 0.0f);
                    }
                }
            }

            motion.Tracks.RemoveAll(t => t.Times == null || t.Values == null || t.Times.Length == 0 || t.Times.Length != t.Values.Length);
            return motion;
        }
    }
}
