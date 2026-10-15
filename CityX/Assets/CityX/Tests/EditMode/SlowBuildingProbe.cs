using System.Text;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Not a test: times the shared steps of building one of the slow chunks' buildings (ChunkCostBench), to find the one that explodes.</summary>
    [Explicit("a measurement, not a test")]
    public sealed class SlowBuildingProbe
    {
        [Test]
        public void TimeTheStepsOfASlowBuilding()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                var world = new CityWorld(74102958);
                var report = new StringBuilder();
                // The four street-level buildings with the biggest footprints: the kind whose stairs once took seconds to plan.
                var biggest = new System.Collections.Generic.List<BuildingSite>();
                for (int i = 0; i < world.BuildingCount; i++)
                {
                    BuildingSite b = world.Building(i);
                    if (!b.OnRoof && !b.Params.IsSpecial) biggest.Add(b);
                }
                biggest.Sort((a, b) => (b.Params.Width * b.Params.Depth).CompareTo(a.Params.Width * a.Params.Depth));
                foreach (int id in new[] { biggest[0].Id, biggest[1].Id, biggest[2].Id, biggest[3].Id })
                {
                    BuildingSite site = world.Building(id);
                    report.AppendLine($"#{id} {site.Params.Width:0}x{site.Params.Depth:0}x{site.Params.Height:0}, floors {BuildingInterior.FloorCountFor(site.Params.Height)}");
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    WindowPlan plan = WindowPlan.For(world, site);
                    report.AppendLine($"  WindowPlan.For      {clock.Elapsed.TotalMilliseconds,10:0.0} ms");
                    clock.Restart();
                    BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                    report.AppendLine($"  BuildingStairs.PlanFor {clock.Elapsed.TotalMilliseconds,7:0.0} ms");
                    clock.Restart();
                    var lights = RoomLighting.PlansFor(site);
                    report.AppendLine($"  RoomLighting.PlansFor {clock.Elapsed.TotalMilliseconds,8:0.0} ms");
                    clock.Restart();
                    var buffers = new MeshBuffer[4];
                    for (int i = 0; i < 4; i++) buffers[i] = new MeshBuffer();
                    var origin = new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ);
                    CityMeshBuilder.BuildSite(world, site, origin, buffers[0], buffers[1], buffers[2], buffers[3], null, null, null, null, true);
                    report.AppendLine($"  BuildSite far       {clock.Elapsed.TotalMilliseconds,10:0.0} ms");
                    clock.Restart();
                    stairs = BuildingStairs.PlanFor(site);
                    report.AppendLine($"  PlanFor again       {clock.Elapsed.TotalMilliseconds,10:0.0} ms");
                    _ = plan;
                    _ = lights;
                    _ = stairs;
                }
                TestContext.WriteLine(report.ToString());
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "slow-building-probe.txt"), report.ToString());
            }
            finally
            {
                CityScale.Active = before;
            }
        }
    }
}
