using CityX.Weapons;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The swordsman wears the owner's character "Knight" and the Knight Helmet (2026-10-18).</summary>
    public sealed class SwordsmanLookTests
    {
        private const string PrefabPath = "Assets/CityX/Character/Swordsman.prefab";

        [Test]
        public void HeWearsKnightsPaintAndPutsOnTheKnightHelmet()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no Swordsman.prefab: rebuild the scene (CityMassingSceneBuilder)");

            bool painted = false;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.name == "SwordsmanSkin" && material.GetTexture("_BaseMap") != null)
                    {
                        painted = true;
                    }
                }
            }
            Assert.IsTrue(painted, "the swordsman's body is not wearing Knight's paint");

            var brain = prefab.GetComponent<SwordsmanBrain>();
            Assert.IsNotNull(brain.HelmetPrefab, "the swordsman has no helmet to put on");
            Assert.IsNotNull(brain.HelmetPrefab.GetComponent<KnightHelmet>(), "the swordsman's helmet is not the Knight Helmet");
        }
    }
}
