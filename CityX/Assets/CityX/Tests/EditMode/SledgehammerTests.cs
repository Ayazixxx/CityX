using System.Reflection;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sledgehammer (owner, 2026-11-05: "make a sledgehammer (two handed) and its still a 3 hit combo but the third one he smashes it
    /// straight down vertically. make a low effort model i will replace later"; "use hand ik for the grip"): a two-handed blunt weapon on the
    /// greatsword's moves, its third light an overhead smash, a stand-in model, seen by other players, equipped by name.
    /// </summary>
    public sealed class SledgehammerTests
    {
        private sealed class Blade : ISwordBlade
        {
            public bool Hammer;

            public Vector3 BladeBaseWorld => Vector3.zero;

            public Vector3 BladeTipWorld => Vector3.up;

            public bool IsHammer => Hammer;

            public float GetMeleeDamage() => 20.0f;
        }

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Sledgehammer.prefab");
            Assert.IsNotNull(prefab, "no sledgehammer prefab: rebuild the scene (CityMassingSceneBuilder.Build)");
            return prefab;
        }

        [Test]
        public void ItIsATwoHandedBluntHammerWithAModel()
        {
            SwordWeapon weapon = Prefab().GetComponent<SwordWeapon>();
            Assert.IsNotNull(weapon, "the sledgehammer is not a melee weapon");
            Assert.AreEqual(ItemRegistry.Sledgehammer, weapon.ItemId);
            Assert.IsFalse(weapon.IsOneHanded, "a sledgehammer takes both hands");
            Assert.IsTrue(weapon.IsHammer, "it is not marked a hammer, so its third light would be the sword's diagonal");
            Assert.IsFalse(ItemRegistry.HasEdge(ItemRegistry.Sledgehammer), "a hammer has no edge to cut with");
            Assert.AreEqual(2, Prefab().GetComponentsInChildren<MeshRenderer>().Length, "the stand-in should be a handle and a head");
            Assert.Greater(weapon.BladeLength, 0.8f, "its reach is shorter than a hammer's handle");
        }

        [Test]
        public void ItsThirdLightSmashesStraightDownAndTheGreatswordKeepsItsDiagonal()
        {
            var body = new GameObject("Combat");
            try
            {
                var combat = body.AddComponent<SoulsSwordCombat>();
                combat.Equip(new Blade { Hammer = true });
                Assert.AreEqual(SwordStrikes.Sweep2HName, combat.LightAttack(0).Animation, "a hammer's first light is the greatsword's");
                Assert.AreEqual(SwordStrikes.Reverse2HName, combat.LightAttack(1).Animation, "a hammer's second light is the greatsword's");
                Assert.AreEqual(SwordStrikes.Smash2HName, combat.LightAttack(2).Animation, "a hammer's third light is not the overhead smash");
                Assert.AreEqual(3, combat.LightChainLength, "still a three-hit chain");

                combat.Equip(new Blade { Hammer = false });
                Assert.AreEqual(SwordStrikes.Cut2HName, combat.LightAttack(2).Animation, "the greatsword lost its diagonal finisher");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        /// <summary>Straight down the middle: wound up overhead, the blade in the upright plane through the body, coming down in front.</summary>
        [Test]
        public void TheSmashComesStraightDownTheMiddle()
        {
            Assert.IsFalse(SwordStrikes.IsOneHanded(SwordStrikeKind.Smash2H), "both hands stay on the handle");
            Assert.IsFalse(SwordStrikes.IsHeavy(SwordStrikeKind.Smash2H), "a light, not a heavy: no long hold");
            Assert.IsTrue(SwordStrikes.TryGet(SwordStrikes.Smash2HName, out SwordStrikeKind kind) && kind == SwordStrikeKind.Smash2H,
                "another player's copy cannot play the smash by name");
            Vector3 windup = SwordStrikes.HandPoint(SwordStrikes.Evaluate(SwordStrikeKind.Smash2H, SwordStrikes.StrikeStart(SwordStrikeKind.Smash2H)));
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(SwordStrikeKind.Smash2H);
            Vector3 contact = SwordStrikes.HandPoint(SwordStrikes.Evaluate(SwordStrikeKind.Smash2H, timing.Contact / timing.Total));
            Assert.Greater(windup.y, 2.0f, "the hammer is not raised overhead before it comes down");
            Assert.Less(contact.y, windup.y - 0.5f, "it does not come down");
            Assert.Less(Mathf.Abs(contact.x), 0.25f, "it does not come down the middle");
            Assert.Greater(contact.z, windup.z, "it does not come forward as it comes down");
            Assert.Greater(contact.z, 0.6f, "it does not land out in front of the body");
        }

        /// <summary>
        /// Owner, 2026-11-06, of the smash's wind-up: "looks like its clipping through the head". The overhead moves (the smash, and the heavy
        /// it shares with the greatsword) raise the hands above the brow and in front of it, so the handle passes over the head.
        /// </summary>
        [TestCase(SwordStrikeKind.Smash2H)]
        [TestCase(SwordStrikeKind.Slam2H)]
        public void TheOverheadHandleStaysOutOfTheHead(SwordStrikeKind kind)
        {
            var headCentre = new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f);
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
            const int Samples = 40;
            for (int i = 0; i <= Samples; i++)
            {
                float t = timing.Contact / timing.Total * i / Samples;
                SwordFrame frame = SwordStrikes.Evaluate(kind, t);
                Vector3 hand = SwordStrikes.HandPoint(frame);
                Vector3 butt = hand + frame.Blade * WeaponsEditor.SledgehammerBuilder.HandleBottom;
                Vector3 top = hand + frame.Blade * WeaponsEditor.SledgehammerBuilder.HandleTop;
                float clear = DistanceToSegment(headCentre, butt, top);
                Assert.Greater(clear, FusedBodyShape.HeadRadius, $"at {t:F2} of {kind} the handle passes {clear:F2} m from the head's middle, inside the head (the hand at {hand}, the handle along {frame.Blade})");
            }
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float along = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(point, a + ab * along);
        }

        [Test]
        public void ItIsListedAndCarded()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.Sledgehammer, out ItemDefinition definition), "the sledgehammer is not an item");
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
            MethodInfo kind = typeof(ItemCards).GetMethod("MeleeKind", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(kind, "ItemCards.MeleeKind is gone");
            Assert.AreEqual("Hammer", kind.Invoke(null, new object[] { ItemRegistry.Sledgehammer, false }), "its card calls it something else");
        }

        [Test]
        public void EveryBodyCanEquipItSoOthersSeeIt()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponentInChildren<Inventory>(true);
            bool found = false;
            foreach (Inventory.ItemPrefabEntry entry in inventory.ItemPrefabs)
            {
                found |= entry.ItemId == ItemRegistry.Sledgehammer && entry.Prefab != null;
            }
            Assert.IsTrue(found, "the networked player cannot build the sledgehammer, so other players would not see it");
        }

        [Test]
        public void TheConsoleEquipsItByName()
        {
            MethodInfo alias = typeof(CityX.UI.DevConsoleView).GetMethod("AliasToItemId", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(alias, "DevConsoleView.AliasToItemId is gone");
            foreach (string name in new[] { "sledgehammer", "sledge", "hammer" })
            {
                Assert.AreEqual(ItemRegistry.Sledgehammer, alias.Invoke(null, new object[] { name }), $"/equip {name}");
            }
        }
    }
}
