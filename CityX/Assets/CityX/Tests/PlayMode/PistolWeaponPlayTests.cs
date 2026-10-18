using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// TryPickup's own look-direction gate, driven directly with an
    /// explicit ePressed flag rather than through Keyboard.current (null
    /// outside a real Input System device -- nothing in this project's
    /// tests exercises it) or a real trigger volume. Destroy() defers to
    /// end of frame, same reason SkinnedGarmentPlayTests is PlayMode and
    /// not EditMode.
    /// </summary>
    public sealed class PistolWeaponPlayTests
    {
        private GameObject _playerObject;
        private GameObject _pistolObject;
        private GameObject _cameraObject;
        private PistolWeapon _pistol;
        private Inventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _playerObject.transform.position = Vector3.zero;
            _inventory = _playerObject.AddComponent<Inventory>();
            _playerObject.AddComponent<PlayerInputReader>();

            _cameraObject = new GameObject("MainCamera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();
            _cameraObject.transform.SetParent(_playerObject.transform, false);

            _pistolObject = new GameObject("Pistol");
            _pistolObject.AddComponent<SphereCollider>().isTrigger = true;
            _pistol = _pistolObject.AddComponent<PistolWeapon>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            if (_pistolObject != null)
            {
                Object.DestroyImmediate(_pistolObject);
            }
        }

        /// <summary>
        /// THE EXACT REPORTED SCENARIO: walk up, stop, press E. The
        /// player BODY's own forward is left facing away from the gun --
        /// PlayerLocomotion only turns the body while actually moving, so
        /// a stopped player's body can be pointing anywhere at all --
        /// while the camera, the only thing this test wires deliberately,
        /// faces the gun directly. InteractionReach.LookSource has to reach past the
        /// body to the camera for this to succeed.
        /// </summary>
        [UnityTest]
        public IEnumerator StandingStillWithBodyFacingAwayButCameraOnItPicksItUp()
        {
            _pistolObject.transform.position = new Vector3(0.0f, 0.0f, 1.5f);
            _playerObject.transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            _cameraObject.transform.LookAt(_pistolObject.transform.position);

            bool picked = _pistol.TryPickup(_playerObject.transform, ePressed: true);

            Assert.IsTrue(picked, "pressing E while the camera looks at the gun should pick it up " +
                "even though the body -- frozen from the last time it actually moved -- faces away");
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.Storage[0].ItemId);

            yield return null;
            Assert.IsTrue(_pistolObject == null, "a picked-up pistol should destroy itself");
            _pistolObject = null;
        }

        [UnityTest]
        public IEnumerator TooFarAwayDoesNotPickItUp()
        {
            _pistolObject.transform.position = new Vector3(0.0f, 0.0f, PistolWeapon.InteractRange + 1.0f);
            _cameraObject.transform.LookAt(_pistolObject.transform.position);

            bool picked = _pistol.TryPickup(_playerObject.transform, ePressed: true);

            Assert.IsFalse(picked);
            Assert.IsTrue(_inventory.Storage[0].IsEmpty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LookingAwayEvenWithTheCameraDoesNotPickItUp()
        {
            _pistolObject.transform.position = new Vector3(0.0f, 0.0f, 1.5f);
            _cameraObject.transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);

            bool picked = _pistol.TryPickup(_playerObject.transform, ePressed: true);

            Assert.IsFalse(picked);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ENotPressedDoesNothing()
        {
            _pistolObject.transform.position = new Vector3(0.0f, 0.0f, 1.5f);
            _cameraObject.transform.LookAt(_pistolObject.transform.position);

            bool picked = _pistol.TryPickup(_playerObject.transform, ePressed: false);

            Assert.IsFalse(picked);
            yield return null;
        }

        /// <summary>
        /// THE REAL PREFABS, WALKED IN VIA A REAL CharacterController.Move,
        /// NOT A HAND-BUILT STAND-IN. Every test above calls TryPickup
        /// directly, which proves the look-direction gate is right but
        /// says nothing about whether OnTriggerEnter -- the SphereCollider
        /// PistolBuilder actually ships, at the actual InteractRange-vs-
        /// WorldPickupScale radius it's built with -- ever fires against
        /// the real Player.prefab's real CharacterController in the first
        /// place. Walking the controller in over several real Move() calls
        /// (rather than teleporting inside the radius) matches how a
        /// player actually approaches one in game, and catches a trigger
        /// that only responds to arriving via movement rather than already
        /// starting inside it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpToTheRealPrefabAndStoppingLetsEPickItUp()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            GameObject pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Weapons/Pistol.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            Assert.IsNotNull(pistolPrefab, "Pistol.prefab is missing");

            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_pistolObject);

            var realPlayer = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.0f, -6.0f), Quaternion.identity);
            var realPistol = Object.Instantiate(pistolPrefab, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity)
                .GetComponent<PistolWeapon>();

            // Player.prefab itself carries no Inventory -- CityMassingSceneBuilder
            // adds one at scene-build time (with ItemPrefabs wired, pistol
            // included) rather than baking it into the prefab. Matched here
            // so this test reflects the actual shipped composition instead
            // of a bare Instantiate() that skips that wiring entirely.
            var realInventory = realPlayer.AddComponent<Inventory>();

            var camera = new GameObject("MainCamera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.SetParent(realPlayer.transform, false);

            CharacterController controller = realPlayer.GetComponent<CharacterController>();
            Assert.IsNotNull(controller, "Player.prefab has no CharacterController");

            // Walk it in, THEN keep going a little further once the trigger
            // fires -- stopping on the exact frame OnTriggerEnter arrives
            // would stop at the CONTROLLER CAPSULE's own surface, not its
            // centre, which is not how anyone actually walks up to
            // something: a real player keeps closing until they're
            // standing on it. Matches the sequence the report described.
            Vector3 towardPistol = (realPistol.transform.position - realPlayer.transform.position).normalized;
            for (int step = 0; step < 400; step++)
            {
                float remaining = Vector3.Distance(realPlayer.transform.position, realPistol.transform.position);
                if (realPistol.HasNearbyPlayer && remaining <= 1.0f)
                {
                    break;
                }
                controller.Move(towardPistol * (4.0f * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(realPistol.HasNearbyPlayer,
                "walking the real CharacterController into the real pistol's real pickup trigger " +
                "should have fired OnTriggerEnter -- if this fails, E was never going to do anything " +
                "regardless of the look-direction fix, because TickPickup bails before it ever checks E");

            camera.transform.LookAt(realPistol.transform.position);
            bool picked = realPistol.TryPickup(realPlayer.transform, ePressed: true);

            Assert.IsTrue(picked, "standing still next to the real pistol with the camera on it, " +
                "pressing E should pick it up");
            Assert.AreEqual(ItemRegistry.Pistol, realInventory.Storage[0].ItemId);

            Object.DestroyImmediate(realPlayer);
            yield return null;
#else
            Assert.Ignore("Player.prefab/Pistol.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
