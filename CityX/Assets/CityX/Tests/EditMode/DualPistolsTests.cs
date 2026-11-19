using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// DUAL PISTOLS (owner, 2026-11-19: "make dual wield pistols a thing. it will be a skill you have to unlock on the tree"; "Each click fires a
    /// gun"; "Two pistols in inventory"; "Call the guns skill tree "Gunslinger" for now"; "make it so you can equip two pistols in the primary
    /// or secondary slot once dual wield has been unlocked ... so technically you could carry 4 pistols"). The skill, where a pistol goes, the
    /// mirrored hold and the inventory with pairs: docs/DUAL_PISTOLS.md.
    /// </summary>
    public sealed class DualPistolsTests
    {
        // ---- the skill ----

        [Test]
        public void DualPistolsIsTheGunslingersFirstStarAndNeverGoesOnTheBar()
        {
            SkillNode node = SkillCatalog.Find(SkillCatalog.DualPistols);
            Assert.IsNotNull(node);
            Assert.AreEqual("gunslinger", node.TreeId);
            Assert.AreEqual("Gunslinger", SkillCatalog.FindTree("gunslinger").Name, "the owner's name for now");
            Assert.IsTrue(SkillCatalog.FindTree("gunslinger").InGame);
            Assert.IsEmpty(node.Requires, "the tree's root");
            Assert.AreEqual(SkillKind.Passive, node.Kind);
            Assert.IsFalse(node.GoesOnBar, "nothing to press: it changes what the hands can hold");
        }

        [Test]
        public void ItHasToBeLearntWithAPoint()
        {
            Assert.IsFalse(SkillBook.AsBeforeTheTree().Knows(SkillCatalog.DualPistols), "a body with no character does not have it");
            var book = new SkillBook();
            int points = 1;
            Assert.AreEqual(SkillBook.Refusal.None, book.TryLearn(SkillCatalog.DualPistols, ref points));
            Assert.AreEqual(0, points);
            Assert.IsTrue(book.Knows(SkillCatalog.DualPistols));
        }

        // ---- where a pistol goes ----

        private static string SlotFor(Dictionary<string, string> held, string itemId, bool pairs) =>
            PistolPairs.SlotFor(itemId, slot => held.TryGetValue(slot, out string id) ? id : string.Empty, pairs, EquipSlots.Primary);

        [Test]
        public void WithTheSkillAPistolGoesBesideAPistolBeforeTheNextSlot()
        {
            var held = new Dictionary<string, string>();
            string[] expected = { EquipSlots.Primary, EquipSlots.PrimaryOffHand, EquipSlots.Secondary, EquipSlots.SecondaryOffHand };
            foreach (string slot in expected)
            {
                Assert.AreEqual(slot, SlotFor(held, ItemRegistry.Pistol, true));
                held[slot] = ItemRegistry.Pistol;
            }
            Assert.AreEqual(EquipSlots.Primary, SlotFor(held, ItemRegistry.Pistol, true), "four pistols carried: the fifth replaces the one in hand");
        }

        [Test]
        public void WithoutItOrForAnythingButAPistolTheSlotsFillAsBefore()
        {
            var held = new Dictionary<string, string> { [EquipSlots.Primary] = ItemRegistry.Pistol };
            Assert.AreEqual(EquipSlots.Secondary, SlotFor(held, ItemRegistry.Pistol, false), "not learnt");
            Assert.AreEqual(EquipSlots.Secondary, SlotFor(held, ItemRegistry.CombatSword, true), "a sword is never paired");
            held[EquipSlots.Primary] = ItemRegistry.CombatSword;
            Assert.AreEqual(EquipSlots.Secondary, SlotFor(held, ItemRegistry.Pistol, true), "a pistol goes beside a pistol only");
            held[EquipSlots.Secondary] = ItemRegistry.AK47;
            Assert.AreEqual(EquipSlots.Primary, SlotFor(held, ItemRegistry.Pistol, true), "nor beside a rifle");
        }

        // ---- the hold, mirrored ----

        [Test]
        public void TheLeftHandHoldsItsGunAsTheRightHoldsItsOwnSeenInAMirror()
        {
            Quaternion hold = Quaternion.Euler(-5.0f, 90.0f, 180.0f) * Quaternion.Euler(12.0f, -7.0f, 3.0f);
            Vector3 point = new Vector3(0.02f, -0.05f, 0.10f);
            Assert.AreEqual(new Vector3(-0.02f, -0.05f, 0.10f), DualPistolHold.Mirror(point));
            Quaternion left = DualPistolHold.Mirror(hold);
            // Seen in the mirror (x flipped), the gun's barrel and top go where the mirror sends the right-hand gun's.
            Vector3 mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
            Assert.Less(Vector3.Distance(left * Vector3.forward, mirror(hold * Vector3.forward)), 1e-5f, "the barrel");
            Assert.Less(Vector3.Distance(left * Vector3.up, mirror(hold * Vector3.up)), 1e-5f, "the top");
            Assert.Less(Quaternion.Angle(hold, DualPistolHold.Mirror(left)), 0.01f, "a mirror twice is no mirror");
            Assert.AreEqual(1.0f, Mathf.Abs(Quaternion.Dot(left, left)), 1e-5f, "still a rotation");
        }

        // ---- the ammo counter ----

        [Test]
        public void TheCounterShowsThePairSideBySideAsItIsHeld()
        {
            var right = new CountedGun("15 / 45");
            var left = new CountedGun("7 / 30");
            Assert.AreEqual("15 / 45", PlayerHud.AmmoText(right, null));
            string both = PlayerHud.AmmoText(right, left);
            Assert.Less(both.IndexOf("7 / 30", System.StringComparison.Ordinal), both.IndexOf("15 / 45", System.StringComparison.Ordinal), "the left hand's on the left");
        }

        private sealed class CountedGun : IEquippableGun
        {
            private readonly string _text;

            public CountedGun(string text) => _text = text;

            public bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder) => false;
            public void TryReload() { }
            public string GetAmmoText() => _text;
            public bool IsReloading => false;
            public bool IsAutomatic => false;
            public event System.Action<GunShot> ShotResolved { add { } remove { } }
            public bool IsBarrelBlocked(Vector3 origin, Vector3 direction, GameObject wielder) => false;
            public void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder, out Vector3 point, out bool barrelBlocked)
            {
                point = origin;
                barrelBlocked = false;
            }
        }
    }
}
