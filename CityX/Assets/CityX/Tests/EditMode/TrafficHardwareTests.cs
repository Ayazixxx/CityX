using System;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The STOP signs and traffic lights as the chunks build them in the real city (docs/TRAFFIC_CONTROL.md): where the standard puts them, and what each lamp is told.</summary>
    public sealed class TrafficHardwareTests
    {
        private const int Seed = 95998845;
        private const int Reach = 3;

        private static CityWorld _world;

        private static CityWorld World => _world ??= new CityWorld(Seed);

        private struct Lamp
        {
            public Vector3 Centre, Normal;
            public float Cycle, Offset, Start, Length;
            public int Flags;
        }

        private struct Vertex
        {
            public Vector3 At, Normal;
            public Color32 Look;
        }

        /// <summary>Everything the chunks round a junction build of signs and lights, in the world's own coordinates.</summary>
        private static void BuildAround(TrafficJunction junction, List<Lamp> lamps, List<Vertex> structures)
        {
            int cx = PlanWorld.ChunkOf(junction.Node.P.X), cy = PlanWorld.ChunkOf(junction.Node.P.Y);
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var built = new MeshBuffer();
            var markings = new MeshBuffer();
            var dividers = new MeshBuffer();
            var rail = new MeshBuffer();
            var signalLamps = new SignalLampBuffer();
            for (int dy = -Reach; dy <= Reach; dy++)
            {
                for (int dx = -Reach; dx <= Reach; dx++)
                {
                    ground.Reset();
                    pavement.Reset();
                    built.Reset();
                    markings.Reset();
                    dividers.Reset();
                    rail.Reset();
                    signalLamps.Reset();
                    var origin = new Vector3((cx + dx) * (float)PlanWorld.ChunkSize, 0.0f, (cy + dy) * (float)PlanWorld.ChunkSize);
                    CitySurfaceBuilder.Build(World, cx + dx, cy + dy, origin, ground, pavement, built, markings, dividers, rail, false, null, signalLamps);
                    for (int i = 0; i < built.Vertices.Count; i++)
                    {
                        structures.Add(new Vertex { At = built.Vertices[i] + origin, Normal = built.Normals[i], Look = built.Colors[i] });
                    }
                    for (int q = 0; q + 3 < signalLamps.Vertices.Count; q += 4)
                    {
                        Vector3 a = signalLamps.Vertices[q], b = signalLamps.Vertices[q + 1], c = signalLamps.Vertices[q + 2], d = signalLamps.Vertices[q + 3];
                        Vector4 cycle = signalLamps.Cycle[q], window = signalLamps.Window[q];
                        lamps.Add(new Lamp
                        {
                            Centre = (a + b + c + d) * 0.25f + origin,
                            Normal = Vector3.Cross(d - a, b - a).normalized,   // toward whoever the lamp faces
                            Cycle = cycle.z, Offset = cycle.w, Start = window.x, Length = window.y, Flags = Mathf.RoundToInt(window.z),
                        });
                    }
                }
            }
        }

        private static TrafficJunction Nearest(Func<TrafficJunction, bool> wanted)
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            TrafficJunction best = null;
            double nearest = double.MaxValue;
            foreach (TrafficJunction junction in World.Plan.Traffic.Junctions)
            {
                if (junction == null || !wanted(junction)) continue;
                double away = (junction.Node.P - new Vec2(sx, sz)).Length;
                if (away < nearest)
                {
                    nearest = away;
                    best = junction;
                }
            }
            Assert.IsNotNull(best, "the city has no such junction");
            return best;
        }

        private static int Driven(TrafficJunction junction)
        {
            int count = 0;
            foreach (bool drives in junction.Drives) if (drives) count++;
            return count;
        }

        private static Vector3 Flat(Vec2 v) => new Vector3((float)v.X, 0.0f, (float)v.Y);

        private static float Ground(Vec2 at) => (float)World.Plan.GroundHeight(at.X, at.Y);

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private const int Lens = SignalLampBuffer.Glow | SignalLampBuffer.Square;

        [Test]
        public void EveryApproachToTheLightsFacesTwoOrMoreHeadsOnTheFarSideOfTheJunction()
        {
            foreach (bool mainRoad in new[] { true, false })
            {
                TrafficJunction junction = Nearest(j => j.Control == JunctionControl.Signal && Driven(j) == 4 && (j.ClassOf(true) >= RoadClass.Arterial) == mainRoad);
                var lamps = new List<Lamp>();
                BuildAround(junction, lamps, new List<Vertex>());
                SignalTiming timing = junction.Timing;
                for (int leg = 0; leg < junction.Legs.Length; leg++)
                {
                    if (!junction.Drives[leg]) continue;
                    Vector3 back = Flat(junction.Dirs[leg]);
                    SignalWindow green = timing.Vehicle(junction.Major[leg], SignalLamp.Green);
                    int heads = 0, overTheRoad = 0;
                    foreach (Lamp lamp in lamps)
                    {
                        if ((lamp.Flags & Lens) != 0 || Vector3.Dot(lamp.Normal, back) < 0.9f) continue;
                        if (Mathf.Abs(lamp.Start - (float)green.Start) > 0.01f || Mathf.Abs(lamp.Length - (float)green.Length) > 0.01f) continue;
                        Vector3 from = lamp.Centre - Flat(junction.Node.P);
                        if ((new Vector2(from.x, from.z)).magnitude > 60.0f) continue;   // another junction's
                        heads++;
                        Assert.Greater(Vector3.Dot(from, -back), 0.0f, $"node {junction.Node.Id} leg {leg}: a head on the near side of the junction");
                        float up = lamp.Centre.y - Ground(junction.Node.P);
                        Assert.Greater(up, 3.5f, $"node {junction.Node.Id} leg {leg}: a head's green lens only {up:F1} m up");
                        if (up > 6.0f) overTheRoad++;
                        Assert.AreEqual((float)timing.Cycle, lamp.Cycle, 0.01f);
                        Assert.AreEqual((float)timing.Offset, lamp.Offset, 0.01f);
                    }
                    Assert.GreaterOrEqual(heads, 2, $"node {junction.Node.Id} leg {leg} ({junction.Legs[leg].Class}): {heads} heads face it (MUTCD 4D.11: two)");
                    if (junction.Legs[leg].Class >= RoadClass.Arterial)
                    {
                        Assert.GreaterOrEqual(overTheRoad, 2, $"node {junction.Node.Id} leg {leg}: a main road without heads over its lanes");
                    }
                }
            }
        }

        [Test]
        public void EachHeadHasRedOverYellowOverGreenAndOnlyTheRightOnesFlashAtNight()
        {
            TrafficJunction junction = Nearest(j => j.Control == JunctionControl.Signal && j.Timing.NightFlash && Driven(j) == 4);
            var lamps = new List<Lamp>();
            BuildAround(junction, lamps, new List<Vertex>());
            SignalTiming timing = junction.Timing;
            int flashing = 0;
            foreach (Lamp lamp in lamps)
            {
                if ((lamp.Flags & Lens) != 0) continue;
                Assert.AreNotEqual(0, lamp.Flags & SignalLampBuffer.NightFlashJunction, "a lamp of a junction that flashes at night does not know it");
                if ((lamp.Flags & SignalLampBuffer.FlashesAtNight) == 0) continue;
                flashing++;
                // Yellow to the main street, red to the side street (MUTCD 4D.30).
                bool mainYellow = Near(lamp, timing.Vehicle(true, SignalLamp.Yellow)), sideRed = Near(lamp, timing.Vehicle(false, SignalLamp.Red));
                Assert.IsTrue(mainYellow || sideRed, "a lamp flashes at night that is neither the main street's yellow nor the side street's red");
            }
            Assert.Greater(flashing, 3);

            // Red on top: of the lenses of one head (the same place across, different heights), the red is the highest and the green the lowest.
            foreach (Lamp red in lamps)
            {
                if ((red.Flags & Lens) != 0 || !Near(red, timing.Vehicle(true, SignalLamp.Red))) continue;
                foreach (Lamp green in lamps)
                {
                    if ((green.Flags & Lens) != 0 || !Near(green, timing.Vehicle(true, SignalLamp.Green))) continue;
                    Vector3 apart = green.Centre - red.Centre;
                    if (new Vector2(apart.x, apart.z).magnitude < 0.05f)
                    {
                        Assert.Less(green.Centre.y, red.Centre.y, "a head with its green over its red");
                    }
                }
            }
        }

        private static bool Near(in Lamp lamp, SignalWindow window) => Mathf.Abs(lamp.Start - (float)window.Start) < 0.01f && Mathf.Abs(lamp.Length - (float)window.Length) < 0.01f;

        [Test]
        public void EveryCrossingAtTheLightsHasAWalkSignalAtEachEnd()
        {
            TrafficJunction junction = Nearest(j => j.Control == JunctionControl.Signal && Driven(j) == 4 && j.Legs.Length == 4);
            var lamps = new List<Lamp>();
            BuildAround(junction, lamps, new List<Vertex>());
            SignalTiming timing = junction.Timing;
            foreach (bool crossesMain in new[] { true, false })
            {
                SignalWindow walk = timing.Pedestrian(crossesMain, WalkLamp.Walk);
                int figures = 0;
                foreach (Lamp lamp in lamps)
                {
                    Vector3 from = lamp.Centre - Flat(junction.Node.P);
                    if ((lamp.Flags & SignalLampBuffer.Square) == 0 || !Near(lamp, walk) || new Vector2(from.x, from.z).magnitude > 60.0f) continue;
                    figures++;
                    float up = lamp.Centre.y - Ground(junction.Node.P);
                    Assert.Greater(up, 2.6f, "a walk signal under 7 ft (MUTCD 4E.05)");
                    Assert.Less(up, 4.3f, "a walk signal over 10 ft");
                }
                // A signal at each end of every crossing of that street, five parts to a walking figure.
                int crossings = 0;
                for (int leg = 0; leg < junction.Legs.Length; leg++)
                {
                    EdgeGeometry g = World.Plan.Geometry.Edges[junction.Legs[leg].Id];
                    if (junction.Drives[leg] && junction.Major[leg] == crossesMain
                        && CitySurfaceBuilder.HasCrossing(World.Plan, g, junction.Legs[leg].A == junction.Node ? 0 : 1))
                    {
                        crossings++;
                    }
                }
                Assert.Greater(crossings, 0, $"node {junction.Node.Id}: no crossing of the {(crossesMain ? "main" : "side")} street at all");
                Assert.AreEqual(crossings * 2 * 5, figures, $"node {junction.Node.Id}: walk signals for crossing the {(crossesMain ? "main" : "side")} street");
            }
        }

        [Test]
        public void AStopSignStandsOnTheRightOfItsApproachFacingItAndNoneFacesTheStreetThatRunsFree()
        {
            foreach (JunctionControl control in new[] { JunctionControl.StopMinor, JunctionControl.AllWayStop })
            {
                TrafficJunction junction = Nearest(j => j.Control == control && j.Legs.Length == Driven(j));
                var structures = new List<Vertex>();
                BuildAround(junction, new List<Lamp>(), structures);
                for (int leg = 0; leg < junction.Legs.Length; leg++)
                {
                    Vec2 d = junction.Dirs[leg];
                    int faces = 0;
                    foreach (Vertex vertex in structures)
                    {
                        if (!Same(vertex.Look, TrafficProps.StopRed)) continue;
                        Vec2 at = new Vec2(vertex.At.x, vertex.At.z) - junction.Node.P;
                        double along = Vec2.Dot(at, d), aside = Vec2.Dot(at, d.Perp);
                        // Beside this leg, on the right of the traffic coming in along it.
                        if (along < junction.Trims[leg] || along > junction.Trims[leg] + 9.0 || aside < junction.HalfWidths[leg] || aside > junction.HalfWidths[leg] + 2.0) continue;
                        faces++;
                        Assert.Greater(Vector3.Dot(vertex.Normal, Flat(d)), 0.9f, $"node {junction.Node.Id} leg {leg}: the sign does not face the traffic it stops");
                        float up = vertex.At.y - Ground(junction.Node.P + d * along + d.Perp * aside);
                        Assert.Greater(up, 2.5f, "a sign under 7 ft (MUTCD 2A.18)");
                    }
                    if (junction.Stops(leg))
                    {
                        // (A street too short for its stop line may have no room for its sign either.)
                        EdgeGeometry g = World.Plan.Geometry.Edges[junction.Legs[leg].Id];
                        if (CitySurfaceBuilder.StopLineAt(World.Plan, g, junction.Legs[leg].A == junction.Node ? 0 : 1) >= 0.0)
                        {
                            Assert.Greater(faces, 0, $"node {junction.Node.Id} leg {leg} ({control}): stops, and has no sign");
                        }
                    }
                    else
                    {
                        Assert.AreEqual(0, faces, $"node {junction.Node.Id} leg {leg} ({control}): runs free, and has a STOP sign");
                    }
                }
            }
        }

        [Test]
        public void NoPoleOrPostStandsInTheRoad()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            int stood = 0;
            foreach (TrafficJunction junction in World.Plan.Traffic.Junctions)
            {
                if (junction == null || junction.Control == JunctionControl.None || (junction.Node.P - new Vec2(sx, sz)).Length > 320.0) continue;
                var structures = new List<Vertex>();
                BuildAround(junction, new List<Lamp>(), structures);
                foreach (Vertex vertex in structures)
                {
                    if (!Same(vertex.Look, TrafficProps.Galvanised)) continue;
                    Vec2 at = new Vec2(vertex.At.x, vertex.At.z);
                    if ((at - junction.Node.P).Length > 45.0 || vertex.At.y - Ground(at) > 2.0f) continue;   // only what stands on the ground here
                    stood++;
                    for (int leg = 0; leg < junction.Legs.Length; leg++)
                    {
                        Vec2 from = at - junction.Node.P;
                        double along = Vec2.Dot(from, junction.Dirs[leg]), aside = Math.Abs(Vec2.Dot(from, junction.Dirs[leg].Perp));
                        if (along > 0.0 && along < junction.Legs[leg].Length)
                        {
                            Assert.Greater(aside, junction.HalfWidths[leg] + 0.2, $"node {junction.Node.Id} ({junction.Control}): a pole {aside:F1} m from the middle of a road {junction.HalfWidths[leg]:F1} m to its kerb");
                        }
                    }
                }
            }
            Assert.Greater(stood, 100, "hardly a pole near the spawn");
        }

        [Test]
        public void TheStopLineIsBeforeTheCrossingAndTheMainStreetHasNeitherAtASideStreetsStop()
        {
            PlanWorld plan = World.Plan;
            int lines = 0;
            foreach (EdgeGeometry g in plan.Geometry.Edges)
            {
                if (g == null) continue;
                for (int end = 0; end < 2; end++)
                {
                    if (!plan.Traffic.TryApproach(g.Edge, end == 0, out TrafficJunction junction, out int leg)) continue;
                    double line = CitySurfaceBuilder.StopLineAt(plan, g, end);
                    double trim = end == 0 ? g.TrimA : g.TrimB;
                    bool governed = junction.Stops(leg) || junction.Signalled(leg);
                    if (!governed)
                    {
                        Assert.Less(line, 0.0, "a stop line where nothing stops");
                        if (junction.Control == JunctionControl.StopMinor)
                        {
                            Assert.IsFalse(CitySurfaceBuilder.HasCrossing(plan, g, end), "a zebra across the street that runs free");
                        }
                        continue;
                    }
                    if (line < 0.0) continue;   // too short a road for a line
                    lines++;
                    if (CitySurfaceBuilder.HasCrossing(plan, g, end))
                    {
                        Assert.GreaterOrEqual(line, trim + CitySurfaceBuilder.CrossingTo + CitySurfaceBuilder.StopLineBeforeCrossing - 1e-9, "the stop line is not 4 ft before the crossing (MUTCD 3B.16)");
                    }
                    if (junction.Signalled(leg) && trim >= 1.0 && g.Length - trim - CitySurfaceBuilder.CrossingTo >= 0.5)
                    {
                        Assert.IsTrue(CitySurfaceBuilder.HasCrossing(plan, g, end), "lights without a crossing");
                    }
                }
            }
            Assert.Greater(lines, 1000);
        }

        /// <summary>The kerb goes round each corner in an arc (KerbCorners): the plan's sharp corner is out in the road, and what stood back from it stood in the road.</summary>
        [Test]
        public void NoPoleOrPostStandsInTheRoadARoundedCornerGivesUp()
        {
            foreach (JunctionControl control in new[] { JunctionControl.Signal, JunctionControl.StopMinor, JunctionControl.AllWayStop })
            {
                TrafficJunction junction = Nearest(j => j.Control == control && j.Legs.Length == Driven(j));
                var structures = new List<Vertex>();
                BuildAround(junction, new List<Lamp>(), structures);
                int rounded = 0;
                for (int c = 0; c < junction.Legs.Length; c++)
                {
                    if (!junction.HasCorner[c] || !KerbCorners.TryAt(World.Plan, junction.Corners[c], out KerbArc arc))
                    {
                        continue;
                    }
                    rounded++;
                    foreach (Vertex vertex in structures)
                    {
                        var at = new Vec2(vertex.At.x, vertex.At.z);
                        // A pole's or a post's foot: low, and near this corner. (An arm or a head may well hang out over the road.)
                        if (!Same(vertex.Look, TrafficProps.Galvanised) || (at - arc.Corner).Length > arc.TangentLength + 1.0 || vertex.At.y - Ground(at) > 1.0f)
                        {
                            continue;
                        }
                        Assert.IsFalse(arc.GivesUp(at), $"node {junction.Node.Id} corner {c} ({control}): a pole or post stands in the road the rounded corner gives up");
                    }
                }
                TestContext.WriteLine($"{control}: node {junction.Node.Id}, {rounded} rounded corners");
            }
        }
    }
}
