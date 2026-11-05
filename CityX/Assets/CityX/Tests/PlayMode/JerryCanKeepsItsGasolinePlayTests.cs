using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A jerry can keeps what is left in it when it is put away. Changing weapons takes the can out of the world and makes a new one when
    /// it is drawn again, and the new one came full: an empty can scrolled away and back was twenty litres again, for ever.
    /// </summary>
    public sealed class JerryCanKeepsItsGasolinePlayTests
    {
        private const string CanPath = "Assets/CityX/Weapons/JerryCan.prefab";
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

        private T Make<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- rebuild the scene");
            GameObject made = Object.Instantiate(prefab, new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity);
            _made.Add(made);
            return made.GetComponent<T>();
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator PutAwayAndTakenOutAgainTheCanHoldsWhatItHeld()
        {
            GameObject player = Make<PlayerVitals>("Assets/CityX/Character/Player.prefab").gameObject;
            player.GetComponent<PlayerInputReader>().enabled = false;
            yield return null;
            JerryCanItem can = Make<JerryCanItem>(CanPath);
            can.OnEquip(player, ItemInstanceData.None);
            Assert.AreEqual(FireRules.CanLitres, can.Litres, 1e-4f, "setup: a new can is not full");
            can.Litres = 3.5f;

            // What changing weapons does (Inventory.Holster, then Draw).
            ItemInstanceData kept = can.GetInstanceData();
            can.OnUnequip(player);
            yield return null;
            JerryCanItem again = Make<JerryCanItem>(CanPath);
            again.OnEquip(player, kept);
            Assert.AreEqual(3.5f, again.Litres, 1e-4f, "put away and taken out again, the can was full");

            // Emptied, it stays empty.
            again.Litres = 0.0f;
            kept = again.GetInstanceData();
            again.OnUnequip(player);
            yield return null;
            JerryCanItem empty = Make<JerryCanItem>(CanPath);
            empty.OnEquip(player, kept);
            Assert.AreEqual(0.0f, empty.Litres, 1e-4f, "an empty can put away came back full");
        }
    }
}
