using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "RAISE THE GUN WITH HIS RIGHT HAND" -- pistol_aim_in.fbx was already
    /// sitting retargeted and unused in the Animations folder (see
    /// UpdateHeldPose's own pre-existing "no dedicated reload mocap clip
    /// exists (unlike shoot/aim-in/locomotion, all real retargeted Mixamo
    /// clips)" comment, which named it before anything actually used it).
    /// PlayerRigBuilder.AddAimingState now wires it to a real Animator
    /// state gated on a bool, fed from LocomotionInput.Aiming, itself fed
    /// from PlayerInputReader's own gun-aiming right-click state.
    ///
    /// NEEDS THE REAL Player.prefab -- see PlayerLocomotionHipsPlayTests's
    /// own comment on why; there is no "Aiming" animator state on a bare
    /// GameObject with no Animator at all.
    /// </summary>
    public sealed class PlayerAimingPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

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
        public IEnumerator HoldingAimEntersTheAimingStateAndReleasingLeavesIt()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            var notAiming = new LocomotionInput { Move = Vector2.zero, Aiming = false };
            var aiming = new LocomotionInput { Move = Vector2.zero, Aiming = true };

            for (int step = 0; step < 5; step++)
            {
                _locomotion.Tick(notAiming, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "should not start in the aiming pose");

            // MANY frames, not a handful -- the Animator's own transition
            // blend advances on real Time.deltaTime, not the fixed dt this
            // test hands to Tick() for its own movement math, and batch
            // mode runs uncapped at roughly 2500fps (see
            // PlayerLocomotionPlayTests's own comment on the identical
            // gotcha) -- a short loop finishes while barely any real
            // transition time has passed.
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(aiming, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "holding aim should raise the gun into the Aiming state");

            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(notAiming, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "releasing aim should leave the Aiming state");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        /// <summary>
        /// Bug report: "the legs move as you hold the pistol and
        /// aim in if you are walking [in Godot] which it doesnt do in
        /// unity." Before PlayerRigBuilder.AddGunLocomotion existed, the
        /// "Aiming" Any State transition fired on Aiming alone (see
        /// AddAimingState's own doc comment) -- holding right-click while
        /// also walking forward left the Animator stuck on the single
        /// static standing pistol_aim_in pose, legs included, while
        /// PlayerLocomotion.Tick kept sliding the transform forward
        /// underneath it. GunLocomotion (gated on GunEquipped and Speed,
        /// not Aiming) is what should show instead once genuinely moving,
        /// matching character.gd's own set_walking(), which shows a full
        /// gun_walk/gun_run cycle any time the gun is held and the
        /// character is moving, aiming or not.
        /// </summary>
        [UnityTest]
        public IEnumerator AimingWhileWalkingWithAGunEntersGunLocomotionNotTheStaticAimPose()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            var aimingAndWalking = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = 0.0f,
                Aiming = true,
                GunEquipped = true,
            };

            // MANY frames -- see HoldingAimEntersTheAimingStateAndReleasingIt's
            // own comment on why real Animator transition blending needs
            // far more iterations than PlayerLocomotion.Tick's own math does.
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(aimingAndWalking, 1.0f / 60.0f);
                yield return null;
            }

            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("GunLocomotion"),
                "aiming while walking with a gun equipped should show the gun-walk cycle, not freeze on the static aim pose");
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "should not still be stuck on the static standing aim pose while genuinely moving");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        /// <summary>
        /// Bug report: "the head is still being angled to the left
        /// slightly when aiming in just have it face forward and move with
        /// the mouse obviously." pistol_aim_in.fbx's own retargeted mocap
        /// bakes a realistic sighting head-cant into the head bone's LOCAL
        /// rotation, which the body's own instant snap-to-camera (see
        /// PlayerLocomotionTests' own AimingSnapsInstantlyRatherThanEasing
        /// LikeAnOrdinaryWalkingTurn) does nothing to correct -- a local
        /// bone rotation is relative to its parent, untouched by the root
        /// turning underneath it. ApplyGunAimPose now corrects the head the
        /// same way it already corrects the right upper arm, reusing the
        /// SAME clamped, camera-converged targetDirection for both -- see
        /// its own doc comment.
        /// </summary>
        [UnityTest]
        public IEnumerator AimingTurnsTheHeadToFaceTheSameDirectionAsTheCamera()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.IsNotNull(head, "Player.prefab's Animator has no Head bone mapped");

            // A ROTATED yaw, not 0 -- confirms this is real camera-tracking,
            // not just a coincidence of the character's own spawn facing
            // already lining up with a leftover baked head pose.
            const float yawDegrees = 25.0f;
            Quaternion cameraRotation = Quaternion.Euler(0.0f, yawDegrees, 0.0f);
            Vector3 cameraForward = cameraRotation * Vector3.forward;
            Vector3 cameraPosition = _player.transform.position +
                Vector3.up * 1.6f - cameraForward * 4.5f + cameraRotation * Vector3.right * 0.65f;

            var aiming = new LocomotionInput
            {
                Move = Vector2.zero,
                YawDegrees = yawDegrees,
                Aiming = true,
                AimCameraPosition = cameraPosition,
                AimCameraForward = cameraForward,
            };

            // A modest loop, not thousands of frames -- unlike the Animator
            // STATE transition tests above, ApplyGunAimPose's own blend
            // (_gunAimBlend) eases against the FIXED dt handed to Tick, not
            // real Time.deltaTime, so it fully reaches strength in about
            // 1/GunAimBlendPerSecond real seconds of simulated time
            // regardless of how fast this loop actually runs.
            for (int step = 0; step < 30; step++)
            {
                _locomotion.Tick(aiming, 1.0f / 60.0f);
                yield return null;
            }

            float angle = Vector3.Angle(head.forward, cameraForward);
            Assert.Less(angle, 10.0f,
                $"the head should face the same direction as the camera while aiming, not the clip's own baked cant -- off by {angle:0.0} degrees");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        /// <summary>
        /// FOLLOW-UP Bug report, after the aim-only fix above
        /// shipped: "the walking animation and his head is still angled to
        /// the left slightly why is it doing this... there was no issue in
        /// godot." The fix above only ever corrects the head while
        /// _gunAimBlend is positive -- i.e. while right-click is actually
        /// held. Ordinary hip-fire movement with the gun equipped
        /// (Aiming=false, GunEquipped=true, which is what the new
        /// GunLocomotion state shows -- see PlayerRigBuilder.
        /// AddGunLocomotion) never triggered it at all, so pistol_walk.fbx's
        /// own baked head cant showed straight through. ApplyGunWalkHeadPose
        /// now corrects the head toward transform.forward (not a
        /// camera-converged point -- there is no aim lock during hip-fire,
        /// matching Godot's own gun_walk, which never raises the gun to eye
        /// level either) whenever GunEquipped is true and Aiming is false.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingWithAGunEquippedButNotAimingStillFacesTheHeadForward()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            // A ROTATED spawn facing, not the default identity -- confirms
            // this is a real correction tracking the body's own current
            // facing, not a coincidence of spawning already aligned with
            // whatever the clip bakes in.
            const float yawDegrees = 25.0f;
            Quaternion spawnRotation = Quaternion.Euler(0.0f, yawDegrees, 0.0f);
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), spawnRotation);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.IsNotNull(head, "Player.prefab's Animator has no Head bone mapped");

            var walkingWithGun = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = yawDegrees,
                Aiming = false,
                GunEquipped = true,
            };

            for (int step = 0; step < 30; step++)
            {
                _locomotion.Tick(walkingWithGun, 1.0f / 60.0f);
                yield return null;
            }

            float angle = Vector3.Angle(head.forward, _player.transform.forward);
            Assert.Less(angle, 10.0f,
                $"the head should face straight ahead while walking with the gun drawn but not aiming, " +
                $"not the clip's own baked cant -- off by {angle:0.0} degrees");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
