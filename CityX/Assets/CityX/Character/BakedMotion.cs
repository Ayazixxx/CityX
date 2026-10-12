using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A BODY ANIMATION MADE IN BLENDER, played straight onto the live skeleton. For every frame it holds each of BodyRig's bones' local
    /// rotation and where the hips are, at a fixed rate. The frames are cut into named segments, such as a wind-up, a hold that loops and a dash.
    ///
    /// The export script next to the .blend (CityX/SourceArt/&lt;move&gt;/) writes it as JSON in Resources. The rotations are already in the live
    /// skeleton's terms (a T-pose whose every bone's rest rotation is the identity, BodyRigBuilder.BuildSkeleton), so playing a frame is
    /// setting each bone's localRotation.
    ///
    /// WHY NOT A HUMANOID CLIP. A humanoid clip stores muscle values, clamped to the avatar's ranges. The moves this is for go past them on
    /// purpose: Octaslash's body rolls four times in a quarter of a second, the sword arm whipping round with it. Played after the Animator
    /// (PlayerLocomotion.SetBakedPose), the bones land exactly where Blender put them.
    /// </summary>
    public sealed class BakedMotion
    {
        [Serializable]
        private sealed class Data
        {
            public int fps;
            public int frames;
            public string[] bones;
            public SegmentData[] segments;
            public int[] cutFrames;
            public float[] hips;
            public float[] rotations;
        }

        [Serializable]
        private sealed class SegmentData
        {
            public string name;
            public int start;
            public int end;
        }

        /// <summary>The skeleton's root bone: the only one that moves as well as turns, and the one a whole-pose turn is applied to.</summary>
        public const string RootBone = "hips";

        private readonly string[] _bones;
        private readonly Quaternion[] _rotations;
        private readonly Vector3[] _hips;
        private readonly int[] _cutFrames;
        private readonly Dictionary<string, Vector2Int> _segments = new Dictionary<string, Vector2Int>();

        public int FramesPerSecond { get; }

        public int FrameCount { get; }

        /// <summary>The bones, the root first, in the order every frame lists them.</summary>
        public IReadOnlyList<string> Bones => _bones;

        /// <summary>The frames the move's hits land on, as the animator marked them in Blender.</summary>
        public IReadOnlyList<int> CutFrames => _cutFrames;

        private BakedMotion(Data data)
        {
            FramesPerSecond = data.fps;
            FrameCount = data.frames;
            _bones = data.bones;
            _cutFrames = data.cutFrames ?? new int[0];
            int count = _bones.Length;
            _rotations = new Quaternion[FrameCount * count];
            for (int i = 0; i < _rotations.Length; i++)
            {
                var q = new Quaternion(data.rotations[i * 4], data.rotations[i * 4 + 1], data.rotations[i * 4 + 2], data.rotations[i * 4 + 3]);
                _rotations[i] = Normalized(q);
            }
            _hips = new Vector3[FrameCount];
            for (int f = 0; f < FrameCount; f++)
            {
                _hips[f] = new Vector3(data.hips[f * 3], data.hips[f * 3 + 1], data.hips[f * 3 + 2]);
            }
            foreach (SegmentData segment in data.segments ?? new SegmentData[0])
            {
                _segments[segment.name] = new Vector2Int(segment.start, segment.end);
            }
        }

        /// <summary>Reads an exported motion. Throws ArgumentException if the file does not hold a whole one.</summary>
        public static BakedMotion FromJson(string json)
        {
            Data data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Data>(json);
            if (data == null || data.fps <= 0 || data.frames <= 0 || data.bones == null || data.bones.Length == 0)
            {
                throw new ArgumentException("not a baked motion");
            }
            if (data.bones[0] != RootBone)
            {
                throw new ArgumentException($"a baked motion's first bone must be the root, '{RootBone}', not '{data.bones[0]}'");
            }
            if (data.rotations == null || data.rotations.Length != data.frames * data.bones.Length * 4 || data.hips == null || data.hips.Length != data.frames * 3)
            {
                throw new ArgumentException("a baked motion's frames are not all there");
            }
            foreach (SegmentData segment in data.segments ?? new SegmentData[0])
            {
                if (segment.start < 0 || segment.end >= data.frames || segment.end < segment.start)
                {
                    throw new ArgumentException($"segment '{segment.name}' runs outside the motion");
                }
            }
            return new BakedMotion(data);
        }

        private static readonly Dictionary<string, BakedMotion> Loaded = new Dictionary<string, BakedMotion>();

        /// <summary>The motion in Resources at <paramref name="path"/>, read once and shared by every body; null if there is none.</summary>
        public static BakedMotion Load(string path)
        {
            if (Loaded.TryGetValue(path, out BakedMotion motion))
            {
                return motion;
            }
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"CityX: no baked motion at Resources/{path}");
            }
            motion = asset != null ? FromJson(asset.text) : null;
            Loaded[path] = motion;
            return motion;
        }

        public bool HasSegment(string name) => name != null && _segments.ContainsKey(name);

        /// <summary>The segment's first frame.</summary>
        public int SegmentStart(string name) => _segments[name].x;

        /// <summary>How many frames the segment runs. Its last frame is the next one's first, so one played after another joins up.</summary>
        public int SegmentFrames(string name) => _segments[name].y - _segments[name].x;

        public float SegmentSeconds(string name) => SegmentFrames(name) / (float)FramesPerSecond;

        public Vector3 HipsAt(int frame) => _hips[Mathf.Clamp(frame, 0, FrameCount - 1)];

        /// <summary>Bone <paramref name="bone"/>'s local rotation on frame <paramref name="frame"/>.</summary>
        public Quaternion RotationAt(int frame, int bone) => _rotations[Mathf.Clamp(frame, 0, FrameCount - 1) * _bones.Length + bone];

        /// <summary>
        /// Fills <paramref name="pose"/> with segment <paramref name="name"/>, <paramref name="seconds"/> into it. A looping segment wraps from its
        /// last frame back to its first (never on into the next segment); any other is held at its ends.
        /// </summary>
        public void Sample(string name, float seconds, bool loop, BakedPose pose)
        {
            Vector2Int range = _segments[name];
            int length = range.y - range.x;
            float at = seconds * FramesPerSecond;
            if (loop && length > 0)
            {
                at = Mathf.Repeat(at, length);
                int a = Mathf.FloorToInt(at);
                int b = (a + 1) % length;
                Blend(range.x + a, range.x + b, at - a, pose);
                return;
            }
            at = Mathf.Clamp(at, 0.0f, length);
            int from = range.x + Mathf.FloorToInt(at);
            Blend(from, Mathf.Min(from + 1, range.y), at - Mathf.Floor(at), pose);
        }

        private void Blend(int a, int b, float t, BakedPose pose)
        {
            int count = _bones.Length;
            for (int i = 0; i < count; i++)
            {
                pose.Rotations[i] = Quaternion.Slerp(_rotations[a * count + i], _rotations[b * count + i], t);
            }
            pose.Hips = Vector3.Lerp(_hips[a], _hips[b], t);
        }

        private static Quaternion Normalized(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 1e-6f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }
    }

    /// <summary>
    /// One frame of a <see cref="BakedMotion"/>, ready to be put on a body (PlayerLocomotion.SetBakedPose): each bone's local rotation, where the
    /// hips are, and optionally a turn of the whole pose about a pivot (Octaslash pitching its level dash up or down the way it really goes).
    /// </summary>
    public sealed class BakedPose
    {
        public readonly IReadOnlyList<string> Bones;

        public readonly Quaternion[] Rotations;

        /// <summary>The hips' local position, in the body's own frame.</summary>
        public Vector3 Hips;

        /// <summary>Turns the whole pose about <see cref="Pivot"/>, in the body's own frame. The identity leaves it as authored.</summary>
        public Quaternion Turn = Quaternion.identity;

        public Vector3 Pivot;

        public BakedPose(IReadOnlyList<string> bones)
        {
            Bones = bones;
            Rotations = new Quaternion[bones.Count];
            for (int i = 0; i < Rotations.Length; i++)
            {
                Rotations[i] = Quaternion.identity;
            }
        }
    }
}
