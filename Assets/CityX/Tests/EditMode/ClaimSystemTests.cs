using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// /claim's target. Ported from player.gd's try_claim_building()/
    /// _building_extents(). Building detection itself is not retested here
    /// -- it reuses PlayerAudio.CurrentBuilding, already covered by
    /// PlayerAudioTests -- what matters here is the claim bookkeeping and
    /// the ground-level extents math.
    /// </summary>
    public sealed class ClaimSystemTests
    {
        private GameObject _playerObject;
        private PlayerAudio _audio;
        private ClaimSystem _claim;
        private GameObject _buildingObject;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _audio = _playerObject.AddComponent<PlayerAudio>();
            _claim = _playerObject.AddComponent<ClaimSystem>();
            _claim.Audio = _audio;

            _buildingObject = new GameObject("Building");
            _buildingObject.transform.position = new Vector3(10.0f, 15.0f, 20.0f);
            var box = _buildingObject.AddComponent<BoxCollider>();
            box.size = new Vector3(12.0f, 30.0f, 12.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_buildingObject);
        }

        [Test]
        public void ClaimingOutdoorsFailsWithTheRightMessage()
        {
            string message = null;
            _claim.BannerMessage += m => message = m;

            bool result = _claim.TryClaim();

            Assert.IsFalse(result);
            Assert.AreEqual(ClaimSystem.FailureText, message);
            Assert.IsFalse(_claim.HasClaimedBuilding);
        }

        [Test]
        public void ClaimingIndoorsSucceeds()
        {
            _audio.EnterBuilding(_buildingObject.transform);
            string message = null;
            _claim.BannerMessage += m => message = m;
            Vector3 claimedCentre = default;
            Vector3 claimedSize = default;
            _claim.BuildingClaimed += (centre, size) => { claimedCentre = centre; claimedSize = size; };

            bool result = _claim.TryClaim();

            Assert.IsTrue(result);
            Assert.AreEqual(ClaimSystem.SuccessText, message);
            Assert.IsTrue(_claim.HasClaimedBuilding);
            Assert.AreEqual(_playerObject.transform.position, _claim.ClaimedSpawnPosition);
            Assert.AreEqual(0.0f, claimedCentre.y, 0.001f, "should report the ground floor, not the building's vertical middle");
            Assert.AreEqual(new Vector3(12.0f, 30.0f, 12.0f), claimedSize);
        }

        [Test]
        public void ClaimingTheSameBuildingTwiceIsRefusedTheSecondTime()
        {
            _audio.EnterBuilding(_buildingObject.transform);
            _claim.TryClaim();

            string message = null;
            _claim.BannerMessage += m => message = m;
            bool result = _claim.TryClaim();

            Assert.IsFalse(result);
            Assert.AreEqual(ClaimSystem.AlreadyOwnedText, message);
        }

        [Test]
        public void ClaimingADifferentBuildingAfterOneAlreadyClaimedSucceeds()
        {
            _audio.EnterBuilding(_buildingObject.transform);
            _claim.TryClaim();
            _audio.ExitBuilding(_buildingObject.transform);

            var secondBuilding = new GameObject("SecondBuilding");
            secondBuilding.transform.position = new Vector3(-5.0f, 15.0f, -5.0f);
            secondBuilding.AddComponent<BoxCollider>().size = new Vector3(10.0f, 20.0f, 10.0f);
            try
            {
                _audio.EnterBuilding(secondBuilding.transform);
                bool result = _claim.TryClaim();
                Assert.IsTrue(result, "owning one building should not block claiming a second, different one");
            }
            finally
            {
                Object.DestroyImmediate(secondBuilding);
            }
        }

        [Test]
        public void ASuccessfulClaimStartsTheOrbitCutscene()
        {
            _audio.EnterBuilding(_buildingObject.transform);
            var cameraObject = new GameObject("Camera");
            _claim.CameraTransform = cameraObject.transform;
            try
            {
                _claim.TryClaim();
                Assert.IsTrue(_claim.IsInCutscene);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ClaimingWithNoCameraAssignedStillRecordsTheClaim()
        {
            _audio.EnterBuilding(_buildingObject.transform);

            bool result = _claim.TryClaim();

            Assert.IsTrue(result, "the cutscene is a bonus, not a precondition for owning the building");
            Assert.IsFalse(_claim.IsInCutscene);
        }

        [Test]
        public void ClaimingWhileDeadIsRefused()
        {
            var vitals = _playerObject.AddComponent<PlayerVitals>();
            _claim.Vitals = vitals;
            _audio.EnterBuilding(_buildingObject.transform);
            vitals.TakeDamage(vitals.MaxHealth);

            bool result = _claim.TryClaim();

            Assert.IsFalse(result);
            Assert.IsFalse(_claim.HasClaimedBuilding);
        }

        [Test]
        public void ClaimingMidCutsceneIsRefused()
        {
            _audio.EnterBuilding(_buildingObject.transform);
            var cameraObject = new GameObject("Camera");
            _claim.CameraTransform = cameraObject.transform;
            try
            {
                _claim.TryClaim();
                Assert.IsTrue(_claim.IsInCutscene);

                _audio.ExitBuilding(_buildingObject.transform);
                var otherBuilding = new GameObject("Other");
                otherBuilding.transform.position = Vector3.zero;
                otherBuilding.AddComponent<BoxCollider>().size = Vector3.one * 10.0f;
                try
                {
                    _audio.EnterBuilding(otherBuilding.transform);
                    bool result = _claim.TryClaim();
                    Assert.IsFalse(result, "a second claim should not be able to interrupt an orbit already in progress");
                }
                finally
                {
                    Object.DestroyImmediate(otherBuilding);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
