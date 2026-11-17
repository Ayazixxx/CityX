using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.Weapons;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The blade enemies wear the owner's characters: the swordsman "Knight" and the Knight Helmet (2026-10-18); Crazy Guy "guycrazy", his kick hair,
    /// jeans and sneakers, with the kitchen knife and the knife's way of fighting (2026-11-16).
    /// </summary>
    public sealed class SwordsmanLookTests
    {
        private const string PrefabPath = "Assets/CityX/Character/Swordsman.prefab";
        private const string CrazyGuyPath = "Assets/CityX/Character/CrazyGuy.prefab";

        private static bool WearsPaint(GameObject prefab, string materialName)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.name == materialName && material.GetTexture("_BaseMap") != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [Test]
        public void HeWearsKnightsPaintAndPutsOnTheKnightHelmet()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no Swordsman.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsTrue(WearsPaint(prefab, "SwordsmanSkin"), "the swordsman's body is not wearing Knight's paint");

            var brain = prefab.GetComponent<SwordsmanBrain>();
            Assert.AreEqual(1, brain.Wearing.Length, "the swordsman wears the Knight Helmet and nothing else");
            Assert.IsNotNull(brain.Wearing[0].GetComponent<KnightHelmet>(), "the swordsman's helmet is not the Knight Helmet");
            Assert.AreEqual(DuelistStyleKind.Swordsman, brain.Style);
        }

        [Test]
        public void CrazyGuyIsGuycrazyWithAKitchenKnifeFightingTheKnifesWay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrazyGuyPath);
            Assert.IsNotNull(prefab, "no CrazyGuy.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsTrue(WearsPaint(prefab, "CrazyGuySkin"), "Crazy Guy's body is not wearing guycrazy's paint");

            var brain = prefab.GetComponent<SwordsmanBrain>();
            Assert.IsNotNull(brain, "Crazy Guy has no brain to fight with");
            Assert.AreEqual(DuelistStyleKind.Knife, brain.Style, "Crazy Guy does not fight the knife's way");
            SwordWeapon blade = brain.SwordPrefab != null ? brain.SwordPrefab.GetComponent<SwordWeapon>() : null;
            Assert.IsNotNull(blade, "Crazy Guy has no blade");
            Assert.AreEqual(CityX.Items.ItemRegistry.KitchenKnife, blade.ItemId, "Crazy Guy's blade is not the kitchen knife");

            CharacterSave look = JsonUtility.FromJson<CharacterSave>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CityX/Character/NpcLooks/GuyCrazy.json").text);
            Assert.AreEqual("guycrazy", look.Name);
            Assert.AreEqual(look.Wearing.Count, brain.Wearing.Count(piece => piece != null), "Crazy Guy does not wear what guycrazy wears");
            Assert.AreEqual(look.HairStyle, prefab.GetComponentInChildren<CharacterHair>(true).Style, "Crazy Guy's hair is not guycrazy's");
        }
    }
}
