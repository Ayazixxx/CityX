using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Crimson Claw's one-handed grab and throw, from the moment its swipe closes on someone (owner, 2026-11-08: "i wanted it to be a 1
    /// handed grab... you can use the swing attack animation but he just grabs his and the raises his arm and throws him"): the striking claw
    /// lifts its arm high with them upright in its fist (lifted, not tipped up: tipped up about its wrist, which is far back
    /// under its head, the arm carried them back over the head), rears back a little, flings it forward and down past where it let go, and settles; the other claw
    /// braces; the head rears back with the arm and lunges into the throw. Before the catch it is the swipe itself (<see cref="ClawStrikeMotion"/>),
    /// so the first key is where the swipe had the claw when it caught, and every part runs on from there along one smooth curve
    /// (<see cref="KeyedMotion"/>). Poses are for the claw on the model's -X side, as the swipe's are (<see cref="ClawStrikeMotion.Pose.Mirrored"/>).
    /// </summary>
    public sealed class ClawGrabMotion
    {
        /// <summary>Raising its arm with what it caught, seconds.</summary>
        public const float LiftSeconds = 0.45f;

        /// <summary>Rearing it back further to throw, seconds.</summary>
        public const float RearSeconds = 0.35f;

        /// <summary>Flinging it, seconds: it lets go halfway through.</summary>
        public const float ThrowSeconds = 0.2f;

        public const float SettleSeconds = 0.4f;

        /// <summary>From the catch to back at rest.</summary>
        public const float Seconds = LiftSeconds + RearSeconds + ThrowSeconds + SettleSeconds;

        /// <summary>When it lets go, seconds after the catch.</summary>
        public const float LetGoSeconds = LiftSeconds + RearSeconds + ThrowSeconds * 0.5f;

        // Times of the keys: caught, arm raised, reared back, flinging, flung, settling, rest.
        private static readonly float[] Times =
        {
            0.0f, LiftSeconds, LiftSeconds + RearSeconds, LetGoSeconds, LiftSeconds + RearSeconds + ThrowSeconds,
            LiftSeconds + RearSeconds + ThrowSeconds + SettleSeconds * 0.45f, Seconds,
        };

        // The keys after the catch (the first of each is where the swipe had it). Held, the claw and the head together turn the fist about
        // 15 degrees back and 20 out, the angle measured to hold a body best (CrimsonClaw.FistHollow).
        private static readonly Vector3[] StrikeTurns =
        {
            Vector3.zero, new Vector3(-11.0f, -20.0f, 0.0f), new Vector3(-13.0f, -24.0f, -2.0f), new Vector3(-5.0f, 10.0f, 2.0f),
            new Vector3(24.0f, 30.0f, 8.0f), new Vector3(8.0f, 12.0f, 3.0f), Vector3.zero,
        };

        private static readonly Vector3[] StrikeShifts =
        {
            Vector3.zero, new Vector3(-0.04f, 0.50f, 0.08f), new Vector3(-0.06f, 0.55f, -0.02f), new Vector3(-0.02f, 0.58f, 0.14f),
            new Vector3(0.04f, 0.25f, 0.26f), new Vector3(0.01f, 0.04f, 0.06f), Vector3.zero,
        };

        private static readonly Vector3[] OtherTurns =
        {
            Vector3.zero, new Vector3(-4.0f, 6.0f, 0.0f), new Vector3(-6.0f, 8.0f, 0.0f), new Vector3(2.0f, -6.0f, 0.0f),
            new Vector3(4.0f, -10.0f, 0.0f), new Vector3(1.0f, -3.0f, 0.0f), Vector3.zero,
        };

        private static readonly Vector3[] OtherShifts =
        {
            Vector3.zero, new Vector3(0.01f, 0.01f, -0.02f), new Vector3(0.01f, 0.02f, -0.03f), Vector3.zero,
            new Vector3(-0.01f, -0.01f, 0.02f), Vector3.zero, Vector3.zero,
        };

        private static readonly Vector3[] BodyTurns =
        {
            Vector3.zero, new Vector3(-4.0f, -6.0f, 2.0f), new Vector3(-8.0f, -10.0f, 3.0f), new Vector3(4.0f, 6.0f, -2.0f),
            new Vector3(10.0f, 12.0f, -4.0f), new Vector3(3.0f, 4.0f, -1.0f), Vector3.zero,
        };

        private static readonly Vector3[] BodyShifts =
        {
            Vector3.zero, new Vector3(0.0f, 0.04f, -0.02f), new Vector3(0.0f, 0.05f, -0.05f), new Vector3(0.0f, 0.0f, 0.08f),
            new Vector3(0.0f, -0.02f, 0.12f), new Vector3(0.0f, 0.0f, 0.03f), Vector3.zero,
        };

        private readonly Vector3[] _strikeTurns = (Vector3[])StrikeTurns.Clone();
        private readonly Vector3[] _strikeShifts = (Vector3[])StrikeShifts.Clone();
        private readonly Vector3[] _otherTurns = (Vector3[])OtherTurns.Clone();
        private readonly Vector3[] _otherShifts = (Vector3[])OtherShifts.Clone();
        private readonly Vector3[] _bodyTurns = (Vector3[])BodyTurns.Clone();
        private readonly Vector3[] _bodyShifts = (Vector3[])BodyShifts.Clone();

        /// <summary>The grab on from <paramref name="caught"/>, where the swipe had every part when its claw closed (the -X claw's pose).</summary>
        public ClawGrabMotion(ClawStrikeMotion.Pose caught)
        {
            _strikeTurns[0] = caught.StrikeTurn;
            _strikeShifts[0] = caught.StrikeShift;
            _otherTurns[0] = caught.OtherTurn;
            _otherShifts[0] = caught.OtherShift;
            _bodyTurns[0] = caught.BodyTurn;
            _bodyShifts[0] = caught.BodyShift;
        }

        /// <summary>Every part <paramref name="seconds"/> after the catch; at rest from <see cref="Seconds"/>.</summary>
        public ClawStrikeMotion.Pose At(float seconds)
        {
            float t = Mathf.Clamp(seconds, 0.0f, Seconds);
            return new ClawStrikeMotion.Pose
            {
                StrikeTurn = KeyedMotion.Along(Times, _strikeTurns, t),
                StrikeShift = KeyedMotion.Along(Times, _strikeShifts, t),
                OtherTurn = KeyedMotion.Along(Times, _otherTurns, t),
                OtherShift = KeyedMotion.Along(Times, _otherShifts, t),
                BodyTurn = KeyedMotion.Along(Times, _bodyTurns, t),
                BodyShift = KeyedMotion.Along(Times, _bodyShifts, t),
            };
        }
    }
}
