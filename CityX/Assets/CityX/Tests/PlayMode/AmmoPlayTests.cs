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
    /// AMMUNITION ON THE REAL BODY (owner, 2026-11-21: "make a basic ammunition model for each type of gun that is an in game right now as an item
    /// spawn you can spawn and pick up into your inventory and press r to reload into your guns"). A box picked up puts its rounds in the bag; R
    /// loads a gun from its own spare rounds first, then from the bag; a magazine gun and the shotgun's tube alike; and a gun with no rounds
    /// anywhere does not reload.
    /// </summary>
    public sealed class AmmoPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

#if UNITY_EDITOR
        private GameObject Spawn(string path, Vector3 at)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- rebuild the scene (CityMassingSceneBuilder)");
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            _spawned.Add(go);
            return go;
        }

        private IEnumerator PlayerWith(string gunPath, System.Action<GameObject, PistolWeapon> ready)
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(60, 1, 60);
            _spawned.Add(ground);
            GameObject player = Spawn("Assets/CityX/Character/Player.prefab", new Vector3(0, 0.05f, 0));
            player.GetComponent<PlayerInputReader>().enabled = false;
            if (player.GetComponent<Inventory>() == null)
            {
                player.AddComponent<Inventory>();   // the scene's player has one; Player.prefab alone does not
            }
            var gun = Spawn(gunPath, new Vector3(20, 1, 20)).GetComponent<PistolWeapon>();
            gun.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < 0.4f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
            }
            ready(player, gun);
        }

        private static IEnumerator Reload(GameObject player, PistolWeapon gun)
        {
            gun.TryReload();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; gun.IsReloading && t < 10.0f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
            }
            Assert.IsFalse(gun.IsReloading, "the reload never finished");
        }
#endif

        [UnityTest]
        public IEnumerator APickedUpBoxPutsItsRoundsInTheBagAndRLoadsThePistolFromThem()
        {
#if UNITY_EDITOR
            GameObject player = null;
            PistolWeapon gun = null;
            yield return PlayerWith("Assets/CityX/Weapons/Pistol.prefab", (p, g) => { player = p; gun = g; });
            var inventory = player.GetComponent<Inventory>();
            GameObject box = Spawn("Assets/CityX/Weapons/Ammo/PistolAmmo.prefab", new Vector3(1, 0, 1));
            var ammo = box.GetComponent<AmmoBox>();
            int rounds = ammo.Rounds;
            Assert.Greater(rounds, 0, "the box is empty");
            Assert.AreEqual(ItemRegistry.PistolAmmo, ammo.ItemId);
            Assert.AreEqual(0, GroundPickup.TakeStack(player.transform, box, ammo.ItemId, ammo.Rounds), "rounds were left in the box");
            yield return null;
            Assert.IsTrue(box == null, "the empty box was left on the ground");
            Assert.AreEqual(rounds, inventory.CountInStorage(ItemRegistry.PistolAmmo), "the box's rounds are not in the bag");

            gun.SetAmmo(0, 5);
            Assert.AreEqual(5 + rounds, gun.SpareRounds, "the spare rounds are not the gun's own and the bag's");
            StringAssert.EndsWith("/ " + gun.SpareRounds, gun.GetAmmoText(), "the HUD does not count the bag's rounds");
            yield return Reload(player, gun);
            int size = gun.Profile.MagazineSize;
            Assert.AreEqual(size, gun.MagazineAmmo, "the magazine was not filled");
            Assert.AreEqual(0, gun.ReserveAmmo, "the gun's own spare rounds were not used first");
            Assert.AreEqual(rounds - (size - 5), inventory.CountInStorage(ItemRegistry.PistolAmmo), "the bag lost the wrong number of rounds");
#else
            Assert.Ignore("needs the editor's asset database");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheShotgunLoadsItsTubeShellByShellFromTheBag()
        {
#if UNITY_EDITOR
            GameObject player = null;
            PistolWeapon gun = null;
            yield return PlayerWith("Assets/CityX/Weapons/Shotgun.prefab", (p, g) => { player = p; gun = g; });
            var inventory = player.GetComponent<Inventory>();
            Assert.AreEqual(3, inventory.AddStacked(ItemRegistry.ShotgunShells, 3));
            gun.SetAmmo(1, 0);
            yield return Reload(player, gun);
            Assert.AreEqual(4, gun.MagazineAmmo, "the tube did not take the bag's three shells");
            Assert.AreEqual(0, inventory.CountInStorage(ItemRegistry.ShotgunShells), "shells are left in the bag");
#else
            Assert.Ignore("needs the editor's asset database");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WithNoRoundsAnywhereRDoesNothingAndAnotherGunsAmmoIsNotTaken()
        {
#if UNITY_EDITOR
            GameObject player = null;
            PistolWeapon gun = null;
            yield return PlayerWith("Assets/CityX/Weapons/Pistol.prefab", (p, g) => { player = p; gun = g; });
            var inventory = player.GetComponent<Inventory>();
            inventory.AddStacked(ItemRegistry.RifleAmmo, 30);
            gun.SetAmmo(0, 0);
            gun.TryReload();
            Assert.IsFalse(gun.IsReloading, "a pistol with no pistol rounds started a reload");
            Assert.AreEqual(30, inventory.CountInStorage(ItemRegistry.RifleAmmo), "the pistol took rifle rounds");
            yield return null;
#else
            Assert.Ignore("needs the editor's asset database");
            yield break;
#endif
        }
    }
}
