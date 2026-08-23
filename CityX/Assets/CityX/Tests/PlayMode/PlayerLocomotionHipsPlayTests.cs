using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE REPORTED "BUTT WIGGLES SIDE TO SIDE" / "TOP OF THE TORSO WADDLES"
    /// REGRESSION. character.gd's retarget() forces the hips bone's
    /// calibrated global basis to a bare identity for every walk/run/punch
    /// clip (see its own "our game's own walk/run/punch animations already
    /// follow this convention" comment) -- the raw Mixamo mocap's natural
    /// pelvis sway never reaches the skeleton in Godot at all. Unity's
    /// Humanoid retargeting has no equivalent per-clip switch, so that sway
    /// landed on the actual Hips joint here. Freezing hips alone just moved
    /// the reported wiggle up to the spine instead of removing it -- Unity's
    /// muscle-space retargeting of the same Mixamo curves onto this rig's
    /// own proportions does not carry Godot's implicit guarantee that spine
    /// sway stays small, so both bones now get the same treatment.
    ///
    /// NEEDS THE REAL Player.prefab, NOT A BARE GameObject -- there is no
    /// Hips/Spine bone, no Animator and no sprint clip to actually exhibit
    /// the sway without the genuine rig, unlike PlayerLocomotionPlayTests'
    /// own primitives-only tests (see that file's own comment on why those
    /// deliberately test the controller rather than the art).
    /// </summary>
    public sealed class PlayerLocomotionHipsPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private Transform _hips;
        private Transform _spine;

        private const float StepSeconds = 1.0f / 60.0f;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator TheHipsAndSpineNeverRotateAwayFromIdentityWhileSprinting()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            Assert.IsNotNull(_hips, "Player.prefab's Animator has no Hips bone mapped");
            Assert.IsNotNull(_spine, "Player.prefab's Animator has no Spine bone mapped");

            var input = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Sprint = true };

            // Long enough to actually get moving and well into the sprint
            // blend tree -- short of this, the animator can still be
            // blending in from idle and the assertion would prove nothing.
            for (int step = 0; step < 30; step++)
            {
                _locomotion.Tick(input, StepSeconds);
                yield return null;
            }

            // Sampled across a further two seconds of real sprinting -- long
            // enough to cross the sprint clip's own loop point more than
            // once, so a sway tied to any particular phase of the cycle
            // cannot hide between samples.
            for (int step = 0; step < 120; step++)
            {
                _locomotion.Tick(input, StepSeconds);
                yield return null;

                float hipsAngle = Quaternion.Angle(_hips.localRotation, Quaternion.identity);
                Assert.Less(hipsAngle, 0.01f,
                    $"hips rotated {hipsAngle:0.000} degrees off identity at step {step} -- " +
                    "the reported side-to-side wiggle is still there");

                float spineAngle = Quaternion.Angle(_spine.localRotation, Quaternion.identity);
                Assert.Less(spineAngle, 0.01f,
                    $"spine rotated {spineAngle:0.000} degrees off identity at step {step} -- " +
                    "the reported top-of-the-torso waddle is still there");
            }

            Object.Destroy(_player);
            _player = null;
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
