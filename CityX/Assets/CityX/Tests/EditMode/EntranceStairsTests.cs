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
        private const int Seed = 95998845;

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
        public void ALowDoorsStairIsOneFlightAlongTheWallAsBefore()
        {
            // A 2 m rise on flat ground with a long wall: one flight, the same steps the single-flight planner gives.
            EntranceStairs.Folded f = EntranceStairs.PlanFolded(2.0f, 0.0f, 1.0f, 10.0f, 3, (lane, x) => 0.0f);
            Assert.IsTrue(f.Reaches);
            Assert.AreEqual(1, f.Runs.Count);
            Assert.AreEqual(0, f.Turns.Count);
            Assert.AreEqual(EntranceStairs.Plan(2.0f, (side, along) => 0.0f).Steps, f.Steps);
        }

        [Test]
        public void AHighDoorsStairFoldsBackWithinItsWallAndReachesTheGround()
        {
            // An 8 m rise in front of a 16 m wall (2026-10-16: on the hills a single flight ran 20 m, past the end of its own front).
            const float wallHalf = 8.0f;
            EntranceStairs.Folded f = EntranceStairs.PlanFolded(8.0f, 0.0f, 1.0f, wallHalf, 3, (lane, x) => 0.0f);
            Assert.IsTrue(f.Reaches, "the stair does not reach the ground");
            Assert.Greater(f.Lanes, 1, "it should fold");
            Assert.LessOrEqual(f.Lanes, 3);
            float y = 8.0f;
            for (int i = 0; i < f.Runs.Count; i++)
            {
                EntranceStairs.Run run = f.Runs[i];
                Assert.AreEqual(i, run.Lane, "each flight a lane further out");
                Assert.AreEqual(y, run.From, 1e-4f, $"flight {i} does not start where the stair is");
                for (int k = 1; k <= run.Steps; k++)
                {
                    Assert.LessOrEqual(Mathf.Max(Mathf.Abs(run.StepA(k)), Mathf.Abs(run.StepB(k))), wallHalf - EntranceStairs.EndMargin + 1e-4f, $"flight {i} step {k} past the wall's end");
                }
                y = run.Bottom;
                if (i < f.Turns.Count)
                {
                    EntranceStairs.Turn t = f.Turns[i];
                    Assert.AreEqual(y, t.Top, 1e-4f, "the turn is not at the flight's foot");
                    Assert.AreEqual(run.End, t.X0, 1e-4f, "the turn does not start at the flight's end");
                    Assert.LessOrEqual(Mathf.Max(Mathf.Abs(t.X0), Mathf.Abs(t.X1)), wallHalf - EntranceStairs.EndMargin + 1e-4f, "a turn past the wall's end");
                    Assert.AreEqual(-run.Dir, f.Runs[i + 1].Dir, "the next flight does not come back");
                }
            }
            Assert.LessOrEqual(f.Bottom, EntranceStairs.Riser + 0.02f, "the last step must be one riser off the ground at most");
        }

        [Test]
        public void AVeryHighDoorWithALongFrontFoldsPastSixtySteps()
        {
            // A 16 m rise in front of a 30 m wall: more than a single flight's MaxSteps, within the folded stair's (seed 19077151: five doors near
            // the spawn, 13-17 m up, stopped at 60 steps with their walls' room to spare).
            EntranceStairs.Folded f = EntranceStairs.PlanFolded(16.0f, 0.0f, 1.0f, 15.0f, 3, (lane, x) => 0.0f);
            Assert.IsTrue(f.Reaches, "the stair does not reach the ground");
            Assert.Greater(f.Steps, EntranceStairs.MaxSteps);
            Assert.LessOrEqual(f.Steps, EntranceStairs.MaxFoldedSteps);
            Assert.LessOrEqual(f.Bottom, EntranceStairs.Riser + 0.02f, "the last step must be one riser off the ground at most");
        }

        [Test]
        public void AHighDoorOnAShortFrontReachesTheGroundOnATightStair()
        {
            // A 14 m rise before a 13.9 m wall with 4.2 m to the road (seed 19077151's buildings 1743 and 1744): two broad lanes fit and end metres up;
            // three tight ones fit the same room and reach the ground.
            const float wallHalf = 6.95f, room = 4.2f;
            int broadLanes = Mathf.FloorToInt((room - 0.4f) / EntranceStairs.Width), tightLanes = Mathf.FloorToInt((room - 0.4f) / EntranceStairs.NarrowWidth);
            EntranceStairs.Folded broad = EntranceStairs.PlanFolded(14.0f, 0.0f, 1.0f, wallHalf, broadLanes, (lane, x) => 0.0f);
            EntranceStairs.Folded tight = EntranceStairs.PlanFolded(14.0f, 0.0f, 1.0f, wallHalf, tightLanes, (lane, x) => 0.0f, EntranceStairs.NarrowWidth);
            Assert.AreEqual(2, broadLanes);
            Assert.AreEqual(3, tightLanes);
            Assert.IsFalse(broad.Reaches, "the broad stair should not fit");
            Assert.IsTrue(tight.Reaches, $"the tight stair does not reach the ground: it ends {tight.Bottom:F1} m up");
            Assert.AreEqual(EntranceStairs.NarrowWidth, tight.LaneWidth);
            Assert.LessOrEqual(tight.Lanes * tight.LaneWidth, room - 0.4f + 1e-4f, "the tight stair reaches further out than the broad one may");
            foreach (EntranceStairs.Turn t in tight.Turns)
            {
                Assert.AreEqual(EntranceStairs.NarrowWidth, Mathf.Abs(t.X1 - t.X0), 1e-4f, "a turn platform is not a tight flight's width long");
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(t.X0), Mathf.Abs(t.X1)), wallHalf - EntranceStairs.EndMargin + 1e-4f, "a turn past the wall's end");
            }
        }

        [Test]
        public void AStairWithNoRoomToFoldSaysItEndsInTheAir()
        {
            EntranceStairs.Folded f = EntranceStairs.PlanFolded(8.0f, 0.0f, 1.0f, 4.0f, 1, (lane, x) => 0.0f);
            Assert.IsFalse(f.Reaches);
            Assert.AreEqual(1, f.Lanes);
            Assert.Greater(f.Bottom, 1.0f);
        }

        [Test]
        public void EveryRealEntranceStairStaysInFrontOfItsWallAndLandsOnThePaving()
        {
            // The stairs as built (EntranceStairs.ForDoor), at every raised door near the spawn of the game's hills: within the wall's ends, no
            // more lanes than fit before the road, reaching the paving (a door too high for the stairs that fit is counted, and must be rare).
            var world = new CityWorld(Seed);
            int stairs = 0, folded = 0, tight = 0, short_ = 0;
            float worstDrop = 0.0f;
            var shortOnes = new System.Collections.Generic.List<string>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 14))
            {
                if (site.OnRoof || site.Shell || site.Sides >= 3 || !BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs)
                {
                    continue;
                }
                float wallZ = site.Params.Depth * 0.5f, wallHalf = site.Params.Width * 0.5f;
                EntranceStairs.Folded f = EntranceStairs.ForDoor(world.Plan, site, 0.0f, site.BaseHeight, wallZ, wallHalf);
                int lanes = EntranceStairs.LanesFor(world.Plan, site, 0.0f, wallZ, wallHalf, f.LaneWidth);
                Assert.LessOrEqual(f.Lanes, lanes, $"building {site.Id}: more lanes than fit before the road");
                foreach (EntranceStairs.Run run in f.Runs)
                {
                    for (int k = 1; k <= run.Steps; k++)
                    {
                        Assert.LessOrEqual(Mathf.Max(Mathf.Abs(run.StepA(k)), Mathf.Abs(run.StepB(k))), wallHalf - EntranceStairs.EndMargin + 1e-3f,
                            $"building {site.Id}: a step past the end of its front");
                    }
                }
                stairs++;
                if (f.Lanes > 1) folded++;
                if (f.LaneWidth < EntranceStairs.Width) tight++;
                if (!f.Reaches)
                {
                    // How far its end stands over the paving there: a drop to jump down, or a ledge to climb.
                    short_++;
                    EntranceStairs.Run last = f.Runs[f.Runs.Count - 1];
                    site.LocalToWorld(last.End, wallZ + (last.Lane + 0.5f) * f.LaneWidth, out double ex, out double ez);
                    worstDrop = Mathf.Max(worstDrop, f.Bottom - (float)(world.Plan.GroundHeight(ex, ez) + PlanBuildings.PavingRise));
                    shortOnes.Add($"#{site.Id}: door {site.BaseHeight - site.DoorGroundHeight:F1} m up, front {site.Params.Width:F1} m, {lanes} lanes fit, {f.Steps} steps, ends {f.Bottom - (float)(world.Plan.GroundHeight(ex, ez) + PlanBuildings.PavingRise):F1} m up");
                }
            }
            Debug.Log($"CityX entrance stairs near the spawn: {stairs}, {folded} folded ({tight} tight), {short_} too high for the stairs that fit (their ends up to {worstDrop:F1} m over the paving)" +
                (shortOnes.Count > 0 ? ":\n  " + string.Join("\n  ", shortOnes) : ""));
            Assert.Greater(stairs, 3, "need some real raised doors");
            Assert.That(short_, Is.LessThanOrEqualTo(stairs / 40), "too many doors the stairs cannot reach");   // none on seed 19077151 since the tight stair (was 10 of 199)
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
                float wallZ = site.Params.Depth * 0.5f, wallHalf = site.Params.Width * 0.5f;
                float Paving(float x, float z)
                {
                    site.LocalToWorld(x, z, out double wx, out double wz);
                    return (float)(world.Plan.GroundHeight(wx, wz) + PlanBuildings.PavingRise);
                }
                EntranceStairs.Folded f = EntranceStairs.ForDoor(world.Plan, site, 0.0f, site.BaseHeight, wallZ, wallHalf);
                if (!f.Reaches)
                {
                    continue;   // too high for the stairs that fit (EveryRealEntranceStairStaysInFrontOfItsWallAndLandsOnThePaving counts these)
                }
                // Where the foot lands stepping off the last step: the next tread's middle in the last flight's lane (the planner stops a flight at the
                // first step the ground there has risen to meet, so it is at most a riser down). Under the last step's own middle the ground can be
                // lower on a steep bank (seed 63428175's building 3040: 0.47 m), but each step is a solid block down to the paving, so nothing hangs.
                EntranceStairs.Run last = f.Runs[f.Runs.Count - 1];
                float laneZ = wallZ + (last.Lane + 0.5f) * f.LaneWidth;
                float x = last.StepA(last.Steps + 1) + last.Dir * EntranceStairs.Tread * 0.5f;
                float gap = last.Bottom - Paving(x, laneZ);
                if (f.Turns.Count >= f.Runs.Count)
                {
                    // It ended on a turn: the platform's own ground.
                    EntranceStairs.Turn turn = f.Turns[f.Turns.Count - 1];
                    gap = turn.Top - Mathf.Max(Paving((turn.X0 + turn.X1) * 0.5f, laneZ), Paving((turn.X0 + turn.X1) * 0.5f, laneZ + f.LaneWidth));
                }
                Assert.Less(gap, EntranceStairs.Riser * 2.0f + 0.01f, $"building {site.Id}: its entrance stairs end {gap:0.00} m above the paving");   // (two risers, to the centimetre: seed 47360519 ended one 0.4408 m up)
                flights++;
            }
            Assert.Greater(flights, 3, "need some real raised doors");
        }
    }
}
