using System.Collections.Generic;
using System.Linq;
using System.Text;
using CityX.World;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Counts how varied the outsides of the plan city's buildings actually are: massing, facade style, roof, and how many
    /// distinct looks that makes. A number to move when the exteriors are reworked, not a test.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -nographics -executeMethod CityX.ViewEditor.ExteriorSurvey.Run
    /// </summary>
    public static class ExteriorSurvey
    {
        private const int Seed = 95998845;

        [MenuItem("CityX/Survey Exteriors")]
        public static void Run()
        {
            var world = new CityWorld(Seed);
            int count = world.BuildingCount;

            var mass = new Dictionary<BuildingMass, int>();
            var style = new Dictionary<FacadeStyle, int>();
            var roof = new Dictionary<string, int>();
            var zone = new Dictionary<ZoneKind, int>();
            var looks = new HashSet<string>();
            var skylineHeights = new List<float>();
            int total = 0;
            int special = 0;

            for (int id = 0; id < count; id++)
            {
                BuildingSite site = world.Building(id);
                if (!site.Params.HasBuilding)
                {
                    continue;
                }
                total++;
                WindowPlan plan = WindowPlan.For(world, site);
                Bump(mass, site.Params.Mass);
                Bump(style, plan.Style);
                Bump(roof, string.IsNullOrEmpty(site.Params.Roof) ? "(none)" : site.Params.Roof);
                Bump(zone, site.Zone);
                looks.Add($"{site.Params.Mass}/{plan.Style}/{site.Params.Roof}/{site.Zone}");
                special += site.Params.IsSpecial ? 1 : 0;
                skylineHeights.Add(site.Params.Height);
            }

            var report = new StringBuilder();
            report.AppendLine($"CityX EXTERIOR SURVEY: {total} buildings, seed {Seed}");
            report.AppendLine("footprint: every building is ONE axis-aligned rectangle extruded straight up (no L/T/U plans, no setbacks, no podium+tower, no tapered or stepped crowns)");
            report.AppendLine("wall material: one shared tiling concrete for every building (no per-building colour, no per-style texture)");
            report.AppendLine("massing: " + Format(mass, total));
            report.AppendLine("facade style: " + Format(style, total));
            report.AppendLine("roof: " + Format(roof, total));
            report.AppendLine("zone: " + Format(zone, total));
            report.AppendLine($"distinct (mass, style, roof, zone) combinations: {looks.Count} for {total} buildings");
            report.AppendLine($"special landmark buildings: {special}");
            Debug.Log(report.ToString());
        }

        private static void Bump<T>(Dictionary<T, int> map, T key)
        {
            map.TryGetValue(key, out int n);
            map[key] = n + 1;
        }

        private static string Format<T>(Dictionary<T, int> map, int total)
        {
            return string.Join(", ", map.OrderByDescending(p => p.Value)
                .Select(p => $"{p.Key} {p.Value} ({(float)p.Value / Mathf.Max(1, total):P0})"));
        }
    }
}
