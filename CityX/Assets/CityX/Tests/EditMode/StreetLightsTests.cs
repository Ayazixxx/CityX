using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The street lights (World/StreetLights, docs/AMERICAN_STREETS.md): where the rule stands them along the real city's streets, what the
    /// chunks build at each (a pole on the pavement, a lamp over the road, lit glass only where the street has power), and which lamps the
    /// night's real lights are lent to.
    /// </summary>
    public sealed class StreetLightsTests
    {
        private const int ReachChunks = 8;

        [TearDown]
        public void TearDown()
        {
            StreetLampSites.Clear();
            CurrentSky.Clear();
        }

        [Test]
        public void LampsStandOnThePavementStaggeredAlongEveryDrivenStreetAndNowhereElse()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var lamps = new List<StreetLamp>();
            int lit = 0, powered = 0, total = 0;
            foreach (EdgeGeometry g in SpawnStreets.Roads(ReachChunks))
            {
                lamps.Clear();
                StreetLights.Along(plan, g, lamps);
                string where = $"road {g.Edge.Id}";
                if (g.Edge.Bridge || PedestrianStreets.Is(plan, g.Edge) || StepStreets.Is(plan, g.Edge))
                {
                    Assert.IsEmpty(lamps, $"{where}: a street that is walked, climbed or carried on a bridge has street lights");
                    continue;
                }
                if (lamps.Count == 0)
                {
                    Assert.Less(g.Length - g.TrimA - g.TrimB, StreetLights.ShortestLitStreet, $"{where}: a street long enough for a lamp has none");
                    continue;
                }
                lit++;
                total += lamps.Count;
                if (lamps[0].Powered)
                {
                    powered++;
                }
                double before = double.NaN;
                int sideBefore = 0;
                foreach (StreetLamp lamp in lamps)
                {
                    Vec2 from = lamp.At - g.A;
                    double along = Vec2.Dot(from, g.Dir), aside = Vec2.Dot(from, g.Dir.Perp);
                    Assert.AreEqual(g.HalfWidth + StreetLights.FromKerb, System.Math.Abs(aside), 1e-6, $"{where}: a pole is not just inside the kerb");
                    Assert.GreaterOrEqual(along, g.TrimA + StreetLights.FromJunction - 1e-6, $"{where}: a pole stands at the junction");
                    Assert.LessOrEqual(along, g.Length - g.TrimB - StreetLights.FromJunction + 1e-6, $"{where}: a pole stands at the junction");
                    Assert.Greater(Vec2.Dot(lamp.Over, g.Dir.Perp * -System.Math.Sign(aside)), 0.99, $"{where}: an arm does not reach over the road");
                    Assert.AreEqual(lamps[0].Powered, lamp.Powered, $"{where}: one street's lamps are part lit and part dead");
                    int side = System.Math.Sign(aside);
                    if (!double.IsNaN(before))
                    {
                        Assert.LessOrEqual(along - before, StreetLights.SpacingFor(g.Edge.Class) + 1e-6, $"{where}: lamps too far apart");
                        Assert.Greater(along - before, StreetLights.SpacingFor(g.Edge.Class) * 0.45, $"{where}: lamps crowded together");
                        Assert.AreEqual(-sideBefore, side, $"{where}: two lamps running on the same side");
                    }
                    before = along;
                    sideBefore = side;
                }
            }
            TestContext.WriteLine($"{lit} lit streets round the spawn, {total} lamps, {powered} streets with power");
            Assert.Greater(lit, 30, "hardly a street near the spawn has lights");
            Assert.Greater(powered, lit / 6, "hardly a street has power for its lamps");
            Assert.Less(powered, lit * 6 / 10, "most streets have power: the nights are meant to be dark");
        }

        private static bool Near(Vector3 v, Vec2 at, double within) => (new Vec2(v.x, v.z) - at).Length < within;

        [Test]
        public void EachLampIsAPoleOnThePavementWithItsLampOverTheRoadAndLitGlassOnlyWithPower()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var lamps = new List<StreetLamp>();
            int looked = 0, withPower = 0, without = 0;
            foreach (EdgeGeometry g in SpawnStreets.Roads(ReachChunks))
            {
                lamps.Clear();
                StreetLights.Along(plan, g, lamps);
                foreach (StreetLamp lamp in lamps)
                {
                    // One whose pole and lamp are in one chunk, and of the kind still wanted.
                    if (PlanWorld.ChunkOf(lamp.At.X) != PlanWorld.ChunkOf(lamp.Head.X) || PlanWorld.ChunkOf(lamp.At.Y) != PlanWorld.ChunkOf(lamp.Head.Y)
                        || (lamp.Powered ? withPower : without) >= 3 || plan.OnTutorialLot(lamp.At.X, lamp.At.Y, 1.0))
                    {
                        continue;
                    }
                    SpawnStreets.Surface built = SpawnStreets.BuildAt(lamp.At);
                    float pavementY = SpawnStreets.PavementHeight(lamp.At);
                    string where = $"the lamp at {lamp.At.X:0},{lamp.At.Y:0}";

                    float lowest = float.MaxValue, highest = float.MinValue;
                    bool head = false;
                    foreach (Vector3 local in built.Structures.Vertices)
                    {
                        Vector3 v = local + built.Origin;
                        if (Near(v, lamp.At, 0.4))
                        {
                            lowest = Mathf.Min(lowest, v.y);
                            highest = Mathf.Max(highest, v.y);
                        }
                        head |= Near(v, lamp.Head, 0.7) && Mathf.Abs(v.y - (pavementY + lamp.Height)) < 0.4f;
                    }
                    if (lowest == float.MaxValue)
                    {
                        continue;   // not stood here: the pavement is cut away under it (a ramp's foot, a garage's drive)
                    }
                    looked++;
                    Assert.LessOrEqual(lowest, pavementY + 0.01f, $"{where}: the pole floats over the pavement");
                    Assert.Greater(lowest, pavementY - 0.3f, $"{where}: the pole is sunk into the ground");
                    Assert.Greater(highest, pavementY + lamp.Height - 1.5f, $"{where}: the pole is a stump");
                    Assert.IsTrue(head, $"{where}: no lamp hangs over the road at the end of the arm");

                    bool glows = false;
                    foreach (Vector3 local in built.Lit.Vertices)
                    {
                        glows |= Near(local + built.Origin, lamp.Head, 0.9) && Mathf.Abs(local.y - (pavementY + lamp.Height)) < 0.4f;
                    }
                    Assert.AreEqual(lamp.Powered, glows, lamp.Powered ? $"{where}: a lamp with power has no lit lens" : $"{where}: a dead lamp has a lit lens");

                    var found = new List<Vector3>();
                    StreetLampSites.Near(new Vector3((float)lamp.Head.X, pavementY + lamp.Height, (float)lamp.Head.Y), 1.5f, found);
                    Assert.AreEqual(lamp.Powered ? 1 : 0, found.Count, $"{where}: the night's lights are told of {found.Count} lamp(s) here");
                    if (lamp.Powered) withPower++; else without++;
                }
            }
            Assert.GreaterOrEqual(withPower, 2, "no powered lamp near the spawn was looked at");
            Assert.GreaterOrEqual(without, 2, "no dead lamp near the spawn was looked at");
            Assert.Greater(looked, 0);
        }

        [Test]
        public void TheSitesGiveOnlyTheLampsWithinReach()
        {
            var near = new Vector3(80.0f, 10.0f, 100.0f);
            var beside = new Vector3(81.5f, 10.0f, 100.0f);
            var far = new Vector3(800.0f, 10.0f, 800.0f);
            int cx = PlanWorld.ChunkOf(near.x), cy = PlanWorld.ChunkOf(near.z);
            Assert.AreEqual(cx, PlanWorld.ChunkOf(beside.x), "setup: the two lamps are not in one chunk");
            StreetLampSites.Report(cx, cy, new List<Vector3> { near, beside });
            StreetLampSites.Report(PlanWorld.ChunkOf(far.x), PlanWorld.ChunkOf(far.z), new List<Vector3> { far });

            var found = new List<Vector3>();
            StreetLampSites.Near(near, 1.0f, found);
            Assert.AreEqual(1, found.Count, "a lamp out of reach was given");
            found.Clear();
            StreetLampSites.Near(near, 60.0f, found);
            Assert.AreEqual(2, found.Count, "the lamps within reach are not the ones given");

            StreetLampSites.Report(cx, cy, null);
            found.Clear();
            StreetLampSites.Near(near, 60.0f, found);
            Assert.IsEmpty(found, "a chunk built again without lamps keeps its old ones");
        }
    }
}
