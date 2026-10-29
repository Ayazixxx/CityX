using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The fire on the ground with no scene (docs/FIRE.md): a flat floor, no rain, no water unless a test says so.</summary>
    public sealed class FireFieldTests
    {
        private const float Step = 1.0f / 60.0f;

        private static bool FlatFloor(float x, float z, float nearY, out float floorY)
        {
            floorY = 0.0f;
            return true;
        }

        private static FireField Field(float rain = 0.0f, FireField.FloorProbe floor = null, System.Func<Vector3, bool> underWater = null)
        {
            return new FireField(new System.Random(7), floor ?? FlatFloor, _ => rain, underWater);
        }

        /// <summary>A trail of gasoline along +X from the origin, a cell every <see cref="FireRules.CellMetres"/>.</summary>
        private static void Trail(FireField field, int cells, float litresEach = 0.1f)
        {
            for (int i = 0; i < cells; i++)
            {
                field.Pour(new Vector3(FireField.Middle(i), 0.0f, FireField.Middle(0)), litresEach);
            }
        }

        private static void Run(FireField field, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                field.Tick(Step);
            }
        }

        [Test]
        public void ALitTrailBurnsAlongItsLength()
        {
            FireField field = Field();
            Trail(field, 12);
            Vector3 farEnd = new Vector3(FireField.Middle(11), 0.0f, FireField.Middle(0));
            Assert.AreEqual(1, field.Ignite(Vector3.zero + new Vector3(FireField.Middle(0), 0.0f, FireField.Middle(0)), 0.1f, true), "the near end did not light");
            Assert.IsFalse(field.TryBurningUnder(farEnd, out _), "the far end lit at once");

            Run(field, 8.0f);
            Assert.IsTrue(field.TryBurningUnder(farEnd, out FireCell far), "the fire did not run along the trail to its far end in eight seconds");
            Assert.IsTrue(far.Gasoline, "the far end burns as bare ground, not as fuel");
            Assert.GreaterOrEqual(field.EverLit, 12, "not every cell of the trail caught");
        }

        [Test]
        public void BarePavementBurnsOutAndSpreadsNoFurther()
        {
            FireField field = Field();
            int lit = field.Ignite(new Vector3(10.0f, 0.0f, 10.0f), 1.0f, false);
            Assert.Greater(lit, 3, "a fireball's burst lit too little bare ground");
            Run(field, 0.5f);
            Assert.AreEqual(lit, field.EverLit, "bare ground lit more bare ground");

            Run(field, FireRules.BareFuelSeconds + 1.0f);
            Assert.AreEqual(0, field.BurningCount, "bare ground kept burning past its fuel");
            Assert.AreEqual(lit, field.EverLit, "bare ground lit more bare ground");
            Assert.AreEqual(lit, field.CountBurned(), "the burned-out cells left no black spots");
        }

        [Test]
        public void AGasolineFireBurnsOutIntoScorchedCellsAndAFringe()
        {
            FireField field = Field();
            Trail(field, 4);
            field.Ignite(new Vector3(FireField.Middle(0), 0.0f, FireField.Middle(0)), 0.1f, true);
            Run(field, FireRules.PuddleFuelSeconds + FireRules.BareFuelSeconds + 2.0f);
            Assert.AreEqual(0, field.BurningCount, "the gasoline fire never burned out");
            Assert.GreaterOrEqual(field.CountBurned(), 4, "the trail's cells are not scorched");
            // The fringe: the bare cells round a four-cell trail are at most the ring round it.
            Assert.LessOrEqual(field.EverLit, 4 + 14, "the fire ran out across the bare street");
            Assert.Greater(field.EverLit, 4, "no bare ground round the fuel caught at all");
        }

        [Test]
        public void WaterPutsItOutAndTheGroundWillNotCatchWhileWet()
        {
            FireField field = Field();
            Trail(field, 6);
            Vector3 start = new Vector3(FireField.Middle(0), 0.0f, FireField.Middle(0));
            field.Ignite(start, 0.1f, true);
            Run(field, 1.0f);
            Assert.Greater(field.BurningCount, 0);
            Assert.Greater(field.Douse(start + Vector3.right * 2.0f, 4.0f), 0, "nothing was doused");
            Assert.AreEqual(0, field.BurningCount, "the fire kept burning under the water");
            Assert.AreEqual(0, field.Ignite(start, 0.1f, true), "wet ground caught again at once");
            Run(field, FireRules.WetSeconds + 0.5f);
            Assert.AreEqual(1, field.Ignite(start, 0.1f, true), "dry again, the gasoline left did not catch");
        }

        [Test]
        public void RainKeepsBareGroundFromCatchingAndBurnsFuelOffFaster()
        {
            FireField wet = Field(rain: 1.0f);
            Assert.AreEqual(0, wet.Ignite(new Vector3(5.0f, 0.0f, 5.0f), 1.0f, false), "bare pavement caught in a downpour");

            FireField dry = Field();
            foreach (FireField field in new[] { wet, dry })
            {
                field.Pour(new Vector3(FireField.Middle(20), 0.0f, FireField.Middle(20)), 1.0f);
                field.Ignite(new Vector3(FireField.Middle(20), 0.0f, FireField.Middle(20)), 0.1f, true);
            }
            Run(wet, FireRules.PuddleFuelSeconds * 0.6f);
            Run(dry, FireRules.PuddleFuelSeconds * 0.6f);
            Assert.AreEqual(0, wet.BurningCount, "the puddle in the rain is still burning at 60% of its dry time");
            Assert.Greater(dry.BurningCount, 0, "the dry puddle went out early");
        }

        [Test]
        public void FireDoesNotJumpOffARoofEdge()
        {
            // A roof ten metres up over x < 0, the street at x >= 0.
            bool RoofAndStreet(float x, float z, float nearY, out float floorY)
            {
                floorY = x < 0.0f ? 10.0f : 0.0f;
                return true;
            }
            FireField field = Field(floor: RoofAndStreet);
            for (int i = -6; i < 4; i++)
            {
                float x = FireField.Middle(i);
                field.Pour(new Vector3(x, x < 0.0f ? 10.0f : 0.0f, FireField.Middle(0)), 0.2f);
            }
            field.Ignite(new Vector3(FireField.Middle(-6), 10.0f, FireField.Middle(0)), 0.1f, true);
            Run(field, 8.0f);
            Assert.IsTrue(field.TryBurningUnder(new Vector3(FireField.Middle(-1), 10.0f, FireField.Middle(0)), out _), "the roof's own trail did not burn to its edge");
            Assert.IsFalse(field.TryCellUnder(new Vector3(FireField.Middle(0), 0.0f, FireField.Middle(0)), out FireCell street) && street.Burning,
                "the fire jumped ten metres down off the roof's edge");
        }

        [Test]
        public void PouringOnOneSpotMakesAPuddle()
        {
            FireField field = Field();
            Vector3 spot = new Vector3(FireField.Middle(3), 0.0f, FireField.Middle(3));
            for (int i = 0; i < 60 * 5; i++)
            {
                field.Pour(spot, FireRules.PourLitresPerSecond * Step);   // five seconds on one spot
            }
            Assert.AreEqual(FireRules.MaxLitres, field.LitresUnder(spot), 0.01f, "the cell holds more than a cell can");
            Assert.Greater(field.LitresUnder(spot + Vector3.right * FireRules.CellMetres), FireRules.MinLitres, "nothing spilled into the cell beside");
            Assert.Greater(field.LitresUnder(spot + Vector3.forward * FireRules.CellMetres), FireRules.MinLitres, "nothing spilled into the cell ahead");
        }

        [Test]
        public void ACellUnderWaterNeverBurns()
        {
            FireField field = Field(underWater: p => p.x > 2.0f);
            Trail(field, 8);
            field.Ignite(new Vector3(FireField.Middle(0), 0.0f, FireField.Middle(0)), 0.1f, true);
            Run(field, 6.0f);
            Assert.IsTrue(field.TryBurningUnder(new Vector3(FireField.Middle(1), 0.0f, FireField.Middle(0)), out _), "the dry part of the trail did not burn");
            Assert.IsFalse(field.TryBurningUnder(new Vector3(FireField.Middle(6), 0.0f, FireField.Middle(0)), out _), "gasoline under water burned");
        }

        [Test]
        public void AnotherMachinesFireIsCosmeticUntilThisOneLightsIt()
        {
            FireField field = Field();
            Trail(field, 6);
            Vector3 start = new Vector3(FireField.Middle(0), 0.0f, FireField.Middle(0));
            field.Ignite(start, 0.1f, true, cosmetic: true);
            Run(field, 3.0f);
            foreach (FireCell cell in field.Burning)
            {
                Assert.IsTrue(cell.Cosmetic, "a cell lit from a cosmetic fire is not cosmetic");
            }
            field.Ignite(start, 0.1f, true, cosmetic: false, lighter: 3);
            Assert.IsTrue(field.TryBurningUnder(start, out FireCell lit) && !lit.Cosmetic && lit.Lighter == 3, "this machine lighting the same cell did not make the fire its own");
        }
    }
}
