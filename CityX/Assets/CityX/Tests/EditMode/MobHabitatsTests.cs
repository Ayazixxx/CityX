using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What may appear where (owner, 2026-11-14: "monster and ghost type enemies spawn outside and humanoid enemies spawn indoors for
    /// starters", and "it's not that the mobs have set spawn locations right? it should be more akin to minecraft where they spawn at
    /// random"; docs/MOB_SPAWNING.md): what a spot of the city is to an enemy, and the rules the spawner rolls its random spots by.
    /// </summary>
    public sealed class MobHabitatsTests
    {
        private const int Seed = CitySeed.Default;

        private static PlanWorld _plan;

        private static PlanWorld Plan => _plan ??= new CityWorld(Seed).Plan;

        [Test]
        public void OpenGroundIsForGhostsAndMonstersAndTheInsideOfAnOrdinaryBuildingForHumanoids()
        {
            PlanWorld plan = Plan;
            Assert.IsTrue(plan.TryFindSpawn(out double startX, out double startZ), "the plan has no start");
            int outdoors = 0, indoors = 0, nowhere = 0;
            for (double x = startX - 300.0; x <= startX + 300.0; x += 3.7)
            {
                for (double z = startZ - 300.0; z <= startZ + 300.0; z += 3.7)
                {
                    MobHabitat habitat = MobHabitats.At(plan, x, z, out BuildingSite site);
                    if (plan.OnTutorialLot(x, z, MobHabitats.TutorialSafeMetres - 0.01))
                    {
                        Assert.AreEqual(MobHabitat.None, habitat, "an enemy may appear near Old Guy's house, where a new character starts with nothing");
                    }
                    switch (habitat)
                    {
                        case MobHabitat.Outdoors:
                            outdoors++;
                            Assert.Less(plan.BuildingAt(x, z, MobHabitats.OutdoorWallMarginMetres - 0.01), 0, $"open ground at ({x:0.0}, {z:0.0}) is inside or against a building");
                            Assert.AreNotEqual(MobFamily.Humanoid, MobHabitats.FamilyFor(habitat, 0.5f));
                            break;
                        case MobHabitat.Indoors:
                            indoors++;
                            Assert.IsTrue(MobHabitats.Holds(site), "enemies may appear in a building that should hold none (a car park, a mall, a sealed block, a wing)");
                            site.WorldToLocal(x, z, out double u, out double v);
                            Assert.LessOrEqual(System.Math.Abs(u), site.Params.Width * 0.5 - MobHabitats.IndoorWallMarginMetres + 1e-6, "an enemy may appear in a building's wall");
                            Assert.LessOrEqual(System.Math.Abs(v), site.Params.Depth * 0.5 - MobHabitats.IndoorWallMarginMetres + 1e-6, "an enemy may appear in a building's wall");
                            Assert.AreEqual(MobFamily.Humanoid, MobHabitats.FamilyFor(habitat, 0.5f));
                            // Its floors: the ground floor at the building's base, each one above the last, all under its roof.
                            int floors = MobHabitats.Floors(site);
                            Assert.GreaterOrEqual(floors, 1);
                            Assert.AreEqual(site.BaseHeight, MobHabitats.FloorY(site, 0), 1e-4f);
                            for (int floor = 1; floor < floors; floor++)
                            {
                                Assert.Greater(MobHabitats.FloorY(site, floor), MobHabitats.FloorY(site, floor - 1) + 2.0f, "two floors less than a storey apart");
                                Assert.Less(MobHabitats.FloorY(site, floor), site.Top, "a floor above the roof");
                            }
                            break;
                        default:
                            nowhere++;
                            break;
                    }
                }
            }
            Debug.Log($"CityX mob habitats within 300 m of the start: {outdoors} spots of open ground, {indoors} inside ordinary buildings, {nowhere} nowhere an enemy appears");
            Assert.Greater(outdoors, 2000, "hardly any open ground for ghosts and monsters");
            Assert.Greater(indoors, 300, "hardly any building floor for humanoids");
            // The insides of buildings are a small part of the ground: why a building is rolled first for a humanoid (BuildingsNear).
            Assert.Less(indoors, outdoors);
        }

        [Test]
        public void ARandomSpotInARandomBuildingNearByIsInsideAnOrdinaryBuildingClearOfItsWalls()
        {
            PlanWorld plan = Plan;
            Assert.IsTrue(plan.TryFindSpawn(out double startX, out double startZ));
            int centreX = PlanWorld.ChunkOf(startX), centreY = PlanWorld.ChunkOf(startZ);
            const int Reach = 12;
            var near = new System.Collections.Generic.List<int>();
            MobHabitats.BuildingsNear(plan, centreX, centreY, Reach, near);
            Assert.Greater(near.Count, 30, "hardly a building near the start that can hold enemies");
            Assert.AreEqual(near.Count, new System.Collections.Generic.HashSet<int>(near).Count, "a building is listed twice");
            foreach (int building in near)
            {
                BuildingSite listed = plan.Buildings[building];
                Assert.IsTrue(MobHabitats.Holds(listed), "a building that should hold none is listed");
                Assert.LessOrEqual(System.Math.Abs(listed.OwnerChunkX - centreX), Reach, "a building further off than asked is listed");
                Assert.LessOrEqual(System.Math.Abs(listed.OwnerChunkY - centreY), Reach);
            }

            var dice = new System.Random(2468);
            int found = 0;
            const int Rolls = 3000;
            for (int i = 0; i < Rolls; i++)
            {
                BuildingSite site = plan.Buildings[near[dice.Next(near.Count)]];
                if (!MobHabitats.TryIndoorSpot(plan, site, (float)dice.NextDouble(), (float)dice.NextDouble(), out double x, out double z))
                {
                    continue;   // (too near Old Guy's house)
                }
                found++;
                site.WorldToLocal(x, z, out double u, out double v);
                Assert.LessOrEqual(System.Math.Abs(u), site.Params.Width * 0.5 - MobHabitats.IndoorWallMarginMetres + 1e-6, "a spot in a building's wall");
                Assert.LessOrEqual(System.Math.Abs(v), site.Params.Depth * 0.5 - MobHabitats.IndoorWallMarginMetres + 1e-6, "a spot in a building's wall");
                Assert.IsFalse(plan.OnTutorialLot(x, z, MobHabitats.TutorialSafeMetres - 0.01), "a spot near Old Guy's house");
                Assert.AreNotEqual(MobHabitat.Outdoors, MobHabitats.At(plan, x, z, out _), "a spot inside a building is called open ground");
            }
            Assert.Greater(found, Rolls / 2, "most rolls of a building near by find nowhere indoors");
            Assert.IsFalse(MobHabitats.TryIndoorSpot(plan, BuildingSite.None, 0.5f, 0.5f, out _, out _), "a spot was rolled in no building");
        }

        [Test]
        public void OutOfDoorsAQuarterAreMonstersAndTheRestGhosts()
        {
            Assert.AreEqual(MobFamily.Monster, MobHabitats.FamilyFor(MobHabitat.Outdoors, MobHabitats.MonsterShare - 0.01f));
            Assert.AreEqual(MobFamily.Ghost, MobHabitats.FamilyFor(MobHabitat.Outdoors, MobHabitats.MonsterShare + 0.01f));
            Assert.AreEqual(MobFamily.Humanoid, MobHabitats.FamilyFor(MobHabitat.Indoors, 0.0f));
            Assert.Less(MobHabitats.MonsterShare, 0.5f, "monsters are meant to be the rarer");
            Assert.AreEqual(MobHabitats.MostOutdoors, MobHabitats.Most(MobHabitat.Outdoors));
            Assert.AreEqual(MobHabitats.MostIndoors, MobHabitats.Most(MobHabitat.Indoors));
            Assert.AreEqual(0, MobHabitats.Most(MobHabitat.None));
        }

        [Test]
        public void TheSpawnersRules()
        {
            Assert.AreEqual(MobSpawnRules.Ghost, MobSpawnRules.KindFor(MobFamily.Ghost, 0.9f));
            Assert.AreEqual(MobSpawnRules.Monster, MobSpawnRules.KindFor(MobFamily.Monster, 0.1f));
            Assert.AreEqual(MobSpawnRules.Gunner, MobSpawnRules.KindFor(MobFamily.Humanoid, MobSpawnRules.GunnerShare - 0.01f));
            Assert.AreEqual(MobSpawnRules.Swordsman, MobSpawnRules.KindFor(MobFamily.Humanoid, MobSpawnRules.GunnerShare + 0.01f));

            // A random spot is in the ring round the player, any way round, and no part of the ring is likelier than another: half of
            // them fall beyond the circle that halves the ring's area.
            var viewer = new Vector3(40.0f, 7.0f, -25.0f);
            float from = MobSpawnRules.OutdoorsFromMetres, to = MobSpawnRules.ToMetres;
            float halving = Mathf.Sqrt((from * from + to * to) * 0.5f);
            var dice = new System.Random(12345);
            int beyond = 0, east = 0;
            const int Rolls = 4000;
            for (int i = 0; i < Rolls; i++)
            {
                Vector2 spot = MobSpawnRules.SpotRound(viewer, from, to, (float)dice.NextDouble(), (float)dice.NextDouble());
                float away = Vector2.Distance(spot, new Vector2(viewer.x, viewer.z));
                Assert.That(away, Is.InRange(from - 0.01f, to + 0.01f), "a spot outside the ring");
                if (away > halving) beyond++;
                if (spot.x > viewer.x) east++;
            }
            Assert.AreEqual(0.5f, beyond / (float)Rolls, 0.04f, "the spots crowd toward the player (or away)");
            Assert.AreEqual(0.5f, east / (float)Rolls, 0.04f, "the spots favour one side");

            Assert.Less(MobSpawnRules.IndoorsFromMetres, MobSpawnRules.OutdoorsFromMetres, "a building should be occupied before the player reaches its door");
            Assert.AreEqual(MobSpawnRules.IndoorsFromMetres, MobSpawnRules.FromMetres(MobHabitat.Indoors));
            Assert.AreEqual(MobSpawnRules.OutdoorsFromMetres, MobSpawnRules.FromMetres(MobHabitat.Outdoors));
            // Taken away only well past where it appears: an enemy at the ring's edge does not flicker in and out.
            Assert.Greater(MobHabitats.CrowdMetres, MobSpawnRules.ToMetres + 20.0f);
            Assert.IsFalse(MobSpawnRules.TooFar(viewer + Vector3.forward * MobSpawnRules.ToMetres, viewer));
            Assert.IsTrue(MobSpawnRules.TooFar(viewer + Vector3.forward * (MobHabitats.CrowdMetres + 1.0f), viewer));
            Assert.LessOrEqual(MobSpawnRules.ToMetres + 10.0f, EnemyNetwork.WorldSpawnReachMetres, "the server would refuse what the spawner asks");
        }
    }
}
