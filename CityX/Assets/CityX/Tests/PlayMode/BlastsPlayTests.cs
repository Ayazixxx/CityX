using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What a blast (a grenade, a giant's slam) reaches and throws. A grenade at a grown body's feet did nothing: the blast was measured to
    /// a chest twelve metres up, out of its reach. And a blast threw down a body it never hurt: one dashing through it, or behind an Arcane
    /// Barrier, a held guard or a shield.
    /// </summary>
    public sealed class BlastsPlayTests
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
            foreach (Explosion blast in Object.FindObjectsByType<Explosion>(FindObjectsSortMode.None)) Object.Destroy(blast.gameObject);
        }

        private void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
        }

        private PlayerVitals Body(Vector3 at, float scale = 1.0f)
        {
#if UNITY_EDITOR
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            body.transform.localScale = Vector3.one * scale;
            body.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(body);
            return body.GetComponent<PlayerVitals>();
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator AGrenadeAtAGrownBodysFeetHurtsIt()
        {
            Ground();
            PlayerVitals grown = Body(new Vector3(0.0f, 0.05f, 0.0f), 10.0f);
            yield return null;
            yield return null;
            Physics.SyncTransforms();
            float before = grown.Health;
            Explosion.Blast(new Vector3(1.5f, 0.2f, 0.0f), null);
            Assert.Less(grown.Health, before, "a grenade going off at a grown body's feet did nothing to it");
        }
    }
}
