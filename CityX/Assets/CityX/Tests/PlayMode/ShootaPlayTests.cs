using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE SHOOTA ON THE REAL BODY WITH THE REAL AK (owner, 2026-10-15): he shoots a player he can see and hurts them, never through a wall,
    /// runs behind a crate when hurt, and when his magazine runs dry he takes cover and changes it there. Simulated at a steady 60 frames a
    /// second.
    /// </summary>
    public sealed class ShootaPlayTests
    {
        private const string ShootaPath = "Assets/CityX/Character/Shoota.prefab";
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private static GameObject Load(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = centre;
            box.transform.localScale = size;
            _made.Add(box);
            return box;
        }

        /// <summary>Ground, the Shoota at the origin facing +z, and the player <paramref name="distance"/> m away along +z, hard to kill.</summary>
        private (ShootaBrain shoota, PlayerVitals player) Stage(float distance, bool sturdy = true)
        {
            Time.captureFramerate = 60;
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(120.0f, 1.0f, 120.0f));
            GameObject playerObject = Object.Instantiate(Load(PlayerPath), new Vector3(0.0f, 0.05f, distance), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            _made.Add(playerObject);
            var player = playerObject.GetComponent<PlayerVitals>();
            player.InfiniteHealth = sturdy;
            GameObject shootaObject = Object.Instantiate(Load(ShootaPath), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(shootaObject);
            Physics.SyncTransforms();
            var shoota = shootaObject.GetComponent<ShootaBrain>();
            Assert.IsNotNull(shoota, "the Shoota prefab has no brain -- rebuild the scene");
            shoota.Target = player;
            return (shoota, player);
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds * 60.0f); i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HeShootsAPlayerHeCanSeeAndHurtsThem()
        {
            (ShootaBrain shoota, PlayerVitals player) = Stage(15.0f, sturdy: false);
            int hurt = 0;
            player.Damaged += amount => hurt++;
            Assert.IsNotNull(shoota.GetComponentInChildren<PistolWeapon>(), "no AK in his hands");
            yield return Seconds(3.0f);
            Assert.Greater(shoota.RoundsFired, 5, $"he hardly fired ({shoota.State})");
            Assert.Greater(hurt, 2, $"{shoota.RoundsFired} rounds at 15 m and only {hurt} hit");
            Assert.Less(player.Health, player.MaxHealth, "the hits did no harm");
        }

        [UnityTest]
        public IEnumerator HeNeverShootsThroughAWall()
        {
            (ShootaBrain shoota, PlayerVitals player) = Stage(15.0f);
            Box(new Vector3(0.0f, 2.5f, 7.0f), new Vector3(10.0f, 5.0f, 0.5f));
            Physics.SyncTransforms();
            yield return Seconds(3.0f);
            Assert.AreEqual(0, shoota.RoundsFired, "he shot at someone he could not see");
        }

        [UnityTest]
        public IEnumerator HurtHeRunsBehindACrateAndOutOfRoundsHeReloadsInCover()
        {
            (ShootaBrain shoota, PlayerVitals player) = Stage(18.0f);
            // A crate off to his left, between him and where the player stands.
            Box(new Vector3(-4.0f, 1.0f, 3.0f), new Vector3(3.0f, 2.0f, 1.0f));
            Physics.SyncTransforms();
            yield return Seconds(1.5f);
            Assert.AreEqual(ShootaState.Engage, shoota.State, "he never engaged");

            ((IDamageable)shoota.GetComponent<PlayerVitals>()).ApplyHit(10.0f, player.transform.position, 0.0f);
            bool tookCover = false;
            for (int i = 0; i < 60 * 4 && !tookCover; i++)
            {
                yield return null;
                tookCover = shoota.HasCover && Vector3.Distance(Flat(shoota.transform.position), Flat(shoota.CoverSpot)) < ShootaBrain.AtCoverMetres;
            }
            Assert.IsTrue(tookCover, $"hurt, he never reached cover ({shoota.State}, cover {shoota.HasCover} at {shoota.CoverSpot})");
            Assert.Less(shoota.transform.position.z, 2.6f, "the spot is not behind the crate");

            // Fight on until the magazine runs dry: he goes back to cover (or stays) and changes it.
            bool reloaded = false;
            bool wasDry = false;
            for (int i = 0; i < 60 * 20 && !reloaded; i++)
            {
                yield return null;
                wasDry |= shoota.Magazine <= 3 || shoota.State == ShootaState.Reload;
                reloaded = wasDry && shoota.Magazine == 30 && shoota.State != ShootaState.Reload;
            }
            Assert.IsTrue(wasDry, $"he never ran low ({shoota.Magazine} left, {shoota.RoundsFired} fired)");
            Assert.IsTrue(reloaded, $"he never finished changing the magazine ({shoota.State}, {shoota.Magazine})");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
