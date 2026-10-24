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
    /// A successful TryPickup destroys the flamethrower's own GameObject,
    /// and Destroy() defers to end of frame -- same reason
    /// PistolWeaponPlayTests is PlayMode and not EditMode; every other
    /// Flamethrower behaviour (the stream's own cone/range/damage math) is
    /// covered in FlamethrowerTests (EditMode), which never needs a real
    /// frame at all.
    /// </summary>
    public sealed class FlamethrowerPlayTests
    {
        private GameObject _playerObject;
        private GameObject _flamethrowerObject;
        private GameObject _cameraObject;
        private Flamethrower _flamethrower;
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

            _flamethrowerObject = new GameObject("Flamethrower");
            _flamethrowerObject.AddComponent<SphereCollider>().isTrigger = true;
            _flamethrower = _flamethrowerObject.AddComponent<Flamethrower>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            if (_flamethrowerObject != null)
            {
                Object.DestroyImmediate(_flamethrowerObject);
            }
        }

        [UnityTest]
        public IEnumerator StandingCloseWithTheCameraOnItPicksItUp()
        {
            _flamethrowerObject.transform.position = new Vector3(0.0f, 0.0f, 1.5f);
            _cameraObject.transform.LookAt(_flamethrowerObject.transform.position);

            bool picked = _flamethrower.TryPickup(_playerObject.transform, ePressed: true);

            Assert.IsTrue(picked, "standing close with the camera on it should pick up the flamethrower");
            Assert.AreEqual(ItemRegistry.Flamethrower, _inventory.Storage[0].ItemId);

            yield return null;
            Assert.IsTrue(_flamethrowerObject == null, "a picked-up flamethrower should destroy itself");
            _flamethrowerObject = null;
        }

        [UnityTest]
        public IEnumerator TooFarAwayDoesNotPickItUp()
        {
            _flamethrowerObject.transform.position = new Vector3(0.0f, 0.0f, Flamethrower.InteractRange + 1.0f);
            _cameraObject.transform.LookAt(_flamethrowerObject.transform.position);

            bool picked = _flamethrower.TryPickup(_playerObject.transform, ePressed: true);

            Assert.IsFalse(picked);
            Assert.IsTrue(_inventory.Storage[0].IsEmpty);
            yield return null;
        }

        /// <summary>
        /// NO ANIMATOR AT ALL, deliberately -- GetBoneTransform THROWS
        /// rather than returning null on an Animator with no Avatar (HeadWearable.OnEquip guards against
        /// it), so a bare `AddComponent&lt;Animator&gt;()` here would
        /// throw instead of testing anything. Skipping the Animator
        /// entirely still exercises the wiring this test actually cares
        /// about: EquippedFlamethrower gets set/cleared regardless of
        /// whether a hand bone was ever found to parent onto.
        /// </summary>
        [UnityTest]
        public IEnumerator OnEquipWiresPlayerInputReaderEvenWithNoAnimator()
        {
            GameObject rig = new GameObject("Rig");
            var input = rig.AddComponent<PlayerInputReader>();

            Assert.DoesNotThrow(() => _flamethrower.OnEquip(rig, ItemInstanceData.None));
            Assert.AreSame(_flamethrower, input.EquippedFlamethrower,
                "equipping should wire PlayerInputReader.EquippedFlamethrower");

            _flamethrower.OnUnequip(rig);
            Assert.IsNull(input.EquippedFlamethrower, "unequipping should clear it again");

            yield return null; // OnUnequip destroys the flamethrower
            Object.DestroyImmediate(rig);
            _flamethrowerObject = null;
        }
    }
}
