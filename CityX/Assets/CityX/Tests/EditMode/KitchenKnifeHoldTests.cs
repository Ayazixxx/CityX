using CityX.Weapons;
using CityX.WeaponsEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The kitchen knife's size and hold, as the owner set them by hand in the editor (2026-11-15: "the knife model needs to be scaled up and
    /// adjust", then "set this as the hold for the kitchen knife"): its own, not the one shared by the other one-handed weapons.
    /// </summary>
    public sealed class KitchenKnifeHoldTests
    {
        private static SwordWeapon Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<SwordWeapon>();

        [Test]
        public void TheKitchenKnifeIsHeldAndSizedAsTheOwnerSetIt()
        {
            SwordWeapon knife = Load(KitchenKnifeBuilder.PrefabPath);
            Assert.IsTrue(knife.HasOwnHold, "the knife uses the shared one-handed hold");
            Assert.Less((knife.HoldPosition - new Vector3(0.109f, -0.04f, 0.17f)).magnitude, 1e-4f, "the knife is not where the owner put it in the hand");
            Assert.Less(Quaternion.Angle(knife.HoldRotation, Quaternion.Euler(-85.6f, -5.15f, -178.1f)), 0.01f, "the knife is not turned as the owner turned it");

            Transform model = knife.transform.Find("Model");
            Assert.IsNotNull(model, "the knife's parts are not under one scaled model");
            Assert.AreEqual(Vector3.one * 2.11f, model.localScale, "the knife is not the size the owner made it");
            Assert.AreEqual(KitchenKnifeBuilder.BladeLength * 2.11f, knife.BladeLength, 1e-4f, "a swing does not reach as far as the bigger blade");
        }

        [Test]
        public void TheOtherOneHandedWeaponsKeepTheSharedHold()
        {
            foreach (string path in new[] { KitchenKnifeBuilder.ButterKnifePrefabPath, "Assets/CityX/Weapons/CombatSword.prefab", "Assets/CityX/Weapons/MetalCrowbar.prefab" })
            {
                SwordWeapon weapon = Load(path);
                Assert.IsFalse(weapon.HasOwnHold, path);
                Assert.Less((weapon.HoldPosition - SwordWeapon.OneHandGripPosition).magnitude, 1e-5f, path);
            }
        }
    }
}
