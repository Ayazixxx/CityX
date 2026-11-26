using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Squirrel Boy on the real body (docs/SQUIRREL_BOY.md): in the owner's character's outfit, the SB design on his shirt; calm beside the
    /// player until struck, then punching the player with his fists; badly hurt, up the nearest wall.
    /// </summary>
    public sealed class SquirrelBoyPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
        }

        private static GameObject Asset(string path)
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, $"{path} is missing -- rebuild the scene");
            return asset;
#else
            return null;
#endif
        }

        /// <summary>The ground, a tall wall 10 m off, the player and Squirrel Boy 2 m apart.</summary>
        private (SquirrelBoyBrain squirrel, PlayerLocomotion player) Stage()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 10.0f, 12.0f);
            wall.transform.localScale = new Vector3(30.0f, 20.0f, 2.0f);
            _made.Add(wall);
            GameObject player = Object.Instantiate(Asset("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, -2.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(player);
            GameObject squirrel = Object.Instantiate(Asset("Assets/CityX/Character/SquirrelBoy.prefab"), new Vector3(0.0f, 0.05f, 0.0f),
                Quaternion.Euler(0.0f, 180.0f, 0.0f));
            _made.Add(squirrel);
            var brain = squirrel.GetComponent<SquirrelBoyBrain>();
            brain.Target = player.GetComponent<PlayerVitals>();   // a body with its controls off is no player to PlayerBodies: named, as for the swordsman
            return (brain, player.GetComponent<PlayerLocomotion>());
        }

        private static IEnumerator Run(PlayerLocomotion player, int frames)
        {
            var ragdoll = player.GetComponent<Ragdoll>();
            for (int i = 0; i < frames; i++)
            {
                if (ragdoll == null || !ragdoll.IsLimp)
                {
                    player.Tick(new LocomotionInput(), Dt);   // (knocked down by a heavy punch, the body is the ragdoll's)
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HisShirtIsPrintedWithTheSbDesign()
        {
            (SquirrelBoyBrain squirrel, PlayerLocomotion player) = Stage();
            yield return Run(player, 2);
            Assert.IsNotNull(squirrel.ShirtDesign, "the SB design is not in his prefab");
            string design = DesignStore.IdOf(squirrel.ShirtDesign.bytes);
            var worn = new List<string>();
            foreach (MonoBehaviour part in squirrel.GetComponentsInChildren<MonoBehaviour>())
            {
                if (part is IPrintable && part is IEquippableItem shirt)
                {
                    Assert.AreEqual(design, shirt.GetInstanceData().DesignId, "his shirt is not printed with the SB design");
                    worn.Add("shirt");
                }
            }
            Assert.Contains("shirt", worn, "he wears no shirt");
        }

        [UnityTest]
        public IEnumerator CalmBesideThePlayerUntilStruckThenPunchesWithHisFists()
        {
            (SquirrelBoyBrain squirrel, PlayerLocomotion player) = Stage();
            var health = player.GetComponent<PlayerVitals>();
            float full = health.Health;
            yield return Run(player, 120);
            Assert.AreEqual(SquirrelBoyState.Calm, squirrel.Mind.State);
            Assert.AreEqual(full, health.Health, "he hurt the player before anybody hurt him");

            squirrel.GetComponent<PlayerVitals>().TakeDamage(5.0f);
            yield return Run(player, 300);
            Assert.AreEqual(SquirrelBoyState.Fight, squirrel.Mind.State, "struck, he does not fight");
            Assert.Less(health.Health, full, "struck, he did not land a punch on the player in five seconds");
            Assert.IsNull(squirrel.GetComponentInChildren<SwordWeapon>(), "he fights with a weapon");
        }

        /// <summary>
        /// In a fight he now and then swipes with his claws (owner, 2026-11-25): the Wraith's punch on his upper body, three streaks of light
        /// behind the claws, a cut on the player in front; and a copy of him on another screen swipes and streaks too, cutting nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator InAFightHeClawsWithThreeStreaksAndACopyOfHimShowsIt()
        {
            (SquirrelBoyBrain squirrel, PlayerLocomotion player) = Stage();
            player.GetComponent<PlayerVitals>().InfiniteHealth = true;   // a long fight
            var claw = squirrel.GetComponent<ClawSwipe>();
            Assert.IsNotNull(claw, "he has no claws");
            Assert.IsNotNull(claw.SlashMaterial, "his claws have nothing to draw their streaks in");
            Assert.IsNotNull(claw.SparkMaterial, "the sparks off his claws have nothing to be drawn in");

            GameObject copyBody = Object.Instantiate(Asset("Assets/CityX/Character/SquirrelBoy.prefab"), new Vector3(30.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(copyBody);
            copyBody.GetComponent<SquirrelBoyBrain>().enabled = false;   // another screen's copy: only shown what he does
            var copyDriver = copyBody.GetComponent<PlayerAnimationDriver>();
            squirrel.GetComponent<PlayerAnimationDriver>().TimedStatePlayed += (state, speed, start) => copyDriver.PlayRemoteState(state, speed, start);
            var copyClaw = copyBody.GetComponent<ClawSwipe>();

            squirrel.GetComponent<PlayerVitals>().TakeDamage(5.0f);
            int mostStreaks = 0, mostCopyStreaks = 0;
            bool copySwiped = false;
            for (int i = 0; i < 60 * 30 && claw.Cuts == 0; i++)
            {
                yield return Run(player, 1);
                mostStreaks = Mathf.Max(mostStreaks, claw.Trail.Streaks);
                mostCopyStreaks = Mathf.Max(mostCopyStreaks, copyClaw.Trail.Streaks);
                copySwiped |= copyClaw.IsActive;
            }
            Assert.Greater(claw.Cuts, 0, "in thirty seconds of fighting he never cut the player with his claws");
            var sparkSystem = claw.Trail.GetComponentInChildren<ParticleSystem>();
            var sparks = new ParticleSystem.Particle[64];
            bool sparksChecked = false;
            for (int i = 0; i < 60 && claw.IsActive; i++)
            {
                yield return Run(player, 1);
                mostStreaks = Mathf.Max(mostStreaks, claw.Trail.Streaks);
                mostCopyStreaks = Mathf.Max(mostCopyStreaks, copyClaw.Trail.Streaks);
                int flying = sparkSystem != null ? sparkSystem.GetParticles(sparks) : 0;
                if (flying > 0 && !sparksChecked && claw.Trail.IsShowing)
                {
                    sparksChecked = true;
                    Bounds slashes = claw.Trail.Extent;
                    slashes.Expand(0.3f);
                    for (int s = 0; s < flying; s++)
                    {
                        Assert.IsTrue(slashes.Contains(sparks[s].position),
                            $"a spark at {sparks[s].position} is off the slashes ({claw.Trail.Extent.center}, size {claw.Trail.Extent.size})");
                    }
                }
            }
            Assert.IsTrue(sparksChecked, "no sparks came off the slashes");
            Assert.AreEqual(ClawTrail.Claws, mostStreaks, "the swipe did not draw three streaks");
            Assert.IsTrue(copySwiped, "the copy of him on another screen did not swipe");
            Assert.AreEqual(ClawTrail.Claws, mostCopyStreaks, "the copy's swipe did not draw three streaks");
            Assert.AreEqual(0, copyClaw.Cuts, "the copy cut too: the blow would land twice");

            yield return Run(player, (int)((ClawTrail.FadeSeconds + 0.3f) * 60));
            Assert.IsFalse(claw.Trail.IsShowing, "the streaks never faded");
        }

        [UnityTest]
        public IEnumerator BadlyHurtHeRunsToTheWallAndClimbsIt()
        {
            (SquirrelBoyBrain squirrel, PlayerLocomotion player) = Stage();
            var vitals = squirrel.GetComponent<PlayerVitals>();
            yield return Run(player, 10);
            vitals.TakeDamage(vitals.MaxHealth * 0.8f);
            float startY = squirrel.transform.position.y, highest = startY;
            bool climbed = false;
            for (int i = 0; i < 60 * 20 && !climbed; i++)
            {
                player.Tick(new LocomotionInput(), Dt);
                yield return null;
                highest = Mathf.Max(highest, squirrel.transform.position.y);
                climbed = squirrel.Mind.State == SquirrelBoyState.Perched || highest - startY > 8.0f;
            }
            Assert.Greater(highest - startY, 6.0f,
                $"badly hurt he did not climb the wall (highest {highest - startY:0.0} m up, {squirrel.Mind.State}, at {squirrel.transform.position})");
        }
    }
}
