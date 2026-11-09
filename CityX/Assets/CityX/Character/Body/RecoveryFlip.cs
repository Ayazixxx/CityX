using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE RECOVERY FLIP'S SHAPE (the Freerunner skill Recovery; owner, 2026-11-08: "make the recover skill work like the recovery in saints row
    /// 4", whose Ninja Reflexes upgrade gets the knocked-down Boss straight back up with a flip). From the body as it lies or tumbles, this says
    /// which way the flip goes, how far it turns, how long it takes and how fast the body leaves: a plain class, tested without a scene; the
    /// Ragdoll plays it (Ragdoll.Recovery.cs).
    ///
    /// The flip turns the body head over heels about its right side, from the pitch it is at to upright: on, the way it is already spinning, or,
    /// spinning hardly at all, the long way round, so it is always a real flip (between <see cref="MinSweepDegrees"/> and a full turn and a half)
    /// and never a nudge upright. Its speed follows the tuck as a diver's does: slow while the body is open at either end, fast while it is curled.
    /// The body keeps its momentum; one that would land before the flip is over (lying on the ground, or just above it) is sent up enough to
    /// finish it in the air and land on its feet.
    /// </summary>
    public readonly struct RecoveryFlip
    {
        /// <summary>How long a whole turn takes, and a half turn (the least the flip turns). Saints Row's is about half a second.</summary>
        public const float FullTurnSeconds = 0.55f;
        public const float HalfTurnSeconds = 0.42f;

        /// <summary>A flip following the body's own spin turns at least this far; a still body's turns at least half round.</summary>
        public const float MinSweepDegrees = 150.0f;

        /// <summary>Spinning head over heels faster than this, degrees a second, the flip carries on the way it spins.</summary>
        public const float FollowSpinDegreesPerSecond = 90.0f;

        /// <summary>Upright and on its feet in the air for this long before it lands, at least.</summary>
        public const float UprightBeforeLandingSeconds = 0.25f;

        /// <summary>How much faster the curled body turns than the open one at the flip's two ends (the speed's swing about its mean).</summary>
        public const float TuckSpeedSwing = 0.6f;

        /// <summary>The tuck: curling in over the first share of the flip, held, and opened out (legs down for the landing) over the last.</summary>
        public const float TuckInEnd = 0.25f;
        public const float TuckOutStart = 0.58f;
        public const float TuckOutEnd = 0.9f;

        /// <summary>Which way the body faces through the flip and after it: level, the flip turning about its right.</summary>
        public Vector3 Facing { get; }

        /// <summary>The body's pitch when the flip begins, degrees, 0 upright, 90 head forward along <see cref="Facing"/>.</summary>
        public float StartDegrees { get; }

        /// <summary>How far it turns: positive forward (head over), negative back. Start plus sweep is a whole number of turns: upright.</summary>
        public float SweepDegrees { get; }

        public float Seconds { get; }

        /// <summary>The body's velocity as the flip begins, metres a second: its own, raised if it would land before the flip was over.</summary>
        public Vector3 Velocity { get; }

        private RecoveryFlip(Vector3 facing, float startDegrees, float sweepDegrees, float seconds, Vector3 velocity)
        {
            Facing = facing;
            StartDegrees = startDegrees;
            SweepDegrees = sweepDegrees;
            Seconds = seconds;
            Velocity = velocity;
        }

        /// <param name="bodyUp">From the hips to the neck.</param>
        /// <param name="bodyRight">From the left thigh to the right.</param>
        /// <param name="spin">The torso's angular velocity, radians a second (Unity's sense: positive about an axis turns as AngleAxis does).</param>
        /// <param name="velocity">The torso's velocity.</param>
        /// <param name="heightAboveGround">How high the feet will be above the ground when the flip begins (0 lying on it).</param>
        /// <param name="gravity">The walker's gravity, which carries the body through the flip and after.</param>
        /// <param name="fallbackFacing">Which way to face when the body gives no answer (lying on its side, its right pointing up).</param>
        public static RecoveryFlip Plan(Vector3 bodyUp, Vector3 bodyRight, Vector3 spin, Vector3 velocity, float heightAboveGround, float gravity,
            Vector3 fallbackFacing)
        {
            Vector3 facing = FacingOf(bodyUp, bodyRight, fallbackFacing);
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            float start = Mathf.Repeat(Mathf.Atan2(Vector3.Dot(bodyUp, facing), Vector3.Dot(bodyUp, Vector3.up)) * Mathf.Rad2Deg, 360.0f);
            float pitchSpin = Vector3.Dot(spin, right) * Mathf.Rad2Deg;
            float sweep = SweepFrom(start, pitchSpin);
            float seconds = Mathf.Lerp(HalfTurnSeconds, FullTurnSeconds, Mathf.InverseLerp(180.0f, 360.0f, Mathf.Abs(sweep)));
            return new RecoveryFlip(facing, start, sweep, seconds, Launch(velocity, heightAboveGround, gravity, seconds));
        }

        /// <summary>The flip's pitch <paramref name="elapsed"/> seconds in, degrees (start plus sweep at the end).</summary>
        public float PitchAt(float elapsed)
        {
            float u = Mathf.Clamp01(Seconds > 0.0f ? elapsed / Seconds : 1.0f);
            float turned = u - TuckSpeedSwing * Mathf.Sin(2.0f * Mathf.PI * u) / (2.0f * Mathf.PI);
            return StartDegrees + SweepDegrees * turned;
        }

        /// <summary>How curled the body is <paramref name="elapsed"/> seconds in: 0 open, 1 tucked.</summary>
        public float TuckAt(float elapsed)
        {
            float u = Mathf.Clamp01(Seconds > 0.0f ? elapsed / Seconds : 1.0f);
            return Smooth(0.0f, TuckInEnd, u) * (1.0f - Smooth(TuckOutStart, TuckOutEnd, u));
        }

        /// <summary>The flip as a turn of the body: its pitch about its right, in the frame it faces along.</summary>
        public Quaternion TurnAt(float elapsed) => Quaternion.AngleAxis(PitchAt(elapsed), Vector3.right);

        private static Vector3 FacingOf(Vector3 bodyUp, Vector3 bodyRight, Vector3 fallbackFacing)
        {
            Vector3 right = Vector3.ProjectOnPlane(bodyRight, Vector3.up);
            if (right.sqrMagnitude > 0.1f * bodyRight.sqrMagnitude && right.sqrMagnitude > 1e-6f)
            {
                return Vector3.Cross(right.normalized, Vector3.up);
            }
            // On its side: it faces where its head points, and flips forward from lying along it.
            Vector3 head = Vector3.ProjectOnPlane(bodyUp, Vector3.up);
            if (head.sqrMagnitude > 1e-6f)
            {
                return head.normalized;
            }
            Vector3 fallback = Vector3.ProjectOnPlane(fallbackFacing, Vector3.up);
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;
        }

        private static float SweepFrom(float start, float pitchSpin)
        {
            float forward = 360.0f - start;
            float back = start;
            if (pitchSpin > FollowSpinDegreesPerSecond)
            {
                return forward < MinSweepDegrees ? forward + 360.0f : forward;
            }
            if (pitchSpin < -FollowSpinDegreesPerSecond)
            {
                return back < MinSweepDegrees ? -(back + 360.0f) : -back;
            }
            return forward >= 180.0f ? forward : -back;
        }

        /// <summary>
        /// The body's own velocity, its rise raised if it would come down before it had been upright for <see cref="UprightBeforeLandingSeconds"/>
        /// (solved for the launch that is still <paramref name="height"/> above where it started when that time is up).
        /// </summary>
        private static Vector3 Launch(Vector3 velocity, float height, float gravity, float seconds)
        {
            float airborne = seconds + UprightBeforeLandingSeconds;
            float needed = (0.5f * gravity * airborne * airborne - Mathf.Max(0.0f, height)) / airborne;
            velocity.y = Mathf.Max(velocity.y, needed);
            return velocity;
        }

        private static float Smooth(float from, float to, float u)
        {
            float t = Mathf.Clamp01((u - from) / (to - from));
            return t * t * (3.0f - 2.0f * t);
        }
    }
}
