using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Owner, 2026-10-18: under the Knight Helmet "make all hairstyles go bald until it is taken off then it goes back to the hairstyle you
    /// had equipped". The hair is hidden while the helmet is on, kept hidden through a change of style, and back when it comes off.
    /// </summary>
    public sealed class KnightHelmetPlayTests
    {
        [UnityTest]
        public IEnumerator TheHairIsHiddenWhileTheHelmetIsOnAndBackWhenItComesOff()
        {
            var body = new GameObject("Body");
            var hair = body.AddComponent<CharacterHair>();
            var helmetObject = new GameObject("KnightHelmet");
            var helmet = helmetObject.AddComponent<KnightHelmet>();

            helmet.OnEquip(body, ItemInstanceData.None);
            Assert.IsTrue(hair.IsCovered, "the hair shows through the helmet");
            hair.SetStyle(CharacterHair.Hyde);
            Assert.IsTrue(hair.IsCovered, "a new hairstyle came out through the helmet");

            helmet.OnUnequip(body);
            yield return null;   // Destroy() is deferred to the end of the frame
            Assert.IsFalse(hair.IsCovered, "the hair did not come back with the helmet off");
            Assert.AreEqual(CharacterHair.Hyde, hair.Style, "the hairstyle was lost under the helmet");
            Object.Destroy(body);
        }

        /// <summary>An open hat: any head wearable that does not say it hides the hair (the top hat was one until 2026-10-24).</summary>
        private sealed class OpenHat : HeadWearable
        {
            protected override string ItemId => "open_hat";

            protected override Vector3 HeadPosition => Vector3.zero;
        }

        [Test]
        public void AHatDoesNotHideTheHair()
        {
            var body = new GameObject("Body");
            var hair = body.AddComponent<CharacterHair>();
            var hat = new GameObject("OpenHat").AddComponent<OpenHat>();

            hat.OnEquip(body, ItemInstanceData.None);
            Assert.IsFalse(hair.IsCovered, "an open hat made the wearer bald");
            Object.DestroyImmediate(hat.gameObject);
            Object.DestroyImmediate(body);
        }

        [Test]
        public void TwoCoversNeedTwoUncovers()
        {
            var hair = new GameObject("Body").AddComponent<CharacterHair>();
            hair.Cover();
            hair.Cover();
            hair.Uncover();
            Assert.IsTrue(hair.IsCovered, "taking one helmet off showed the hair under the other");
            hair.Uncover();
            Assert.IsFalse(hair.IsCovered);
            Object.DestroyImmediate(hair.gameObject);
        }
    }
}
