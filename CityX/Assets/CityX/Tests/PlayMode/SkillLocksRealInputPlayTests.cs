using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE SKILLS, FROM THE REAL KEYS IN THE REAL SCENE (owner, 2026-10-14). The number keys cast whatever is in their slot of the bar, once it is
    /// learnt; Dash, Climb and Super Jump work only once learnt (vaulting always does); a brand new character has vaulting only.
    /// </summary>
    public sealed class SkillLocksRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private GameObject _wall;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_wall != null) Object.Destroy(_wall);
            foreach (IceShardProjectile shard in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None))
            {
                Object.Destroy(shard.gameObject);
            }
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                yield return null;
            }
        }

        /// <summary>Loads the city, finds its own player (not one left over from another test) and waits for it to land.</summary>
        private IEnumerator Stage(System.Action<PlayerInputReader> done)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            Assert.IsNotNull(input, "no player in the real scene");
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            yield return Wait(0.3f);
            done(input);
#else
            yield break;
#endif
        }

        /// <summary>Makes the body a brand new character's: vaulting, and the points given.</summary>
        private static PlayerSkills MakeNew(PlayerInputReader input, int points)
        {
            PlayerSkills skills = input.GetComponent<PlayerSkills>();
            Assert.IsNotNull(skills, "the player has no skills");
            skills.Load(CharacterSave.Create("New", Color.white));
            skills.SetPoints(points);
            Assert.IsFalse(skills.Knows(SkillCatalog.Climb));
            return skills;
        }

        private IEnumerator Tap(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANewCharactersKeysCastOnlyWhatIsLearntAndOnTheBar()
        {
            PlayerInputReader input = null;
            yield return Stage(i => input = i);
            PlayerSkills skills = MakeNew(input, 1);
            var rage = input.GetComponent<BloodrageAbility>();

            yield return Tap(_keyboard.digit8Key);
            Assert.IsFalse(rage.IsActive, "8 cast Bloodrage for a character who never learnt it");
            yield return Tap(_keyboard.digit1Key);
            Assert.IsFalse(rage.IsActive);

            Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.Bloodrage));
            Assert.AreEqual(0, skills.Bar.SlotOf(SkillCatalog.Bloodrage), "learnt, it went to the first empty slot");
            yield return Tap(_keyboard.digit8Key);
            Assert.IsFalse(rage.IsActive, "8 is empty: the old key means nothing now");
            yield return Tap(_keyboard.digit1Key);
            Assert.IsTrue(rage.IsActive, "1 holds Bloodrage now");
        }

        [UnityTest]
        public IEnumerator ASpellMovedToKeyZeroCastsFromThere()
        {
            PlayerInputReader input = null;
            yield return Stage(i => input = i);
            PlayerSkills skills = input.GetComponent<PlayerSkills>();
            skills.LearnEverything();
            skills.Bar.Load(SkillCatalog.DefaultBar);
            Assert.AreEqual(2, skills.Bar.SlotOf(SkillCatalog.IceShards), "setup: the ice shards on 3, as always");
            var caster = input.GetComponent<IceShardCaster>();
            Assert.IsNotNull(caster, "the player has no ice shards -- rebuild the scene");

            Assert.IsTrue(skills.PutOnBar(9, SkillCatalog.IceShards));
            yield return Tap(_keyboard.digit3Key);
            yield return Wait(0.2f);
            Assert.AreEqual(0, caster.ShotsFired, "3 no longer holds the ice shards");

            yield return Tap(_keyboard.digit0Key);
            Assert.IsTrue(caster.IsVolleying || caster.ShotsFired > 0, "0 should cast the ice shards now");
        }

        [UnityTest]
        public IEnumerator DashingNeedsTheDashSkill()
        {
            PlayerInputReader input = null;
            yield return Stage(i => input = i);
            PlayerSkills skills = MakeNew(input, 0);
            var locomotion = input.GetComponent<PlayerLocomotion>();

            yield return Tap(_keyboard.leftShiftKey);
            Press(_keyboard.leftShiftKey);
            yield return null;
            Assert.IsFalse(locomotion.IsDashing, "a double tap of Shift dashed without Dash learnt");
            Release(_keyboard.leftShiftKey);
            yield return Wait(0.5f);

            skills.SetPoints(1);
            Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.Dash));
            yield return Tap(_keyboard.leftShiftKey);
            Press(_keyboard.leftShiftKey);
            yield return null;
            Assert.IsTrue(locomotion.IsDashing, "a double tap of Shift should dash once Dash is learnt");
            Release(_keyboard.leftShiftKey);
        }

        [UnityTest]
        public IEnumerator ClimbingAWallNeedsTheClimbSkill()
        {
            PlayerInputReader input = null;
            yield return Stage(i => input = i);
            PlayerSkills skills = MakeNew(input, 0);
            var climbing = input.GetComponent<PlayerClimbing>();

            // A tall wall just ahead of where the camera looks, so W walks straight at it.
            Camera camera = Camera.main;
            Vector3 ahead = camera != null ? camera.transform.forward : input.transform.forward;
            ahead.y = 0.0f;
            ahead.Normalize();
            _wall = new GameObject("TestWall");
            _wall.transform.position = input.transform.position + ahead * 1.7f + Vector3.up * 14.0f;
            _wall.transform.rotation = Quaternion.LookRotation(ahead);
            _wall.AddComponent<BoxCollider>().size = new Vector3(14.0f, 30.0f, 1.0f);
            Physics.SyncTransforms();

            Press(_keyboard.wKey);
            yield return null;
            Press(_keyboard.spaceKey);
            yield return Wait(0.8f);
            Assert.IsFalse(climbing.IsClimbing, "held Space at a wall climbed it without Climb learnt");
            Release(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.wKey);
            yield return Wait(1.0f);

            skills.SetPoints(1);
            Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.Climb));
            Press(_keyboard.wKey);
            yield return null;
            Press(_keyboard.spaceKey);
            for (float t = 0.0f; t < 1.2f && !climbing.IsClimbing; t += Time.deltaTime)
            {
                yield return null;
            }
            Assert.IsTrue(climbing.IsClimbing, "held Space at a wall should grab it once Climb is learnt");
            Release(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.wKey);
        }

        [UnityTest]
        public IEnumerator HoldingSpaceChargesASuperJumpOnlyOnceItIsLearnt()
        {
            PlayerInputReader input = null;
            yield return Stage(i => input = i);
            PlayerSkills skills = MakeNew(input, 0);
            var locomotion = input.GetComponent<PlayerLocomotion>();
            Animator animator = input.GetComponentInChildren<Animator>();

            float plainRise = 0.0f;
            yield return HeldJump(locomotion, animator, rise => plainRise = rise, expectCharging: false);
            Assert.Greater(plainRise, 0.3f, "setup: an ordinary jump should leave the ground");
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Wait(0.5f);

            skills.SetPoints(1);
            Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.SuperJump));
            var vitals = input.GetComponent<PlayerVitals>();
            float healthBefore = vitals.Health;
            float chargedRise = 0.0f;
            yield return HeldJump(locomotion, animator, rise => chargedRise = rise, expectCharging: true);
            Debug.Log($"CityX super jump: plain jump rose {plainRise:0.00} m, a full charge {chargedRise:0.00} m");
            Assert.Greater(chargedRise, plainRise * 3.0f, "a full charge should go far higher than an ordinary jump");
            Assert.Greater(chargedRise, PlayerVitals.SafeFallDistance, "setup: the leap should rise past the safe fall for the next check to mean anything");
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Wait(0.3f);
            Assert.AreEqual(healthBefore, vitals.Health, 0.01f, "landing a super jump back on the street it left should not hurt");
        }

        /// <summary>Holds Space for longer than a full charge, lets go, and reports how far the body rose.</summary>
        private IEnumerator HeldJump(PlayerLocomotion locomotion, Animator animator, System.Action<float> rose, bool expectCharging)
        {
            float startY = locomotion.transform.position.y;
            float topY = startY;
            Press(_keyboard.spaceKey);
            bool charged = false;
            for (float t = 0.0f; t < PlayerInputReader.ChargedJumpMaxHoldSeconds + 0.2f; t += Time.deltaTime)
            {
                yield return null;
                charged |= animator.GetBool("ChargingJump");
                topY = Mathf.Max(topY, locomotion.transform.position.y);
            }
            Assert.AreEqual(expectCharging, charged, expectCharging ? "holding Space should crouch to charge" : "Space charged without Super Jump learnt");
            Release(_keyboard.spaceKey);
            for (float t = 0.0f; t < 3.5f; t += Time.deltaTime)
            {
                yield return null;
                topY = Mathf.Max(topY, locomotion.transform.position.y);
            }
            rose(topY - startY);
        }
    }
}
