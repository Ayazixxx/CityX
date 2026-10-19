using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The combat sword (owner's COMBATSWORDX.glb, 2026-10-13): the first one-handed sword, its own item, equippable and seen by other players.</summary>
    public sealed class CombatSwordTests
    {
        private const string PrefabPath = "Assets/CityX/Weapons/CombatSword.prefab";

        [Test]
        public void ItIsAOneHandedSwordItemOfItsOwn()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.CombatSword, out ItemDefinition definition));
            Assert.AreEqual("hand_r", definition.EquipSlot);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "CombatSword.prefab is missing -- rebuild (CityX/Rebuild Combat Sword)");
            SwordWeapon sword = prefab.GetComponent<SwordWeapon>();
            Assert.IsNotNull(sword, "not a sword");
            Assert.AreEqual(ItemRegistry.CombatSword, sword.ItemId, "picking it up would give another sword");
            Assert.IsTrue(sword.OneHanded, "it should be held in one hand");
            Assert.IsTrue(((ISwordBlade)sword).IsOneHanded);
            Assert.AreEqual(1.39f, sword.BladeLength, 0.02f, "the blade's reach should be the model's own");
            Bounds bounds = prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            Assert.Greater(bounds.size.y, 1.5f, "the model did not come through");
        }

        [Test]
        public void TheGuardSitsAtTheRootWithTheHandleBelowAndTheBladeAbove()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
            Transform model = filter.transform;
            Bounds bounds = filter.sharedMesh.bounds;
            float top = model.TransformPoint(new Vector3(0.0f, bounds.max.y, 0.0f)).y;
            float bottom = model.TransformPoint(new Vector3(0.0f, bounds.min.y, 0.0f)).y;
            Assert.Greater(top, 1.3f, "the tip should be well above the guard");
            Assert.Less(bottom, -0.4f, "the handle and pommel should hang below the guard");
            Assert.Greater(bottom, -0.6f);

            // The handle's centre line runs through the root (sideways and front-to-back), so the hand's hold is on the handle.
            int count = 0;
            Vector3 sum = Vector3.zero;
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 w = model.TransformPoint(v);
                if (w.y > -0.45f && w.y < -0.12f)
                {
                    sum += w;
                    count++;
                }
            }
            Assert.Greater(count, 50, "no handle vertices found under the guard");
            Vector3 centre = sum / count;
            Assert.Less(Mathf.Abs(centre.x), 0.01f, $"the handle is {centre.x:0.000} m off the root sideways");
            Assert.Less(Mathf.Abs(centre.z), 0.01f, $"the handle is {centre.z:0.000} m off the root front-to-back");
        }

        [Test]
        public void TheOneHandedHoldPutsTheBladeOutPastTheThumbWithTheEdgeAlongTheKnuckles()
        {
            Quaternion hold = SwordWeapon.DefaultOneHandGripRotation;
            Vector3 blade = hold * Vector3.up;
            Vector3 edge = hold * Vector3.left;
            Assert.Less(Vector3.Angle(blade, SwordWeapon.OneHandBladeAxisInHand), 0.1f, "the blade does not run along the hold's axis");
            Assert.Greater(edge.x, 0.9f, $"the edge should face along the fingers (+X), not {edge.ToString("0.00")}");
            Assert.Less(Mathf.Abs(Vector3.Dot(blade, edge)), 0.001f);
            Vector3 position = SwordWeapon.DefaultOneHandGripPosition;
            Assert.Less(position.y, -0.05f, "the handle should sit on the palm's side of the hand (-Y)");
            Assert.Greater(position.z, 0.05f, "the guard should sit out past the thumb (+Z)");
        }

        /// <summary>
        /// The combat sword's sharp side is its +X (the model is 2-7 mm thick there, 24-27 mm on -X: measured 2026-10-17), so its poses lead with
        /// that side and its default hold turns that side to the knuckles, as the owner's own saved hold does.
        /// </summary>
        [Test]
        public void ItsEdgeIsTheModelsThinSideAndTheHoldTurnsItToTheKnuckles()
        {
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<SwordWeapon>();
            Assert.IsTrue(sword.EdgeOnPlusX, "the combat sword's edge is marked on its thick side");
            Assert.AreEqual(Vector3.right, sword.EdgeLocal);
            Quaternion hold = SwordWeapon.DefaultOneHandGripRotationFor(true);
            Assert.Less(Vector3.Angle(hold * Vector3.up, SwordWeapon.OneHandBladeAxisInHand), 0.1f, "turning the hold for the edge moved the blade");
            Assert.Greater((hold * sword.EdgeLocal).x, 0.9f, "the default hold does not turn the combat sword's edge to the knuckles");
        }

        /// <summary>The owner's white-and-black paint (2026-10-19), kept small: one 512 colour map and one 256 metallic map, on a model still light.</summary>
        [Test]
        public void ItWearsItsPaintInTwoSmallMaps()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Mesh mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "the sword has no texture coordinates");
            Assert.LessOrEqual(mesh.triangles.Length / 3, 5000, "the sword is heavier than it needs to be");
            Material material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Texture colour = material.GetTexture("_BaseMap");
            Texture metal = material.GetTexture("_MetallicGlossMap");
            Assert.IsNotNull(colour, "the sword has no paint");
            Assert.IsNotNull(metal, "the sword does not know which parts are steel");
            Assert.LessOrEqual(colour.width, 512);
            Assert.LessOrEqual(metal.width, 256);
            Assert.IsTrue(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), "the metallic map is set but not used");
        }

        [Test]
        public void EveryBodyCanEquipItSoOthersSeeIt()
        {
            foreach (string path in new[] { "Assets/CityX/Character/NetworkedPlayer.prefab" })
            {
                var inventory = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Inventory>(true);
                bool found = false;
                foreach (Inventory.ItemPrefabEntry entry in inventory.ItemPrefabs)
                {
                    found |= entry.ItemId == ItemRegistry.CombatSword && entry.Prefab != null;
                }
                Assert.IsTrue(found, $"{path} cannot build the combat sword, so other players would not see it");
            }
        }
    }
}
