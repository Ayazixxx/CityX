using CityX.Weapons;
using CityX.WeaponsEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The PHIZZ can (docs/PHIZZ_CAN.md): a real can's shape at the bottles' size, its parts (gold body, silver ends, the tab, the opening, the
    /// label), within a small prop's budget, the tab and the opening on the lid inside its rim and clear of each other.
    /// </summary>
    public sealed class PhizzCanTests
    {
        [Test]
        public void TheCanIsBuiltWithItsPartsAtTheBottlesSize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhizzCanBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "run CityX/Rebuild PHIZZ Can");
            int triangles = 0;
            Bounds bounds = default;
            bool first = true;
            foreach (string part in new[] { "Body", "Foot", "Top", "Rivet", "Tab", "Opening", "Print" })
            {
                Transform child = prefab.transform.Find(part);
                Assert.IsNotNull(child, $"the can has no {part}");
                Mesh mesh = child.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh, $"the {part} lost its mesh");
                Assert.Greater(mesh.triangles.Length, 0, $"the {part} is empty");
                triangles += mesh.triangles.Length / 3;
                if (first)
                {
                    bounds = mesh.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(mesh.bounds);
                }
            }
            Assert.Less(triangles, 3500, "over a small prop's budget (the bottles are allowed 5000)");
            Assert.AreEqual(CanMesh.Height * BottleShape.LifeScale, bounds.max.y, 0.004f, "it stands on its base at the origin, a real can's height");
            Assert.AreEqual(0.0f, bounds.min.y, 0.002f);
            Assert.AreEqual(CanMesh.Radius * BottleShape.LifeScale, bounds.max.x, 0.004f, "a real can's width");
            Assert.IsNotNull(prefab.transform.Find("Print").GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"), "the label is not printed");
        }

        [Test]
        public void TheTabAndTheOpeningLieOnTheLidInsideItsRimAndApart()
        {
            BottleMesh.Model tab = CanMesh.Tab(), opening = CanMesh.Opening();
            float tabNearest = float.MaxValue, openingFurthest = float.MinValue;
            foreach (Vector3 v in tab.Vertices)
            {
                Assert.Less(new Vector2(v.x, v.z).magnitude, CanMesh.LidRadius - 0.002f, "the tab reaches over the rim");
                Assert.GreaterOrEqual(v.y, CanMesh.LidHeight, "the tab is under the lid");
                tabNearest = Mathf.Min(tabNearest, v.x);
            }
            foreach (Vector3 v in opening.Vertices)
            {
                Assert.Less(new Vector2(v.x, v.z).magnitude, CanMesh.LidRadius - 0.002f, "the opening runs into the rim");
                openingFurthest = Mathf.Max(openingFurthest, v.x);
            }
            Assert.Less(openingFurthest, 0.0f, "the opening is on the other side of the rivet from the tab's finger end");
            Assert.Less(tabNearest, 0.0f, "the tab's nose does not reach back over the rivet, where it presses the opening");
        }
    }
}
