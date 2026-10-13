using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using CityX.Character;
using UnityEngine;

/// <summary>
/// The body's shape outside Unity: render it, measure it, weigh it.
///   render out.png            a sheet: front, three-quarter, side, back, and close-ups of the shoulders, hips, a hand, a foot
///   profile                   the body's half-width down its centre, from the front (x) and the side (z), every 2 cm
///   probe x y z               the field's distance at a point, the nearest bone, and the skin weights there
///   mesh                      extract the body as the builder does (fine hands stitched on) and report triangles, holes, worst vertex
/// </summary>
public static class Program
{
    private static readonly Vector3 HeadCentre = new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f);

    public static int Main(string[] args)
    {
        string command = args.Length > 0 ? args[0] : "render";
        List<FusedBodyShape.Segment> shape = FusedBodyShape.LimbCapsules(BodyRig.ShapePositionOf, FusedBodyShape.ReferenceHeight);
        List<FusedBodyShape.Segment> segments = FusedBodyShape.Segments(BodyRig.ShapePositionOf, FusedBodyShape.ReferenceHeight);
        switch (command)
        {
            case "render":
                // render out.png [name ...]: the whole sheet, or only the named views.
                var only = new HashSet<string>();
                for (int i = 2; i < args.Length; i++) only.Add(args[i]);
                Render(shape, args.Length > 1 ? args[1] : "body.png", only);
                return 0;
            case "profile":
                Profile(shape);
                return 0;
            case "probe":
            {
                var p = new Vector3(float.Parse(args[1]), float.Parse(args[2]), float.Parse(args[3]));
                Console.WriteLine($"distance {FusedBodyShape.Distance(shape, p):0.0000} (head {HeadDistance(p):0.0000}); nearest bone {FusedBodyShape.NearestBone(segments, p)}; dominant {FusedBodyShape.DominantBone(segments, p)}");
                var bones = new HumanBodyBones[4];
                var weights = new float[4];
                int used = FusedBodyShape.WeightsAt(segments, p, bones, weights);
                for (int i = 0; i < used; i++) Console.WriteLine($"  {bones[i]} {weights[i]:P1}");
                return 0;
            }
            case "mesh":
                Mesh(shape);
                return 0;
            default:
                Console.WriteLine("render [out.png] | profile | probe x y z | mesh");
                return 1;
        }
    }

    private static float HeadDistance(Vector3 p) => (p - HeadCentre).magnitude - FusedBodyShape.HeadRadius;

    /// <summary>The body and the head together, as the game draws them: a hard union (the head is its own mesh).</summary>
    private static float Field(List<FusedBodyShape.Segment> shape, Vector3 p) => Mathf.Min(FusedBodyShape.Distance(shape, p), HeadDistance(p));

    // ---------------------------------------------------------------- render

    private sealed class View
    {
        public string Name;
        public Vector3 Eye;        // where the orthographic camera sits (direction only matters)
        public Vector3 Target;     // what is at the centre of the image
        public float Half;         // metres from the centre to the top of the image
        public int Width, Height;
    }

    private static void Render(List<FusedBodyShape.Segment> shape, string path, HashSet<string> only)
    {
        var views = new List<View>
        {
            new View { Name = "front", Eye = new Vector3(0, 1.3f, 5), Target = new Vector3(0, 1.3f, 0), Half = 1.4f, Width = 420, Height = 600 },
            new View { Name = "three-quarter", Eye = new Vector3(3.5f, 1.3f, 3.5f), Target = new Vector3(0, 1.3f, 0), Half = 1.4f, Width = 420, Height = 600 },
            new View { Name = "side", Eye = new Vector3(5, 1.3f, 0), Target = new Vector3(0, 1.3f, 0), Half = 1.4f, Width = 420, Height = 600 },
            new View { Name = "back", Eye = new Vector3(0, 1.3f, -5), Target = new Vector3(0, 1.3f, 0), Half = 1.4f, Width = 420, Height = 600 },
            new View { Name = "shoulders", Eye = new Vector3(2, 2.3f, 4), Target = new Vector3(0, 2.0f, 0), Half = 0.5f, Width = 420, Height = 420 },
            new View { Name = "neck side", Eye = new Vector3(5, 2.1f, 0), Target = new Vector3(0, 2.0f, 0), Half = 0.45f, Width = 420, Height = 420 },
            new View { Name = "hips", Eye = new Vector3(2.5f, 1.1f, 4), Target = new Vector3(0, 0.8f, 0), Half = 0.5f, Width = 420, Height = 420 },
            new View { Name = "hand", Eye = new Vector3(1.5f, 0.8f, 4), Target = BodyRig.ShapePositionOf(HumanBodyBones.RightHand) + new Vector3(0, -0.02f, 0), Half = 0.2f, Width = 420, Height = 420 },
            new View { Name = "hand back", Eye = new Vector3(4f, 0.8f, -1.5f), Target = BodyRig.ShapePositionOf(HumanBodyBones.RightHand) + new Vector3(0, -0.02f, 0), Half = 0.2f, Width = 420, Height = 420 },
            new View { Name = "hand palm", Eye = new Vector3(-3f, 0.9f, 1.5f), Target = BodyRig.ShapePositionOf(HumanBodyBones.RightHand) + new Vector3(0.1f, -0.02f, 0), Half = 0.2f, Width = 420, Height = 420 },
            new View { Name = "feet", Eye = new Vector3(2.5f, 1.0f, 4), Target = new Vector3(0, 0.1f, 0.05f), Half = 0.3f, Width = 420, Height = 420 },
        };
        if (only.Count > 0)
        {
            views.RemoveAll(v => !only.Contains(v.Name));
        }
        int columns = Math.Min(5, views.Count);
        int rows = (views.Count + columns - 1) / columns;
        int tileW = 420, tileH = 600;
        using (var sheet = new Bitmap(columns * tileW, rows * tileH))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(40, 44, 54));
            for (int v = 0; v < views.Count; v++)
            {
                View view = views[v];
                using (Bitmap tile = RenderView(shape, view))
                {
                    g.DrawImage(tile, (v % columns) * tileW, (v / columns) * tileH);
                }
                g.DrawString(view.Name, new Font("Arial", 12), Brushes.White, (v % columns) * tileW + 6, (v / columns) * tileH + 4);
            }
            sheet.Save(path, ImageFormat.Png);
        }
        Console.WriteLine("wrote " + path);
    }

    private static Bitmap RenderView(List<FusedBodyShape.Segment> shape, View view)
    {
        int w = view.Width, h = view.Height;
        Vector3 forward = (view.Target - view.Eye).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right);
        float pixel = 2.0f * view.Half / h;
        Vector3 light = new Vector3(-0.4f, 0.8f, 0.5f).normalized;
        Vector3 fill = new Vector3(0.6f, 0.1f, -0.6f).normalized;
        var pixels = new int[w * h];
        Parallel.For(0, h, py =>
        {
            for (int px = 0; px < w; px++)
            {
                float sx = (px - w * 0.5f + 0.5f) * pixel, sy = (h * 0.5f - py - 0.5f) * pixel;
                Vector3 origin = view.Target + right * sx + up * sy - forward * 4.0f;
                float t = 0.0f;
                bool hit = false;
                for (int step = 0; step < 160; step++)
                {
                    Vector3 p = origin + forward * t;
                    float d = Field(shape, p);
                    if (d < 0.0005f)
                    {
                        hit = true;
                        break;
                    }
                    t += Mathf.Max(d * 0.9f, 0.0004f);
                    if (t > 9.0f) break;
                }
                Color colour;
                if (!hit)
                {
                    colour = Color.FromArgb(40, 44, 54);
                }
                else
                {
                    Vector3 p = origin + forward * t;
                    const float e = 0.002f;
                    var n = new Vector3(
                        Field(shape, p + Vector3.right * e) - Field(shape, p - Vector3.right * e),
                        Field(shape, p + Vector3.up * e) - Field(shape, p - Vector3.up * e),
                        Field(shape, p + Vector3.forward * e) - Field(shape, p - Vector3.forward * e)).normalized;
                    float key = Mathf.Max(0.0f, Vector3.Dot(n, light));
                    float rim = Mathf.Max(0.0f, Vector3.Dot(n, fill)) * 0.35f;
                    float shade = 0.22f + 0.65f * key + rim;
                    // The head drawn a shade lighter, so where it meets the body is plain to see.
                    bool head = HeadDistance(p) < FusedBodyShape.Distance(shape, p);
                    float r = (head ? 235 : 225) * shade, gch = (head ? 200 : 185) * shade, b = (head ? 180 : 165) * shade;
                    colour = Color.FromArgb(Clamp(r), Clamp(gch), Clamp(b));
                }
                pixels[py * w + px] = colour.ToArgb();
            }
        });
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bitmap.UnlockBits(data);
        return bitmap;
    }

    private static int Clamp(float v) => (int)Mathf.Clamp(v, 0.0f, 255.0f);

    // ---------------------------------------------------------------- profile

    private static void Profile(List<FusedBodyShape.Segment> shape)
    {
        Console.WriteLine("  y     front half-width (x)   side half-depth (z)    [body only; head bottom at " + (BodyRig.HeadCentreY - FusedBodyShape.HeadRadius).ToString("0.000") + "]");
        for (float y = 2.3f; y >= -0.02f; y -= 0.02f)
        {
            float wx = HalfWidth(shape, y, Vector3.right), wz = HalfWidth(shape, y, Vector3.forward), wzb = HalfWidth(shape, y, Vector3.back);
            float headHalf = Mathf.Abs(y - BodyRig.HeadCentreY) < FusedBodyShape.HeadRadius ? Mathf.Sqrt(FusedBodyShape.HeadRadius * FusedBodyShape.HeadRadius - (y - BodyRig.HeadCentreY) * (y - BodyRig.HeadCentreY)) : 0.0f;
            Console.WriteLine($"{y,5:0.00}   {wx,6:0.000}                  {wz,6:0.000} / {wzb,6:0.000}" + (headHalf > 0 ? $"   head {headHalf:0.000}" : ""));
        }
    }

    /// <summary>How far from the centreline the body reaches along <paramref name="direction"/> at height y: the first air walking outward.</summary>
    private static float HalfWidth(List<FusedBodyShape.Segment> shape, float y, Vector3 direction)
    {
        float width = 0.0f;
        bool inside = false;
        for (float s = 0.0f; s < 0.8f; s += 0.002f)
        {
            Vector3 p = new Vector3(0, y, 0) + direction * s;
            bool solid = FusedBodyShape.Distance(shape, p) < 0.0f;
            if (solid) { inside = true; width = s; }
            else if (inside) break;
        }
        return width;
    }

    // ---------------------------------------------------------------- mesh

    private static void Mesh(List<FusedBodyShape.Segment> shape)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        List<Vector3> vertices;
        List<int> triangles;
        if (Environment.GetEnvironmentVariable("BODYSHAPE_PLAIN") == "1")
        {
            // The coarse body alone, no hands stitched: to tell a hole in the extraction from one in the stitching.
            shape = shape.FindAll(s => s.Bone != HumanBodyBones.LeftHand && s.Bone != HumanBodyBones.RightHand);
            Bounds bounds = FusedBodyShape.BoundsOf(shape);
            bounds.SetMinMax(FusedBodyShape.PhaseAlignedOrigin(bounds.min, FusedBodyShape.CellSize) + Vector3.one * FusedBodyMesh.GridShift, bounds.max);
            FusedBodyMesh.Extract(shape, bounds, FusedBodyShape.CellSize, out vertices, out triangles);
        }
        else
        {
            FusedBodyMesh.ExtractBody(shape, BodyRig.ShapePositionOf, FusedBodyShape.CellSize, FusedBodyShape.HandCellSize,
                out vertices, out triangles, Console.WriteLine);
        }
        sw.Stop();
        // Holes, counted by POSITION (coincident vertices welded), undirected: an edge used by one triangle only.
        var byPosition = new Dictionary<Vector3Int, int>();
        var canonical = new int[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(vertices[i].x * 1000000f), Mathf.RoundToInt(vertices[i].y * 1000000f), Mathf.RoundToInt(vertices[i].z * 1000000f));
            if (!byPosition.TryGetValue(key, out int first)) byPosition[key] = first = i;
            canonical[i] = first;
        }
        var edges = new Dictionary<(int, int), int>();
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = canonical[triangles[i]], b = canonical[triangles[i + 1]], c = canonical[triangles[i + 2]];
            if (a == b || b == c || a == c) continue;
            foreach ((int p, int q) in new[] { (a, b), (b, c), (c, a) })
            {
                (int, int) e = p < q ? (p, q) : (q, p);
                edges[e] = edges.TryGetValue(e, out int n) ? n + 1 : 1;
            }
        }
        int open = 0;
        Vector3 openSum = Vector3.zero;
        foreach (KeyValuePair<(int, int), int> edge in edges)
        {
            if (edge.Value == 1)
            {
                open++;
                openSum += vertices[edge.Key.Item1];
                if (open <= 5) Console.WriteLine($"  open edge at {vertices[edge.Key.Item1]} -> {vertices[edge.Key.Item2]}");
            }
        }
        if (open > 0) Console.WriteLine($"  open edges centre {openSum / open}");
        float worst = 0.0f;
        foreach (Vector3 v in vertices) worst = Mathf.Max(worst, Mathf.Abs(FusedBodyShape.Distance(shape, v)));
        Console.WriteLine($"{vertices.Count} vertices, {triangles.Count / 3} triangles, {open} open edges, worst vertex {worst * 1000:0.0} mm off the field, {sw.ElapsedMilliseconds} ms");
    }
}
