using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The footbridges' shapes, stairs, rails and heights (owner, 2026-10-16, with four reference images: "ive noticed the sky bridges are often
    /// times very low, make these have a lower chance of appearing closer to the surface and a higher chance of appear on taller buildings. I also
    /// want to make the sky bridges more intricate ... more variety in angles/curves/turns, make them have different types of guard rails ... have
    /// some of them [staircases] connect to the sky bridges"). Every shape, rail and deck kind is built near the spawn; every path is walkable (its
    /// ends on the walls, its deck no steeper than a deck, its flights stairs a body climbs, its turns landings, clear of every other building and
    /// of the street); the bridges stand high; and the street stairs come down along a wall onto the paving, clear of the road.
    /// </summary>
    public sealed class FootbridgeShapeTests
    {
        private const int Seed = 80469103;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<Footbridge> _bridges;

        private static List<Footbridge> Bridges
        {
            get
            {
                if (_bridges != null) return _bridges;
                var seen = new HashSet<(int, int, int)>();
                _bridges = new List<Footbridge>();
                foreach (BuildingSite site in TestSites.NearSpawn(World, 50))
                {
                    foreach (Footbridge b in Footbridges.For(World, site))
                    {
                        if (seen.Add((b.SiteA, b.SiteB, b.Index))) _bridges.Add(b);
                    }
                }
                return _bridges;
            }
        }

        [Test]
        public void EveryShapeRailAndDeckIsBuiltNearTheSpawn()
        {
            var shapes = new Dictionary<BridgeShape, int>();
            var rails = new Dictionary<BridgeRail, int>();
            var decks = new Dictionary<BridgeDeck, int>();
            int stairs = 0, streetStairs = 0, piers = 0;
            foreach (Footbridge b in Bridges)
            {
                shapes[b.Shape] = (shapes.TryGetValue(b.Shape, out int s) ? s : 0) + 1;
                rails[b.Rail] = (rails.TryGetValue(b.Rail, out int r) ? r : 0) + 1;
                decks[b.Deck] = (decks.TryGetValue(b.Deck, out int d) ? d : 0) + 1;
                if (b.Path.HasStairs) stairs++;
                if (b.Stair != null) streetStairs++;
                piers += b.Path.Piers.Length;
            }
            Debug.Log($"CityX footbridges near the spawn: {Bridges.Count}; shapes {string.Join(", ", shapes)}; rails {string.Join(", ", rails)}; decks " +
                $"{string.Join(", ", decks)}; {stairs} with flights, {streetStairs} with a stair down to the street, {piers} piers");
            foreach (BridgeShape shape in new[] { BridgeShape.Straight, BridgeShape.Angled, BridgeShape.DogLeg, BridgeShape.Curve })
            {
                Assert.IsTrue(shapes.ContainsKey(shape), $"no {shape} bridge near the spawn");
            }
            Assert.GreaterOrEqual(rails.Count, 4, "too few kinds of rail");
            Assert.AreEqual(2, decks.Count, "not both decks");
            Assert.Greater(stairs, 2, "too few bridges with flights");
            Assert.Greater(streetStairs, 0, "no bridge has a stair down to the street");
            Assert.Greater(piers, 0, "no piers");
        }

        [Test]
        public void EveryPathIsWalkableAndClearOfTheStreetAndTheOtherBuildings()
        {
            int paths = 0, flights = 0;
            foreach (Footbridge b in Bridges)
            {
                BuildingSite a = World.Building(b.SiteA), c = World.Building(b.SiteB);
                BridgePath path = b.Path;
                string what = $"bridge {b.SiteA}->{b.SiteB} #{b.Index} ({b.Shape}, floors {b.FloorA}/{b.FloorB})";
                Assert.That(Vector3.Distance(path.Points[0], b.StartWorld), Is.LessThan(1e-3f), $"{what}: the path does not start at A's doorway");
                Assert.That(Vector3.Distance(path.Points[path.Points.Length - 1], b.EndWorld), Is.LessThan(1e-3f), $"{what}: the path does not end at B's doorway");
                // Deck no steeper than a deck; each flight's steps at most a riser high and a tread deep; no more steps than a flight takes.
                for (int i = 0; i < path.Stretches; i++)
                {
                    Vector3 p = path.Points[i], q = path.Points[i + 1];
                    float run = new Vector2(q.x - p.x, q.z - p.z).magnitude;
                    float rise = Mathf.Abs(q.y - p.y);
                    if (path.Risers[i] <= 0)
                    {
                        if (run > 0.05f) Assert.That(rise / run, Is.LessThanOrEqualTo(Footbridges.MaxGrade + 1e-3f), $"{what}: stretch {i} is a deck steeper than a deck");
                        continue;
                    }
                    flights++;
                    int n = path.Risers[i];
                    Assert.That(n, Is.LessThanOrEqualTo(Footbridges.FlightMax), $"{what}: a flight of {n} steps with no landing");
                    Assert.That(rise / n, Is.LessThanOrEqualTo(Footbridges.MaxRiser + 1e-3f), $"{what}: a step {rise / n:F3} m high");
                    Assert.That(run / n, Is.EqualTo(Footbridges.Tread).Within(0.01f), $"{what}: a step {run / n:F3} m deep");
                }
                // Turns only at a dog-leg's two landings, never sharper than a right angle.
                for (int i = 1; i + 1 < path.Points.Length; i++)
                {
                    Vector2 d0 = Flat(path.Points[i] - path.Points[i - 1]).normalized, d1 = Flat(path.Points[i + 1] - path.Points[i]).normalized;
                    float turn = Vector2.Angle(d0, d1);
                    Assert.That(turn, Is.LessThanOrEqualTo(b.Shape == BridgeShape.DogLeg ? 75.0f : b.Shape == BridgeShape.Curve ? 30.0f : 0.5f),
                        $"{what}: a turn of {turn:F0} degrees at point {i}");
                }
                // Clear of every other building, and of A and B away from its ends; clear of the street the whole way.
                float length = path.Length;
                for (float s = 0.6f; s < length - 0.6f; s += 0.75f)
                {
                    Vector2 x = path.PlanAt(s, out Vector2 dir);
                    Vector2 side = new Vector2(-dir.y, dir.x) * (Footbridges.DeckWidth * 0.5f);
                    foreach (Vector2 at in new[] { x, x + side, x - side })
                    {
                        if (World.TryBuildingAt(at.x, at.y, out BuildingSite other))
                        {
                            bool nearEnd = other.Id == a.Id && s < 1.2f || other.Id == c.Id && s > length - 1.2f;
                            Assert.IsTrue(nearEnd, $"{what}: building {other.Id} stands on the path {s:F1} m along it");
                        }
                    }
                    float ground = (float)World.Plan.GroundHeight(x.x, x.y);
                    Assert.That(path.HeightAt(s) - Footbridges.DeckThickness - ground, Is.GreaterThan(Footbridges.GroundClearance - 0.6f),
                        $"{what}: too low over the street {s:F1} m along it");
                }
                // Piers stand off the roads.
                foreach (Vector3 pier in path.Piers)
                {
                    Assert.IsFalse(Footbridges.OnACarriageway(World, pier.x, pier.z, 0.0), $"{what}: a pier on a road at ({pier.x:F1}, {pier.z:F1})");
                }
                paths++;
            }
            Debug.Log($"CityX footbridge paths: {paths} walked in plan, {flights} flights");
            Assert.Greater(paths, 20);
        }

        [Test]
        public void TheBridgesStandHighAndMostlyBetweenTallBlocks()
        {
            int low = 0, tallPairs = 0;
            float clearances = 0.0f;
            foreach (Footbridge b in Bridges)
            {
                float lowest = float.MaxValue;
                for (float s = 0.0f; s <= b.Path.Length; s += 1.0f)
                {
                    Vector2 x = b.Path.PlanAt(s, out _);
                    lowest = Mathf.Min(lowest, b.Path.HeightAt(s) - Footbridges.DeckThickness - (float)World.Plan.GroundHeight(x.x, x.y));
                }
                clearances += lowest;
                if (lowest < 12.0f) low++;
                int floors = Mathf.Min(BuildingInterior.FloorCountFor(World.Building(b.SiteA).Params.Height), BuildingInterior.FloorCountFor(World.Building(b.SiteB).Params.Height));
                if (floors >= Footbridges.SecondBridgeFloors) tallPairs++;
            }
            // The tall blocks among the buildings round about (the ordinary blocks of three floors or more a bridge could join): the bridges
            // favour them (owner: "a higher chance of appear on taller buildings"), however many there are where the spawn happens to be
            // (seed 19077151's spawn is among mid-rise blocks: 24 of its 51 bridges join blocks of eight floors).
            int ordinary = 0, tallBlocks = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 50))
            {
                if (site.OnRoof || site.Wing || site.Sides >= 3 || site.Params.IsSpecial) continue;
                int floors = BuildingInterior.FloorCountFor(site.Params.Height);
                if (floors < 3) continue;
                ordinary++;
                if (floors >= Footbridges.SecondBridgeFloors) tallBlocks++;
            }
            float share = low / (float)Bridges.Count;
            float tallShare = tallPairs / (float)Bridges.Count, tallBlockShare = tallBlocks / (float)System.Math.Max(1, ordinary);
            Debug.Log($"CityX footbridge heights: {Bridges.Count} bridges, mean clearance {clearances / Bridges.Count:F1} m, {share:P0} under 12 m, " +
                $"{tallPairs} ({tallShare:P0}) between blocks of {Footbridges.SecondBridgeFloors} floors or more, where {tallBlockShare:P0} of the " +
                $"{ordinary} blocks of three floors or more round about are that tall");
            Assert.That(share, Is.LessThan(0.35f), "too many low bridges");
            Assert.That(clearances / Bridges.Count, Is.GreaterThan(16.0f), "the bridges stand low on average");
            Assert.That(tallShare, Is.GreaterThan(System.Math.Max(0.35f, tallBlockShare * 1.3f)), "the bridges should favour the tall blocks");
        }

        [Test]
        public void AStreetStairRunsDownAlongItsWallOntoThePavingClearOfTheRoad()
        {
            int checkedStairs = 0, towers = 0;
            foreach (Footbridge b in Bridges)
            {
                StreetStair s = b.Stair;
                if (s == null) continue;
                BuildingSite site = World.Building(s.Site);
                string what = $"bridge {b.SiteA}->{b.SiteB} #{b.Index}'s street stair";
                Assert.AreEqual(b.SiteA, s.Site, $"{what}: not on the deciding building");
                Assert.AreEqual(b.WallA, s.Wall, $"{what}: not on the bridge's wall");
                // The top at the deck by the wall, the foot on the paving.
                Assert.That(s.Points[0].y, Is.EqualTo(b.Path.HeightAt(Footbridges.StreetStairOffset)).Within(0.05f), $"{what}: its top is not at the deck");
                Vector3 foot = s.Points[s.Points.Length - 1];
                float paving = (float)(World.Plan.GroundHeight(foot.x, foot.z) + CityX.World.Plan.PlanBuildings.PavingRise);
                Assert.That(foot.y - paving, Is.InRange(-0.15f, 0.25f), $"{what}: its foot is {foot.y - paving:F2} m off the paving");
                for (int i = 0; i + 1 < s.Points.Length; i++)
                {
                    Vector3 p = s.Points[i], q = s.Points[i + 1];
                    // Along the wall at its offset.
                    site.WorldToLocal(p.x, p.z, out double lx, out double lz);
                    // On its lane by the wall: the one lane of a straight run, either of a tower's two.
                    float off = OffWall(site, s.Wall, (float)lx, (float)lz);
                    float outer = Footbridges.StreetStairOffset + Footbridges.StreetStairWidth + Footbridges.TowerLaneGap;
                    bool onLane = Mathf.Abs(off - Footbridges.StreetStairOffset) < 0.05f || s.Switchback && Mathf.Abs(off - outer) < 0.05f;
                    Assert.IsTrue(onLane, $"{what}: point {i} is {off:F2} m out from its wall, off its lanes");
                    if (s.Risers[i] > 0)
                    {
                        Assert.That((p.y - q.y) / s.Risers[i], Is.InRange(0.05f, Footbridges.MaxRiser + 1e-3f), $"{what}: flight {i}'s steps");
                        Assert.That(s.Risers[i], Is.LessThanOrEqualTo(Footbridges.FlightMax));
                    }
                    // Off the road, and inside no building.
                    for (float t = 0.0f; t <= 1.0f; t += 0.25f)
                    {
                        Vector3 x = Vector3.Lerp(p, q, t);
                        Assert.IsFalse(Footbridges.OnACarriageway(World, x.x, x.z, 0.0), $"{what}: over a road");
                        Assert.IsFalse(World.TryBuildingAt(x.x, x.z, out BuildingSite other) && other.Id != site.Id, $"{what}: through building {other.Id}");
                    }
                }
                // The facade keeps clear of it: the stair's stretch of wall is a clearing.
                Footbridges.For(World, site);
                var clearings = new List<FacadeClearing>();
                Footbridges.ClearingsOn(site, s.Wall, clearings, 0.0f);
                float length = s.Wall == WindowGrid.Wall.North || s.Wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;
                bool cleared = false;
                foreach (FacadeClearing cl in clearings)
                {
                    cleared |= cl.U0 <= s.U0 - length * 0.5f + 0.01f && cl.U1 >= s.U1 - length * 0.5f - 0.01f && cl.Y0 <= s.Bottom - site.BaseHeight;
                }
                Assert.IsTrue(cleared, $"{what}: the facade is not kept clear of it");
                checkedStairs++;
                if (s.Switchback) towers++;
            }
            Debug.Log($"CityX street stairs: {checkedStairs} checked, {towers} of them zig-zag towers");
            Assert.Greater(checkedStairs, 0, "no street stairs near the spawn");
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static float OffWall(BuildingSite site, WindowGrid.Wall wall, float lx, float lz)
        {
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            switch (wall)
            {
                case WindowGrid.Wall.North: return lz - hd;
                case WindowGrid.Wall.South: return -hd - lz;
                case WindowGrid.Wall.East: return lx - hw;
                default: return -hw - lx;
            }
        }
    }
}
