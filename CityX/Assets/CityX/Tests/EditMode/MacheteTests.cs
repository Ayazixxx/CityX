using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using CityX.WeaponsEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The machete (docs/MACHETE.md; owner, 2026-11-23, with Meshy's "Machete": "put this machete in the game as a one handed sword, texture it so the
    /// blade is silver and the handle is black with those 3 silver dots, also lower the face count to a reasonable amount and get rid of the square grid
    /// pattern on the handle just make it smooth"): the item, the one-handed edged sword it is, and the built model: its faces, its colours, its smooth
    /// handle, its rivets, and its frame (the handle down, the edge where every swing leads).
    /// </summary>
    public sealed class MacheteTests
    {
        /// <summary>A weapon's triangles at most for "fine" (tools/model_qa.py).</summary>
        private const int TriangleBudget = 6000;

        /// <summary>Meshy's handle alone was 185,000 triangles of chequering; a smooth one is a few rings.</summary>
        private const int SmoothHandleMost = 2500;

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MacheteBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "rebuild the scene (CityMassingSceneBuilder) or CityX/Rebuild Machete");
            return prefab;
        }

        private static Mesh Part(GameObject prefab, string name)
        {
            Transform part = prefab.transform.Find(name);
            Assert.IsNotNull(part, "no " + name);
            return part.GetComponent<MeshFilter>().sharedMesh;
        }

        [Test]
        public void ItIsAOneHandedSwordWithAnEdgeAndItsOwnItem()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.Machete, out ItemDefinition definition));
            Assert.AreEqual("Machete", definition.DisplayName);
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
            Assert.IsTrue(ItemRegistry.HasEdge(ItemRegistry.Machete), "a machete cuts");
            SwordWeapon sword = Prefab().GetComponent<SwordWeapon>();
            Assert.AreEqual(ItemRegistry.Machete, sword.ItemId);
            Assert.IsTrue(sword.IsOneHanded, "not one-handed");
            Assert.IsFalse(sword.EdgeOnPlusX, "the model is laid with its edge on -X");
            Assert.AreEqual(CombatTuning.OneHandedSwordDamage, sword.GetMeleeDamage(), "not a one-handed sword's blow");
        }

        [Test]
        public void TheModelIsSmallSilverBladeBlackHandleSilverRivets()
        {
            GameObject prefab = Prefab();
            int triangles = 0;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                triangles += filter.sharedMesh.triangles.Length / 3;
            }
            TestContext.WriteLine($"{triangles} triangles, the handle {Part(prefab, "Handle").triangles.Length / 3}");
            Assert.Less(triangles, TriangleBudget, "over a weapon's budget");
            Assert.Less(Part(prefab, "Handle").triangles.Length / 3, SmoothHandleMost, "the handle is not the smooth one");

            Color Colour(string part) => prefab.transform.Find(part).GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
            foreach (string silver in new[] { "Blade", "Rivets" })
            {
                Assert.Greater(Colour(silver).grayscale, 0.6f, silver + " is not silver");
            }
            Assert.Less(Colour("Handle").grayscale, 0.1f, "the handle is not black");
        }

        [Test]
        public void ItsHandleIsDownItsEdgeLeadsAndThreeRivetsSitOnEachFace()
        {
            GameObject prefab = Prefab();
            Bounds blade = Part(prefab, "Blade").bounds, handle = Part(prefab, "Handle").bounds;
            Assert.Greater(blade.min.y, -0.02f, "the blade goes down past the guard");
            Assert.Less(handle.max.y, 0.02f, "the handle comes up past the guard");
            Assert.That(blade.max.y - handle.min.y, Is.InRange(1.0f, 1.2f), "not a machete's length on this body");
            Assert.AreEqual(blade.max.y, prefab.GetComponent<SwordWeapon>().BladeLength, 1e-3f, "the sword does not reach as far as its blade");

            // The edge is the blade's thin side, and the butt's hook (the fingers' side) is on it.
            // (Measured over a stretch of the blade, each side against that stretch's own width: the blade widens toward the tip.)
            var stretch = new List<Vector3>();
            foreach (Vector3 v in Part(prefab, "Blade").vertices)
            {
                if (v.y > 0.3f && v.y < 0.4f) stretch.Add(v);
            }
            float left = float.MaxValue, right = float.MinValue;
            foreach (Vector3 v in stretch)
            {
                left = Mathf.Min(left, v.x);
                right = Mathf.Max(right, v.x);
            }
            float Thickness(float side)
            {
                float most = 0.0f;
                float near = side < 0.0f ? left : right;
                foreach (Vector3 v in stretch)
                {
                    if (Mathf.Abs(v.x - near) < 0.01f) most = Mathf.Max(most, Mathf.Abs(v.z));
                }
                return most;
            }
            TestContext.WriteLine($"the blade is {Thickness(-1.0f) * 2000.0f:0.0} mm thick at -X, {Thickness(1.0f) * 2000.0f:0.0} mm at +X");
            Assert.Less(Thickness(-1.0f), Thickness(1.0f), "the thin side, the edge, is not on -X");
            Assert.Less(handle.min.x, -handle.max.x, "the butt's hook is not on the edge's side");

            // Three rivets each side, out of the handle's faces, down its middle.
            var faces = new Dictionary<float, int> { [1.0f] = 0, [-1.0f] = 0 };
            Mesh rivets = Part(prefab, "Rivets");
            Assert.Greater(rivets.bounds.max.z, handle.max.z, "the rivets do not stand out of the face");
            Assert.Less(rivets.bounds.min.z, handle.min.z, "the rivets do not stand out of the other face");
            Assert.That(rivets.bounds.min.y, Is.GreaterThan(handle.min.y), "a rivet below the handle");
            foreach (float side in new[] { 1.0f, -1.0f })
            {
                var down = new List<float>();
                foreach (Vector3 v in rivets.vertices)
                {
                    if (Mathf.Sign(v.z) == side)
                    {
                        down.Add(v.y);
                        Assert.Less(Mathf.Abs(v.x), 0.01f, "a rivet off the handle's middle line");
                    }
                }
                down.Sort();
                int count = down.Count > 0 ? 1 : 0;
                for (int i = 1; i < down.Count; i++)
                {
                    if (down[i] - down[i - 1] > 0.01f) count++;
                }
                faces[side] = count;
            }
            Assert.AreEqual(3, faces[1.0f], "not three rivets on one face");
            Assert.AreEqual(3, faces[-1.0f], "not three rivets on the other face");
        }
    }
}
