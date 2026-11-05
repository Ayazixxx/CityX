using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What stands along the sidewalks (World/StreetFurniture, docs/AMERICAN_STREETS.md): where the rule stands each thing along the real
    /// city's streets (in the strip by the kerb, clear of the corners, of the street lights and of each other), and what the chunks build
    /// there (the thing itself on the pavement, a parking stall's mark in the road at each meter's line).
    /// </summary>
    public sealed class StreetFurnitureTests
    {
        private const int ReachChunks = 8;

        private static float HeightOf(StreetFurnitureKind kind)
        {
            switch (kind)
            {
                case StreetFurnitureKind.Hydrant: return CityProps.HydrantHeight;
                case StreetFurnitureKind.Meter: return CityProps.MeterHeight;
                case StreetFurnitureKind.Mailbox: return CityProps.MailboxHeight;
                case StreetFurnitureKind.NewsBoxes: return CityProps.NewsBoxHeight;
                case StreetFurnitureKind.Tree: return 5.0f;
                default: return 1.1f;
            }
        }

        [Test]
        public void EverythingStandsInTheStripByTheKerbClearOfCornersLampsAndEachOther()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var things = new List<StreetFurnishing>();
            var lamps = new List<StreetLamp>();
            var count = new Dictionary<StreetFurnitureKind, int>();
            int furnished = 0, treeLined = 0;
            foreach (EdgeGeometry g in SpawnStreets.Roads(ReachChunks))
            {
                things.Clear();
                StreetFurniture.Along(plan, g, things);
                string where = $"road {g.Edge.Id}";
                if (g.Edge.Bridge || PedestrianStreets.Is(plan, g.Edge) || StepStreets.Is(plan, g.Edge))
                {
                    Assert.IsEmpty(things, $"{where}: a street that is walked, climbed or carried on a bridge is furnished like a driven one");
                    continue;
                }
                if (things.Count == 0)
                {
                    continue;
                }
                furnished++;
                if (StreetFurniture.TreeLined(plan, g.Edge))
                {
                    treeLined++;
                }
                lamps.Clear();
                StreetLights.Along(plan, g, lamps);
                for (int i = 0; i < things.Count; i++)
                {
                    StreetFurnishing thing = things[i];
                    count[thing.Kind] = count.TryGetValue(thing.Kind, out int had) ? had + 1 : 1;
                    Vec2 from = thing.At - g.A;
                    double along = Vec2.Dot(from, g.Dir), aside = Vec2.Dot(from, g.Dir.Perp);
                    double fromKerb = System.Math.Abs(aside) - g.HalfWidth;
                    Assert.That(fromKerb, Is.InRange(0.3, 1.2), $"{where}: a {thing.Kind} is not in the strip by the kerb");
                    Assert.GreaterOrEqual(along, g.TrimA + StreetFurniture.CornerClear - 1e-6, $"{where}: a {thing.Kind} stands at the corner");
                    Assert.LessOrEqual(along, g.Length - g.TrimB - StreetFurniture.CornerClear + 1e-6, $"{where}: a {thing.Kind} stands at the corner");
                    Assert.Greater(Vec2.Dot(thing.ToRoad, g.Dir.Perp * -System.Math.Sign(aside)), 0.99, $"{where}: a {thing.Kind} does not know where the road is");
                    if (thing.Kind == StreetFurnitureKind.Tree)
                    {
                        Assert.IsTrue(StreetFurniture.TreeLined(plan, g.Edge), $"{where}: a tree on a street that has none");
                    }
                    if (thing.Kind == StreetFurnitureKind.Meter)
                    {
                        Assert.IsTrue(StreetFurniture.Metered(plan, g.Edge, System.Math.Sign(aside)), $"{where}: a meter on a side with no stalls");
                    }
                    foreach (StreetLamp lamp in lamps)
                    {
                        Assert.Greater((lamp.At - thing.At).Length, 0.8, $"{where}: a {thing.Kind} stands in a street light's pole");
                    }
                    for (int j = 0; j < i; j++)
                    {
                        Assert.Greater((things[j].At - thing.At).Length, 0.8, $"{where}: a {thing.Kind} and a {things[j].Kind} stand in each other");
                    }
                }
            }
            var summary = new List<string>();
            foreach (KeyValuePair<StreetFurnitureKind, int> pair in count)
            {
                summary.Add($"{pair.Value} {pair.Key}");
            }
            TestContext.WriteLine($"{furnished} furnished streets round the spawn ({treeLined} lined with trees): {string.Join(", ", summary)}");
            foreach (StreetFurnitureKind kind in System.Enum.GetValues(typeof(StreetFurnitureKind)))
            {
                Assert.IsTrue(count.TryGetValue(kind, out int n) && n >= 3, $"hardly a {kind} stands round the spawn");
            }
            Assert.Less(treeLined, furnished * 0.7, "nearly every street is lined with trees: some are meant to be bare");
        }

        [Test]
        public void EachThingIsBuiltStandingOnThePavement()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var things = new List<StreetFurnishing>();
            var looked = new Dictionary<StreetFurnitureKind, int>();
            foreach (EdgeGeometry g in SpawnStreets.Roads(ReachChunks))
            {
                things.Clear();
                StreetFurniture.Along(plan, g, things);
                foreach (StreetFurnishing thing in things)
                {
                    looked.TryGetValue(thing.Kind, out int had);
                    // A couple of each kind, each well inside one chunk so one build holds all of it.
                    double size = PlanWorld.ChunkSize;
                    double inX = thing.At.X - System.Math.Floor(thing.At.X / size) * size, inZ = thing.At.Y - System.Math.Floor(thing.At.Y / size) * size;
                    if (had >= 2 || inX < 4.0 || inX > size - 4.0 || inZ < 4.0 || inZ > size - 4.0 || plan.OnTutorialLot(thing.At.X, thing.At.Y, 1.0))
                    {
                        continue;
                    }
                    SpawnStreets.Surface built = SpawnStreets.BuildAt(thing.At);
                    float pavement = SpawnStreets.PavementHeight(thing.At);
                    float lowest = float.MaxValue, highest = float.MinValue;
                    // Near enough for its own corners, not a neighbour's (a lamp's base is sunk).
                    double round = thing.Kind == StreetFurnitureKind.Cabinet ? 1.0 : (thing.Kind == StreetFurnitureKind.Bin ? 0.7 : 0.5);
                    foreach (Vector3 local in built.Structures.Vertices)
                    {
                        if ((built.InTheWorld(local) - thing.At).Length < round)
                        {
                            lowest = Mathf.Min(lowest, local.y);
                            highest = Mathf.Max(highest, local.y);
                        }
                    }
                    if (lowest == float.MaxValue)
                    {
                        continue;   // not stood here: the pavement is cut away under it, or a stairway opens here
                    }
                    string where = $"the {thing.Kind} at {thing.At.X:0},{thing.At.Y:0}";
                    Assert.AreEqual(pavement, lowest, 0.02f, $"{where} does not stand on the pavement");
                    Assert.Greater(highest, pavement + HeightOf(thing.Kind) - 0.05f, $"{where} is a stump");
                    looked[thing.Kind] = had + 1;
                }
            }
            foreach (StreetFurnitureKind kind in System.Enum.GetValues(typeof(StreetFurnitureKind)))
            {
                Assert.IsTrue(looked.TryGetValue(kind, out int n) && n >= 1, $"no {kind} near the spawn was built to be looked at");
            }
        }

        /// <summary>The kerb by a hydrant is painted red either way of it, and no parking stall reaches into it.</summary>
        [Test]
        public void TheKerbByAHydrantIsRedAndNobodyParksThere()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var things = new List<StreetFurnishing>();
            var lines = new List<double>();
            int painted = 0;
            foreach (EdgeGeometry g in SpawnStreets.Roads(ReachChunks))
            {
                things.Clear();
                StreetFurniture.Along(plan, g, things);
                foreach (StreetFurnishing hydrant in things)
                {
                    if (hydrant.Kind != StreetFurnitureKind.Hydrant)
                    {
                        continue;
                    }
                    double along = Vec2.Dot(hydrant.At - g.A, g.Dir);
                    int side = System.Math.Sign(Vec2.Dot(hydrant.At - g.A, g.Dir.Perp));
                    lines.Clear();
                    StreetFurniture.StallLines(plan, g, side, lines);
                    foreach (double line in lines)
                    {
                        Assert.GreaterOrEqual(System.Math.Abs(line - along), StreetFurniture.RedKerbEach - 1e-6,
                            $"road {g.Edge.Id}: a parking stall reaches into the red kerb by its hydrant");
                    }
                    if (painted >= 4 || plan.OnTutorialLot(hydrant.At.X, hydrant.At.Y, 1.0))
                    {
                        continue;
                    }
                    // A metre either way of the hydrant, on the kerb's face: red paint, in the chunk that holds that point.
                    foreach (double offset in new[] { -1.0, 1.0 })
                    {
                        Vec2 kerb = g.A + g.Dir * (along + offset) - hydrant.ToRoad * g.HalfWidth;
                        SpawnStreets.Surface built = SpawnStreets.BuildAt(kerb);
                        bool red = false;
                        for (int i = 0; i < built.Structures.Vertices.Count && !red; i++)
                        {
                            red = built.Structures.Colors[i].Equals(CitySurfaceBuilder.RedKerbLook) && (built.InTheWorld(built.Structures.Vertices[i]) - kerb).Length < 1.2;
                        }
                        Assert.IsTrue(red, $"road {g.Edge.Id}: the kerb {offset:+0;-0} m from its hydrant is not red");
                    }
                    painted++;
                }
            }
            Assert.GreaterOrEqual(painted, 2, "hardly a hydrant near the spawn was looked at");
        }

        [Test]
        public void AMeteredSideHasAStallMarkedInTheRoadAtEachMetersLine()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var lines = new List<double>();
            int marked = 0;
            foreach (EdgeGeometry g in SpawnStreets.Roads(ReachChunks))
            {
                for (int side = 1; side >= -1 && marked < 6; side -= 2)
                {
                    lines.Clear();
                    StreetFurniture.StallLines(plan, g, side, lines);
                    if (lines.Count == 0)
                    {
                        continue;
                    }
                    Assert.IsTrue(StreetFurniture.Metered(plan, g.Edge, side));
                    Assert.AreNotEqual(RoadClass.Boulevard, g.Edge.Class, "a boulevard has parking stalls");
                    for (int k = 1; k < lines.Count; k++)
                    {
                        Assert.AreEqual(StreetFurniture.StallLength, lines[k] - lines[k - 1], 1e-6, $"road {g.Edge.Id}: a stall is not a stall long");
                    }
                    // The mark's outer end, out in the road: white paint lies there in the chunk that holds it.
                    Vec2 end = g.A + g.Dir * lines[lines.Count / 2] + g.Dir.Perp * (side * (g.HalfWidth - StreetFurniture.StallDepth));
                    SpawnStreets.Surface built = SpawnStreets.BuildAt(end);
                    float road = (float)plan.GroundHeight(end.X, end.Y) + CitySurfaceBuilder.RoadLift;
                    bool paint = false;
                    foreach (Vector3 local in built.White.Vertices)
                    {
                        paint |= (built.InTheWorld(local) - end).Length < 0.7 && Mathf.Abs(local.y - road) < 0.12f;
                    }
                    Assert.IsTrue(paint, $"road {g.Edge.Id}: no stall is marked in the road at {end.X:0},{end.Y:0}");
                    marked++;
                }
            }
            Assert.GreaterOrEqual(marked, 3, "hardly a metered side near the spawn");
        }
    }
}
