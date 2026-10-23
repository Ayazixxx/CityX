using CityX.Character;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// San Francisco's hills (PlanFields.Hillock; owner, 2026-10-16: "maybe make the terrain more hilly overall"). At the game's own scale (varied:
    /// before them the rolling ground, stretched with the buildings' footprint, left half the streets under 1 %) a good share of the land round
    /// the downtown is on a hill and the hills are steep, as San Francisco's are; no hill rises by a river or a shore (a river would cut a gorge
    /// through it); and the ground never sinks to where the player's fall-through rescue would catch them.
    /// </summary>
    public sealed class HillTests
    {
        private const int Seed = 80469103;

        [Test]
        public void TheGamesCityHasSteepHills()
        {
            PlanConfig cfg = PlanConfig.For(CityScale.Varied);
            var fields = new PlanFields(Seed, cfg);
            int land = 0, hilly = 0, steep = 0;
            double highest = double.MinValue, highestHill = 0.0;
            const double step = 20.0;
            for (double x = -2500.0; x <= 2500.0; x += step)
            {
                for (double z = -2500.0; z <= 2500.0; z += step)
                {
                    if (fields.IsWater(x, z)) continue;
                    land++;
                    double hill = fields.Hillock(x, z);
                    if (hill > 2.0) hilly++;
                    highestHill = System.Math.Max(highestHill, hill);
                    highest = System.Math.Max(highest, fields.Height(x, z));
                    double gx = fields.Height(x + 2.0, z) - fields.Height(x - 2.0, z), gz = fields.Height(x, z + 2.0) - fields.Height(x, z - 2.0);
                    if (System.Math.Sqrt(gx * gx + gz * gz) / 4.0 >= 0.14) steep++;
                }
            }
            double hillShare = (double)hilly / land, steepShare = (double)steep / land;
            Debug.Log($"CityX hills (varied): {hillShare:P0} of the land within 2.5 km of the origin on a hill, {steepShare:P1} at 14 % or steeper; " +
                $"the highest hill {highestHill:F0} m over the ground round it, the ground up to {highest:F0} m");
            Assert.That(hillShare, Is.GreaterThan(0.12), "too little of the city on a hill");
            Assert.That(steepShare, Is.GreaterThan(0.02), "the hills are not steep");
            Assert.That(highestHill, Is.LessThanOrEqualTo(cfg.HillockAmplitude + 1e-9));
            Assert.That(highestHill, Is.GreaterThan(cfg.HillockAmplitude * 0.6), "no hill comes near its full height");
        }

        [Test]
        public void NoHillRisesByTheWaterAndTheGroundStaysAboveTheFallRescue()
        {
            foreach (CityScale scale in new[] { CityScale.Classic, CityScale.Varied })
            {
                PlanConfig cfg = PlanConfig.For(scale);
                var fields = new PlanFields(Seed, cfg);
                int wet = 0;
                double lowest = double.MaxValue;
                for (double x = -3000.0; x <= 3000.0; x += 25.0)
                {
                    for (double z = -3000.0; z <= 3000.0; z += 25.0)
                    {
                        lowest = System.Math.Min(lowest, fields.Height(x, z));
                        if (!fields.IsWater(x, z) && System.Math.Abs(fields.RiverNoise(x, z)) >= cfg.RiverBand * 2.2) continue;
                        wet++;
                        Assert.AreEqual(0.0, fields.Hillock(x, z), 1e-9, $"{scale.Name}: a hill at ({x}, {z}) by the water");
                    }
                }
                Debug.Log($"CityX hills ({scale.Name}): {wet} points in or by the water, none on a hill; the lowest ground {lowest:F1} m");
                Assert.Greater(wet, 0);
                Assert.That(lowest, Is.GreaterThan(PlayerLocomotion.FallThroughWorldY + 5.0f), $"{scale.Name}: the ground sinks toward the fall-through rescue");
            }
        }
    }
}
