using System.Diagnostics;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>Not a test: how long the game's city plan takes to work out, stage by stage (the title waits for it). Explicit; run by name.</summary>
    [Explicit("a measurement, not a test")]
    public sealed class PlanTimeProbe
    {
        [Test]
        public void TimeThePlan()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                const int seed = PlanPrewarm.DefaultSeed;
                var watch = Stopwatch.StartNew();
                PlanConfig config = PlanConfig.For(CityScale.Default);
                CityPlanData data = CityPlan.Generate(seed, config);
                long generate = watch.ElapsedMilliseconds;
                PlanGeometry.Build(data);
                long geometry = watch.ElapsedMilliseconds;
                PlanBuildings.Resolve(data, data.Fields, unchecked((uint)seed));
                long resolve = watch.ElapsedMilliseconds;
                watch.Restart();
                PlanWorld world = PlanWorld.Create(seed, config);
                long whole = watch.ElapsedMilliseconds;
                watch.Restart();
                PlanWorld.Create(seed, config);
                long again = watch.ElapsedMilliseconds;
                long print = 17;
                foreach (BuildingSite b in world.Buildings)
                {
                    foreach (double v in new double[] { b.CentreX, b.CentreZ, b.AxisX, b.BaseHeight, b.PodiumBase, b.DoorGroundHeight, b.GroundLevel, b.Params.Height, b.Params.Width, b.Seed })
                    {
                        print = unchecked(print * 31 + System.BitConverter.DoubleToInt64Bits(v));
                    }
                }
                UnityEngine.Debug.Log($"CityX plan fingerprint {print}");
                UnityEngine.Debug.Log($"CityX plan places: house {world.TutorialHouse.OriginX:R} {world.TutorialHouse.OriginZ:R} {world.TutorialHouse.AxisX:R} {world.TutorialHouse.FloorHeight:R} hub {world.SkyHub.CentreX:R} {world.SkyHub.CentreZ:R} {world.SkyHub.StartAngle:R} {world.SkyHub.FootHeight:R} buildings {world.Buildings.Length}");
                UnityEngine.Debug.Log($"CityX plan time: generate {generate} ms, geometry {geometry - generate} ms, resolve {resolve - geometry} ms; whole {whole} ms " +
                    $"({world.Buildings.Length} buildings), again {again} ms");
            }
            finally
            {
                CityScale.Active = before;
            }
        }
    }
}
