using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The fitted cap (docs/FITTED_CAP.md; owner, 2026-11-24: "make a basic fitted cap model we can make a bunch of colorways for ... make it
    /// fitted to our playermodel's round head like the red circle image i attached"): every colourway an item on the head with its prefab and
    /// colours; the crown round from above and hugging the ball of the head; the band above the eyes; the brim out in front.
    /// </summary>
    public sealed class FittedCapTests
    {
        private static Mesh Model() => FittedCapBuilder.BuildMesh();

        [Test]
        public void EveryColourwayIsACapOnTheHeadWithItsPrefabAndColours()
        {
            Assert.Greater(FittedCapColourways.All.Length, 0);
            foreach (FittedCapColourways.Colourway colourway in FittedCapColourways.All)
            {
                Assert.IsTrue(ItemRegistry.TryGet(colourway.ItemId, out ItemDefinition definition), colourway.ItemId);
                Assert.AreEqual(EquipSlots.Helmet, definition.EquipSlot, $"{colourway.ItemId} is not worn on the head");
                Assert.AreEqual(colourway.ItemId, FittedCapColourways.ItemIdForAlias(colourway.Alias));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FittedCapColourways.PrefabPath(colourway.ItemId));
                Assert.IsNotNull(prefab, $"{colourway.ItemId}'s prefab is missing -- rebuild the scene");
                Assert.IsNotNull(prefab.GetComponent<FittedCap>(), colourway.ItemId);
                Material[] materials = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                Assert.AreEqual(3, materials.Length, "the crown, the brim's underside and the button each have their colour");
                Assert.Less(Vector4.Distance(colourway.Crown, materials[(int)FittedCapBuilder.Part.Crown].GetColor("_BaseColor")), 1e-3f);
                Assert.Less(Vector4.Distance(colourway.UnderBrim, materials[(int)FittedCapBuilder.Part.UnderBrim].GetColor("_BaseColor")), 1e-3f);
                Assert.Less(Vector4.Distance(colourway.Button, materials[(int)FittedCapBuilder.Part.Button].GetColor("_BaseColor")), 1e-3f);
            }
            Assert.AreEqual(FittedCapColourways.All[0].ItemId, FittedCapColourways.ItemIdForAlias("cap"));
        }

        /// <summary>The crown hugs the ball of the head, just off it everywhere, and from above it is a circle (the owner's red circle).</summary>
        [Test]
        public void TheCrownIsRoundAndHugsTheHead()
        {
            Mesh mesh = Model();
            Vector3[] points = mesh.vertices;
            float head = FusedBodyShape.HeadRadius;
            float left = 0.0f, right = 0.0f, front = 0.0f, back = 0.0f;
            foreach (int i in mesh.GetTriangles((int)FittedCapBuilder.Part.Crown))
            {
                Vector3 p = points[i];
                if (p.y < FittedCapBuilder.BandHeight(Mathf.Atan2(p.x, p.z)) - 0.001f)
                {
                    continue;   // the brim, out in front and down from the band
                }
                Assert.GreaterOrEqual(p.magnitude, head + FittedCapBuilder.CrownGap - 0.001f, $"the crown goes into the head at {p}");
                Assert.LessOrEqual(p.magnitude, head + FittedCapBuilder.CrownGap + FittedCapBuilder.PanelPuff + 0.001f, $"the crown stands off the head at {p}");
                if (p.y > -0.1f)
                {
                    left = Mathf.Min(left, p.x);
                    right = Mathf.Max(right, p.x);
                    back = Mathf.Min(back, p.z);
                }
                if (p.y > -0.1f && Mathf.Abs(p.x) < 0.05f && p.z > 0.0f)
                {
                    front = Mathf.Max(front, p.z);
                }
            }
            float across = right - left, along = front - back;
            Assert.Less(Mathf.Abs(across - along), 0.03f, $"from above the crown is not round: {across:0.000} m across, {along:0.000} m front to back");
        }

        /// <summary>The band sits above the eyes in front and lower behind; the brim reaches out ahead of the face, as wide as the crown where it meets it.</summary>
        [Test]
        public void TheBandIsAboveTheEyesAndTheBrimOutInFront()
        {
            Assert.Greater(FittedCapBuilder.BandHeight(0.0f), 0.08f, "the band comes down over the eyes");
            Assert.Less(FittedCapBuilder.BandHeight(Mathf.PI), FittedCapBuilder.BandHeight(0.0f), "the band is not lower behind");
            Mesh mesh = Model();
            Vector3[] points = mesh.vertices;
            float reach = 0.0f;
            foreach (int i in mesh.GetTriangles((int)FittedCapBuilder.Part.UnderBrim))
            {
                Assert.GreaterOrEqual(points[i].z, -0.01f, $"the brim goes back past the sides at {points[i]}");
                reach = Mathf.Max(reach, points[i].z);
            }
            Assert.Greater(reach, FittedCapBuilder.CrownRadius + FittedCapBuilder.BrimReach * 0.9f, "the brim hardly reaches out");
            Assert.Less(mesh.triangles.Length / 3, 3000, "too many triangles for a cap");
        }
    }
}
