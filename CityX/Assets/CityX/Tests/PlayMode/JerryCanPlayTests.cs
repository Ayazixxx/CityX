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
    /// <summary>The jerry can (docs/FIRE.md): in the hand, pouring gasoline where it is pointed until it is empty, in a copy's hand too.</summary>
    public sealed class JerryCanPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private const string PrefabPath = "Assets/CityX/Weapons/JerryCan.prefab";
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            FireScene.Clear();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            FireScene.Clear();
            if (FireScene.Instance != null)
            {
                Object.Destroy(FireScene.Instance.gameObject);
            }
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().Revive();
            player.GetComponent<PlayerLocomotion>().Bind();
            _made.Add(player);
            return player;
#else
            return null;
#endif
        }

        private static GameObject CanPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            return null;
#endif
        }

        /// <summary>The can in a body's hand, as the inventory would put it there (the plain test body has no inventory list).</summary>
        private JerryCanItem Equip(GameObject player)
        {
            GameObject prefab = CanPrefab();
            Assert.IsNotNull(prefab, "no JerryCan.prefab -- run CityX/Rebuild Massing Scene");
            GameObject can = Object.Instantiate(prefab);
            _made.Add(can);
            var item = can.GetComponent<JerryCanItem>();
            Assert.IsNotNull(item, "JerryCan.prefab has no JerryCanItem");
            item.OnEquip(player, ItemInstanceData.None);
            return item;
        }

        [UnityTest]
        public IEnumerator ItPoursGasolineAheadOfItselfUntilItIsEmpty()
        {
            GameObject player = Body(Vector3.zero);
            JerryCanItem can = Equip(player);
            Assert.IsNotNull(can.Spout, "the can has no spout");
            Assert.AreSame(can, player.GetComponent<PlayerInputReader>().EquippedFlamethrower, "the can is not the thing left-click holds");
            var locomotion = player.GetComponent<PlayerLocomotion>();
            can.Litres = 1.0f;

            for (float t = 0.0f; t < 1.0f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                can.Tick(true, player.transform.position + Vector3.up * 2.4f, Vector3.forward, player, Step);
                yield return null;
            }
            Assert.IsTrue(can.IsPouring, "the can is not pouring with the trigger held");
            Assert.Greater(can.Poured, 0.3f, "a second of pouring laid too little gasoline");
            Assert.Less(can.Litres, 0.6f, "the can did not empty as it poured");
            FireField field = FireScene.Instance.Field;
            bool ahead = false;
            foreach (FireCell cell in field.Cells)
            {
                if (cell.HasGasoline && cell.Centre.z > 0.0f && cell.Centre.z < 3.0f)
                {
                    ahead = true;
                }
            }
            Assert.IsTrue(ahead, "the gasoline did not land on the ground ahead of the can");
            Assert.Greater(FireMarks.GasolineCount, 0, "the poured gasoline is not shown");

            for (float t = 0.0f; t < 1.5f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                can.Tick(true, player.transform.position + Vector3.up * 2.4f, Vector3.forward, player, Step);
                yield return null;
            }
            Assert.AreEqual(0.0f, can.Litres, 1e-4f, "the can never ran dry");
            Assert.IsFalse(can.IsPouring, "an empty can is still pouring");
            Assert.AreEqual(1.0f, can.Poured, 0.05f, "the can poured a different amount than it held");
        }

        [UnityTest]
        public IEnumerator TheGasolineItPoursCatchesFire()
        {
            GameObject player = Body(new Vector3(10.0f, 0.0f, 10.0f));
            JerryCanItem can = Equip(player);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < 1.0f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                can.Tick(true, player.transform.position + Vector3.up * 2.4f, Vector3.forward, player, Step);
                yield return null;
            }
            FireField field = FireScene.Instance.Field;
            FireCell wet = null;
            foreach (FireCell cell in field.Cells)
            {
                if (cell.HasGasoline)
                {
                    wet = cell;
                    break;
                }
            }
            Assert.IsNotNull(wet, "nothing was poured");
            Assert.Greater(FireScene.Ignite(wet.Centre, FireRules.SparkIgniteRadius, true, player), 0, "a spark did not light the poured gasoline");
            yield return null;
            Assert.IsTrue(wet.Burning && wet.Gasoline);
        }

        [UnityTest]
        public IEnumerator ACopyOfAnotherPlayerCarriesItInTheSameHand()
        {
            GameObject owner = Body(Vector3.zero);
            JerryCanItem held = Equip(owner);
            Transform ownerHand = owner.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Assert.IsTrue(held.transform.IsChildOf(ownerHand), "the can is not held in the right hand");

            GameObject copy = Body(new Vector3(5.0f, 0.0f, 0.0f));
            GameObject prefab = CanPrefab();
            Assert.IsNotNull(prefab, "no JerryCan.prefab -- run CityX/Rebuild Massing Scene");
            GameObject shown = Object.Instantiate(prefab);
            _made.Add(shown);
            shown.GetComponent<ICosmeticMountable>().MountCosmetic(copy);
            Transform copyHand = copy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Assert.IsTrue(shown.transform.IsChildOf(copyHand), "the copy's can is not in its right hand");
            Assert.AreEqual(held.transform.localPosition, shown.transform.localPosition, "the copy holds the can differently from its owner");
            yield return null;
        }
    }
}
