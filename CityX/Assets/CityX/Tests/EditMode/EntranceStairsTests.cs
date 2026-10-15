using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The stairs up to a raised front door (EntranceStairs): a landing at the door and a flight down along the wall that ends where the ground meets
    /// it. Reported: "floating staircases, some blocking the streets".
    /// </summary>
    public sealed class EntranceStairsTests
    {
        private const int Seed = 83920571;

        [Test]
        public void OnFlatGroundTheFlightIsJustLongEnough()
        {
            EntranceStairs.Flight f = EntranceStairs.Plan(2.0f, (side, along) => 0.0f);
            Assert.Greater(f.TopOf(f.Steps, 2.0f), 0.0f, "a step buried in the ground");
            Assert.LessOrEqual(f.TopOf(f.Steps, 2.0f), EntranceStairs.Riser + 0.02f, "the last step must be one riser off the ground at most");
        }

        [Test]
        public void TheFlightRunsTheWayTheGroundComesUpToMeetIt()
        {
            // Ground rising toward -x, falling toward +x: going -x lands sooner.
            EntranceStairs.Flight f = EntranceStairs.Plan(3.0f, (side, along) => side < 0 ? along * 0.3f : -along * 0.3f);
            Assert.AreEqual(-1, f.Side);
            Assert.Less(f.Steps, EntranceStairs.StepsDown(3.0f, along => -along * 0.3f));
        }

        [Test]
        public void ADroppingSlopeNeverLeavesTheFootInTheAir()
        {
            // The ground keeps falling away as the flight goes: the old flight, sized from one sample, ended metres up.
            EntranceStairs.Flight f = EntranceStairs.Plan(4.0f, (side, along) => -along * 0.25f);
            float lastTop = f.TopOf(f.Steps, 4.0f);
            float groundThere = -(f.StartOf(f.Steps) + EntranceStairs.Tread * 0.5f) * 0.25f;
            Assert.Less(lastTop - groundThere, EntranceStairs.Riser * 2.0f, "the flight ended in the air");
            Assert.LessOrEqual(f.Steps, EntranceStairs.MaxSteps);
        }

        [Test]
        public void EveryRealEntranceFlightLandsOnThePaving()
        {
            var world = new CityWorld(Seed);
            int flights = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 14))
            {
                if (site.OnRoof || site.Shell || site.Sides >= 3 || !BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs)
                {
                    continue;
                }
                float wallZ = site.Params.Depth * 0.5f;
                float lineZ = wallZ + EntranceStairs.Width * 0.5f;
                float half = Mathf.Max(EntranceStairs.LandingLength, BuildingInterior.EntranceWidth(site) + 0.4f) * 0.5f;   // as the mesh builder: a pair of doors has a longer landing
                float Paving(float x, float z)
                {
                    site.LocalToWorld(x, z, out double wx, out double wz);
                    return (float)(world.Plan.GroundHeight(wx, wz) + PlanBuildings.PavingRise);
                }
                EntranceStairs.Flight f = EntranceStairs.Plan(site.BaseHeight, (side, along) => Paving(side * (half + along), lineZ));
                if (f.Steps == EntranceStairs.MaxSteps)
                {
                    continue;   // a cliff: capped, not a landing to test
                }
                float x = f.Side * (half + f.StartOf(f.Steps) + EntranceStairs.Tread * 0.5f);
                float gap = f.TopOf(f.Steps, site.BaseHeight) - Paving(x, lineZ);
                Assert.Less(gap, EntranceStairs.Riser * 2.0f + 0.01f, $"building {site.Id}: its entrance stairs end {gap:0.00} m above the paving");   // (two risers, to the centimetre: seed 47360519 ended one 0.4408 m up)
                flights++;
            }
            Assert.Greater(flights, 3, "need some real raised doors");
        }
    }
}
