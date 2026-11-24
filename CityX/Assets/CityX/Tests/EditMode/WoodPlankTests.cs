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
    /// The wood plank (docs/WOOD_PLANK.md; owner, 2026-11-23: "a wood plank weapon, just a wood plank like a 2x4 ... that is common rarity level, its
    /// like one of the lowest level 2 handed weapons ... make it about the length of the metal pole maybe a tad less", textured like a cartoon plank,
    /// "add the chip at the top of it"): the item, the weapon it is, and the board: its size, its chip, its faces and its drawn wood.
    /// </summary>
    public sealed class WoodPlankTests
    {
        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WoodPlankBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "rebuild the scene (CityMassingSceneBuilder) or CityX/Rebuild Wood Plank");
            return prefab;
        }

        [Test]
        public void ItIsACommonTwoHandedBluntWeaponAsLowAsTheBat()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.WoodPlank, out ItemDefinition definition));
            Assert.AreEqual("Wood Plank", definition.DisplayName);
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
            Assert.AreEqual(Rarity.Common, ItemRarity.Of(ItemRegistry.WoodPlank), "the owner's common");
            Assert.IsFalse(ItemRegistry.HasEdge(ItemRegistry.WoodPlank), "a plank has no edge to cut with");
            SwordWeapon plank = Prefab().GetComponent<SwordWeapon>();
            SwordWeapon bat = AssetDatabase.LoadAssetAtPath<GameObject>(BaseballBatBuilder.PrefabPath).GetComponent<SwordWeapon>();
            Assert.AreEqual(ItemRegistry.WoodPlank, plank.ItemId);
            Assert.IsFalse(plank.IsOneHanded, "not two-handed");
            Assert.AreEqual(bat.GetMeleeDamage(), plank.GetMeleeDamage(), "not as low a blow as the bat's, the lowest two-handed one");
            Assert.IsNotNull(Prefab().GetComponent<WoodPlankGrip>(), "the hands would close through the board");
            Assert.AreEqual(WoodPlankShape.SupportPalmOffset, plank.SupportPalmOffset, "the left palm is not put on the board");
        }

        [Test]
        public void ItIsABoardATadShortOfThePoleWithAChipInItsTop()
        {
            Assert.That(WoodPlankShape.Length, Is.InRange(StaffRules.Tip - StaffRules.Butt - 0.4f, StaffRules.Tip - StaffRules.Butt - 0.05f),
                "not a tad short of the metal pole");
            Assert.That(WoodPlankShape.Width / WoodPlankShape.Thickness, Is.InRange(2.0f, 2.6f), "not a 2x4's look (3.5 by 1.5 inches)");
            Mesh mesh = Prefab().transform.Find("Model").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(WoodPlankShape.Bottom, mesh.bounds.min.y, 1e-4f);
            Assert.AreEqual(WoodPlankShape.Top, mesh.bounds.max.y, 1e-4f);
            Assert.AreEqual(WoodPlankShape.Top, Prefab().GetComponent<SwordWeapon>().BladeLength, 1e-4f, "it does not reach as far as the board");

            // The chip: points down into the top end, between its sides, on both broad faces.
            float chipBottom = WoodPlankShape.Top - WoodPlankShape.ChipDepth;
            int inTheChip = 0;
            foreach (Vector3 v in mesh.vertices)
            {
                if (Mathf.Abs(v.y - chipBottom) < 1e-4f && Mathf.Abs(v.x) < WoodPlankShape.Width * 0.5f - 0.01f) inTheChip++;
            }
            Assert.Greater(inTheChip, 0, "no chip in the top end");

            // Every face faces out of the board.
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 facing = Vector3.Cross(b - a, c - a);
                Assert.Greater(Vector3.Dot(facing, normals[triangles[i]]), 0.0f, "a face is wound against its normal");
            }
            Assert.Less(triangles.Length / 3, 100, "a board needs few faces");
        }

        [Test]
        public void ItIsPaintedWithTheDrawnWoodTilingAlongIt()
        {
            Material wood = Prefab().transform.Find("Model").GetComponent<MeshRenderer>().sharedMaterial;
            var texture = wood.GetTexture("_BaseMap") as Texture2D;
            Assert.IsNotNull(texture, "no wood drawn on it");
            Assert.AreEqual(WoodPlankBuilder.TexturePath, AssetDatabase.GetAssetPath(texture));
            Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapModeU, "the grain does not run on along the plank");
            Assert.AreEqual(TextureWrapMode.Clamp, texture.wrapModeV, "the two boards bleed into each other");
            foreach (Vector2 uv in Prefab().transform.Find("Model").GetComponent<MeshFilter>().sharedMesh.uv)
            {
                bool broad = uv.y >= WoodPlankShape.BroadBoard.x - 1e-4f && uv.y <= WoodPlankShape.BroadBoard.y + 1e-4f;
                bool narrow = uv.y >= WoodPlankShape.NarrowBoard.x - 1e-4f && uv.y <= WoodPlankShape.NarrowBoard.y + 1e-4f;
                Assert.IsTrue(broad || narrow, $"a point mapped between the texture's boards ({uv})");
            }
        }
    }
}
