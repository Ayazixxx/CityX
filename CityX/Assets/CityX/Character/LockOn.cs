using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Picking something to fight and staying pointed at it, ported from
    /// player.gd's own lock-on.
    ///
    /// PURE, AND SEPARATE FROM THE CAMERA THAT USES IT. Which target a press
    /// picks, how the cycling wraps, and when a lock should drop are the parts
    /// that can be wrong in a way nobody would spot -- and the parts a test can
    /// reach without a scene. The camera does the swinging.
    /// </summary>
    public static class LockOn
    {
        /// <summary>
        /// How far a target can be. Losing an existing lock uses this with a
        /// little slack, so a target hovering exactly at the boundary does not
        /// flicker in and out.
        /// </summary>
        public const float Range = 20.0f;

        public const float ReleaseSlack = 1.15f;

        /// <summary>
        /// ACQUISITION ONLY. How far off the crosshair a candidate can be and
        /// still be picked by a fresh press, so locking on grabs whoever you
        /// are actually looking toward rather than whoever happens to be
        /// nearest. Cycling through the ones already in range ignores it.
        /// </summary>
        public const float MaxAcquireAngle = 65.0f;

        public const float CameraLerpSpeed = 10.0f;

        /// <summary>
        /// How long a target can be out of sight -- ducking behind a corner
        /// mid-fight -- before the lock actually releases. Forgiving a brief
        /// occlusion rather than dropping the instant one raycast clips
        /// scenery.
        /// </summary>
        public const float SightGraceSeconds = 0.75f;

        /// <summary>
        /// The camera pitches by atan2(height, distance), and against a TALL
        /// target at close range the horizontal distance shrinks toward zero
        /// while the height does not -- so that ratio spikes toward straight
        /// up. Pitching a boom camera up swings its far end DOWN and under the
        /// player, so an uncapped value puts the camera on the pavement looking
        /// at the character's underside.
        /// </summary>
        public const float MaxPitchDegrees = 35.0f;

        /// <summary>
        /// A floor under the horizontal distance for that same pitch. Even
        /// before the cap engages, closing to melee range against something
        /// tall sends the raw ratio climbing steeply, which reads as the camera
        /// diving as you close rather than settling.
        /// </summary>
        public const float MinPitchDistance = 2.0f;

        /// <summary>
        /// Everything lockable within range, nearest first.
        ///
        /// ANYTHING THAT CAN BE DAMAGED AND IS STILL ALIVE. Godot uses a
        /// "lockable_npcs" group, which in Unity would be a tag somebody has to
        /// remember to set. Being hittable is the same statement and cannot be
        /// forgotten.
        /// </summary>
        public static List<Transform> Candidates(Vector3 from, IEnumerable<IDamageable> world)
        {
            var found = new List<Transform>();
            foreach (IDamageable target in world)
            {
                if (target == null || !target.IsAlive || !(target is Component component))
                {
                    continue;
                }
                if (Vector3.Distance(from, component.transform.position) <= Range)
                {
                    found.Add(component.transform);
                }
            }

            found.Sort((a, b) => Vector3.SqrMagnitude(a.position - from)
                .CompareTo(Vector3.SqrMagnitude(b.position - from)));
            return found;
        }

        /// <summary>
        /// Which candidate a fresh press should grab: the one nearest the
        /// crosshair, provided anything is close enough to it at all. Null when
        /// nothing qualifies, which leaves the camera alone rather than
        /// snapping it at somebody behind you.
        /// </summary>
        public static Transform Acquire(Vector3 from, Vector3 lookDirection,
            IReadOnlyList<Transform> candidates)
        {
            Transform best = null;
            float bestAngle = float.MaxValue;

            foreach (Transform candidate in candidates)
            {
                Vector3 toTarget = candidate.position - from;
                if (toTarget.sqrMagnitude < 0.0001f)
                {
                    continue;
                }
                float angle = Vector3.Angle(lookDirection, toTarget);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }

            return bestAngle <= MaxAcquireAngle ? best : null;
        }

        /// <summary>
        /// The next target when the button is pressed while already locked.
        ///
        /// Null means "that was the last one, let go" -- pressing through the
        /// whole list releases rather than looping forever, so the same button
        /// both cycles and cancels. A current target that has just left the
        /// list -- died or walked out of range this instant -- falls back to
        /// the front rather than indexing with -1.
        /// </summary>
        public static Transform Cycle(Transform current, IReadOnlyList<Transform> candidates,
            int visits)
        {
            // STRICTLY GREATER, not >=. With two targets the presses go
            // acquire, cycle, release -- one look at each and then let go. An
            // off-by-one here releases before you have seen the second one,
            // which reads as the lock refusing to cycle at all.
            if (candidates.Count == 0 || visits > candidates.Count)
            {
                return null;
            }

            int index = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == current)
                {
                    index = i;
                    break;
                }
            }
            return candidates[(index + 1) % candidates.Count];
        }

        /// <summary>
        /// The yaw and pitch that point at a target, in degrees.
        ///
        /// The pitch is capped and its distance floored, both for the reason
        /// above: a boom camera swings under the player when it pitches up, and
        /// against a tall target at arm's length the uncapped answer is
        /// straight down at your own feet.
        /// </summary>
        public static void Bearing(Vector3 from, Vector3 to, out float yaw, out float pitch)
        {
            Vector3 offset = to - from;
            var flat = new Vector3(offset.x, 0.0f, offset.z);

            yaw = flat.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg
                : 0.0f;

            float distance = Mathf.Max(flat.magnitude, MinPitchDistance);
            pitch = Mathf.Clamp(
                -Mathf.Atan2(offset.y, distance) * Mathf.Rad2Deg,
                -MaxPitchDegrees, MaxPitchDegrees);
        }
    }
}
