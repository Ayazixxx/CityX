using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE REPORTED "PINK/PURPLE LIKE A MISSING TEXTURE" muzzle sparks.
    /// PistolBuilder used to assign a bare in-memory `new Material(...)`
    /// straight to the sparks' ParticleSystemRenderer with no asset backing
    /// of its own -- it did not reliably survive
    /// PrefabUtility.SaveAsPrefabAsset, confirmed directly: loading the
    /// saved Pistol.prefab back found sharedMaterial null, which URP
    /// renders as its usual bright pink "missing shader" fallback (see
    /// CharacterSkin's own doc comment for the identical symptom on a body
    /// with no material at all). Now saved as its own real .mat asset
    /// first, matching BloodSplatterBuilder/GlassShatterBuilder/
    /// GlassShardsBuilder's own established convention for particle
    /// materials.
    /// </summary>
    public sealed class PistolSparkMaterialTests
    {
        [Test]
        public void TheMuzzleSparksMaterialSurvivesLoadingTheSavedPrefab()
        {
#if UNITY_EDITOR
            GameObject pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Weapons/Pistol.prefab");
            Assert.IsNotNull(pistolPrefab, "Pistol.prefab is missing");

            var pistol = pistolPrefab.GetComponent<PistolWeapon>();
            Assert.IsNotNull(pistol.MuzzleSparks, "PistolWeapon.MuzzleSparks is not wired");

            var renderer = pistol.MuzzleSparks.GetComponent<ParticleSystemRenderer>();
            Material material = renderer.sharedMaterial;

            Assert.IsNotNull(material,
                "the sparks renderer's material is null on the SAVED asset -- this is exactly what " +
                "renders as bright pink/purple in play, not a missing texture on a real material");
            Assert.IsNotNull(material.shader, "the sparks material has no shader assigned");
            Assert.IsTrue(material.shader.isSupported,
                $"the sparks material's shader '{material.shader.name}' is not supported, which also " +
                "renders as the pink/purple fallback");
#else
            Assert.Ignore("Pistol.prefab can only be loaded by path inside the editor.");
#endif
        }
    }
}
