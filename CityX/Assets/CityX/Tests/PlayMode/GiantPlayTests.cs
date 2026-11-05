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
    /// The giant on a real body (docs/GIANT.md): the player's body at the Grow spell's size with a giant's health; it strides at a player; near
    /// one it crouches, leaps, and its slam hurts the player and throws them down, and does not hurt itself.
    /// </summary>
    public sealed class GiantPlayTests
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
            foreach (GiantSlam slam in Object.FindObjectsByType<GiantSlam>(FindObjectsSortMode.None)) Object.Destroy(slam.gameObject);
        }

        private GameObject Spawn(string path, Vector3 at, float yaw)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(600.0f, 1.0f, 600.0f);
                _made.Add(ground);
                Physics.SyncTransforms();
            }
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.Euler(0.0f, yaw, 0.0f));
            Assert.IsNotNull(body, $"no {path} -- rebuild the scene");
            _made.Add(body);
            return body;
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            return null;
#endif
        }

        /// <summary>A player's body (its reader on: no keyboard here, so it stands), for the giant to go for.</summary>
        private GameObject Player(Vector3 at)
        {
            GameObject player = Spawn("Assets/CityX/Character/Player.prefab", at, 0.0f);
            player.GetComponent<PlayerInputReader>().enabled = true;
            return player;
        }

        private static IEnumerator Wait(float seconds, System.Func<bool> until = null)
        {
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0.0f;
            b.y = 0.0f;
            return Vector3.Distance(a, b);
        }

        [UnityTest]
        public IEnumerator ItIsTheGrowSpellsSizeWithAGiantsHealthAndStridesAtAPlayer()
        {
            GameObject player = Player(Vector3.zero);
            GameObject giant = Spawn("Assets/CityX/Character/Giant.prefab", new Vector3(0.0f, 0.0f, 90.0f), 180.0f);
            Assert.AreEqual(GiantRules.Scale, giant.transform.lossyScale.y, 1e-3f, "the giant is not the Grow spell's size");
            var vitals = giant.GetComponent<PlayerVitals>();
            Assert.AreEqual(GiantRules.MaxHealth, vitals.MaxHealth, 1.0f, "the giant has a man's health");
            Assert.AreEqual(vitals.MaxHealth, vitals.Health, 1e-3f, "the giant was not given its whole health");
            float before = Flat(giant.transform.position, player.transform.position);
            yield return Wait(3.0f);
            float after = Flat(giant.transform.position, player.transform.position);
            Assert.Less(after, before - 10.0f, "the giant did not stride at the player");
        }

        [UnityTest]
        public IEnumerator NearAPlayerItLeapsAndItsSlamThrowsThemDownAndSparesItself()
        {
            GameObject player = Player(Vector3.zero);
            GameObject giant = Spawn("Assets/CityX/Character/Giant.prefab", new Vector3(0.0f, 0.0f, 20.0f), 180.0f);
            var brain = giant.GetComponent<GiantBrain>();
            var playerVitals = player.GetComponent<PlayerVitals>();
            var giantVitals = giant.GetComponent<PlayerVitals>();
            float playerBefore = playerVitals.Health;
            bool crouched = false, airborne = false;
            yield return Wait(15.0f, () =>
            {
                crouched |= brain.Mind.Current == GiantMind.Phase.Crouch;
                airborne |= brain.Mind.Current == GiantMind.Phase.Airborne && !giant.GetComponent<PlayerLocomotion>().IsGrounded;
                return brain.Slams > 0;
            });
            Assert.IsTrue(crouched, "the giant leapt without its tell");
            Assert.IsTrue(airborne, "the giant never left the ground");
            Assert.Greater(brain.Slams, 0, "the giant never came down on the player");
            Assert.Less(playerVitals.Health, playerBefore, "the slam did not hurt the player");
            Assert.IsTrue(player.GetComponent<Ragdoll>().IsLimp || playerVitals.IsDead, "the slam did not throw the player down");
            Assert.AreEqual(giantVitals.MaxHealth, giantVitals.Health, 1e-3f, "the giant's own slam hurt it");
        }

        /// <summary>
        /// Thrown limp in the middle of its leap, its mind stayed in the leap while it lay there: the moment it was back on its feet it
        /// "landed", and the slam came with no crouch to see and run from.
        /// </summary>
        [UnityTest]
        public IEnumerator KnockedOutOfItsLeapItDoesNotSlamUntilItHasCrouchedAgain()
        {
            Player(Vector3.zero);
            GameObject giant = Spawn("Assets/CityX/Character/Giant.prefab", new Vector3(0.0f, 0.0f, 20.0f), 180.0f);
            var brain = giant.GetComponent<GiantBrain>();
            var ragdoll = giant.GetComponent<Ragdoll>();
            var locomotion = giant.GetComponent<PlayerLocomotion>();
            yield return Wait(15.0f, () => brain.Mind.Current == GiantMind.Phase.Airborne && !locomotion.IsGrounded);
            Assert.AreEqual(GiantMind.Phase.Airborne, brain.Mind.Current, "setup: the giant never leapt");
            Assert.AreEqual(0, brain.Slams, "setup: the giant had already come down");

            giant.GetComponent<PlayerVitals>().TriggerLaunchKnockdown(Vector3.up * 2.0f);
            yield return null;
            Assert.IsTrue(ragdoll.IsLimp, "setup: the giant was not thrown limp");
            yield return Wait(20.0f, () => !ragdoll.IsLimp);
            Assert.IsFalse(ragdoll.IsLimp, "setup: the giant never stood up again");

            bool crouched = false;
            yield return Wait(8.0f, () =>
            {
                crouched |= brain.Mind.Current == GiantMind.Phase.Crouch;
                return brain.Slams > 0;
            });
            Assert.IsTrue(brain.Slams == 0 || crouched, "back on its feet, the giant slammed without crouching first");
        }
    }
}
