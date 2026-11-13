using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One cast of Glacier Spikes: where the line starts (on the ground), which way it runs (flat), how long it is, and its seed.</summary>
    public readonly struct GlacierSpikesCast
    {
        public readonly Vector3 Start;
        public readonly Vector3 Direction;
        public readonly float Length;
        public readonly int Seed;

        public GlacierSpikesCast(Vector3 start, Vector3 direction, float length, int seed)
        {
            Start = start;
            direction.y = 0.0f;
            Direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            Length = Mathf.Max(0.0f, length);
            Seed = seed;
        }

        /// <summary>To the caster's right, across the line.</summary>
        public Vector3 Side => Vector3.Cross(Vector3.up, Direction);

        public Vector3 End => Start + Direction * Length;

        /// <summary>The point <paramref name="along"/> (0 to 1) down the line, at the start's height.</summary>
        public Vector3 PointAt(float along) => Start + Direction * (Mathf.Clamp01(along) * Length);
    }

    /// <summary>
    /// One crystal of the field, as dice only (the reference's records): where it sits down and across the line, or round the far end, and
    /// how its size, lean and turn vary. Everything else follows from the cast and the time (<see cref="GlacierSpikesLayout.Pose"/>).
    /// </summary>
    public struct GlacierSpike
    {
        /// <summary>0 at the start of the line, 1 at its end (the impact cluster's are all 1).</summary>
        public float Along;

        /// <summary>Across the band, in half-widths, clumped toward the middle and scattered (it can pass 1).</summary>
        public float Lateral;

        /// <summary>One of the cluster thrown up round the far end: <see cref="Angle"/> round it and <see cref="Radial"/> (0 to 1) out from it.</summary>
        public bool Impact;
        public float Angle;
        public float Radial;

        public float Yaw;
        public float Stagger;
        public float HeightJitter;
        public float RadiusJitter;
        public float LeanJitter;

        /// <summary>Knocked down to ankle-high rubble.</summary>
        public bool Rubble;

        /// <summary>Which of the crystal meshes it is drawn with.</summary>
        public int Variant;
    }

    /// <summary>Where a crystal stands at a moment: its base on the ground, its turn and size; hidden before it breaks the ground.</summary>
    public struct GlacierSpikePose
    {
        public bool Visible;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Size;

        /// <summary>How far out of the ground it is (below 0 not yet out, past 1 springing).</summary>
        public float Emergence;
    }

    /// <summary>
    /// GLACIER SPIKES' FIELD, AS A PURE FUNCTION OF THE CAST (docs/GLACIER_SPIKES.md): the seed's dice for every crystal (<see cref="Build"/>)
    /// and where each one stands at a moment (<see cref="Pose"/>), so another player's copy grows the identical field from the same seed.
    /// The ground a crystal stands on and how far across the line it can go before a wall are measured in the world and handed in.
    /// A port of the "Frost Lance" in achrefelouafi's LinearAbiltyCastingThreeJS (MIT): its IceAbility's spawn and spike update.
    /// </summary>
    public static class GlacierSpikesLayout
    {
        /// <summary>How far a crystal's base is sunk under a lean, as a share of the width lifted off the ground on its high side.</summary>
        private const float LeanSink = 0.5f;

        /// <summary>How far below the ground a sunk crystal's tip goes past its own height, metres (the reference's 0.4, scaled).</summary>
        private const float SinkPast = 0.4f * GlacierSpikesRules.Scale;

        /// <summary>The field's crystals from <paramref name="seed"/>, written into <paramref name="spikes"/> (<see cref="GlacierSpikesRules.SpikeCount"/> long).</summary>
        public static void Build(int seed, GlacierSpike[] spikes)
        {
            var random = new DeterministicRandom(seed);
            int count = spikes.Length;
            int impactCount = Mathf.RoundToInt(count * GlacierSpikesRules.ImpactShare);
            int groundCount = count - impactCount;
            for (int i = 0; i < count; i++)
            {
                var spike = new GlacierSpike { Variant = i % GlacierSpikesRules.Variants };
                if (i < groundCount)
                {
                    spike.Along = Mathf.Pow((i + Next(random)) / groundCount, GlacierSpikesRules.FrontBias);
                    float raw = Signed(random);
                    spike.Lateral = Mathf.Sign(raw) * Mathf.Pow(Mathf.Abs(raw), GlacierSpikesRules.Clumping) + GlacierSpikesRules.Scatter * Signed(random);
                }
                else
                {
                    spike.Impact = true;
                    spike.Along = 1.0f;
                    spike.Angle = Next(random) * Mathf.PI * 2.0f;
                    spike.Radial = Mathf.Sqrt(Next(random));
                }
                spike.Yaw = Next(random) * Mathf.PI * 2.0f;
                spike.Stagger = Next(random);
                spike.HeightJitter = Signed(random);
                spike.RadiusJitter = Signed(random);
                spike.LeanJitter = Signed(random);
                spike.Rubble = Next(random) < GlacierSpikesRules.Rubble;
                spikes[i] = spike;
            }
        }

        private static float Next(DeterministicRandom random) => (float)random.NextDouble();

        private static float Signed(DeterministicRandom random) => (float)random.NextDouble() * 2.0f - 1.0f;

        /// <summary>The point on the line a crystal grows out from: its place down the line, or the far end for the impact cluster.</summary>
        public static Vector3 Anchor(in GlacierSpike spike, in GlacierSpikesCast cast) => cast.PointAt(spike.Along);

        /// <summary>From its anchor out to where the crystal stands, flat, metres.</summary>
        public static Vector3 Across(in GlacierSpike spike, in GlacierSpikesCast cast)
        {
            if (spike.Impact)
            {
                float reach = GlacierSpikesRules.HalfWidth(1.0f) * GlacierSpikesRules.ImpactRadiusShare * spike.Radial;
                return new Vector3(Mathf.Cos(spike.Angle), 0.0f, Mathf.Sin(spike.Angle)) * reach;
            }
            return cast.Side * (spike.Lateral * GlacierSpikesRules.HalfWidth(spike.Along));
        }

        /// <summary>How tall the crystal stands when fully out, metres: taller down the line, swelling at the end, lower at the band's edges.</summary>
        public static float HeightOf(in GlacierSpike spike)
        {
            float along = Mathf.Clamp01(spike.Along);
            float height = Mathf.Lerp(GlacierSpikesRules.HeightNear, GlacierSpikesRules.Height, Mathf.Pow(along, GlacierSpikesRules.HeightCurve));
            height *= 1.0f + (GlacierSpikesRules.Peak - 1.0f) * SmoothStep(1.0f - GlacierSpikesRules.PeakWidth, 1.0f, along);
            float edge = spike.Impact ? spike.Radial : Mathf.Clamp01(Mathf.Abs(spike.Lateral));
            height *= Mathf.Lerp(1.0f, 1.0f - GlacierSpikesRules.Crown, Mathf.Pow(edge, 1.4f));
            height *= 1.0f + GlacierSpikesRules.HeightJitter * spike.HeightJitter;
            if (spike.Rubble)
            {
                height *= GlacierSpikesRules.RubbleScale;
            }
            return Mathf.Max(0.02f, height);
        }

        /// <summary>
        /// How wide the crystal is, metres: wider down the line, rubble wider still; never narrower than <see cref="SlenderestShare"/> of its
        /// height (mine: the reference's thinnest, ringed in the cel look's black lines, read as poles).
        /// </summary>
        public static float WidthOf(in GlacierSpike spike)
        {
            float width = GlacierSpikesRules.Radius * Mathf.Lerp(0.72f, 1.15f, Mathf.Pow(Mathf.Clamp01(spike.Along), 0.6f))
                * (1.0f + GlacierSpikesRules.RadiusJitter * spike.RadiusJitter) * (spike.Rubble ? 1.25f : 1.0f);
            return Mathf.Max(Mathf.Max(0.01f, width), HeightOf(spike) * SlenderestShare);
        }

        /// <summary>The slenderest a crystal may be: its width as a share of its height.</summary>
        public const float SlenderestShare = 0.1f;

        /// <summary>When the crystal starts to break the ground, seconds after the cast: as the front passes it, staggered a little.</summary>
        public static float EruptTime(in GlacierSpike spike, in GlacierSpikesCast cast) =>
            GlacierSpikesRules.ArrivalTime(spike.Along * cast.Length) + spike.Stagger * GlacierSpikesRules.RiseStagger;

        /// <summary>Which way it leans and how far: away from the caster and outward from the line, more down the line.</summary>
        public static Quaternion Turn(in GlacierSpike spike, in GlacierSpikesCast cast, out float leanAngle)
        {
            float outward = spike.Impact ? Mathf.Sign(Mathf.Cos(spike.Angle)) * spike.Radial : spike.Lateral;
            Vector3 lean = cast.Direction * 0.75f + cast.Side * (outward * 0.85f);
            lean = lean.sqrMagnitude < 1e-6f ? cast.Direction : lean.normalized;
            float along = Mathf.Clamp01(spike.Along);
            leanAngle = GlacierSpikesRules.Lean * (0.35f + 0.65f * along) * (1.0f + GlacierSpikesRules.LeanJitter * spike.LeanJitter);
            Vector3 up = Vector3.up * Mathf.Cos(leanAngle) + lean * Mathf.Sin(leanAngle);
            return Quaternion.FromToRotation(Vector3.up, up) * Quaternion.AngleAxis(spike.Yaw * Mathf.Rad2Deg, Vector3.up);
        }

        /// <summary>
        /// Where <paramref name="spike"/> stands <paramref name="age"/> seconds after the cast: on <paramref name="groundY"/>, no further across
        /// than <paramref name="reach"/> metres from its anchor (a wall), rising out of the ground as the front passes, springing, then sinking.
        /// </summary>
        public static GlacierSpikePose Pose(in GlacierSpike spike, in GlacierSpikesCast cast, float age, float groundY, float reach)
        {
            var pose = new GlacierSpikePose { Rotation = Quaternion.identity };
            float emergence = GlacierSpikesRules.Emergence(age - EruptTime(spike, cast));
            pose.Emergence = emergence;
            if (emergence < 0.0f)
            {
                return pose;
            }
            float height = HeightOf(spike);
            float width = WidthOf(spike);
            Vector3 across = Across(spike, cast);
            float distance = across.magnitude;
            if (distance > reach)
            {
                across *= Mathf.Max(0.0f, reach) / distance;
            }
            Vector3 flat = Anchor(spike, cast) + across;
            pose.Rotation = Turn(spike, cast, out float leanAngle);
            float lift = (emergence - 1.0f) * height * GlacierSpikesRules.RiseDepth
                         - GlacierSpikesRules.Sunk(age) * (height + width + SinkPast)
                         - Mathf.Abs(Mathf.Sin(leanAngle)) * width * LeanSink;
            pose.Position = new Vector3(flat.x, groundY + lift, flat.z);
            pose.Size = new Vector3(width, height, width) * Mathf.Lerp(GlacierSpikesRules.RiseScale, 1.0f, Mathf.Min(1.0f, emergence));
            pose.Visible = pose.Position.y + height > groundY - 0.01f;
            return pose;
        }

        private static float SmoothStep(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3.0f - 2.0f * t);
        }
    }
}
