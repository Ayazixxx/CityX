using System.Collections.Generic;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A SHIRT WORN UNDER A COAT drawn only in the coat's open front (owner, 2026-11-21: "make it so i can wear a shirt underneath the parka", then
    /// "im seeing the shirt clipping through around the shoulders. make the shirt sleeves invisible underneath the parka"). Everywhere else the coat
    /// covers it, and that is where it came out through the coat: the long-sleeved shirt's sleeves through the parka's fitted upper sleeves, both
    /// shirts' shoulders by the collar, their backs. Measured against the parka at rest, every point of either shirt out through it lies outside the
    /// open front. The body under a long coat is hidden by the same opening (LongCoatBody).
    /// </summary>
    public static class UnderCoat
    {
        /// <summary>
        /// A long coat's open front in the body's rest frame (+Z ahead), from the waist to the collar: what shows of whatever is under the coat
        /// (owner, 2026-10-19, of the Midnight Coat's: "make a little bit of the top of the torso visible, any amount that is in view regularly").
        /// </summary>
        public static readonly Bounds FrontOpening = new Bounds(new Vector3(0.0f, 1.5f, 0.25f), new Vector3(0.4f, 1.2f, 0.5f));

        private static readonly Dictionary<Mesh, Mesh> Fronts = new Dictionary<Mesh, Mesh>();
        private static readonly Dictionary<(Mesh, Mesh), Mesh> CloseFronts = new Dictionary<(Mesh, Mesh), Mesh>();

        /// <summary>The coat's cover is read on a grid this fine, metres; a shirt is tucked this many cells under the coat's edges.</summary>
        public const float CoverCell = 0.005f;

        public const int TuckCells = 3;

        /// <summary>
        /// UNDER A CLOSE-FITTING COAT THE SHIRT HUGS THE BODY (owner, 2026-11-24, of a T-shirt through the Brown Coat's front panels: "maybe make the
        /// t shirt fit a bit tigher when brown coat is equipped so it doesnt overlap like that", "the top part looks good its just the stomach"). The
        /// Brown Coat's cloth lies 2.8 cm off the body and its open front is narrow; a T-shirt hangs looser than that round the stomach. So from the
        /// chest down the shirt is drawn in to this far off the trunk (a round of <see cref="TrunkRadius"/> about the body's middle line), easing
        /// back to its own shape up to the chest, which is left alone.
        /// </summary>
        public const float TrunkRadius = 0.26f;

        public const float CloseOffTheTrunk = 0.008f;

        /// <summary>Drawn in fully below the first height, not at all above the second (the body's rest frame, metres).</summary>
        public const float CloseBelow = 1.55f, OwnShapeFrom = 1.72f;

        /// <summary>Whether a coat fits close enough that a shirt under it is drawn in at the stomach.</summary>
        public static bool FitsClose(string coatId) => coatId == ItemRegistry.BrownCoat;

        /// <summary>Whether a worn item is a shirt that goes under a coat: worn as a top, and not a coat itself (Character.TopLayers).</summary>
        public static bool IsShirt(string itemId) =>
            !ItemRegistry.IsCoat(itemId) && ItemRegistry.TryGet(itemId, out ItemDefinition definition) && definition.EquipSlot == EquipSlots.Torso;

        /// <summary>The triangles whose middle is in the coat's open front.</summary>
        public static List<int> InTheOpening(IReadOnlyList<int> triangles, IReadOnlyList<Vector3> vertices)
        {
            var kept = new List<int>(triangles.Count);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                Vector3 middle = (vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3.0f;
                if (FrontOpening.Contains(middle))
                {
                    kept.Add(triangles[t]);
                    kept.Add(triangles[t + 1]);
                    kept.Add(triangles[t + 2]);
                }
            }
            return kept;
        }

        /// <summary>
        /// The same under a close-fitting coat (<see cref="FitsClose"/>), <paramref name="coat"/> its mesh as worn: only what lies in the coat's own
        /// gap, and a tuck under its edges, is drawn (the shared opening is the parka's width; this coat's panels cover most of it, and the shirt
        /// came through them), and its stomach is drawn in to the body. Made once for a shirt and a coat.
        /// </summary>
        public static Mesh FrontOnlyClose(Mesh shirt, Mesh coat)
        {
            if (shirt == null || !shirt.isReadable || coat == null || !coat.isReadable)
            {
                return FrontOnly(shirt);
            }
            if (!CloseFronts.TryGetValue((shirt, coat), out Mesh close))
            {
                close = Object.Instantiate(shirt);
                close.name = shirt.name + " (under a close coat)";
                close.SetTriangles(InTheGap(shirt.triangles, shirt.vertices, CoveredInFront(coat)), 0);
                Vector3[] points = close.vertices;
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = DrawnIn(points[i]);
                }
                close.vertices = points;
                close.RecalculateBounds();
                CloseFronts[(shirt, coat)] = close;
            }
            return close;
        }

        /// <summary>Where a coat covers the body's front, seen from straight ahead at rest: the cells (x, y) its front-facing cloth lies over.</summary>
        public static HashSet<(int, int)> CoveredInFront(Mesh coat)
        {
            var covered = new HashSet<(int, int)>();
            Vector3[] points = coat.vertices;
            int[] triangles = coat.triangles;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
                if (a.z <= 0.0f || b.z <= 0.0f || c.z <= 0.0f)
                {
                    continue;   // the back half
                }
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / CoverCell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / CoverCell);
                int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) / CoverCell), y1 = Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) / CoverCell);
                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        var middle = new Vector2((x + 0.5f) * CoverCell, (y + 0.5f) * CoverCell);
                        if (Inside(middle, a, b, c))
                        {
                            covered.Add((x, y));
                        }
                    }
                }
            }
            return covered;
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            return !((d1 < 0.0f || d2 < 0.0f || d3 < 0.0f) && (d1 > 0.0f || d2 > 0.0f || d3 > 0.0f));
        }

        /// <summary>The triangles in the shared opening whose middle the coat does not cover, or lies within the tuck of somewhere it does not.</summary>
        public static List<int> InTheGap(IReadOnlyList<int> triangles, IReadOnlyList<Vector3> vertices, HashSet<(int, int)> covered)
        {
            List<int> opening = InTheOpening(triangles, vertices);
            var kept = new List<int>(opening.Count);
            for (int t = 0; t + 2 < opening.Count; t += 3)
            {
                Vector3 middle = (vertices[opening[t]] + vertices[opening[t + 1]] + vertices[opening[t + 2]]) / 3.0f;
                int x = Mathf.FloorToInt(middle.x / CoverCell), y = Mathf.FloorToInt(middle.y / CoverCell);
                bool shows = false;
                for (int dx = -TuckCells; dx <= TuckCells && !shows; dx++)
                {
                    for (int dy = -TuckCells; dy <= TuckCells && !shows; dy++)
                    {
                        shows = !covered.Contains((x + dx, y + dy));
                    }
                }
                if (shows)
                {
                    kept.Add(opening[t]);
                    kept.Add(opening[t + 1]);
                    kept.Add(opening[t + 2]);
                }
            }
            return kept;
        }

        /// <summary>A point of a shirt as it lies under a close-fitting coat: no further off the trunk than it may be, from the chest down.</summary>
        public static Vector3 DrawnIn(Vector3 point)
        {
            float own = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(CloseBelow, OwnShapeFrom, point.y));
            float reach = new Vector2(point.x, point.z).magnitude;
            float most = TrunkRadius + CloseOffTheTrunk;
            if (own >= 1.0f || reach <= most)
            {
                return point;
            }
            float drawn = Mathf.Lerp(most, reach, own) / reach;
            return new Vector3(point.x * drawn, point.y, point.z * drawn);
        }

        /// <summary>The shirt's mesh as it shows under a coat, made once a mesh (the same points, only the open front's triangles).</summary>
        public static Mesh FrontOnly(Mesh shirt)
        {
            if (shirt == null || !shirt.isReadable)
            {
                return shirt;
            }
            if (!Fronts.TryGetValue(shirt, out Mesh front))
            {
                front = Object.Instantiate(shirt);
                front.name = shirt.name + " (under a coat)";
                front.SetTriangles(InTheOpening(shirt.triangles, shirt.vertices), 0);
                Fronts[shirt] = front;
            }
            return front;
        }
    }
}
