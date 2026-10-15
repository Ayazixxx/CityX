using System;
using System.Collections.Generic;
using System.Text;
using CityX.World;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Measures what the interiors of the real plan city are actually like: footprint sizes, floors, and above all how steep the
    /// stairs are. A number to move when the stairs are reworked, not a test.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -nographics -executeMethod CityX.ViewEditor.InteriorSurvey.Run
    ///
    /// The report goes to the log, one "CityX SURVEY" line at a time.
    /// </summary>
    public static class InteriorSurvey
    {
        private const int Seed = 61928374;

        [MenuItem("CityX/Survey Interiors")]
        public static void Run()
        {
            var world = new CityWorld(Seed);
            int count = world.BuildingCount;

            var every = new List<float>();     // the angle of every flight in every building
            var top = new List<float>();       // the angle of each building's top flight
            var ordinary = new List<float>();  // a non-top flight
            var topRise = new List<float>();
            var widths = new List<float>();
            var depths = new List<float>();
            var shortSide = new List<float>();
            var floorsOf = new List<float>();
            var heights = new List<float>();
            var runs = new List<float>();
            var topSteps = new List<float>();
            int capped = 0;
            int basements = 0;
            int tooSmallForRoom = 0;
            var byZone = new Dictionary<ZoneKind, List<float>>();

            for (int id = 0; id < count; id++)
            {
                BuildingSite site = world.Building(id);
                if (!site.Params.HasBuilding)
                {
                    continue;
                }

                float height = site.Params.Height;
                float halfX = site.Params.Width * 0.5f;
                float halfZ = site.Params.Depth * 0.5f;
                float storey = BuildingInterior.StoreyHeight(height);
                int floors = BuildingInterior.FloorCountFor(height);
                float run = BuildingInterior.RunFor(halfX, halfZ);

                widths.Add(site.Params.Width);
                depths.Add(site.Params.Depth);
                shortSide.Add(Mathf.Min(site.Params.Width, site.Params.Depth));
                floorsOf.Add(floors);
                heights.Add(height);
                runs.Add(run);
                basements += site.Params.HasBasement ? 1 : 0;
                if (floors >= BuildingInterior.MaxFloors && height > floors * storey + 0.01f)
                {
                    capped++;
                }
                if (!BuildingInterior.RoomFor(0, halfX, halfZ).Exists)
                {
                    tooSmallForRoom++;
                }

                if (!byZone.TryGetValue(site.Zone, out List<float> zoneList))
                {
                    zoneList = new List<float>();
                    byZone[site.Zone] = zoneList;
                }

                for (int floor = 0; floor < floors; floor++)
                {
                    float bottom = floor * storey;
                    float rise = BuildingInterior.FloorTop(floor, floors, height, storey) - bottom;
                    float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
                    every.Add(angle);
                    if (floor == floors - 1)
                    {
                        top.Add(angle);
                        topRise.Add(rise);
                        topSteps.Add(BuildingInterior.StepsFor(rise));
                        zoneList.Add(angle);
                    }
                    else
                    {
                        ordinary.Add(angle);
                    }
                }
            }

            var report = new StringBuilder();
            report.AppendLine($"CityX SURVEY: {count} buildings, seed {Seed}");
            report.AppendLine($"storey {BuildingInterior.StoreyHeight(3.2f * 10):F2} m (two rows of {CellLayout.NominalFloorMetres} m), MaxFloors {BuildingInterior.MaxFloors}, step rise {BuildingInterior.StairStepHeight} m");
            report.AppendLine(Describe("width m", widths));
            report.AppendLine(Describe("depth m", depths));
            report.AppendLine(Describe("short side m", shortSide));
            report.AppendLine(Describe("height m", heights));
            report.AppendLine(Describe("floors", floorsOf));
            report.AppendLine(Describe("flight run m (what the room allows)", runs));
            report.AppendLine(Describe("EVERY flight angle deg", every));
            report.AppendLine(Describe("ordinary flight angle deg", ordinary));
            report.AppendLine(Describe("TOP flight angle deg", top));
            report.AppendLine(Describe("top flight rise m", topRise));
            report.AppendLine(Describe("top flight step count", topSteps));
            report.AppendLine($"flights steeper than 35 deg: {Share(every, 35.0f):P0} of all, {Share(ordinary, 35.0f):P0} of ordinary, {Share(top, 35.0f):P0} of top");
            report.AppendLine($"flights steeper than 45 deg: {Share(every, 45.0f):P0} of all, {Share(ordinary, 45.0f):P0} of ordinary, {Share(top, 45.0f):P0} of top");
            report.AppendLine($"flights steeper than 60 deg: {Share(every, 60.0f):P0} of all, {Share(ordinary, 60.0f):P0} of ordinary, {Share(top, 60.0f):P0} of top");
            report.AppendLine($"buildings where the leftover height lands on the top floor (cap hit): {capped} ({(float)capped / Mathf.Max(1, widths.Count):P0})");
            report.AppendLine($"buildings with a basement: {basements}; too small for the carved corner room: {tooSmallForRoom}");
            foreach (KeyValuePair<ZoneKind, List<float>> pair in byZone)
            {
                report.AppendLine(Describe($"top flight angle in {pair.Key}", pair.Value));
            }
            Debug.Log(report.ToString());
        }

        private static float Share(List<float> values, float above)
        {
            if (values.Count == 0)
            {
                return 0.0f;
            }
            int n = 0;
            foreach (float v in values)
            {
                n += v > above ? 1 : 0;
            }
            return (float)n / values.Count;
        }

        private static string Describe(string name, List<float> values)
        {
            if (values.Count == 0)
            {
                return $"{name}: none";
            }
            var sorted = new List<float>(values);
            sorted.Sort();
            float At(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            return $"{name}: n={sorted.Count} min {sorted[0]:F1}  p10 {At(0.1f):F1}  median {At(0.5f):F1}  p90 {At(0.9f):F1}  max {sorted[sorted.Count - 1]:F1}";
        }
    }
}
