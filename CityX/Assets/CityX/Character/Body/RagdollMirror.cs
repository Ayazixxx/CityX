using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ANOTHER PLAYER'S LIMP BODY, POSED AS THEIRS IS (docs/MULTIPLAYER_STATE.md; owner, 2026-10-29: "make it so whatever ragdoll position you
    /// are in is the same on all multiplayer screens ... i want all the ragdolls to be synced up"). Whatever threw the body limp -- a water jet,
    /// a jetpack crash, a finisher, the grapple rope, death -- only its owner's machine runs the physics; the owner sends the pose
    /// (<see cref="Capture"/>, NetworkedRagdollSync) and every copy plays it back here: no physics of its own, its bones set to the owner's pose
    /// a little in the past (<see cref="DelaySeconds"/>), between the two poses either side of that moment, so it moves smoothly however the
    /// packets arrive. The get-up is sent the same way, so it stands up as its owner does, and only then is the body handed back to its
    /// Animator. A copy that had already fallen on its own (an attacker's machine throws the copy it hit at once, so the blow lands without
    /// waiting on the network) is blended into the owner's pose rather than snapped.
    ///
    /// Runs after everything else that poses bones (<see cref="DefaultExecutionOrder"/>): nothing animates over the owner's pose.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RagdollMirror : MonoBehaviour
    {
        /// <summary>How far behind the newest pose the copy plays: long enough for the next one (sent every 1/20 s) to have arrived.</summary>
        public const float DelaySeconds = 0.1f;

        /// <summary>Further behind than this, the copy jumps ahead to <see cref="DelaySeconds"/> behind the newest (a stall, a hitch).</summary>
        public const float MaxBehindSeconds = 0.5f;

        /// <summary>How hard the playback is pulled back to <see cref="DelaySeconds"/> behind the newest pose: each second behind it runs this much faster.</summary>
        public const float CatchUpPerSecond = 4.0f;

        /// <summary>A copy that was in some other pose is blended into the owner's over this long.</summary>
        public const float BlendInSeconds = 0.15f;

        public const int BufferSize = 24;

        private readonly List<RagdollPose> _buffer = new List<RagdollPose>(BufferSize);
        private readonly Stack<RagdollPose> _spare = new Stack<RagdollPose>();
        private readonly RagdollPose _shown = new RagdollPose();
        private readonly RagdollPose _from = new RagdollPose();
        private Ragdoll _ragdoll;
        private Animator _animator;
        private PlayerLocomotion _locomotion;
        private CharacterController _controller;
        private Transform _hips;
        private Transform[] _bones = new Transform[0];
        private Vector3[] _restPlaces = new Vector3[0];
        private bool _mirroring;
        private bool _ending;
        private float _endTime = float.NegativeInfinity;
        private float _playback;
        private float _blend;

        /// <summary>Posing this body as its owner's limp body is.</summary>
        public bool IsMirroring => _mirroring;

        /// <summary>How many bones a pose of this body carries (the hips apart).</summary>
        public int BoneCount => _bones.Length;

        /// <summary>The owner's moment the copy is showing (on the owner's clock), and how many poses it has taken in (tests read them).</summary>
        public float PlaybackTime => _playback;

        public int Received { get; private set; }

        private void Awake()
        {
            Bind();
        }

        /// <summary>Finds the body's parts and its bones. Public and idempotent, for tests where Awake never runs.</summary>
        public void Bind()
        {
            _ragdoll = GetComponent<Ragdoll>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _controller = GetComponent<CharacterController>();
            _animator = _ragdoll != null && _ragdoll.Animator != null ? _ragdoll.Animator : GetComponentInChildren<Animator>();
            _hips = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _bones = BonesOf(_animator);
            // Where each bone sits on its parent at rest: the owner's pose moves them (a ragdoll's joints give under load), and they go back
            // when the copy is its Animator's again.
            _restPlaces = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                _restPlaces[i] = _bones[i].localPosition;
            }
        }

        /// <summary>
        /// The bones a pose carries, in a fixed order every machine agrees on: every humanoid bone the body has but the hips (sent apart), the
        /// fingers (held by HandGrip, not the ragdoll), the eyes and the jaw.
        /// </summary>
        public static Transform[] BonesOf(Animator animator)
        {
            var bones = new List<Transform>();
            if (animator == null || !animator.isHuman)
            {
                return bones.ToArray();
            }
            for (var bone = HumanBodyBones.LeftUpperLeg; bone < HumanBodyBones.LastBone; bone++)
            {
                bool finger = bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal;
                if (finger || bone == HumanBodyBones.LeftEye || bone == HumanBodyBones.RightEye || bone == HumanBodyBones.Jaw)
                {
                    continue;
                }
                Transform transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    bones.Add(transform);
                }
                if (bones.Count == RagdollPoseCodec.MaxBones)
                {
                    break;
                }
            }
            return bones.ToArray();
        }

        /// <summary>This body's pose now, stamped <paramref name="time"/> (the owner's side).</summary>
        public void Capture(RagdollPose into, float time)
        {
            into.Time = time;
            into.Count = _bones.Length;
            if (_hips != null)
            {
                into.HipsPosition = _hips.position;
                into.HipsRotation = _hips.rotation;
            }
            for (int i = 0; i < _bones.Length; i++)
            {
                into.Local[i] = _bones[i].localRotation;
                into.LocalPositions[i] = _bones[i].localPosition;
            }
        }

        /// <summary>A pose of the owner's arrived (the copy's side). One from before the owner last got up is old news.</summary>
        public void Receive(RagdollPose pose)
        {
            if (pose.Count != _bones.Length || _hips == null || pose.Time <= _endTime)
            {
                return;
            }
            RagdollPose kept = _spare.Count > 0 ? _spare.Pop() : new RagdollPose();
            kept.CopyFrom(pose);
            int at = _buffer.Count;
            while (at > 0 && _buffer[at - 1].Time > kept.Time)
            {
                at--;
            }
            if (at > 0 && Mathf.Approximately(_buffer[at - 1].Time, kept.Time))
            {
                _spare.Push(kept);   // the same pose twice
                return;
            }
            _buffer.Insert(at, kept);
            Received++;
            while (_buffer.Count > BufferSize)
            {
                _spare.Push(_buffer[0]);
                _buffer.RemoveAt(0);
            }
            if (!_mirroring)
            {
                Begin(kept.Time);
            }
        }

        /// <summary>The owner's body is up (or revived): the poses already here play out, then the body is its Animator's again.</summary>
        public void End(float lastTime)
        {
            _endTime = Mathf.Max(_endTime, lastTime);
            _ending = _mirroring;
        }

        private void Begin(float firstTime)
        {
            _mirroring = true;
            _ending = false;
            _playback = firstTime - DelaySeconds;
            // The body's own fall (if it had one) stops: it is kinematic again and the owner's pose is laid over it, blended in from where it is.
            if (_ragdoll != null)
            {
                _ragdoll.IsMirrored = true;
                _ragdoll.Deactivate();
            }
            Capture(_from, firstTime);
            _blend = 0.0f;
            SetBodyDrivenHere(false);
        }

        private void Finish()
        {
            _mirroring = false;
            _ending = false;
            while (_buffer.Count > 0)
            {
                _spare.Push(_buffer[_buffer.Count - 1]);
                _buffer.RemoveAt(_buffer.Count - 1);
            }
            if (_ragdoll != null)
            {
                _ragdoll.IsMirrored = false;
            }
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localPosition = _restPlaces[i];
            }
            SetBodyDrivenHere(true);
        }

        /// <summary>The Animator, the locomotion and the standing capsule: off while the owner's pose is laid on the bones, back on after.</summary>
        private void SetBodyDrivenHere(bool on)
        {
            if (_animator != null)
            {
                _animator.enabled = on;
            }
            if (_locomotion != null)
            {
                _locomotion.enabled = on;
            }
            if (_controller != null)
            {
                _controller.enabled = on;
            }
        }

        private void LateUpdate()
        {
            if (!_mirroring || _buffer.Count == 0)
            {
                return;
            }
            // The playback keeps DelaySeconds behind the newest pose: a little faster when it has fallen behind (the first pose came late, a
            // stall), a little slower when it has run ahead of what has arrived, never backwards, and never past the newest pose: with no
            // newer one yet (a body lying still is sent twice a second, a pose lost) it waits there, so the next one is eased into rather than
            // jumped to, and PlaybackTime says which moment is shown.
            float newest = _buffer[_buffer.Count - 1].Time;
            float behind = newest - DelaySeconds - _playback;
            _playback += Mathf.Max(0.0f, Time.deltaTime * (1.0f + Mathf.Clamp(behind * CatchUpPerSecond, -0.5f, 1.0f)));
            _playback = Mathf.Min(_playback, newest);
            if (newest - _playback > MaxBehindSeconds)
            {
                _playback = newest - DelaySeconds;
            }
            Sample(_playback, _shown);
            if (_blend < 1.0f)
            {
                _blend = Mathf.Min(1.0f, _blend + Time.deltaTime / BlendInSeconds);
                RagdollPoseCodec.Lerp(_from, _shown, _blend * _blend * (3.0f - 2.0f * _blend), _shown);
            }
            Apply(_shown);
            if (_ending && _playback >= newest)
            {
                Finish();
            }
        }

        /// <summary>The pose at <paramref name="time"/>: between the two either side of it, or the nearest at either end of what has arrived.</summary>
        private void Sample(float time, RagdollPose into)
        {
            while (_buffer.Count > 2 && _buffer[1].Time <= time)
            {
                _spare.Push(_buffer[0]);
                _buffer.RemoveAt(0);
            }
            RagdollPose a = _buffer[0];
            if (_buffer.Count == 1 || time <= a.Time)
            {
                into.CopyFrom(a);
                return;
            }
            RagdollPose b = _buffer[1];
            float span = b.Time - a.Time;
            RagdollPoseCodec.Lerp(a, b, span > 1e-5f ? (time - a.Time) / span : 1.0f, into);
        }

        private void Apply(RagdollPose pose)
        {
            if (_hips == null)
            {
                return;
            }
            _hips.SetPositionAndRotation(pose.HipsPosition, pose.HipsRotation);
            int count = Mathf.Min(pose.Count, _bones.Length);
            for (int i = 0; i < count; i++)
            {
                _bones[i].SetLocalPositionAndRotation(pose.LocalPositions[i], pose.Local[i]);
            }
        }
    }
}
