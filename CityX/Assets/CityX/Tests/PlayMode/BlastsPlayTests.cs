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

        private sealed class WallOfAura : IBarrier
        {
            public bool Covers(Vector3 fromPosition) => true;

            public void Absorb(float amount, Vector3 fromPosition)
            {
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (Explosion blast in Object.FindObjectsByType<Explosion>(FindObjectsSortMode.None)) Object.Destroy(blast.gameObject);
            foreach (GiantSlam slam in Object.FindObjectsByType<GiantSlam>(FindObjectsSortMode.None)) Object.Destroy(slam.gameObject);
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

        [UnityTest]
        public IEnumerator AGrenadeThrowsOnlyWhomItHurts()
        {
            Ground();
            PlayerVitals dashing = Body(new Vector3(2.0f, 0.05f, 0.0f));
            PlayerVitals behindABarrier = Body(new Vector3(0.0f, 0.05f, 2.0f));
            PlayerVitals standing = Body(new Vector3(-2.0f, 0.05f, 0.0f));
            yield return null;
            yield return null;
            Physics.SyncTransforms();
            dashing.GrantInvulnerability(5.0f);
            behindABarrier.Barrier = new WallOfAura();
            float before = dashing.Health;
            Explosion.Blast(new Vector3(0.0f, 0.2f, 0.0f), null);
            yield return null;
            Assert.AreEqual(before, dashing.Health, 1e-4f, "setup: a body dashing through the blast was hurt by it");
            Assert.IsFalse(dashing.GetComponent<Ragdoll>().IsLimp, "a grenade threw down a body that was dashing through its blast");
            Assert.IsFalse(behindABarrier.GetComponent<Ragdoll>().IsLimp, "a grenade threw down a body behind its barrier");
            Assert.IsTrue(standing.GetComponent<Ragdoll>().IsLimp, "setup: a grenade no longer throws down whom it hurts");
        }

        [UnityTest]
        public IEnumerator AGiantsSlamThrowsOnlyWhomItHurts()
        {
            Ground();
            float away = GiantRules.SlamRadius * 0.4f;
            PlayerVitals dashing = Body(new Vector3(away, 0.05f, 0.0f));
            PlayerVitals standing = Body(new Vector3(-away, 0.05f, 0.0f));
            yield return null;
            yield return null;
            Physics.SyncTransforms();
            dashing.GrantInvulnerability(5.0f);
            float before = dashing.Health;
            Assert.Greater(GiantSlam.Land(Vector3.zero, GiantRules.Scale, null), 0, "setup: the slam reached nobody");
            yield return null;
            Assert.AreEqual(before, dashing.Health, 1e-4f, "setup: a body dashing through the slam was hurt by it");
            Assert.IsFalse(dashing.GetComponent<Ragdoll>().IsLimp, "a giant's slam threw down a body that was dashing through it");
            Assert.IsTrue(standing.GetComponent<Ragdoll>().IsLimp, "setup: a giant's slam no longer throws down whom it hurts");
        }
    }
}
