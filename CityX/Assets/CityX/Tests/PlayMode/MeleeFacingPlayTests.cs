using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Moving in a melee fight (owner, 2026-11-04: "if you dont [lock on] and you press S to move backward it does a whole rotating walking animation instead of just walking
    /// backwards which is not what you want in a melee fight"): ready to fight, the keys back-step and side-step with no lock-on; a sprint still turns and runs; and with nothing
    /// to fight with the body turns to where it walks, GTA style, as ever.
    /// </summary>
    public sealed class MeleeFacingPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float CameraYaw = 0.0f;

        private GameObject _ground;
        private GameObject _player;
        private GameObject _sword;
        private PlayerLocomotion _locomotion;
        private int _capturedFramerate;

        [SetUp]
        public void SetUp()
        {
            _capturedFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = _capturedFramerate;
            if (_player != null) Object.Destroy(_player);
            if (_sword != null) Object.Destroy(_sword);
            if (_ground != null) Object.Destroy(_ground);
        }

#if UNITY_EDITOR
        private IEnumerator Body(string weaponPath)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            if (weaponPath != null)
            {
                var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
                _sword = Object.Instantiate(swordPrefab, new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity);
                _sword.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);
            }
            yield return Walk(Vector2.zero, false, 0.2f);
        }

        private IEnumerator Walk(Vector2 keys, bool sprint, float seconds)
        {
            var input = new LocomotionInput { Move = keys, YawDegrees = CameraYaw, Sprint = sprint, Sword1HEquipped = _sword != null };
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                _locomotion.Tick(input, Dt);
                yield return null;
            }
        }

        private float TurnedFromTheCamera => Mathf.Abs(Mathf.DeltaAngle(_player.transform.eulerAngles.y, CameraYaw));
#endif

        [UnityTest]
        public IEnumerator WithASwordInHandSWalksBackwardFacingTheCamera()
        {
#if UNITY_EDITOR
            yield return Body("Assets/CityX/Weapons/CombatSword.prefab");
            float before = _player.transform.position.z;
            yield return Walk(new Vector2(0.0f, -1.0f), false, 1.0f);
            Assert.Less(TurnedFromTheCamera, 1.0f, "S turned the body round");
            Assert.Less(_player.transform.position.z, before - 1.0f, "S did not walk the body backward");
            Assert.Less(_locomotion.LocalVelocity.y, -1.0f, "the walk is not a backward one, in the body's own terms");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WithASwordInHandTheSideKeysSideStep()
        {
#if UNITY_EDITOR
            yield return Body("Assets/CityX/Weapons/CombatSword.prefab");
            yield return Walk(new Vector2(1.0f, 0.0f), false, 0.6f);
            Assert.Less(TurnedFromTheCamera, 1.0f, "D turned the body round");
            Assert.Greater(_locomotion.LocalVelocity.x, 1.0f, "the walk is not a side-step, in the body's own terms");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator EveryMeleeWeaponIsReadyToFight([Values("Assets/CityX/Weapons/SpikeSword.prefab", "Assets/CityX/Weapons/KitchenKnife.prefab", "Assets/CityX/Weapons/BaseballBat.prefab", "Assets/CityX/Weapons/MetalCrowbar.prefab")] string weapon)
        {
#if UNITY_EDITOR
            yield return Body(weapon);
            yield return Walk(new Vector2(0.0f, -1.0f), false, 0.6f);
            Assert.Less(TurnedFromTheCamera, 1.0f, $"S turned the body round with {weapon} in hand");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASprintStillTurnsAndRunsWithASwordOut()
        {
#if UNITY_EDITOR
            yield return Body("Assets/CityX/Weapons/CombatSword.prefab");
            yield return Walk(new Vector2(0.0f, -1.0f), true, 1.0f);
            Assert.Greater(TurnedFromTheCamera, 150.0f, "sprinting away did not turn the body round");
            Assert.IsTrue(_locomotion.IsSprinting, "sprinting away is not a sprint");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WithNothingToFightWithSStillTurnsRound()
        {
#if UNITY_EDITOR
            yield return Body(null);
            Assert.IsFalse(_locomotion.FistPoseActive);
            yield return Walk(new Vector2(0.0f, -1.0f), false, 1.0f);
            Assert.Greater(TurnedFromTheCamera, 150.0f, "GTA style is gone from an empty-handed walk");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheFistsAreReadyOnlyWhileTheyAreUp()
        {
#if UNITY_EDITOR
            yield return Body(null);
            _player.GetComponent<PlayerAnimationDriver>().PlayLocalState(FistStrikes.RightStraightName);
            yield return Walk(Vector2.zero, false, 0.1f);
            Assert.IsTrue(_locomotion.FistPoseActive, "the punch did not raise the fists");
            yield return Walk(new Vector2(0.0f, -1.0f), false, 0.4f);
            Assert.Less(TurnedFromTheCamera, 1.0f, "S turned the body round straight after a punch");

            for (float t = 0.0f; _locomotion.FistPoseActive; t += Dt)
            {
                Assert.Less(t, 6.0f, "the fists never came down");
                yield return Walk(Vector2.zero, false, Dt);
            }
            yield return Walk(new Vector2(0.0f, -1.0f), false, 1.0f);
            Assert.Greater(TurnedFromTheCamera, 150.0f, "with the fists down the walk did not go back to GTA style");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
