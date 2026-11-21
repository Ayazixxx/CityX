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
