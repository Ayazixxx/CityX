using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SHAPE OF SELF SMITE'S BOLT: a jagged line from the cloud, 1.5 km up, down to the caster, with a few forks near the bottom. Pure and
    /// the same for a seed (testable; another player's copy draws its own, which need not match kink for kink).
    ///
    /// The camera sits a few metres behind the caster, so the bottom of the bolt is seen from close and the top from a kilometre and more: the
    /// kinks are spaced finely at the bottom and ever wider going up (<see cref="FirstStepMetres"/>, growing by <see cref="StepGrowth"/> a
    /// step), about two dozen points in all, and the line is drawn wider as it goes up (<see cref="WidthAt"/>), so it is about as thick on the
    /// screen all the way to the cloud.
    /// </summary>
    public static class SelfSmiteBoltShape
    {
        /// <summary>
        /// The step nearest the caster, metres, and how much longer each step up is. Over a metre: the bottom is what the camera sees from close,
        /// and finer kinks than that blur into a straight beam under the glow (rendered, 2026-10-15).
        /// </summary>
        public const float FirstStepMetres = 1.3f;

        public const float StepGrowth = 1.3f;

        /// <summary>How far a kink strays sideways, as a share of its own step: wilder over the bottom steps, which are seen from close.</summary>
        public const float Roughness = 0.35f;

        public const float NearRoughness = 0.6f;

        /// <summary>How many steps from the bottom are <see cref="NearRoughness"/>.</summary>
        public const int NearSteps = 6;

        /// <summary>The wildest any kink may be.</summary>
        public static float MaxRoughness => Mathf.Max(Roughness, NearRoughness);

        /// <summary>The most points a trunk has: enough for steps of <see cref="FirstStepMetres"/> growing to any cloud up to several km.</summary>
        public const int MaxPoints = 40;

        /// <summary>The forks off the trunk, their points each, and how many points of the trunk they may start from (counted from the bottom).</summary>
        public const int Forks = 4;

        public const int ForkPoints = 6;

        public const int ForkStartLowest = 3;

        public const int ForkStartHighest = 13;

        /// <summary>The core's width where it meets the caster, metres, and how much wider it gets per metre up.</summary>
        public const float CoreWidthAtBottom = 0.32f;

        public const float CoreWidthPerMetreUp = 0.004f;

        /// <summary>
        /// The trunk from <paramref name="bottom"/> up to <paramref name="top"/>, bottom first, written into <paramref name="points"/>; returns how
        /// many. Both ends exactly where asked.
        /// </summary>
        public static int Trunk(Vector3 bottom, Vector3 top, int seed, Vector3[] points)
        {
            if (points == null || points.Length < 2)
            {
                return 0;
            }
            Vector3 along = top - bottom;
            float length = along.magnitude;
            int capacity = Mathf.Min(points.Length, MaxPoints);
            points[0] = bottom;
            if (length < 1e-3f)
            {
                points[1] = top;
                return 2;
            }
            Vector3 up = along / length;
            Vector3 side = Vector3.Cross(up, Vector3.forward);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.Cross(up, Vector3.right);
            }
            side.Normalize();
            Vector3 across = Vector3.Cross(up, side).normalized;

            uint state = Seed(seed);
            int count = 1;
            float travelled = 0.0f;
            float step = FirstStepMetres;
            while (count < capacity - 1 && travelled + step < length - step * 0.5f)
            {
                travelled += step;
                float stray = step * (count <= NearSteps ? NearRoughness : Roughness);
                points[count++] = bottom + up * travelled + (side * Signed(ref state) + across * Signed(ref state)) * stray;
                step *= StepGrowth;
            }
            points[count++] = top;
            return count;
        }

        /// <summary>
        /// Fork <paramref name="index"/> off a trunk of <paramref name="trunkCount"/> points: from one of its lower points, down and outward,
        /// a little over half that point's height long. Returns how many points were written (0 when the trunk is too short to fork).
        /// </summary>
        public static int Fork(Vector3[] trunk, int trunkCount, int seed, int index, Vector3[] points)
        {
            if (trunk == null || points == null || points.Length < 2 || trunkCount <= ForkStartLowest + 1)
            {
                return 0;
            }
            uint state = Seed(seed * 31 + index * 7919 + 17);
            int highest = Mathf.Min(ForkStartHighest, trunkCount - 2);
            int start = Mathf.Clamp(ForkStartLowest + (int)(Unit(ref state) * (highest - ForkStartLowest + 1)), ForkStartLowest, highest);
            Vector3 origin = trunk[start];
            float height = Mathf.Max(0.5f, origin.y - trunk[0].y);
            float angle = Unit(ref state) * Mathf.PI * 2.0f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            float reach = height * (0.45f + 0.35f * Unit(ref state));
            // Down and out, 30 to 60 degrees off the vertical.
            float tilt = Mathf.Lerp(30.0f, 60.0f, Unit(ref state)) * Mathf.Deg2Rad;
            Vector3 end = origin + (outward * Mathf.Sin(tilt) + Vector3.down * Mathf.Cos(tilt)) * reach;

            int count = Mathf.Min(points.Length, ForkPoints);
            Vector3 heading = end - origin;
            Vector3 side = Vector3.Cross(heading, Vector3.up);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float stray = (i == 0 || i == count - 1) ? 0.0f : reach / (count - 1) * Roughness * Signed(ref state);
                points[i] = origin + heading * t + side * stray;
            }
            return count;
        }

        /// <summary>The core's width at a point <paramref name="metresUp"/> above the bottom of the bolt: wider going up, for the distance.</summary>
        public static float WidthAt(float metresUp)
        {
            return CoreWidthAtBottom + Mathf.Max(0.0f, metresUp) * CoreWidthPerMetreUp;
        }

        // A tiny xorshift: the same numbers on every machine and in every test.
        private static uint Seed(int seed)
        {
            uint state = (uint)seed * 2654435761u + 0x9E3779B9u;
            return state == 0 ? 1u : state;
        }

        private static float Unit(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state >> 8) / 16777216.0f;
        }

        private static float Signed(ref uint state) => Unit(ref state) * 2.0f - 1.0f;
    }
}
