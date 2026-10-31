using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The glass of the enclosed sky bridges breaks like a window's (owner, 2026-10-28: "make it so the sky bridges that have windows have
    /// shatterable windows"): every bay of a corridor's long window is a pane of its own (BridgeGlass), a blow on its glass finds that bay and a
    /// blow anywhere else on the bridge finds none, and the city draws each bay as its damage has it -- a broken one left open between its mullions.
    /// </summary>
    public sealed class BridgeGlassTests
    {
        private const int Seed = 81565117;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<Footbridge> _bridges;

        /// <summary>The enclosed bridges near the spawn, each once.</summary>
        private static List<Footbridge> Enclosed
        {
            get
            {
                if (_bridges != null) return _bridges;
                _bridges = new List<Footbridge>();
                foreach (BuildingSite site in TestSites.NearSpawn(World, 50))
                {
                    foreach (Footbridge b in Footbridges.For(World, site))
                    {
                        if (b.SiteA == site.Id && b.Kind == BridgeKind.Enclosed) _bridges.Add(b);
                    }
                }
                return _bridges;
            }
        }

        private static List<BridgePane> PanesOf(in Footbridge b)
        {
            var panes = new List<BridgePane>();
            BridgeGlass.Panes(b, panes);
            return panes;
        }

        /// <summary>A point on a pane's glass, <paramref name="along"/> and <paramref name="up"/> it (0..1), <paramref name="off"/> metres out of its plane.</summary>
        private static Vector3 On(in BridgePane pane, float along, float up, float off = 0.0f)
        {
            Vector3 sill = Vector3.Lerp(pane.Start, pane.End, along);
            return sill + Vector3.up * (pane.Height * up) + pane.Normal * off;
        }

        [Test]
        public void EveryBayOfAnEnclosedBridgesWindowIsAPaneOfItsOwn()
        {
            Assert.IsNotEmpty(Enclosed, "no enclosed bridge near the spawn");
            var keys = new HashSet<PaneKey>();
            int count = 0;
            foreach (Footbridge b in Enclosed)
            {
                List<BridgePane> panes = PanesOf(b);
                Assert.IsNotEmpty(panes, $"bridge {b.SiteA}-{b.SiteB} has no glass to break");
                foreach (BridgePane pane in panes)
                {
                    count++;
                    Assert.IsTrue(keys.Add(pane.Key), $"two bays share the key {pane.Key}");
                    Assert.IsTrue(BridgeGlass.IsBridgePane(pane.Key));
                    Assert.IsFalse(DoorGlass.IsDoorPane(pane.Key), $"{pane.Key} reads as a door's glass");
                    Assert.AreEqual(b.SiteA, pane.Key.Building, "a bay is not keyed to the building that draws its bridge");
                    Assert.That(pane.Width, Is.InRange(Footbridges.CorridorBay * 0.5f, Footbridges.CorridorBay * 1.5f), $"{pane.Key} is not a bay wide");
                    Assert.AreEqual(b.Headroom - Footbridges.CorridorLintel - Footbridges.CorridorSill, pane.Height, 1e-3f);
                    Assert.IsTrue(BridgeGlass.TryPane(World, pane.Key, out BridgePane again), $"{pane.Key} names no pane");
                    Assert.Less((again.Start - pane.Start).magnitude + (again.End - pane.End).magnitude, 1e-4f, $"{pane.Key} names another bay");
                }
            }
            Debug.Log($"CityX bridge glass: {Enclosed.Count} enclosed bridges near the spawn, {count} bays");
        }

        [Test]
        public void ABlowOnABaysGlassFindsThatBayAndABlowAnywhereElseFindsNone()
        {
            foreach (Footbridge b in Enclosed)
            {
                List<BridgePane> panes = PanesOf(b);
                for (int i = 0; i < panes.Count; i++)
                {
                    BridgePane pane = panes[i];
                    foreach (float off in new[] { 0.05f, -0.05f })
                    {
                        Assert.IsTrue(BridgeGlass.TryPaneAt(World, On(pane, 0.3f, 0.6f, off), out BridgePane hit), $"a blow on {pane.Key} found no glass");
                        Assert.AreEqual(pane.Key, hit.Key, "a blow on one bay found another");
                    }
                    Assert.IsFalse(BridgeGlass.TryPaneAt(World, On(pane, 0.5f, -0.3f), out _), $"a blow on the wall under {pane.Key}'s sill found glass");
                    Assert.IsFalse(BridgeGlass.TryPaneAt(World, On(pane, 0.5f, 0.5f) - pane.Normal * 0.9f, out _), "a blow in the corridor found glass");
                    if (i + 1 < panes.Count && panes[i + 1].Key.Column == pane.Key.Column + 1)
                    {
                        // The mullion between two bays of a stretch.
                        Vector3 mullion = Vector3.Lerp(pane.End, panes[i + 1].Start, 0.5f) + Vector3.up * (pane.Height * 0.5f);
                        Assert.IsFalse(BridgeGlass.TryPaneAt(World, mullion, out _), $"a blow on the mullion after {pane.Key} found glass");
                    }
                }
            }
        }

        [Test]
        public void ABlowRecordsWhereOnTheBayItLanded()
        {
            Footbridge b = Enclosed[0];
            BridgePane pane = PanesOf(b)[0];
            BuildingSite site = World.Building(pane.Key.Building);
            Vector2 at = GlassDamage.FractionWithinPane(World, site, pane.Key, On(pane, 0.25f, 0.75f));
            Assert.AreEqual(0.25f, at.x, 0.02f);
            Assert.AreEqual(0.75f, at.y, 0.02f);
        }

        [Test]
        public void OpenBridgesAndBreezewaysHaveNoGlass()
        {
            int seen = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 50))
            {
                foreach (Footbridge b in Footbridges.For(World, site))
                {
                    if (b.SiteA != site.Id || b.Kind == BridgeKind.Enclosed) continue;
                    seen++;
                    Assert.IsEmpty(PanesOf(b), $"a {b.Kind} bridge has glass");
                }
            }
            Assert.Greater(seen, 0);
        }

        /// <summary>The city draws each bay as its damage has it: whole in the glass, cracked in the cracked glass with a box a knock finds, gone open.</summary>
        [Test]
        public void EachBayIsDrawnAsItsDamageHasItABrokenOneLeftOpen()
        {
            Footbridge b = Enclosed.Find(x => !x.Path.HasStairs);
            Assert.IsTrue(b.Path != null, "no enclosed bridge without a flight near the spawn");
            List<BridgePane> panes = PanesOf(b);
            Assert.GreaterOrEqual(panes.Count, 3);
            BridgePane broken = panes[0], cracked = panes[1], whole = panes[2];
            var damage = new GlassDamage();
            damage.Set(broken.Key, PaneState.Shattered);
            damage.Shoot(cracked.Key, new Vector2(0.5f, 0.5f), out _);

            BuildingSite a = World.Building(b.SiteA);
            var origin = new Vector3(a.OwnerChunkX * CityLayout.ChunkSize, 0, a.OwnerChunkY * CityLayout.ChunkSize);
            var glass = new MeshBuffer();
            var crackedGlass = new MeshBuffer();
            var openings = new List<CityMeshBuilder.CrackedOpening>();
            CityMeshBuilder.BuildSite(World, a, origin, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), glass, crackedGlass, damage, openings,
                new MeshBuffer(), false);

            bool Through(MeshBuffer buffer, in BridgePane pane, float along) =>
                MeshRays.Hits(buffer, On(pane, along, 0.5f, 0.5f) - origin, -pane.Normal, 1.0f);
            Assert.IsTrue(Through(glass, whole, 0.5f), "a whole bay is not glazed");
            Assert.IsFalse(Through(glass, broken, 0.5f), "a broken bay still has its glass");
            Assert.IsFalse(Through(crackedGlass, broken, 0.5f), "a broken bay still has its glass");
            Assert.IsFalse(Through(glass, cracked, 0.2f), "a cracked bay is drawn whole");
            Assert.IsTrue(Through(crackedGlass, cracked, 0.2f), "a cracked bay is not drawn cracked");
            Assert.IsTrue(openings.Exists(o => o.Key.Equals(cracked.Key)), "a cracked bay left no box for a knock");
        }
    }
}
