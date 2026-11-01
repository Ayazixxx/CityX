using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// An enemy looks for a player about once a second, whether or not it has one (docs/PERFORMANCE.md, 2026-10-31): the search scans the
    /// whole scene, and every idle enemy used to run it every frame. A target that dies is replaced at once.
    /// </summary>
    public sealed class EnemyTargetSearchPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private GameObject Spawn(string name, Vector3 at)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/" + name + ".prefab");
            Assert.IsNotNull(prefab, "no " + name + ".prefab");
            GameObject made = Object.Instantiate(prefab, at, Quaternion.identity);
            _made.Add(made);
            return made;
#else
            return null;
#endif
        }

        [UnityTest]
        [TestCase("Swordsman", ExpectedResult = null)]
        [TestCase("Shoota", ExpectedResult = null)]
        [TestCase("Giant", ExpectedResult = null)]
        public IEnumerator AnEnemyWithNobodyNearLooksForAPlayerAboutOnceASecondNotEveryFrame(string enemy)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _made.Add(ground);
            Spawn(enemy, new Vector3(0.0f, 0.05f, 0.0f));
            for (int i = 0; i < 10; i++) yield return null;   // woken and settled
            int before = PlayerBodies.Searches;
            for (int i = 0; i < 90; i++) yield return null;   // a second and a half at the steady 60 frames a second
            int searches = PlayerBodies.Searches - before;
            Assert.That(searches, Is.InRange(1, 3), $"the {enemy} searched for a player {searches} times in 90 frames; about once a second is the rule");
        }
    }
}
