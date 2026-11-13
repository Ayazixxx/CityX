using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The water's pure rules (docs/WATER.md): where water is and how deep (CurrentWater, WaterBody), how it slows and floats a body
    /// (Wading), and how a flood rises and recedes (FloodTimeline).
    /// </summary>
    public sealed class WaterRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            CurrentWater.Clear();
        }

        [Test]
        public void TheWorldIsDryWithNoWater()
        {
            Assert.IsFalse(CurrentWater.Any);
            Assert.AreEqual(0.0f, CurrentWater.DepthAt(Vector3.zero));
        }

        [Test]
        public void DepthIsTheSurfaceOverAPointInsideTheWaterAndNothingOutsideIt()
        {
            WaterBody pool = WaterBody.Square(new Vector3(10.0f, 2.0f, 10.0f), 8.0f, 0.6f);
            CurrentWater.Add(pool);

            WaterHere found = CurrentWater.At(new Vector3(10.0f, 2.0f, 10.0f));
            Assert.AreEqual(0.6f, found.Depth, 1e-4f);
            Assert.AreEqual(pool.SurfaceY, found.SurfaceY, 1e-4f);
            Assert.AreEqual(0.0f, CurrentWater.DepthAt(new Vector3(20.0f, 2.0f, 10.0f)), "beside the pool is dry");
            Assert.AreEqual(0.0f, CurrentWater.DepthAt(new Vector3(10.0f, 3.0f, 10.0f)), "above its surface is dry");
            Assert.AreEqual(0.0f, CurrentWater.DepthAt(new Vector3(10.0f, -3.0f, 10.0f)), "under its floor is not in it");
        }

        [Test]
        public void OverlappingWaterIsAsDeepAsTheDeepest()
        {
            CurrentWater.Add(WaterBody.Square(Vector3.zero, 10.0f, 0.3f));
            WaterBody flood = WaterBody.Square(Vector3.zero, 40.0f, 2.0f);
            CurrentWater.Add(flood);
            WaterHere found = CurrentWater.At(Vector3.zero);
            Assert.AreEqual(2.0f, found.Depth, 1e-4f);
            Assert.AreEqual(flood.SurfaceY, found.SurfaceY, 1e-4f);
            CurrentWater.Remove(flood);
            Assert.AreEqual(0.3f, CurrentWater.DepthAt(Vector3.zero), 1e-4f);
        }

        [Test]
        public void WadingSlowsInStepWithTheDepthAndNoFurtherThanTheChest()
        {
            Assert.AreEqual(1.0f, Wading.SpeedShare(0.0f), 1e-6f);
            float ankle = Wading.SpeedShare(0.1f), knee = Wading.SpeedShare(0.27f), waist = Wading.SpeedShare(0.45f);
            Assert.That(ankle, Is.GreaterThan(0.85f), "ankle-deep barely slows");
            Assert.That(knee, Is.InRange(0.65f, 0.85f), "knee-deep slows noticeably");
            Assert.Greater(ankle, knee);
            Assert.Greater(knee, waist);
            Assert.AreEqual(Wading.SlowestWade, Wading.SpeedShare(Wading.ChestDepth), 1e-6f);
            Assert.AreEqual(Wading.SlowestWade, Wading.SpeedShare(2.0f), 1e-6f, "deeper than the chest is no slower");
            Assert.Less(Wading.JumpShare(0.45f), 1.0f, "a jump out of deep water is weaker");
        }

        [Test]
        public void FloatingStartsAndStopsAtDifferentDepthsSoItNeverFlickers()
        {
            Assert.IsFalse(Wading.Floats(false, 0.6f));
            Assert.IsTrue(Wading.Floats(false, Wading.StartFloatingDepth));
            Assert.IsTrue(Wading.Floats(true, 0.6f), "a floating body stays floating a little shallower");
            Assert.IsFalse(Wading.Floats(true, Wading.StopFloatingDepth - 0.01f));
        }

        [Test]
        public void AFloatingBodySinksSlowlyRisesWithSpaceAndStopsWithItsHeadOut()
        {
            const float height = 2.6f;
            Assert.Less(Wading.FloatingClimb(1.5f, height, false), 0.0f, "let go, it sinks");
            Assert.Greater(Wading.FloatingClimb(1.5f, height, true), 0.0f, "Space held, it rises");
            Assert.AreEqual(0.0f, Wading.FloatingClimb(Wading.FloatingDepth, height, true), 1e-4f, "at its floating height it holds");
            Assert.LessOrEqual(Wading.FloatingClimb(Wading.FloatingDepth - 0.1f, height, true), 0.0f, "above it, it settles back");

            // Held, it comes up from deep down to float with its head out and stays there.
            float feet = -8.0f, surface = 0.0f;
            for (int i = 0; i < 60 * 30; i++)
            {
                feet += Wading.FloatingClimb((surface - feet) / height, height, true) / 60.0f;
            }
            Assert.AreEqual(Wading.FloatingDepth, (surface - feet) / height, 0.01f);
            Assert.Greater(feet + height - surface, 0.5f, "the head is not out of the water");
        }

        [Test]
        public void TheFlowCarriesAFloatingBodyFullyAndAWaderByItsDepth()
        {
            Assert.AreEqual(1.0f, Wading.FlowCarry(0.7f, true));
            Assert.AreEqual(0.0f, Wading.FlowCarry(0.0f, false));
            Assert.Less(Wading.FlowCarry(0.1f, false), Wading.FlowCarry(0.4f, false));
        }

        [Test]
        public void AFloodRisesHoldsAndRecedes()
        {
            var flood = new FloodTimeline(10.0f, 5.0f, 10.0f, 3.0f);
            Assert.AreEqual(0.0f, flood.LevelAt(0.0f));
            Assert.AreEqual(1.5f, flood.LevelAt(5.0f), 1e-4f);
            Assert.AreEqual(3.0f, flood.LevelAt(12.0f), 1e-4f);
            Assert.AreEqual(1.5f, flood.LevelAt(20.0f), 1e-4f);
            Assert.AreEqual(0.0f, flood.LevelAt(25.0f));
            Assert.IsTrue(flood.IsOver(25.0f));
            float last = 0.0f;
            for (float t = 0.0f; t < 10.0f; t += 0.5f)
            {
                Assert.GreaterOrEqual(flood.LevelAt(t), last, "it never drops while it rises");
                last = flood.LevelAt(t);
            }
        }
    }
}
