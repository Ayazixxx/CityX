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

        /// <summary>The coat's front panels are read on a grid this fine, metres; a shirt is kept behind them this many cells out past their edges.</summary>
        public const float CoverCell = 0.005f;

        public const int TuckCells = 3;

        /// <summary>
        /// SLID UNDER, NOT CUT ROUND (owner, 2026-11-24, of the shirt under the Brown Coat: "there is a bunch of "shards" missing from the t shirt
        /// look down the sides there is a bunch of triangle shapes holes, fix that without clipping into the jacket too much"): the whole shirt in
        /// the opening is drawn, and wherever the coat's front panels lie over it, it is pushed back to this far behind them (it used to be cut
        /// triangle by triangle where they covered it, and the ragged edge showed between).
        /// </summary>
        public const float BehindThePanels = 0.012f;

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
        /// The same under a close-fitting coat (<see cref="FitsClose"/>), <paramref name="coat"/> its mesh as worn: all of the shirt in the opening,
        /// its stomach drawn in to the body, and wherever the coat's front panels lie over it, slid back behind them
        /// (<see cref="BehindThePanels"/>). Made once for a shirt and a coat.
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
                close.SetTriangles(InTheOpening(shirt.triangles, shirt.vertices), 0);
                Dictionary<(int, int), float> panels = PanelsInFront(coat);
                Vector3[] points = close.vertices;
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = BehindPanels(DrawnIn(points[i]), panels);
                }
                close.vertices = points;
                close.boneWeights = OffTheLegs(close.boneWeights);
                close.RecalculateBounds();
                CloseFronts[(shirt, coat)] = close;
            }
            return close;
        }

        /// <summary>
        /// UNDER THE COAT THE SHIRT HANGS FROM THE HIPS (owner, 2026-11-24, of the shirt's lower corners over the Brown Coat's panels mid-stride:
        /// "slightly clipping here with the white shirt going through the coat a tiny bit", "and on the other side as well same spot"): its hem took
        /// a share of the thighs from the skin nearest it, and a knee coming up swung it forward out through the panel, which hangs from the hips.
        /// Every share a point has of a leg goes to the hips.
        /// </summary>
        public static BoneWeight[] OffTheLegs(BoneWeight[] weights)
        {
            int hips = System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.Hips);
            var legs = new HashSet<int>();
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg })
            {
                legs.Add(System.Array.IndexOf(SkinnedGarment.Bones, bone));
            }
            var moved = new BoneWeight[weights.Length];
            var shares = new Dictionary<int, float>(4);
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                shares.Clear();
                foreach ((int bone, float share) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    int to = legs.Contains(bone) ? hips : bone;
                    shares.TryGetValue(to, out float had);
                    shares[to] = had + share;
                }
                var order = new List<KeyValuePair<int, float>>(shares);
                order.Sort((a, b) => b.Value.CompareTo(a.Value));
                var result = new BoneWeight();
                for (int k = 0; k < order.Count && k < 4; k++)
                {
                    switch (k)
                    {
                        case 0: result.boneIndex0 = order[k].Key; result.weight0 = order[k].Value; break;
                        case 1: result.boneIndex1 = order[k].Key; result.weight1 = order[k].Value; break;
                        case 2: result.boneIndex2 = order[k].Key; result.weight2 = order[k].Value; break;
                        default: result.boneIndex3 = order[k].Key; result.weight3 = order[k].Value; break;
                    }
                }
                moved[i] = result;
            }
            return moved;
        }

        /// <summary>
        /// Where a coat's cloth lies over the body's front at rest, seen from straight ahead: for each cell (x, y) it covers, how far forward its
        /// innermost layer there is (z, the body's rest frame).
        /// </summary>
        public static Dictionary<(int, int), float> PanelsInFront(Mesh coat)
        {
            var depth = new Dictionary<(int, int), float>();
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
                        if (Across(middle, a, b, c, out Vector3 share))
                        {
                            float z = share.x * a.z + share.y * b.z + share.z * c.z;
                            depth[(x, y)] = depth.TryGetValue((x, y), out float there) ? Mathf.Min(there, z) : z;
                        }
                    }
                }
            }
            return depth;
        }

        /// <summary>Whether p lies within the triangle abc seen straight on (x, y), and its share of each corner there.</summary>
        private static bool Across(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 share)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            share = Vector3.zero;
            if (Mathf.Abs(area) < 1e-10f)
            {
                return false;
            }
            float sb = ((p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y)) / area;
            float sc = ((b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y)) / area;
            share = new Vector3(1.0f - sb - sc, sb, sc);
            return share.x >= 0.0f && share.y >= 0.0f && share.z >= 0.0f;
        }

        /// <summary>
        /// A point of a shirt kept <see cref="BehindThePanels"/> behind the coat's front panels wherever they lie over it, or within
        /// <see cref="TuckCells"/> of where they do (so it goes under their edge, not out beside it); elsewhere as it is.
        /// </summary>
        public static Vector3 BehindPanels(Vector3 point, Dictionary<(int, int), float> panels)
        {
            int cx = Mathf.FloorToInt(point.x / CoverCell), cy = Mathf.FloorToInt(point.y / CoverCell);
            float innermost = float.MaxValue;
            for (int dx = -TuckCells; dx <= TuckCells; dx++)
            {
                for (int dy = -TuckCells; dy <= TuckCells; dy++)
                {
                    if (panels.TryGetValue((cx + dx, cy + dy), out float z))
                    {
                        innermost = Mathf.Min(innermost, z);
                    }
                }
            }
            float most = innermost - BehindThePanels;
            return point.z > most ? new Vector3(point.x, point.y, most) : point;
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
