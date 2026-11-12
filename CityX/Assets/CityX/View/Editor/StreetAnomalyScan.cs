#if CITYX_LEGACY_GRID_DIAGNOSTICS
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CityX.View;
using CityX.World;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Looks for STREET GEOMETRY THAT STICKS UP THROUGH THE ROAD: ground surface standing above
    /// where the road should be, which is what "long sloped bits poking through the street" looks
    /// like in the mesh. Builds only the ground/pavement of a street chunk (not its buildings),
    /// samples a grid across it, and finds the highest ground surface above the terrain's own
    /// height at each spot.
    ///
    /// HOW IT WAS FOUND: a hillside ramp built off the street's height at the chunk BORDER reached
    /// a few metres into the chunk, where the road (tilting like a hill) is often lower, and stood
    /// up through it -- 15-25 degree wedges 0.3-0.7m tall, and steeper ones, along the whole edge
    /// of some 5% of street chunks. See CityMeshBuilder.SlopedStreetRampBuriedMetres.
    ///
    /// Elevated highways and bridge decks are ABOVE the road on purpose, and so are the closed-arm
    /// treatments at a junction (a skatepark's ramps); callers decide what to do with those.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.ViewEditor.StreetAnomalyScan.Run
    ///
    /// Writes Logs/street-anomalies.txt.
    /// </summary>
    public static class StreetAnomalyScan
    {
        private const int WorldSeed = 48398181;

        /// <summary>Chunks searched each way from the origin.</summary>
        private const int Radius = 45;

        private const float GridStep = 2.0f;

        /// <summary>A surface this far above the terrain's own height is not the road, a sidewalk or a kerb.</summary>
        public const float AboveTheRoadMetres = 0.3f;

        /// <summary>Steeper than this and a top-facing street surface is a slope you would notice.</summary>
        private const float SteepDegrees = 12.0f;

        private static MethodInfo _buildGround;

        /// <summary>The ground and pavement of one street chunk, built without its buildings.</summary>
        public static void BuildStreetSurface(CityWorld world, int cx, int cy, out MeshBuffer ground, out MeshBuffer pavement)
        {
            _buildGround ??= typeof(CityMeshBuilder).GetMethod("BuildGround", BindingFlags.NonPublic | BindingFlags.Static);
            if (_buildGround == null)
            {
                throw new System.InvalidOperationException("CityMeshBuilder.BuildGround was not found -- the scan needs updating.");
            }

            float size = CityLayout.ChunkSize;
            ground = new MeshBuffer();
            pavement = new MeshBuffer();
            var guardRail = new MeshBuffer();
            var origin = new Vector3((float)(cx * (double)size), 0.0f, (float)(cy * (double)size));
            _buildGround.Invoke(null, new object[] { world, cx, cy, origin, ground, pavement, guardRail, false });
        }

        /// <summary>
        /// How far the highest GROUND surface in a street chunk's built mesh stands above the terrain's
        /// own height there, and where. 0 when nothing does (by more than <see cref="AboveTheRoadMetres"/>).
        /// </summary>
        public static float HighestGroundAboveTheRoad(CityWorld world, int cx, int cy, MeshBuffer ground,
            out string detail, out float slopeDegrees, MeshBuffer pavement = null)
        {
            float size = CityLayout.ChunkSize;
            float worst = 0.0f;
            detail = null;
            slopeDegrees = 0.0f;

            for (float x = GridStep * 0.5f; x < size; x += GridStep)
            {
                for (float z = GridStep * 0.5f; z < size; z += GridStep)
                {
                    double wx = cx * (double)size + x;
                    double wz = cy * (double)size + z;
                    float expected = (float)world.Terrain.ContinuousHeightAt(wx, wz);

                    var layers = new List<(float height, float slope)>();
                    Collect(ground, x, z, layers);

                    // WHAT A PLAYER CAN SEE: a ground surface with a sidewalk on top of it at the same
                    // spot is covered. Only the topmost surface at a spot counts.
                    float pavementTop = float.NegativeInfinity;
                    if (pavement != null)
                    {
                        var pavementLayers = new List<(float height, float slope)>();
                        Collect(pavement, x, z, pavementLayers);
                        foreach ((float height, float slope) covering in pavementLayers)
                        {
                            pavementTop = Mathf.Max(pavementTop, covering.height);
                        }
                    }

                    foreach ((float height, float slope) in layers)
                    {
                        if (height < pavementTop - 0.02f)
                        {
                            continue;   // buried under a sidewalk
                        }
                        float above = height - expected;
                        if (above > AboveTheRoadMetres && above > worst)
                        {
                            worst = above;
                            slopeDegrees = slope;
                            detail = $"{above:0.0}m above the terrain, sloped {slope:0}deg, at local ({x:0},{z:0})";
                        }
                    }
                }
            }
            return worst;
        }

        /// <summary>
        /// How far the sidewalk's top stands above the road surface UNDER it, at the lowest and the
        /// highest place in this chunk. The kerb is <see cref="CityMeshBuilder.KerbHeight"/>; anything
        /// else is the sidewalk and the road disagreeing about where the ground is -- below the road
        /// and the road pokes up through the sidewalk, well above and the sidewalk stands up in a wedge.
        /// </summary>
        public static bool SidewalkAgainstRoad(MeshBuffer ground, MeshBuffer pavement,
            out float lowest, out float highest, out string detail)
        {
            float size = CityLayout.ChunkSize;
            lowest = float.PositiveInfinity;
            highest = float.NegativeInfinity;
            detail = null;
            for (float x = GridStep * 0.5f; x < size; x += GridStep)
            {
                for (float z = GridStep * 0.5f; z < size; z += GridStep)
                {
                    var roadLayers = new List<(float height, float slope)>();
                    var walkLayers = new List<(float height, float slope)>();
                    Collect(ground, x, z, roadLayers);
                    Collect(pavement, x, z, walkLayers);
                    if (roadLayers.Count == 0 || walkLayers.Count == 0)
                    {
                        continue;
                    }
                    float road = float.NegativeInfinity;
                    foreach ((float height, float slope) r in roadLayers) road = Mathf.Max(road, r.height);
                    float top = float.NegativeInfinity;
                    float topSlope = 0.0f;
                    foreach ((float height, float slope) w in walkLayers)
                    {
                        if (w.height > top)
                        {
                            top = w.height;
                            topSlope = w.slope;
                        }
                    }
                    float above = top - road;
                    // A stair, a ramp or a plaza is meant to be well above the road; this is about plain sidewalks.
                    if (above > 3.0f || topSlope > 25.0f)
                    {
                        continue;
                    }
                    if (above < lowest)
                    {
                        lowest = above;
                    }
                    if (above > highest)
                    {
                        highest = above;
                        detail = $"highest {above:0.00}m above the road at local ({x:0},{z:0})";
                    }
                }
            }
            return lowest != float.PositiveInfinity;
        }

        [MenuItem("CityX/Scan Streets For Surfaces Poking Through")]
        public static void Run()
        {
            var world = new CityWorld(WorldSeed);
            var report = new StringBuilder();
            var steepReport = new StringBuilder();
            var sidewalkReport = new StringBuilder();
            int sidewalkChunks = 0;
            int steepChunks = 0;
            int streetChunks = 0;
            int flagged = 0;
            var byKind = new Dictionary<string, int>();

            for (int cx = -Radius; cx <= Radius; cx++)
            {
                for (int cy = -Radius; cy <= Radius; cy++)
                {
                    if (!CityLayout.IsStreetChunk(cx, cy))
                    {
                        continue;
                    }
                    streetChunks++;
                    BuildStreetSurface(world, cx, cy, out MeshBuffer ground, out MeshBuffer pavement);

                    // The steepest TOP-FACING surface of the sidewalks and of the ground (walls and banks excluded):
                    // a road or sidewalk tilted more than a real one reads as a long slope beside or across the street.
                    SteepestTop(pavement, out float steepPavement, out float steepPavementLength);
                    SteepestTop(ground, out float steepGround, out float steepGroundLength);
                    if (steepPavement > SteepDegrees || steepGround > SteepDegrees)
                    {
                        steepChunks++;
                        steepReport.AppendLine($"chunk ({cx},{cy}) {Classify(world, cx, cy)}: pavement {steepPavement:0}deg (edge {steepPavementLength:0.0}m), ground {steepGround:0}deg (edge {steepGroundLength:0.0}m)");
                    }

                    if (SidewalkAgainstRoad(ground, pavement, out float lowestWalk, out float highestWalk, out string walkDetail))
                    {
                        float kerb = CityMeshBuilder.KerbHeight;
                        if (lowestWalk < kerb - 0.12f || highestWalk > kerb + 0.35f)
                        {
                            sidewalkChunks++;
                            sidewalkReport.AppendLine($"chunk ({cx},{cy}) {Classify(world, cx, cy)}: sidewalk {lowestWalk:0.00}..{highestWalk:0.00}m over the road (kerb {kerb:0.00}); {walkDetail}");
                        }
                    }

                    float above = HighestGroundAboveTheRoad(world, cx, cy, ground, out string detail, out _, pavement);
                    if (above <= 0.0f)
                    {
                        continue;
                    }
                    flagged++;
                    string kind = Classify(world, cx, cy);
                    byKind[kind] = byKind.TryGetValue(kind, out int n) ? n + 1 : 1;
                    report.AppendLine($"chunk ({cx},{cy}) {kind}: ground surface {detail}");
                }
            }

            var summary = new StringBuilder();
            summary.AppendLine($"scanned {streetChunks} street chunks within {Radius} of the origin; {flagged} have ground standing more than {AboveTheRoadMetres}m above the terrain");
            foreach (KeyValuePair<string, int> pair in byKind)
            {
                summary.AppendLine($"  {pair.Value,5}  {pair.Key}");
            }
            summary.AppendLine($"{steepChunks} street chunks have a walkable top-facing surface steeper than {SteepDegrees} degrees");
            summary.AppendLine($"{sidewalkChunks} street chunks have a sidewalk that is not a kerb's height above the road (see the third list)");
            summary.AppendLine();
            summary.Append(report);
            summary.AppendLine();
            summary.AppendLine("=== sidewalk against road ===");
            summary.Append(sidewalkReport);
            summary.AppendLine();
            summary.AppendLine("=== steep surfaces ===");
            summary.Append(steepReport);

            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "street-anomalies.txt");
            File.WriteAllText(path, summary.ToString());
            Debug.Log($"CityX: street scan written to {path} -- {flagged}/{streetChunks} chunks flagged");
        }

        /// <summary>The steepest TOP-facing triangle in a buffer, and its longest edge (so a sliver and a real slope can be told apart).</summary>
        private static void SteepestTop(MeshBuffer buffer, out float degrees, out float longestEdge)
        {
            degrees = 0.0f;
            longestEdge = 0.0f;
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float length = normal.magnitude;
                if (length < 1e-6f)
                {
                    continue;
                }
                normal /= length;
                if (normal.y < 0.5f)
                {
                    continue;   // a wall, a steep bank or an underside: not a surface anyone drives across
                }
                float slope = Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
                if (slope > degrees)
                {
                    degrees = slope;
                    longestEdge = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                }
            }
        }

        /// <summary>Which of the street features a chunk carries, so a flagged chunk can be tied to the code that made it.</summary>
        public static string Classify(CityWorld world, int cx, int cy)
        {
            var kinds = new List<string>();
            if (CityLayout.IsHighwayChunk(cx, cy)) kinds.Add("highway");
            if (CityLayout.IsArterialChunk(cx, cy)) kinds.Add("arterial");
            bool junction = CityLayout.IsStreetChunk(cx - 1, cy) && CityLayout.IsStreetChunk(cx + 1, cy)
                            && CityLayout.IsStreetChunk(cx, cy - 1) && CityLayout.IsStreetChunk(cx, cy + 1);
            if (junction) kinds.Add("junction");
            int lowerBuildingNeighbours = 0;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (!CityLayout.IsStreetChunk(cx + dx, cy + dy)
                    && world.Terrain.PadHeightFor(cx + dx, cy + dy) < world.Terrain.PadHeightFor(cx, cy))
                {
                    lowerBuildingNeighbours++;
                }
            }
            if (lowerBuildingNeighbours > 0) kinds.Add($"drops-to-{lowerBuildingNeighbours}-building-side(s)");
            return kinds.Count == 0 ? "plain" : string.Join("+", kinds);
        }

        /// <summary>Every upward-facing triangle of a buffer that covers (x, z), as a height and slope there.</summary>
        private static void Collect(MeshBuffer buffer, float x, float z, List<(float height, float slope)> into)
        {
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];

                // Cheap reject on the triangle's XZ bounds.
                if (x < Mathf.Min(a.x, Mathf.Min(b.x, c.x)) || x > Mathf.Max(a.x, Mathf.Max(b.x, c.x)) ||
                    z < Mathf.Min(a.z, Mathf.Min(b.z, c.z)) || z > Mathf.Max(a.z, Mathf.Max(b.z, c.z)))
                {
                    continue;
                }

                Vector3 normal = Vector3.Cross(b - a, c - a);
                float length = normal.magnitude;
                if (length < 1e-6f)
                {
                    continue;
                }
                normal /= length;
                // Wound so it faces up: an underside or a wall is not a walkable layer.
                if (normal.y < 0.25f)
                {
                    continue;
                }

                // Barycentric in XZ.
                float denom = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denom) < 1e-8f)
                {
                    continue;
                }
                float w0 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denom;
                float w1 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denom;
                float w2 = 1.0f - w0 - w1;
                if (w0 < 0.0f || w1 < 0.0f || w2 < 0.0f)
                {
                    continue;
                }

                into.Add((w0 * a.y + w1 * b.y + w2 * c.y, Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg));
            }
        }
    }
}
#endif // CITYX_LEGACY_GRID_DIAGNOSTICS
