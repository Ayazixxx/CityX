using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>Where a body stood at a moment of its owner's clock.</summary>
    public struct BodyMoment
    {
        public double Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        /// <summary>Stood exactly as <paramref name="other"/> did: a body at rest is told again with the same numbers.</summary>
        public bool StoodAs(in BodyMoment other) => Position == other.Position && Rotation == other.Rotation && Scale == other.Scale;
    }

    /// <summary>
    /// Plays another player's body back from the moments its owner sent (docs/MULTIPLAYER_MOVEMENT.md). Each moment carries the time, on the
    /// owner's clock, at which the body stood there, so it does not matter when a packet was sent or arrived, only what it says: the copy is
    /// shown <see cref="DelaySeconds"/> behind the newest moment, between the two moments either side of that time. A late or lost packet
    /// leaves a longer stretch to cross at the same pace instead of a stall and a lurch. The playback runs a little faster when it has fallen
    /// behind and slower as it runs out of moments, and never past the newest one: with nothing newer it comes to rest there.
    /// </summary>
    public sealed class BodyMotionPlayback
    {
        /// <summary>How far behind the newest moment the copy is shown, seconds: room for the next packets to come late or not at all.</summary>
        public const float DelaySeconds = 0.1f;

        /// <summary>How hard the playback is pulled back to <see cref="DelaySeconds"/> behind the newest: each second off, it runs this much faster.</summary>
        public const float CatchUpPerSecond = 2.0f;

        /// <summary>With less than this left to show, seconds, the playback slows in proportion.</summary>
        public const float BrakeSeconds = 0.04f;

        /// <summary>Further behind than this, seconds, the playback jumps ahead (this machine stalled, or the line did).</summary>
        public const float JumpAheadSeconds = 0.5f;

        /// <summary>Two moments further apart than a body at this speed could go, m/s, are a teleport: the copy is put there, not slid there.</summary>
        public const float TeleportSpeed = 150.0f;

        /// <summary>Moments kept at most: a second and a half at sixty a second, far more than the playback ever has ahead of it.</summary>
        public const int Capacity = 96;

        private readonly List<BodyMoment> _moments = new List<BodyMoment>();
        private double _playback;

        public bool HasAny => _moments.Count > 0;

        /// <summary>The owner's time the copy is shown at.</summary>
        public double ShownTime => _playback;

        public void Clear()
        {
            _moments.Clear();
            _playback = 0.0;
        }

        /// <summary>A moment arrived, in any order; one already here, or older than everything kept while the buffer is full, is dropped.</summary>
        public void Add(in BodyMoment moment)
        {
            int at = _moments.Count;
            while (at > 0 && _moments[at - 1].Time > moment.Time)
            {
                at--;
            }
            if (at > 0 && _moments[at - 1].Time == moment.Time)
            {
                return;
            }
            if (_moments.Count == 0)
            {
                _playback = moment.Time - DelaySeconds;
            }
            else if (at == 0 && (_moments.Count >= Capacity || moment.Time < _playback))
            {
                return;   // from before what is being shown: it would change nothing
            }
            _moments.Insert(at, moment);
            if (_moments.Count > Capacity)
            {
                _moments.RemoveAt(0);
            }
            double target = moment.Time - DelaySeconds;
            if (at == _moments.Count - 1 && at > 0 && _playback >= _moments[0].Time && _playback > target && StandsThroughout())
            {
                // A body standing still is told rarely, so the playback has all but caught up with the newest moment and has little ahead of
                // it. While the copy shows it standing there, and everything it has been told since is the same place, it has stood there for
                // as long as we care to say: the playback is taken back unseen, so the first step, when it comes, is shown with the whole
                // delay ahead of it again instead of stuttering while that builds back up.
                BodyMoment shown = _moments[0];
                shown.Time = Math.Min(shown.Time, target);
                _moments.Clear();
                _moments.Add(shown);
                _moments.Add(moment);
                _playback = target;
            }
        }

        /// <summary>Whether every moment kept is the same place: the body stands.</summary>
        private bool StandsThroughout()
        {
            for (int i = 1; i < _moments.Count; i++)
            {
                if (!_moments[i].StoodAs(_moments[0]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Moves the playback on by <paramref name="deltaTime"/> and says where the body is shown now.</summary>
        public BodyMoment Advance(float deltaTime)
        {
            if (_moments.Count == 0)
            {
                return default;
            }
            double newest = _moments[_moments.Count - 1].Time;
            double behind = newest - DelaySeconds - _playback;
            if (behind > JumpAheadSeconds)
            {
                _playback = newest - DelaySeconds;
            }
            else
            {
                // Nearly out of moments it slows, instead of running into the newest one and stopping dead: a late packet then costs a
                // slower step, not a stall and a lurch.
                double rate = Math.Min(1.0 + Mathf.Clamp((float)behind * CatchUpPerSecond, -0.5f, 1.0f), (newest - _playback) / BrakeSeconds);
                _playback += deltaTime * Math.Max(rate, 0.0);
            }
            _playback = Math.Min(_playback, newest);
            while (_moments.Count >= 2 && _moments[1].Time <= _playback)
            {
                _moments.RemoveAt(0);
            }

            BodyMoment from = _moments[0];
            if (_moments.Count == 1 || _playback <= from.Time)
            {
                from.Time = _playback;
                return from;
            }
            BodyMoment to = _moments[1];
            float span = (float)(to.Time - from.Time);
            if ((to.Position - from.Position).magnitude > TeleportSpeed * span)
            {
                from.Time = _playback;
                return from;   // it is put at the far end when the playback gets there
            }
            float t = Mathf.Clamp01((float)(_playback - from.Time) / Mathf.Max(span, 1e-5f));
            return new BodyMoment
            {
                Time = _playback,
                Position = Vector3.LerpUnclamped(from.Position, to.Position, t),
                Rotation = Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, t),
                Scale = Vector3.LerpUnclamped(from.Scale, to.Scale, t),
            };
        }
    }
}
