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
    /// The frag grenade on real bodies (docs/GRENADE.md): thrown, it bounces and rolls, and when its fuse is out it goes off -- the body in the
    /// open hurt and thrown down, the one behind a wall spared, the thrower credited -- and the explosion is there to see; another player's copy
    /// of it flies the same way and goes off where its owner's did, hurting no one.
    /// </summary>
    public sealed class GrenadePlayTests
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
            foreach (Explosion explosion in Object.FindObjectsByType<Explosion>(FindObjectsSortMode.None)) Object.Destroy(explosion.gameObject);
            foreach (ThrownGrenade grenade in Object.FindObjectsByType<ThrownGrenade>(FindObjectsSortMode.None)) Object.Destroy(grenade.gameObject);
            FireScene.Clear();
        }

        private GameObject Thrown()
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownGrenade.prefab");
            Assert.IsNotNull(prefab, "no ThrownGrenade.prefab -- rebuild the scene");
            return prefab;
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            return null;
#endif
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(body);
            return body;
#else
            return null;
#endif
        }

        private static IEnumerator Wait(float seconds, System.Func<bool> until = null)
        {
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThrownItBouncesThenGoesOffHurtingWhatIsInTheOpenAndSparingWhatAWallShields()
        {
            GameObject thrower = Body(new Vector3(0.0f, 0.0f, -12.0f));
            GameObject open = Body(new Vector3(2.0f, 0.0f, 0.0f));
            GameObject shielded = Body(new Vector3(-3.5f, 0.0f, 0.0f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-2.0f, 2.0f, 0.0f);
            wall.transform.localScale = new Vector3(0.4f, 4.0f, 6.0f);
            _made.Add(wall);
            Physics.SyncTransforms();
            yield return null;
            var openVitals = open.GetComponent<PlayerVitals>();
            var shieldedVitals = shielded.GetComponent<PlayerVitals>();
            float openBefore = openVitals.Health, shieldedBefore = shieldedVitals.Health;

            ThrownGrenade grenade = ThrownGrenade.Spawn(Thrown(), new Vector3(0.0f, 0.5f, 0.0f), new Vector3(0.0f, 2.0f, 0.5f), thrower, false);
            Vector3 went = Vector3.zero;
            grenade.Exploded += at => went = at;
            int explosions = 0;
            System.Action<Vector3, float> counted = (at, radius) => explosions++;
            Explosion.WentOff += counted;
            try
            {
                yield return Wait(GrenadeRules.FuseSeconds * 0.5f);
                Assert.IsFalse(grenade.HasExploded, "it went off before its fuse was out");
                Assert.AreEqual(openBefore, openVitals.Health, 1e-3f, "it hurt someone before it went off");
                yield return Wait(GrenadeRules.FuseSeconds, () => explosions > 0);
            }
            finally
            {
                Explosion.WentOff -= counted;
            }
            Assert.AreEqual(1, explosions, "it did not go off once when its fuse was out");
            Assert.Less(went.y, 0.6f, "it never came down: it should bounce and roll, not hang in the air");
            Assert.Less(openVitals.Health, openBefore - 40.0f, "the body in the open beside it was barely hurt");
            Assert.IsTrue(open.GetComponent<Ragdoll>().IsLimp || openVitals.IsDead, "the blast did not throw the body in the open down");
            Assert.AreEqual(shieldedBefore, shieldedVitals.Health, 1e-3f, "the wall did not shield the body behind it");
            Assert.IsNotNull(Object.FindAnyObjectByType<Explosion>(), "no explosion to see");
        }

        [UnityTest]
        public IEnumerator TheExplosionIsBuiltInLayersAndIsGoneAfterAFewSeconds()
        {
            Body(new Vector3(30.0f, 0.0f, 0.0f));
            Explosion.Show(Vector3.zero);
            yield return null;
            Explosion explosion = Object.FindAnyObjectByType<Explosion>();
            Assert.IsNotNull(explosion);
            Light flash = explosion.GetComponentInChildren<Light>();
            Assert.IsTrue(flash != null && flash.enabled && flash.intensity > 10.0f, "no flash lighting the street");
            int bursting = 0;
            foreach (ParticleSystem layer in explosion.GetComponentsInChildren<ParticleSystem>())
            {
                bursting += layer.particleCount > 0 ? 1 : 0;
            }
            Assert.GreaterOrEqual(bursting, 4, "the flash core, fireball, sparks and debris are not all bursting");
            Assert.Greater(FireScene.Instance != null ? FireScene.Instance.GetComponentsInChildren<ParticleSystem>().Length : 0, 0, "no fire or smoke");
            yield return Wait(Explosion.LifeSeconds + 0.5f);
            Assert.IsTrue(explosion == null, "the explosion was never taken away");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyFliesTheSameWayAndGoesOffWhereItsOwnersDidHurtingNoOne()
        {
            GameObject copy = Body(new Vector3(0.0f, 0.0f, -10.0f));
            GameObject near = Body(new Vector3(1.0f, 0.0f, 0.0f));
            var thrower = copy.GetComponent<GrenadeThrower>();
            if (thrower == null)
            {
                thrower = copy.AddComponent<GrenadeThrower>();
            }
            thrower.ThrownPrefab = Thrown();
            var vitals = near.GetComponent<PlayerVitals>();
            float health = vitals.Health;
            thrower.PlayRemote(new AbilityEvent(GrenadeThrower.Thrown, new Vector3(0.0f, 1.5f, -9.0f), new Vector3(0.0f, 0.3f, 1.0f).normalized, 10.0f));
            yield return null;
            ThrownGrenade shown = Object.FindAnyObjectByType<ThrownGrenade>();
            Assert.IsTrue(shown != null && shown.IsCopy, "the copy threw nothing");
            yield return Wait(0.6f);
            Vector3 where = new Vector3(0.0f, 0.1f, 0.0f);
            thrower.PlayRemote(new AbilityEvent(GrenadeThrower.Exploded, where, Vector3.zero));
            yield return null;
            Explosion explosion = Object.FindAnyObjectByType<Explosion>();
            Assert.IsNotNull(explosion, "the copy did not go off with its owner's");
            Assert.Less(Vector3.Distance(explosion.transform.position, where), 0.05f, "it did not go off where its owner's did");
            yield return Wait(0.5f);
            Assert.AreEqual(health, vitals.Health, 1e-3f, "a copy's explosion hurt someone: the thrower's machine deals the blast");
        }
    }
}
