using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW A BODY TAKES A STAIR, measured from the owner's Mixamo stair clips (2026-11-05, docs/STAIRS.md): the pace, how long a foot is in the air, where it lands and leaves, and
    /// the path it follows. The up clips are clean. The down clips are ruined below the floor line (Mixamo holds the feet at floor height while the hips sink), so going down the
    /// heights are rebuilt from what survives: the feet's travel along the ground and the first frames of each fall. PURE: read by StairSteps, and by the rig builder for the clips' phase.
    /// </summary>
    public static class StairGait
    {
        /// <summary>The share of a body's walking and sprinting speed it keeps on a stair: about three unhurried steps a second at a walk, on 0.36 m treads.</summary>
        public const float WalkSpeedShare = 0.27f;

        public const float SprintSpeedShare = 0.25f;

        /// <summary>The pace of a walk and a run on a stair, metres a second: the gait is read between them.</summary>
        public const float WalkPace = 1.08f;

        public const float RunPace = 1.9f;

        /// <summary>One step on each tread: a whole cycle, left foot and right, covers two 0.36 m treads.</summary>
        public const float StrideMetres = 0.72f;

        /// <summary>A step's time stays within this, seconds, whatever the speed: a body barely moving does not float its foot for a second.</summary>
        public const float LeastSwingSeconds = 0.08f;

        public const float MostSwingSeconds = 0.5f;

        /// <summary>A foot comes down with its toes this far down going down the stair, degrees: toes first. Going up it lands flat.</summary>
        public const float LandingPitchGoingDown = 30.0f;

        /// <summary>Where in each clip the left foot leaves the ground (0 to 1), so the four share one phase, measured frame by frame.</summary>
        public const float UpWalkLeftLifts = 0.695f;

        public const float UpRunLeftLifts = 0.5f;

        public const float DownWalkLeftLifts = 0.857f;

        public const float DownRunLeftLifts = 0.667f;

        private struct Way
        {
            /// <summary>The share of a foot's cycle it is off its tread.</summary>
            public float Swing;

            /// <summary>The ball of the foot past the hips when it lands, and behind them when it leaves (negative: still ahead of them), metres.</summary>
            public float LandAhead;

            public float LiftBehind;
        }

        private static readonly Way UpWalk = new Way { Swing = 0.51f, LandAhead = 0.46f, LiftBehind = -0.11f };
        private static readonly Way UpRun = new Way { Swing = 0.78f, LandAhead = 0.34f, LiftBehind = -0.18f };
        private static readonly Way DownWalk = new Way { Swing = 0.57f, LandAhead = 0.23f, LiftBehind = 0.08f };
        private static readonly Way DownRun = new Way { Swing = 0.67f, LandAhead = 0.30f, LiftBehind = -0.06f };

        // A step, from leaving to landing: how far along it has got and how high (shares of the way to the landing, higher than 1 over the tread), and the heel's rise, degrees.
        // Up: the same shape in the walk and the run. It rises before it travels, the heel up, goes over the tread's edge and comes down flat.
        private static readonly float[] UpAt = { 0.0f, 0.14f, 0.24f, 0.33f, 0.43f, 0.52f, 0.62f, 0.71f, 0.81f, 0.90f, 1.0f };
        private static readonly float[] UpAlong = { 0.0f, 0.09f, 0.11f, 0.14f, 0.20f, 0.32f, 0.50f, 0.69f, 0.85f, 0.96f, 1.0f };
        private static readonly float[] UpHeight = { 0.0f, 0.10f, 0.23f, 0.44f, 0.80f, 1.05f, 1.18f, 1.23f, 1.16f, 1.05f, 1.0f };
        private static readonly float[] UpHeel = { 0.0f, 24.0f, 33.0f, 39.0f, 44.0f, 42.0f, 32.0f, 20.0f, 7.0f, 1.0f, 0.0f };

        // Down: it travels first, toes pointing, then lowers with the hips onto the tread.
        private static readonly float[] DownAt = { 0.0f, 0.19f, 0.31f, 0.38f, 0.44f, 0.56f, 0.75f, 1.0f };
        private static readonly float[] DownAlong = { 0.0f, 0.31f, 0.69f, 0.89f, 0.93f, 1.0f, 1.0f, 1.0f };
        private static readonly float[] DownHeight = { 0.0f, 0.08f, 0.19f, 0.30f, 0.42f, 0.58f, 0.80f, 1.0f };
        private static readonly float[] DownHeel = { 0.0f, 20.0f, LandingPitchGoingDown, LandingPitchGoingDown, LandingPitchGoingDown, LandingPitchGoingDown, LandingPitchGoingDown, LandingPitchGoingDown };

        /// <summary>The share of its speed a body keeps on a stair.</summary>
        public static float SpeedShare(bool sprinting) => sprinting ? SprintSpeedShare : WalkSpeedShare;

        /// <summary>0 at a walk's pace, 1 at a run's and beyond.</summary>
        public static float RunShare(float speed) => Mathf.InverseLerp(WalkPace, RunPace, speed);

        private static Way Between(bool up, float speed)
        {
            Way walk = up ? UpWalk : DownWalk, run = up ? UpRun : DownRun;
            float t = RunShare(speed);
            return new Way
            {
                Swing = Mathf.Lerp(walk.Swing, run.Swing, t),
                LandAhead = Mathf.Lerp(walk.LandAhead, run.LandAhead, t),
                LiftBehind = Mathf.Lerp(walk.LiftBehind, run.LiftBehind, t),
            };
        }

        /// <summary>The share of a foot's cycle it is off its tread.</summary>
        public static float SwingShare(bool up, float speed) => Between(up, speed).Swing;

        public static float LandAhead(bool up, float speed) => Between(up, speed).LandAhead;

        public static float LiftBehind(bool up, float speed) => Between(up, speed).LiftBehind;

        /// <summary>How long a step is in the air, seconds.</summary>
        public static float SwingSeconds(bool up, float speed)
        {
            return Mathf.Clamp(SwingShare(up, speed) * StrideMetres / Mathf.Max(speed, 0.01f), LeastSwingSeconds, MostSwingSeconds);
        }

        /// <summary>A foot <paramref name="progress"/> (0 to 1) through its step: how far along, how high and how far its heel is up.</summary>
        public static void Swing(bool up, float progress, out float along, out float height, out float heelDegrees)
        {
            float[] at = up ? UpAt : DownAt;
            float p = Mathf.Clamp01(progress);
            int next = 1;
            while (next < at.Length - 1 && at[next] < p)
            {
                next++;
            }
            float t = Mathf.InverseLerp(at[next - 1], at[next], p);
            along = Mathf.Lerp((up ? UpAlong : DownAlong)[next - 1], (up ? UpAlong : DownAlong)[next], t);
            height = Mathf.Lerp((up ? UpHeight : DownHeight)[next - 1], (up ? UpHeight : DownHeight)[next], t);
            heelDegrees = Mathf.Lerp((up ? UpHeel : DownHeel)[next - 1], (up ? UpHeel : DownHeel)[next], t);
        }

        /// <summary>How a foot is pitched when it lands, degrees.</summary>
        public static float LandingPitch(bool up) => up ? 0.0f : LandingPitchGoingDown;
    }
}
