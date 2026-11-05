using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// How a player's body moves, on the real Player.prefab (owner, 2026-11-04: "get rid of the gta style movement", "skyrims feels like a good example we should try to imitate"):
    /// facing the camera as it moves, the keys walking it forward, sideways and back whatever is in hand, the backward walk slower, sprint forward only, and a little weight.
    /// </summary>
    public sealed class FreeMovementPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private GameObject _ground;
        private GameObject _player;
        private GameObject _weapon;
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
            if (_weapon != null) Object.Destroy(_weapon);
            if (_ground != null) Object.Destroy(_ground);
        }

#if UNITY_EDITOR
        private IEnumerator Body(string weaponPath = null)
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
                var weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
                _weapon = Object.Instantiate(weaponPrefab, new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity);
                _weapon.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);
            }
            yield return Walk(Vector2.zero, 0.2f);
        }

        private IEnumerator Walk(Vector2 keys, float seconds, float yaw = 0.0f, bool sprint = false, bool aiming = false)
        {
            var input = new LocomotionInput { Move = keys, YawDegrees = yaw, Sprint = sprint, Aiming = aiming, Sword1HEquipped = _weapon != null };
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                _locomotion.Tick(input, Dt);
                yield return null;
            }
        }

        private float Facing => Mathf.DeltaAngle(0.0f, _player.transform.eulerAngles.y);
#endif

        [UnityTest]
        public IEnumerator SWalksBackwardFacingTheCameraWhateverIsInHand([Values(null, "Assets/CityX/Weapons/CombatSword.prefab", "Assets/CityX/Weapons/SpikeSword.prefab", "Assets/CityX/Weapons/KitchenKnife.prefab", "Assets/CityX/Weapons/BaseballBat.prefab", "Assets/CityX/Weapons/MetalCrowbar.prefab")] string weapon)
        {
#if UNITY_EDITOR
            yield return Body(weapon);
            float before = _player.transform.position.z;
            yield return Walk(Vector2.down, 1.0f);
            Assert.AreEqual(0.0f, Facing, 1.0f, "S turned the body round");
            Assert.Less(_player.transform.position.z, before - 1.0f, "S did not walk the body backward");
            Assert.Less(_locomotion.LocalVelocity.y, -1.0f, "the walk is not a backward one, in the body's own terms");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator BackingUpIsSlowerThanWalkingOnAndSideStepsAreAFullWalk()
        {
#if UNITY_EDITOR
            yield return Body();
            yield return Walk(Vector2.up, 0.6f);
            Assert.AreEqual(_locomotion.WalkSpeed, _locomotion.CurrentSpeed, 0.01f, "forward is a full walk");
            yield return Walk(Vector2.down, 0.6f);
            Assert.AreEqual(_locomotion.WalkSpeed * FreeMovement.BackwardSpeedShare, _locomotion.CurrentSpeed, 0.01f, "the backward walk's speed");
            yield return Walk(Vector2.right, 0.6f);
            Assert.AreEqual(_locomotion.WalkSpeed, _locomotion.CurrentSpeed, 0.01f, "a side-step is a full walk");
            Assert.AreEqual(0.0f, Facing, 1.0f, "D turned the body round");
            Assert.Greater(_locomotion.LocalVelocity.x, 1.0f, "the walk is not a side-step, in the body's own terms");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASprintOnlyGoesForward()
        {
#if UNITY_EDITOR
            yield return Body();
            yield return Walk(Vector2.up, 0.8f, sprint: true);
            Assert.AreEqual(_locomotion.SprintSpeed, _locomotion.CurrentSpeed, 0.01f, "W with sprint held is a sprint");
            yield return Walk(Vector2.down, 0.8f, sprint: true);
            Assert.IsFalse(_locomotion.IsSprinting, "it sprinted backward");
            Assert.AreEqual(_locomotion.WalkSpeed * FreeMovement.BackwardSpeedShare, _locomotion.CurrentSpeed, 0.01f, "sprint held, S is still the backward walk");
            Assert.AreEqual(0.0f, Facing, 1.0f, "sprint held, S turned the body round");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheBodyComesRoundToTheCameraOnlyOnceItMoves()
        {
#if UNITY_EDITOR
            yield return Body();
            yield return Walk(Vector2.zero, 0.5f, yaw: 90.0f);
            Assert.AreEqual(0.0f, Facing, 1.0f, "looking round while standing still spun the body");
            yield return Walk(Vector2.up, 0.5f, yaw: 90.0f);
            Assert.AreEqual(90.0f, Facing, 1.0f, "walking did not bring the body round to the camera");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SpeedBuildsAndFallsAndAnAimStaysQuick()
        {
#if UNITY_EDITOR
            yield return Body();
            yield return Walk(Vector2.up, 0.05f);
            Assert.Less(_locomotion.CurrentSpeed, _locomotion.WalkSpeed * 0.5f, "a walk at once: no weight");
            yield return Walk(Vector2.up, 0.4f);
            Assert.AreEqual(_locomotion.WalkSpeed, _locomotion.CurrentSpeed, 0.01f, "a walk takes too long to build");

            yield return Walk(Vector2.zero, 0.05f);
            Assert.Greater(_locomotion.CurrentSpeed, 0.5f, "stopped dead: no weight");
            yield return Walk(Vector2.zero, 0.3f);
            Assert.AreEqual(0.0f, _locomotion.CurrentSpeed, 0.01f, "it slides on too long");

            // Aiming answers the keys at once: a fight with a gun has no use for weight.
            yield return Walk(Vector2.up, 0.05f, aiming: true);
            Assert.Greater(_locomotion.CurrentSpeed, _locomotion.WalkSpeed * _locomotion.AimMoveSpeedMultiplier * 0.9f, "an aim took on the weight");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
