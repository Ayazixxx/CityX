using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// THE SERVER CHECKS A FAR PLAYER'S BAG (docs/SERVER_SAVING.md, step 3b): a player on someone else's server keeps what it carried and what
    /// that server handed it (a dropped item picked up, a fallen enemy's gear), and nothing from nowhere; this machine's own player's bag, and an
    /// admin's console edit, are taken as sent.
    /// </summary>
    public sealed class BagRulesTests
    {
        private const string Player = "0123456789abcdef0123456789abcdef";
        private readonly BagRules _rules = new BagRules();

        private static BagEntry Stored(int index, string itemId, int count = 1) => new BagEntry { Index = index, ItemId = itemId, Count = count };

        private static BagEntry Worn(string slot, string itemId) => new BagEntry { Slot = slot, ItemId = itemId };

        private static BagEntry Gun(string slot, string itemId, int magazine = 15, int reserve = 45) =>
            new BagEntry { Slot = slot, ItemId = itemId, HasAmmo = true, Magazine = magazine, Reserve = reserve };

        private List<BagEntry> Keep(List<BagEntry> before, List<BagEntry> after, Dictionary<string, int> handed, List<string> takenOut = null) =>
            _rules.Keep(before, after, id => handed != null && handed.TryGetValue(id, out int n) ? n : 0, takenOut ?? new List<string>());

        // ---- the ledger ----

        [Test]
        public void AHandedItemIsCountedUntilASaveShowsItOrItIsForgotten()
        {
            var handed = new ItemsHanded();
            handed.Hand("ayaz", ItemRegistry.AK47, 1, 0.0);
            handed.Hand("ayaz", ItemRegistry.AK47, 1, 10.0);
            handed.Hand("other", ItemRegistry.AK47, 1, 10.0);
            Assert.AreEqual(2, handed.Count("ayaz", ItemRegistry.AK47, 20.0));
            handed.UseUp("ayaz", ItemRegistry.AK47, 1, 20.0);
            Assert.AreEqual(1, handed.Count("ayaz", ItemRegistry.AK47, 20.0), "a save showed one: one is left");
            Assert.AreEqual(1, handed.Count("other", ItemRegistry.AK47, 20.0), "another character's was used up");
            Assert.AreEqual(0, handed.Count("ayaz", ItemRegistry.AK47, 10.0 + ItemsHanded.RememberSeconds + 1.0),
                "one picked up and dropped again before a save could be spent on something else later");
        }

        // ---- the rules ----

        [Test]
        public void WhatItHadAndWhatTheServerHandedItAreKept()
        {
            var before = new List<BagEntry> { Stored(0, ItemRegistry.Grenade, 2), Worn(EquipSlots.Torso, ItemRegistry.TShirt), Gun(EquipSlots.Primary, ItemRegistry.Pistol) };
            var after = new List<BagEntry>
            {
                Stored(3, ItemRegistry.Grenade, 1),                          // one thrown, the other moved
                Stored(0, ItemRegistry.TShirt),                              // taken off into storage
                Gun(EquipSlots.Primary, ItemRegistry.Pistol, 4, 30),         // shot
                Gun(EquipSlots.Secondary, ItemRegistry.AK47, 30, 90),       // picked up off the street
            };
            var takenOut = new List<string>();
            List<BagEntry> kept = Keep(before, after, new Dictionary<string, int> { [ItemRegistry.AK47] = 1 }, takenOut);
            Assert.IsEmpty(takenOut, string.Join(", ", takenOut));
            Assert.AreEqual(after.Count, kept.Count);
        }

        [Test]
        public void SomethingFromNowhereIsTakenOutAndAStackGrownFromNowhereCutBack()
        {
            var before = new List<BagEntry> { Stored(0, ItemRegistry.Grenade, 2), Gun(EquipSlots.Primary, ItemRegistry.Pistol) };
            var after = new List<BagEntry>
            {
                Stored(0, ItemRegistry.Grenade, 5),
                Gun(EquipSlots.Primary, ItemRegistry.Pistol),
                Gun(EquipSlots.Secondary, ItemRegistry.AK47),
                Stored(1, ItemRegistry.Pistol),
            };
            var takenOut = new List<string>();
            List<BagEntry> kept = Keep(before, after, null, takenOut);
            Assert.AreEqual(2, kept.Single(e => e.ItemId == ItemRegistry.Grenade).Count, "the grenades grew from nowhere");
            Assert.IsFalse(kept.Any(e => e.ItemId == ItemRegistry.AK47), "an AK-47 from nowhere was kept");
            Assert.AreEqual(1, kept.Count(e => e.ItemId == ItemRegistry.Pistol), "a second pistol from nowhere was kept");
            Assert.AreEqual(3, takenOut.Count, string.Join(", ", takenOut));
        }

        [Test]
        public void WhatCannotBeCarriedAsItIsSaidToBeIsTakenOut()
        {
            var before = new List<BagEntry>
            {
                Stored(0, ItemRegistry.Grenade, 50), Stored(1, ItemRegistry.TShirt), Stored(2, ItemRegistry.AK47), Stored(3, ItemRegistry.Pistol),
                Stored(4, ItemRegistry.Pistol), Stored(5, ItemRegistry.KnightHelmet), Stored(6, ItemRegistry.GlassBottle),
            };
            var after = new List<BagEntry>
            {
                Stored(0, "golden_minigun"),                                          // not a thing the game has
                Stored(1, ItemRegistry.Grenade, 50),                                  // more than a stack
                Worn(EquipSlots.Primary, ItemRegistry.TShirt),                        // a shirt is not a weapon
                Gun(EquipSlots.Secondary, ItemRegistry.Pistol),
                Gun(EquipSlots.SecondaryOffHand, ItemRegistry.AK47),                  // only a pistol beside a pistol
                Gun(EquipSlots.Primary, ItemRegistry.Pistol, 900, 900),               // more rounds than any gun holds
                Worn(EquipSlots.Helmet, ItemRegistry.KnightHelmet),
                Worn(EquipSlots.Helmet, ItemRegistry.KnightHelmet),                   // two helmets on one head
                Stored(-1, ItemRegistry.GlassBottle),                                 // no such storage slot
            };
            var takenOut = new List<string>();
            List<BagEntry> kept = Keep(before, after, null, takenOut);
            CollectionAssert.AreEquivalent(new[] { $"{EquipSlots.Secondary} {ItemRegistry.Pistol}", $"{EquipSlots.Helmet} {ItemRegistry.KnightHelmet}" },
                kept.Select(e => $"{e.Slot} {e.ItemId}").ToArray(), string.Join(", ", takenOut));
            Assert.AreEqual(7, takenOut.Count);
        }

        // ---- the server ----

        [Test]
        public void AFarPlayersBagHoldsWhatTheServerSawItGetAndTheOwnPlayersIsTakenAsSent()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            IBagRules previous = CharacterService.BagRules;
            try
            {
                CharacterService.BagRules = _rules;
                Directory.CreateDirectory(directory);
                double now = 0.0;
                var service = new CharacterService(new CharacterDatabase(directory), () => now);
                File.WriteAllText(Path.Combine(directory, CharacterDatabase.AdminsFileName), "{\"EveryoneIsAdmin\":false,\"Keys\":[]}");
                CharacterSave made = service.Create(Player, CharacterSave.Create("Joiner", UnityEngine.Color.white));
                CharacterSave playing = service.Play(Player, made.Id);
                playing.BagVersion = CharacterSave.CurrentBagVersion;

                // From another machine: an AK-47 from nowhere is taken out; one the server handed (picked up off the street) is kept, once.
                now = 30.0;
                playing.Bag = new List<BagEntry> { Gun(EquipSlots.Primary, ItemRegistry.AK47) };
                service.Save(Player, playing, fromAfar: true);
                Assert.IsEmpty(service.Play(Player, made.Id).Bag, "an AK-47 from nowhere was kept");
                service.Hand(made.Id, ItemRegistry.AK47);
                service.Save(Player, playing, fromAfar: true);
                Assert.AreEqual(1, service.Play(Player, made.Id).Bag.Count, "the AK-47 the server handed was taken out");
                playing.Bag.Add(Stored(0, ItemRegistry.AK47));
                service.Save(Player, playing, fromAfar: true);
                Assert.AreEqual(1, service.Play(Player, made.Id).Bag.Count, "one AK-47 handed was spent twice");

                // An admin's console edit passes only for an admin.
                service.Save(Player, playing, adminEdited: true, fromAfar: true);
                Assert.AreEqual(1, service.Play(Player, made.Id).Bag.Count, "a console edit passed for a player who is not an admin");
                File.WriteAllText(Path.Combine(directory, CharacterDatabase.AdminsFileName), $"{{\"EveryoneIsAdmin\":false,\"Keys\":[\"{Player}\"]}}");
                service.Save(Player, playing, adminEdited: true, fromAfar: true);
                Assert.AreEqual(2, service.Play(Player, made.Id).Bag.Count, "an admin's console edit was refused");

                // This machine's own player: its bag is its own.
                playing.Bag.Add(Stored(1, ItemRegistry.Shotgun));
                service.Save(Player, playing);
                Assert.AreEqual(3, service.Play(Player, made.Id).Bag.Count, "this machine's own player's bag was judged");
            }
            finally
            {
                CharacterService.BagRules = previous;
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }
}
