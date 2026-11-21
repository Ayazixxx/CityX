using System.Collections.Generic;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A SHIRT WORN UNDER A COAT drawn without its sleeves (owner, 2026-11-21: "make it so i can wear a shirt underneath the parka"): the coat's
    /// sleeves always cover the shirt's, and a long-sleeved shirt's, fuller than the parka's fitted upper sleeve, came out through it. As the body
    /// under a garment is not drawn where it is covered (BodyUnderGarment), the shirt's sleeves are not drawn under a coat's; its front still shows
    /// in the coat's open front.
    /// </summary>
    public static class UnderCoat
    {
        /// <summary>A point at least this much on an arm's bones (upper arm, forearm, hand, the half-turning shoulder) is the sleeve's.</summary>
        public const float SleeveWeight = 0.5f;

        private static readonly Dictionary<Mesh, Mesh> Sleeveless = new Dictionary<Mesh, Mesh>();

        /// <summary>Whether a worn item is a shirt that goes under a coat: worn as a top, and not a coat itself (Character.TopLayers).</summary>
        public static bool IsShirt(string itemId) =>
            !ItemRegistry.IsCoat(itemId) && ItemRegistry.TryGet(itemId, out ItemDefinition definition) && definition.EquipSlot == EquipSlots.Torso;

        /// <summary>The garment's bone indices that are an arm's: the body's arm bones and the shoulder helpers after them.</summary>
        public static HashSet<int> ArmBones()
        {
            var arms = new HashSet<int>();
            for (int i = 0; i < SkinnedGarment.Bones.Length; i++)
            {
                HumanBodyBones bone = SkinnedGarment.Bones[i];
                if (bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.LeftHand ||
                    bone == HumanBodyBones.RightUpperArm || bone == HumanBodyBones.RightLowerArm || bone == HumanBodyBones.RightHand)
                {
                    arms.Add(i);
                }
            }
            for (int i = 0; i < GarmentHelperBones.Helpers.Length; i++)
            {
                if (GarmentHelperBones.Helpers[i].kind == GarmentHelperBones.Kind.Shoulder)
                {
                    arms.Add(SkinnedGarment.Bones.Length + i);
                }
            }
            return arms;
        }

        /// <summary>How much of a point follows an arm.</summary>
        public static float OnArm(BoneWeight weight, HashSet<int> arms)
        {
            float on = 0.0f;
            if (arms.Contains(weight.boneIndex0)) on += weight.weight0;
            if (arms.Contains(weight.boneIndex1)) on += weight.weight1;
            if (arms.Contains(weight.boneIndex2)) on += weight.weight2;
            if (arms.Contains(weight.boneIndex3)) on += weight.weight3;
            return on;
        }

        /// <summary>The triangles that are not a sleeve's: a triangle goes when every corner is on an arm.</summary>
        public static List<int> WithoutSleeves(IReadOnlyList<int> triangles, IReadOnlyList<BoneWeight> weights, HashSet<int> arms)
        {
            var kept = new List<int>(triangles.Count);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                bool sleeve = true;
                for (int c = 0; c < 3 && sleeve; c++)
                {
                    int v = triangles[t + c];
                    sleeve = v < weights.Count && OnArm(weights[v], arms) >= SleeveWeight;
                }
                if (!sleeve)
                {
                    kept.Add(triangles[t]);
                    kept.Add(triangles[t + 1]);
                    kept.Add(triangles[t + 2]);
                }
            }
            return kept;
        }

        /// <summary>The shirt's mesh without its sleeves, made once a mesh (the same points, fewer triangles).</summary>
        public static Mesh SleevesOff(Mesh shirt)
        {
            if (shirt == null || !shirt.isReadable)
            {
                return shirt;
            }
            if (!Sleeveless.TryGetValue(shirt, out Mesh sleeveless))
            {
                sleeveless = Object.Instantiate(shirt);
                sleeveless.name = shirt.name + " (under a coat)";
                sleeveless.SetTriangles(WithoutSleeves(shirt.triangles, shirt.boneWeights, ArmBones()), 0);
                Sleeveless[shirt] = sleeveless;
            }
            return sleeveless;
        }
    }
}
