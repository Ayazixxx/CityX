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
    ///
    /// PROPS AND EVENTS (2026-10-14, the gun reloads): a motion may also carry named PROPS -- the position, rotation and visibility of a
    /// held thing's parts (a slide, a magazine) in the held thing's own frame, every frame -- and named EVENTS on frames (the magazine
    /// dropping, seating; the slide racked) that the game acts on. The finger bones ride along like any other bone (HasFingers), and a
    /// motion played UPPER-BODY-ONLY (BakedPose.UpperBodyOnly) leaves the hips and legs to the Animator and the foot IK.
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
            public EventData[] events;
            public float[] hips;
            public float[] rotations;
            public PropData[] props;
        }

        [Serializable]
        private sealed class SegmentData
        {
            public string name;
            public int start;
            public int end;
        }

        [Serializable]
        private sealed class EventData
        {
            public string name;
            public int frame;
        }

        [Serializable]
        private sealed class PropData
        {
            public string name;
            public float[] positions;
            public float[] rotations;
            public int[] visible;
        }

        /// <summary>The skeleton's root bone: the only one that moves as well as turns, and the one a whole-pose turn is applied to.</summary>
        public const string RootBone = "hips";

        private readonly string[] _bones;
        private readonly Quaternion[] _rotations;
        private readonly Vector3[] _hips;
        private readonly int[] _cutFrames;
        private readonly Dictionary<string, Vector2Int> _segments = new Dictionary<string, Vector2Int>();
        private readonly string[] _props;
        private readonly Vector3[][] _propPositions;
        private readonly Quaternion[][] _propRotations;
        private readonly bool[][] _propVisible;
        private readonly List<KeyValuePair<string, int>> _events = new List<KeyValuePair<string, int>>();
        private readonly bool[] _lowerBody;

        public int FramesPerSecond { get; }

        public int FrameCount { get; }

        /// <summary>The bones, the root first, in the order every frame lists them.</summary>
        public IReadOnlyList<string> Bones => _bones;

        /// <summary>The frames the move's hits land on, as the animator marked them in Blender.</summary>
        public IReadOnlyList<int> CutFrames => _cutFrames;

        /// <summary>The held thing's parts this motion moves (none for a body-only move), in the order a pose lists them.</summary>
        public IReadOnlyList<string> Props => _props;

        /// <summary>True when the motion poses the finger bones too (the gun reloads do; Octaslash predates them).</summary>
        public bool HasFingers { get; }

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
            foreach (EventData e in data.events ?? new EventData[0])
            {
                _events.Add(new KeyValuePair<string, int>(e.name, e.frame));
            }
            PropData[] props = data.props ?? new PropData[0];
            _props = new string[props.Length];
            _propPositions = new Vector3[props.Length][];
            _propRotations = new Quaternion[props.Length][];
            _propVisible = new bool[props.Length][];
            for (int p = 0; p < props.Length; p++)
            {
                _props[p] = props[p].name;
                _propPositions[p] = new Vector3[FrameCount];
                _propRotations[p] = new Quaternion[FrameCount];
                _propVisible[p] = new bool[FrameCount];
                for (int f = 0; f < FrameCount; f++)
                {
                    _propPositions[p][f] = new Vector3(props[p].positions[f * 3], props[p].positions[f * 3 + 1], props[p].positions[f * 3 + 2]);
                    _propRotations[p][f] = Normalized(new Quaternion(props[p].rotations[f * 4], props[p].rotations[f * 4 + 1], props[p].rotations[f * 4 + 2], props[p].rotations[f * 4 + 3]));
                    _propVisible[p][f] = props[p].visible == null || props[p].visible.Length <= f || props[p].visible[f] != 0;
                }
            }
            _lowerBody = new bool[count];
            bool fingers = false;
            for (int i = 0; i < count; i++)
            {
                _lowerBody[i] = IsLowerBody(_bones[i]);
                fingers |= IsFingerBone(_bones[i]);
            }
            HasFingers = fingers;
        }

        /// <summary>The hips and the legs: what an upper-body-only play leaves to the Animator.</summary>
        public static bool IsLowerBody(string bone)
        {
            return bone == RootBone || bone.StartsWith("thigh_") || bone.StartsWith("shin_") || bone.StartsWith("foot_");
        }

        /// <summary>A finger bone (BodyRig.AddFingers: index1_r, thumb2_l, ...).</summary>
        public static bool IsFingerBone(string bone)
        {
            foreach (FusedBodyShape.Digit digit in FusedBodyShape.Digits)
            {
                if (bone.StartsWith(digit.Name) && bone.Length == digit.Name.Length + 3)
                {
                    return true;
                }
            }
            return false;
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
            foreach (PropData prop in data.props ?? new PropData[0])
            {
                if (string.IsNullOrEmpty(prop.name) || prop.positions == null || prop.positions.Length != data.frames * 3
                    || prop.rotations == null || prop.rotations.Length != data.frames * 4)
                {
                    throw new ArgumentException($"prop '{prop.name}' does not have every frame");
                }
            }
            foreach (EventData e in data.events ?? new EventData[0])
            {
                if (string.IsNullOrEmpty(e.name) || e.frame < 0 || e.frame >= data.frames)
                {
                    throw new ArgumentException($"event '{e.name}' is outside the motion");
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

        /// <summary>
        /// When the named event falls inside the segment, seconds from the segment's start to it; -1 when the segment has no such event.
        /// (Both of a gun's reloads have a "seat"; each is found within its own segment.)
        /// </summary>
        public float EventSeconds(string segment, string eventName)
        {
            if (!HasSegment(segment))
            {
                return -1.0f;
            }
            Vector2Int range = _segments[segment];
            foreach (KeyValuePair<string, int> e in _events)
            {
                if (e.Key == eventName && e.Value >= range.x && e.Value <= range.y)
                {
                    return (e.Value - range.x) / (float)FramesPerSecond;
                }
            }
            return -1.0f;
        }

        /// <summary>Every event inside the segment, as (name, seconds from its start), in frame order.</summary>
        public List<KeyValuePair<string, float>> EventsIn(string segment)
        {
            var found = new List<KeyValuePair<string, float>>();
            if (!HasSegment(segment))
            {
                return found;
            }
            Vector2Int range = _segments[segment];
            foreach (KeyValuePair<string, int> e in _events)
            {
                if (e.Value >= range.x && e.Value <= range.y)
                {
                    found.Add(new KeyValuePair<string, float>(e.Key, (e.Value - range.x) / (float)FramesPerSecond));
                }
            }
            found.Sort((a, b) => a.Value.CompareTo(b.Value));
            return found;
        }

        public Vector3 HipsAt(int frame) => _hips[Mathf.Clamp(frame, 0, FrameCount - 1)];

        /// <summary>Bone <paramref name="bone"/>'s local rotation on frame <paramref name="frame"/>.</summary>
        public Quaternion RotationAt(int frame, int bone) => _rotations[Mathf.Clamp(frame, 0, FrameCount - 1) * _bones.Length + bone];

        /// <summary>Prop <paramref name="prop"/>'s local position (in the held thing's frame) on frame <paramref name="frame"/>.</summary>
        public Vector3 PropPositionAt(int frame, int prop) => _propPositions[prop][Mathf.Clamp(frame, 0, FrameCount - 1)];

        public Quaternion PropRotationAt(int frame, int prop) => _propRotations[prop][Mathf.Clamp(frame, 0, FrameCount - 1)];

        public bool PropVisibleAt(int frame, int prop) => _propVisible[prop][Mathf.Clamp(frame, 0, FrameCount - 1)];

        /// <summary>True for bones an upper-body-only play skips (the index is into <see cref="Bones"/>).</summary>
        public bool IsLowerBodyBone(int bone) => _lowerBody[bone];

        /// <summary>A pose sized for this motion's bones and props.</summary>
        public BakedPose NewPose() => new BakedPose(_bones, _props);

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
            for (int p = 0; p < _props.Length && p < pose.PropPositions.Length; p++)
            {
                pose.PropPositions[p] = Vector3.Lerp(_propPositions[p][a], _propPositions[p][b], t);
                pose.PropRotations[p] = Quaternion.Slerp(_propRotations[p][a], _propRotations[p][b], t);
                pose.PropVisible[p] = _propVisible[p][a];
            }
        }

        private static Quaternion Normalized(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 1e-6f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }
    }

    /// <summary>
    /// One frame of a <see cref="BakedMotion"/>, ready to be put on a body (PlayerLocomotion.SetBakedPose): each bone's local rotation, where the
    /// hips are, optionally a turn of the whole pose about a pivot (Octaslash pitching its level dash up or down the way it really goes), and
    /// the held thing's parts.
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

        /// <summary>True to leave the hips and legs alone (a reload while walking): only the spine up, the arms and the fingers are posed.</summary>
        public bool UpperBodyOnly;

        /// <summary>
        /// The stab dash and the sword spin: while the move holds the body, the sword in the right hand is held in the thrust grip the move was posed with in Blender, so the
        /// blade points where the move means it, gripped in the fist, whatever hold the sword is otherwise in (ISwordBlade.HoldForThrust).
        /// </summary>
        public bool ThrustGrip;

        /// <summary>
        /// The Octaslash: posed in Blender against the two-handed hold the mocap gave (the handle along the fingers), before the hold became a
        /// hammer grip (2026-10-28), so while it holds the body the sword is held that old way and its cuts point where the move means them
        /// (ISwordBlade.HoldForMocap).
        /// </summary>
        public bool MocapGrip;

        /// <summary>The held thing's parts (BakedMotion.Props), each in the held thing's own frame.</summary>
        public readonly IReadOnlyList<string> Props;

        public readonly Vector3[] PropPositions;

        public readonly Quaternion[] PropRotations;

        public readonly bool[] PropVisible;

        public BakedPose(IReadOnlyList<string> bones) : this(bones, new string[0])
        {
        }

        public BakedPose(IReadOnlyList<string> bones, IReadOnlyList<string> props)
        {
            Bones = bones;
            Rotations = new Quaternion[bones.Count];
            for (int i = 0; i < Rotations.Length; i++)
            {
                Rotations[i] = Quaternion.identity;
            }
            Props = props ?? new string[0];
            PropPositions = new Vector3[Props.Count];
            PropRotations = new Quaternion[Props.Count];
            PropVisible = new bool[Props.Count];
            for (int i = 0; i < Props.Count; i++)
            {
                PropRotations[i] = Quaternion.identity;
                PropVisible[i] = true;
            }
        }
    }
}
