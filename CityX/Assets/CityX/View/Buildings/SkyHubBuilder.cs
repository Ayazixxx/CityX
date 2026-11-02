using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Builds the sky hub (SkyHubPlan, docs/SKY_HUB.md) under a transform at the island's middle, level with the stair's foot: the landing on the
    /// ground, the stair round the island's edge in lengths of its own (each drawn, culled and collided alone), the flat landing at its top, and the
    /// island: a green top with a stone walk round it, a low wall with a gap where the stair comes up, and rock tapering away under it.
    /// </summary>
    public static class SkyHubBuilder
    {
        /// <summary>How high the walls along the stair and round the island stand over the floor, and how thick (a body of 2.6 m: waist high).</summary>
        public const float WallHeight = 1.3f;

        public const float WallThickness = 0.5f;

        /// <summary>How long the flat at the stair's top is, along the island's edge, metres.</summary>
        public const float TopLandingLength = 30.0f;

        /// <summary>How wide the stone walk round the island's edge is.</summary>
        private const float RimWalkWidth = 14.0f;

        /// <summary>The cliff at the island's edge, before the rock starts to taper away.</summary>
        private const float CliffHeight = 22.0f;

        /// <summary>How far below the island's top the rock's lowest point hangs.</summary>
        public const float RockDepth = 380.0f;

        private const int StepsPerLength = 128;
        private const int StepsPerWallPiece = 16;
        private const int RimSegments = 192;
        private const int RockSegments = 64;
        private const int RockRings = 12;

        private static readonly Color32 StairLook = SurfaceLook.Pack(new Color(0.74f, 0.72f, 0.68f), (int)WallMaterial.Stone, 0.2f);
        private static readonly Color32 WallLook = SurfaceLook.Pack(new Color(0.62f, 0.60f, 0.57f), (int)WallMaterial.Stone, 0.3f);
        private static readonly Color32 PavingLook = SurfaceLook.Pack(new Color(0.70f, 0.68f, 0.64f), SurfaceLook.Paving, 0.2f);
        private static readonly Color32 GrassLook = SurfaceLook.Pack(new Color(0.38f, 0.54f, 0.28f), SurfaceLook.Earth, 0.1f);
        private static readonly Color32 RockLook = SurfaceLook.Pack(new Color(0.30f, 0.27f, 0.25f), (int)WallMaterial.Stone, 0.5f);

        public static GameObject Build(Transform parent, Material structure, in SkyHubPlan hub)
        {
            var root = new GameObject("SkyHub");
            root.transform.SetParent(parent, false);

            var landing = new MeshBuffer();
            Landing(landing, hub);
            OldGuysHouseBuilder.Emit(root.transform, "Landing", landing, structure, true, true);

            Stair(root.transform, structure, hub);

            var top = new MeshBuffer();
            TopLanding(top, hub);
            IslandTop(top);
            RimWall(top, hub);
            OldGuysHouseBuilder.Emit(root.transform, "IslandTop", top, structure, true, true);

            var rock = new MeshBuffer();
            Rock(rock);
            OldGuysHouseBuilder.Emit(root.transform, "Rock", rock, structure, true, true);
            return root;
        }

        private static float Height => (float)SkyHubPlan.IslandHeight;

        private static float Inner => (float)SkyHubPlan.StairInnerRadius;

        private static float Outer => (float)SkyHubPlan.StairOuterRadius;

        private static float StepAngle(in SkyHubPlan hub) => (float)(hub.Direction * 2.0 * Mathf.PI * SkyHubPlan.StairTurns / SkyHubPlan.StepCount);

        private static Vector3 At(float angle, float radius, float y) => new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle));

        private static Vector3 Outward(float angle) => new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));

        /// <summary>The way the stair climbs at <paramref name="angle"/>, level.</summary>
        private static Vector3 Along(float angle, int direction) => new Vector3(-Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * direction;

        /// <summary>A quad with its UVs in metres, as the city's walls are, along its bottom edge and up its side.</summary>
        private static void Quad(MeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            float w = Vector3.Distance(a, b), h = Vector3.Distance(a, d);
            buffer.AddQuad(a, b, c, d, normal, new Vector2(0.0f, 0.0f), new Vector2(w, 0.0f), new Vector2(w, h), new Vector2(0.0f, h));
        }

        // ---- the landing ----

        private static void Landing(MeshBuffer buffer, in SkyHubPlan hub)
        {
            buffer.Look = PavingLook;
            float half = (float)SkyHubPlan.LandingHalfWidth, x0 = -(float)SkyHubPlan.LandingLength, x1 = (float)SkyHubPlan.LowRun;
            float foot = (float)(hub.LowestGround - hub.FootHeight) - 0.3f;
            var centre = new Vec2(hub.CentreX, hub.CentreZ);
            Vector3 Corner(double x, double z, float y, in SkyHubPlan plan)
            {
                Vec2 p = plan.LandingPoint(x, z) - centre;
                return new Vector3((float)p.X, y, (float)p.Y);
            }
            Vector3 along = new Vector3((float)hub.FootTangent.X, 0.0f, (float)hub.FootTangent.Y);
            Vector3 toIsland = new Vector3((float)hub.FootInward.X, 0.0f, (float)hub.FootInward.Y);
            Quad(buffer, Corner(x0, -half, 0.0f, hub), Corner(x1, -half, 0.0f, hub), Corner(x1, half, 0.0f, hub), Corner(x0, half, 0.0f, hub), Vector3.up);
            Quad(buffer, Corner(x0, -half, foot, hub), Corner(x1, -half, foot, hub), Corner(x1, -half, 0.0f, hub), Corner(x0, -half, 0.0f, hub), -toIsland);
            Quad(buffer, Corner(x0, half, foot, hub), Corner(x1, half, foot, hub), Corner(x1, half, 0.0f, hub), Corner(x0, half, 0.0f, hub), toIsland);
            Quad(buffer, Corner(x0, -half, foot, hub), Corner(x0, half, foot, hub), Corner(x0, half, 0.0f, hub), Corner(x0, -half, 0.0f, hub), -along);
            Quad(buffer, Corner(x1, -half, foot, hub), Corner(x1, half, foot, hub), Corner(x1, half, 0.0f, hub), Corner(x1, -half, 0.0f, hub), along);
        }

        // ---- the stair ----

        private static void Stair(Transform root, Material structure, in SkyHubPlan hub)
        {
            int steps = SkyHubPlan.StepCount;
            float riser = (float)SkyHubPlan.Riser, stepAngle = StepAngle(hub), start = (float)hub.StartAngle;
            for (int first = 0; first < steps; first += StepsPerLength)
            {
                int end = Mathf.Min(first + StepsPerLength, steps);
                var buffer = new MeshBuffer();
                for (int k = first; k < end; k++)
                {
                    AddStep(buffer, start + k * stepAngle, stepAngle, k * riser, riser, hub.Direction);
                }
                for (int k = first; k < end; k += StepsPerWallPiece)
                {
                    int to = Mathf.Min(k + StepsPerWallPiece, end);
                    AddSides(buffer, start + k * stepAngle, start + to * stepAngle, k * riser, to * riser, riser, inner: true);
                }
                if (first == 0)
                {
                    EndOfWalls(buffer, start, 0.0f, -hub.Direction, inner: true);
                }
                if (end == steps)
                {
                    EndOfWalls(buffer, start + steps * stepAngle, Height, hub.Direction, inner: true, outer: false);
                }
                OldGuysHouseBuilder.Emit(root, "Stair " + first, buffer, structure, true, true);
            }
        }

        /// <summary>One step: its riser where the step before ends, its tread level at the top of it.</summary>
        private static void AddStep(MeshBuffer buffer, float angle, float stepAngle, float y, float riser, int direction)
        {
            buffer.Look = StairLook;
            float next = angle + stepAngle, top = y + riser;
            Quad(buffer, At(angle, Inner, top), At(angle, Outer, top), At(next, Outer, top), At(next, Inner, top), Vector3.up);
            Quad(buffer, At(angle, Inner, y), At(angle, Outer, y), At(angle, Outer, top), At(angle, Inner, top), -Along(angle, direction));
        }

        /// <summary>A length of the walls either side and the deck under the steps between two angles, rising from <paramref name="y0"/> to <paramref name="y1"/>.</summary>
        private static void AddSides(MeshBuffer buffer, float a0, float a1, float y0, float y1, float riser, bool inner)
        {
            buffer.Look = WallLook;
            float mid = (a0 + a1) * 0.5f;
            Vector3 outward = Outward(mid);
            float deck = (float)SkyHubPlan.DeckThickness, wall = WallHeight;

            // The outer wall: its outside runs from under the deck to its top, its inside from the steps up.
            WallFace(buffer, a0, a1, Outer, y0 - deck, y1 - deck, y0 + wall, y1 + wall, outward);
            WallFace(buffer, a0, a1, Outer - WallThickness, y0 - riser, y1 - riser, y0 + wall, y1 + wall, -outward);
            WallTop(buffer, a0, a1, Outer - WallThickness, Outer, y0 + wall, y1 + wall);
            if (inner)
            {
                WallFace(buffer, a0, a1, Inner, y0 - deck, y1 - deck, y0 + wall, y1 + wall, -outward);
                WallFace(buffer, a0, a1, Inner + WallThickness, y0 - riser, y1 - riser, y0 + wall, y1 + wall, outward);
                WallTop(buffer, a0, a1, Inner, Inner + WallThickness, y0 + wall, y1 + wall);
            }

            // Under the deck.
            Quad(buffer, At(a0, Inner, y0 - deck), At(a0, Outer, y0 - deck), At(a1, Outer, y1 - deck), At(a1, Inner, y1 - deck), Vector3.down);
        }

        private static void WallFace(MeshBuffer buffer, float a0, float a1, float radius, float low0, float low1, float high0, float high1, Vector3 normal)
        {
            Quad(buffer, At(a0, radius, low0), At(a1, radius, low1), At(a1, radius, high1), At(a0, radius, high0), normal);
        }

        private static void WallTop(MeshBuffer buffer, float a0, float a1, float r0, float r1, float y0, float y1)
        {
            Quad(buffer, At(a0, r0, y0), At(a0, r1, y0), At(a1, r1, y1), At(a1, r0, y1), Vector3.up);
        }

        /// <summary>The square ends of the walls where they stop, at <paramref name="angle"/>, seen from the way <paramref name="facing"/> says round the island.</summary>
        private static void EndOfWalls(MeshBuffer buffer, float angle, float y, int facing, bool inner, bool outer = true)
        {
            buffer.Look = WallLook;
            Vector3 normal = Along(angle, facing);
            float low = y - (float)SkyHubPlan.DeckThickness, high = y + WallHeight;
            if (outer)
            {
                Quad(buffer, At(angle, Outer - WallThickness, low), At(angle, Outer, low), At(angle, Outer, high), At(angle, Outer - WallThickness, high), normal);
            }
            if (inner)
            {
                Quad(buffer, At(angle, Inner, low), At(angle, Inner + WallThickness, low), At(angle, Inner + WallThickness, high), At(angle, Inner, high), normal);
            }
        }

        // ---- the top of the stair and the island ----

        /// <summary>The angle round the island where the stair comes up, and how far round the flat at its top goes (signed the way the stair climbs).</summary>
        private static void TopLandingAngles(in SkyHubPlan hub, out float from, out float sweep)
        {
            from = (float)hub.StartAngle + SkyHubPlan.StepCount * StepAngle(hub);
            sweep = hub.Direction * TopLandingLength / (float)SkyHubPlan.StairMidRadius;
        }

        /// <summary>The flat at the stair's top: on past the last step, level with the island and running a little onto it, its outer wall carried on, its far end walled.</summary>
        private static void TopLanding(MeshBuffer buffer, in SkyHubPlan hub)
        {
            TopLandingAngles(hub, out float from, out float sweep);
            const int pieces = 8;
            const float onToTheIsland = 1.0f, lift = 0.01f;
            float floor = Height + lift, deck = (float)SkyHubPlan.DeckThickness;
            for (int i = 0; i < pieces; i++)
            {
                float a0 = from + sweep * i / pieces, a1 = from + sweep * (i + 1) / pieces;
                buffer.Look = StairLook;
                Quad(buffer, At(a0, Inner - onToTheIsland, floor), At(a0, Outer, floor), At(a1, Outer, floor), At(a1, Inner - onToTheIsland, floor), Vector3.up);
                AddSides(buffer, a0, a1, Height, Height, 0.0f, inner: false);
            }
            // Its far end, walled across.
            float end = from + sweep;
            buffer.Look = WallLook;
            Vector3 onward = Along(end, hub.Direction);
            Quad(buffer, At(end, Inner, Height - deck), At(end, Outer, Height - deck), At(end, Outer, Height + WallHeight), At(end, Inner, Height + WallHeight), onward);
            Quad(buffer, At(end, Inner, floor), At(end, Outer, floor), At(end, Outer, Height + WallHeight), At(end, Inner, Height + WallHeight), -onward);
        }

        private static void IslandTop(MeshBuffer buffer)
        {
            float radius = (float)SkyHubPlan.IslandRadius, walk = radius - RimWalkWidth;
            for (int i = 0; i < RimSegments; i++)
            {
                float a0 = 2.0f * Mathf.PI * i / RimSegments, a1 = 2.0f * Mathf.PI * (i + 1) / RimSegments;
                buffer.Look = PavingLook;
                Quad(buffer, At(a0, walk, Height), At(a0, radius, Height), At(a1, radius, Height), At(a1, walk, Height), Vector3.up);
                buffer.Look = GrassLook;
                Vector3 c = new Vector3(0.0f, Height, 0.0f), p = At(a0, walk, Height), q = At(a1, walk, Height);
                buffer.AddTriangle(c, p, q, Vector3.up, new Vector2(c.x, c.z), new Vector2(p.x, p.z), new Vector2(q.x, q.z));
            }
        }

        /// <summary>The low wall round the island's edge, open where the stair comes up.</summary>
        private static void RimWall(MeshBuffer buffer, in SkyHubPlan hub)
        {
            TopLandingAngles(hub, out float from, out float sweep);
            float radius = (float)SkyHubPlan.IslandRadius, top = Height + WallHeight, inner = radius - WallThickness;
            float open0 = Mathf.Min(from, from + sweep), openWidth = Mathf.Abs(sweep);
            buffer.Look = WallLook;
            bool Open(float angle)
            {
                float into = Mathf.Repeat(angle - open0, 2.0f * Mathf.PI);
                return into < openWidth;
            }
            for (int i = 0; i < RimSegments; i++)
            {
                float a0 = 2.0f * Mathf.PI * i / RimSegments, a1 = 2.0f * Mathf.PI * (i + 1) / RimSegments, mid = (a0 + a1) * 0.5f;
                if (Open(mid))
                {
                    continue;
                }
                Vector3 outward = Outward(mid);
                Quad(buffer, At(a0, radius, Height), At(a1, radius, Height), At(a1, radius, top), At(a0, radius, top), outward);
                Quad(buffer, At(a0, inner, Height), At(a1, inner, Height), At(a1, inner, top), At(a0, inner, top), -outward);
                Quad(buffer, At(a0, inner, top), At(a0, radius, top), At(a1, radius, top), At(a1, inner, top), Vector3.up);
                if (Open(a0 - 1e-4f))
                {
                    Quad(buffer, At(a0, inner, Height), At(a0, radius, Height), At(a0, radius, top), At(a0, inner, top), Along(a0, -1));
                }
                if (Open(a1 + 1e-4f))
                {
                    Quad(buffer, At(a1, inner, Height), At(a1, radius, Height), At(a1, radius, top), At(a1, inner, top), Along(a1, 1));
                }
            }
        }

        /// <summary>The rock under the island: a cliff at the edge, then tapering to a rough point, flat faced.</summary>
        private static void Rock(MeshBuffer buffer)
        {
            buffer.Look = RockLook;
            float radius = (float)SkyHubPlan.IslandRadius - 0.1f;
            for (int j = 0; j < RockRings; j++)
            {
                for (int i = 0; i < RockSegments; i++)
                {
                    Vector3 a = RockPoint(i, j, radius), b = RockPoint(i + 1, j, radius), c = RockPoint(i + 1, j + 1, radius), d = RockPoint(i, j + 1, radius);
                    RockFace(buffer, a, b, c);
                    RockFace(buffer, a, c, d);
                }
            }
        }

        /// <summary>One flat face of the rock. The rings run round and down in one order, so the way a face looks is the way its corners run, however craggy it is.</summary>
        private static void RockFace(MeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude > 1e-6f)
            {
                buffer.AddTriangle(a, b, c, normal.normalized, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z));
            }
        }

        /// <summary>A point on the rock: ring 0 at the island's edge, ring 1 at the cliff's foot, the last at the rough point, each a little uneven (never outside the edge, where the stair goes).</summary>
        private static Vector3 RockPoint(int segment, int ring, float radius)
        {
            float angle = 2.0f * Mathf.PI * segment / RockSegments;
            if (ring == 0)
            {
                return At(angle, radius, Height);
            }
            float t = (ring - 1f) / (RockRings - 1f);
            float cx = Mathf.Cos(angle), cz = Mathf.Sin(angle);
            // Craggy in how far out each point stands; each ring stays level, so no ring folds over the next (a fold shows the sky through the rock).
            float rough = Mathf.PerlinNoise(cx * 2.5f + 7.0f + ring * 0.31f, cz * 2.5f + 3.0f + ring * 0.23f);
            float fine = Mathf.PerlinNoise(cx * 9.0f + 19.0f + ring * 0.71f, cz * 9.0f + 31.0f + ring * 0.47f);
            float uneven = t > 0.0f ? rough * 0.7f + fine * 0.5f : 0.0f;
            float shape = Mathf.Pow(1.0f - t, 0.6f) * (1.0f - 0.34f * uneven);
            return At(angle, radius * shape, Height - CliffHeight - (RockDepth - CliffHeight) * t);
        }
    }
}
