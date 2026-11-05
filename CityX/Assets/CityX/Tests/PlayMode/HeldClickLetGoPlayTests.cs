using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What a held click was doing is let go of when the body is taken out of its owner's hands, with the real mouse on the real body. A bow
    /// drawn when its archer was staggered stayed drawn (nothing that reads the click runs while a body is staggered, limp or dead), and
    /// loosed its arrow by itself the moment control came back.
    /// </summary>
    public sealed class HeldClickLetGoPlayTests : InputTestFixture
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void TearDownScene()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (ArrowProjectile arrow in Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None)) Object.Destroy(arrow.gameObject);
        }

        [UnityTest]
        public IEnumerator ABowDrawnWhenItsArcherIsStaggeredIsLetDownAndDoesNotLooseByItself()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
            var cameraObject = new GameObject("Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0.0f, 3.0f, -5.0f), Quaternion.identity);
            _made.Add(cameraObject);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(player);
            var vitals = player.GetComponent<PlayerVitals>();
            GameObject bowObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Bow.prefab"));
            Assert.IsNotNull(bowObject, "no Bow.prefab -- rebuild the scene");
            _made.Add(bowObject);
            var bow = bowObject.GetComponent<BowWeapon>();
            bow.OnEquip(player, ItemInstanceData.None);
            int shots = 0;
            bow.ShotResolved += shot => shots++;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Press(_mouse.leftButton);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.IsTrue(bow.IsDrawing, "setup: holding the click does not draw the bow");

            // Staggered with the bow drawn; the click is let go while the stagger lasts.
            vitals.Stun(0.4f);
            yield return null;
            yield return null;
            Release(_mouse.leftButton);
            for (int i = 0; i < 200 && vitals.IsStunned; i++)
            {
                yield return null;
            }
            Assert.IsFalse(vitals.IsStunned, "setup: the stagger never ended");
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
            Assert.AreEqual(0, shots, "the bow loosed by itself when its archer came out of the stagger");
            Assert.IsFalse(bow.IsDrawing, "the bow is still drawn with nobody holding the click");
#else
            yield break;
#endif
        }
    }
}
