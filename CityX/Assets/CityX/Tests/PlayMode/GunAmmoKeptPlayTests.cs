using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A gun keeps its rounds when it is put away. Changing weapons takes the gun's model out of the world and makes a new one when it is
    /// drawn again, and the new one came loaded: scrolling to the other weapon and back was a free, instant reload and a full reserve.
    /// </summary>
    public sealed class GunAmmoKeptPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Player()
        {
#if UNITY_EDITOR
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
#else
            return null;
#endif
        }

        private PistolWeapon Gun(string path)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- rebuild the scene");
            GameObject gun = Object.Instantiate(prefab, new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity);
            _made.Add(gun);
            return gun.GetComponent<PistolWeapon>();
#else
            return null;
#endif
        }

        private IEnumerator PutAwayAndDrawnAgainItHasTheRoundsItHad(string path)
        {
            GameObject player = Player();
            yield return null;
            PistolWeapon gun = Gun(path);
            gun.OnEquip(player, ItemInstanceData.None);
            Assert.AreEqual(gun.Profile.MagazineSize, gun.MagazineAmmo, "setup: a new gun is not loaded");
            gun.SetAmmo(2, 7);

            // What changing weapons does (Inventory.Holster, then Draw): the gun's state is asked for, its model is put away, and a new
            // model is given that state.
            ItemInstanceData kept = gun.GetInstanceData();
            gun.OnUnequip(player);
            yield return null;
            PistolWeapon again = Gun(path);
            again.OnEquip(player, kept);
            Assert.AreEqual(2, again.MagazineAmmo, "put away and drawn again, the magazine was full: a free reload");
            Assert.AreEqual(7, again.ReserveAmmo, "put away and drawn again, the spare rounds were all back");

            // A gun nobody has held yet (no state to give it) is still a loaded gun.
            again.OnUnequip(player);
            yield return null;
            PistolWeapon fresh = Gun(path);
            fresh.OnEquip(player, ItemInstanceData.None);
            Assert.AreEqual(fresh.Profile.MagazineSize, fresh.MagazineAmmo, "a new gun came empty");
            Assert.AreEqual(fresh.Profile.StartingReserveAmmo, fresh.ReserveAmmo);
        }

        [UnityTest]
        public IEnumerator ThePistolPutAwayAndDrawnAgainHasTheRoundsItHad() => PutAwayAndDrawnAgainItHasTheRoundsItHad("Assets/CityX/Weapons/Pistol.prefab");

        [UnityTest]
        public IEnumerator TheRiflePutAwayAndDrawnAgainHasTheRoundsItHad() => PutAwayAndDrawnAgainItHasTheRoundsItHad("Assets/CityX/Weapons/AK47.prefab");
    }
}
