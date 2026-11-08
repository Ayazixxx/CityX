using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Crimson Claw's swipe as one motion of its whole body (owner, 2026-11-07: "make it look more fluid less robotic"): the striking claw
    /// rears back and up, coils tighter through its tell, then whips across and down past where it aims and settles; the other claw braces
    /// back and swings the other way; the head and body turn into the blow and lunge after it. Every part runs through one smooth curve
    /// through its keys (cubic, each key's speed taken from its neighbours), so nothing stops dead or jumps between the phases, which keep
    /// their times (<see cref="CrimsonClaw.WindupSeconds"/> and the rest: the tell and the hit window are unchanged).
    /// Poses are for a swipe by the claw on the model's -X side; <see cref="Pose.Mirrored"/> gives the +X claw's. Turns are degrees (x pitch,
    /// tip down; y yaw, its tip in across the face toward +X; z roll), shifts are in the model's own units (it is drawn at
    /// <see cref="CrimsonClaw.ModelScale"/>).
    /// </summary>
    public static class ClawStrikeMotion
    {
        /// <summary>Where every part is at one moment.</summary>
        public struct Pose
        {
            public Vector3 StrikeTurn;
            public Vector3 StrikeShift;
            public Vector3 OtherTurn;
            public Vector3 OtherShift;
            public Vector3 BodyTurn;
            public Vector3 BodyShift;

            /// <summary>The same moment swung by the other claw: everything across the face turned the other way.</summary>
            public Pose Mirrored()
            {
                return new Pose
                {
                    StrikeTurn = Flip(StrikeTurn), StrikeShift = FlipShift(StrikeShift),
                    OtherTurn = Flip(OtherTurn), OtherShift = FlipShift(OtherShift),
                    BodyTurn = Flip(BodyTurn), BodyShift = FlipShift(BodyShift),
                };
            }

            private static Vector3 Flip(Vector3 turn) => new Vector3(turn.x, -turn.y, -turn.z);
            private static Vector3 FlipShift(Vector3 shift) => new Vector3(-shift.x, shift.y, shift.z);
        }

        /// <summary>The strike drawn back, in its tell, quivers this much, degrees (a creature straining to let go, not a held frame).</summary>
        public const float TellQuiverDegrees = 1.2f;

        /// <summary>... this many times a second.</summary>
        public const float TellQuiverHertz = 22.0f;

        private const float Windup = CrimsonClaw.WindupSeconds;
        private const float Tell = Windup + CrimsonClaw.TellSeconds;
        private const float Swipe = Tell + CrimsonClaw.SwingSeconds;
        private const float End = CrimsonClaw.AttackSeconds;

        // Times of the keys: rest, drawn back, coiled (end of the tell), mid-swipe, end of the swipe, past it, settling, rest.
        private static readonly float[] Times =
        {
            0.0f, Windup, Tell, Tell + CrimsonClaw.SwingSeconds * 0.5f, Swipe,
            Swipe + CrimsonClaw.RecoverSeconds * 0.3f, Swipe + CrimsonClaw.RecoverSeconds * 0.65f, End,
        };

        private static readonly Vector3[] StrikeTurns =
        {
            Vector3.zero,
            new Vector3(-18.0f, -CrimsonClaw.WindupAngleDegrees, -8.0f),
            new Vector3(-26.0f, -CrimsonClaw.WindupAngleDegrees - 9.0f, -12.0f),
            new Vector3(6.0f, 10.0f, 4.0f),
            new Vector3(16.0f, CrimsonClaw.SwingAngleDegrees, 14.0f),
            new Vector3(18.0f, CrimsonClaw.SwingAngleDegrees + 7.0f, 16.0f),
            new Vector3(6.0f, 30.0f, 5.0f),
            Vector3.zero,
        };

        private static readonly Vector3[] StrikeShifts =
        {
            Vector3.zero,
            new Vector3(-0.03f, 0.05f, -0.06f),
            new Vector3(-0.04f, 0.08f, -0.09f),
            new Vector3(0.0f, 0.0f, 0.10f),
            new Vector3(0.03f, -0.03f, 0.06f),
            new Vector3(0.03f, -0.04f, 0.04f),
            new Vector3(0.01f, -0.01f, 0.01f),
            Vector3.zero,
        };

        private static readonly Vector3[] OtherTurns =
        {
            Vector3.zero,
            new Vector3(-4.0f, 6.0f, 0.0f),
            new Vector3(-6.0f, 8.0f, 0.0f),
            new Vector3(2.0f, -4.0f, 0.0f),
            new Vector3(4.0f, -10.0f, 0.0f),
            new Vector3(4.0f, -10.0f, 0.0f),
            new Vector3(1.0f, -3.0f, 0.0f),
            Vector3.zero,
        };

        private static readonly Vector3[] OtherShifts =
        {
            Vector3.zero,
            new Vector3(0.01f, 0.01f, -0.02f),
            new Vector3(0.01f, 0.02f, -0.03f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(-0.01f, -0.01f, 0.02f),
            new Vector3(-0.01f, -0.01f, 0.02f),
            Vector3.zero,
            Vector3.zero,
        };

        private static readonly Vector3[] BodyTurns =
        {
            Vector3.zero,
            new Vector3(-4.0f, -8.0f, 3.0f),
            new Vector3(-6.0f, -11.0f, 4.0f),
            new Vector3(3.0f, 4.0f, -1.0f),
            new Vector3(6.0f, 12.0f, -4.0f),
            new Vector3(6.0f, 13.0f, -4.0f),
            new Vector3(2.0f, 4.0f, -1.0f),
            Vector3.zero,
        };

        private static readonly Vector3[] BodyShifts =
        {
            Vector3.zero,
            new Vector3(0.0f, 0.02f, -0.03f),
            new Vector3(0.0f, 0.03f, -0.05f),
            new Vector3(0.0f, 0.0f, 0.06f),
            new Vector3(0.0f, -0.02f, 0.09f),
            new Vector3(0.0f, -0.02f, 0.08f),
            new Vector3(0.0f, 0.0f, 0.02f),
            Vector3.zero,
        };

        /// <summary>A swipe by the -X claw <paramref name="seconds"/> after it began; rest before it and after <see cref="CrimsonClaw.AttackSeconds"/>.</summary>
        public static Pose At(float seconds)
        {
            float t = Mathf.Clamp(seconds, 0.0f, End);
            var pose = new Pose
            {
                StrikeTurn = Along(StrikeTurns, t),
                StrikeShift = Along(StrikeShifts, t),
                OtherTurn = Along(OtherTurns, t),
                OtherShift = Along(OtherShifts, t),
                BodyTurn = Along(BodyTurns, t),
                BodyShift = Along(BodyShifts, t),
            };
            if (t > Windup && t < Tell)
            {
                // Straining in its tell: a quiver that swells and dies away within it, so it starts and ends still.
                float swell = Mathf.Sin(Mathf.PI * (t - Windup) / (Tell - Windup));
                float quiver = TellQuiverDegrees * swell * swell * Mathf.Sin(2.0f * Mathf.PI * TellQuiverHertz * t);
                pose.StrikeTurn += new Vector3(quiver, quiver * 0.6f, 0.0f);
            }
            return pose;
        }

        /// <summary>The curve through <paramref name="keys"/> at <paramref name="t"/>: cubic between keys, still at the first and last.</summary>
        private static Vector3 Along(Vector3[] keys, float t)
        {
            int i = 0;
            while (i < Times.Length - 2 && t > Times[i + 1])
            {
                i++;
            }
            float span = Times[i + 1] - Times[i];
            float s = Mathf.Clamp01((t - Times[i]) / span);
            Vector3 from = keys[i];
            Vector3 to = keys[i + 1];
            Vector3 leaving = Speed(keys, i) * span;
            Vector3 arriving = Speed(keys, i + 1) * span;
            float s2 = s * s;
            float s3 = s2 * s;
            return (2.0f * s3 - 3.0f * s2 + 1.0f) * from + (s3 - 2.0f * s2 + s) * leaving
                 + (-2.0f * s3 + 3.0f * s2) * to + (s3 - s2) * arriving;
        }

        /// <summary>How fast the curve passes key <paramref name="i"/>: from its neighbours; still at the ends.</summary>
        private static Vector3 Speed(Vector3[] keys, int i)
        {
            if (i == 0 || i == keys.Length - 1)
            {
                return Vector3.zero;
            }
            return (keys[i + 1] - keys[i - 1]) / (Times[i + 1] - Times[i - 1]);
        }
    }
}
