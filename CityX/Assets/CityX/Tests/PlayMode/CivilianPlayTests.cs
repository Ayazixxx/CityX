using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A pedestrian in a real scene.
    ///
    /// CivilianBrainTests already prove the wander logic, exhaustively and
    /// without a scene. This exists for the gap between correct logic and a
    /// body that actually moves -- which is not a hypothetical gap: the player
    /// controller once had perfect movement code and stood completely still,
    /// because CharacterController.minMoveDistance defaults to a millimetre and
    /// silently discards anything smaller. A pedestrian walks slower than the
    /// player, so it is even more exposed to that.
    /// </summary>
    public class CivilianPlayTests
    {
        private GameObject _floor;
        private GameObject _body;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _floor.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.DestroyImmediate(_body);
            if (_floor != null) Object.DestroyImmediate(_floor);
        }

        private Civilian MakeCivilian(int seed)
        {
            _body = new GameObject("Civilian");
            // On a pavement, so the brain's targets are somewhere sensible.
            _body.transform.position = new Vector3(2.0f, 0.1f, 12.0f);

            var capsule = _body.AddComponent<CharacterController>();
            capsule.height = 2.2f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0.0f, 1.1f, 0.0f);
            capsule.minMoveDistance = 0.0f;

            var civilian = _body.AddComponent<Civilian>();
            civilian.Seed = seed;
            return civilian;
        }

        [UnityTest]
        public IEnumerator APedestrianActuallyWalks()
        {
            Civilian civilian = MakeCivilian(1);
            Vector3 start = civilian.transform.position;

            // Real seconds, not frames: batch mode runs far faster than
            // realtime, so counting frames would simulate almost no time and
            // the pedestrian would still be in its opening idle.
            float elapsed = 0.0f;
            while (elapsed < 15.0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            float travelled = Vector3.Distance(
                new Vector3(start.x, 0.0f, start.z),
                new Vector3(civilian.transform.position.x, 0.0f, civilian.transform.position.z));

            Assert.Greater(travelled, 1.0f,
                $"fifteen seconds and the pedestrian moved {travelled:0.00}m -- it has the right " +
                "idea and no legs");
        }

        [UnityTest]
        public IEnumerator APedestrianStaysOnTheGroundRatherThanSinkingOrFloating()
        {
            Civilian civilian = MakeCivilian(2);

            float elapsed = 0.0f;
            while (elapsed < 6.0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.Greater(civilian.transform.position.y, -0.2f, "the pedestrian sank into the road");
            Assert.Less(civilian.transform.position.y, 0.5f, "the pedestrian is hovering");
        }

        [UnityTest]
        public IEnumerator PunchingAPedestrianHardEnoughFellsThem()
        {
            Civilian civilian = MakeCivilian(3);
            yield return null;

            Assert.IsTrue(civilian.IsAlive);

            var damageable = (IDamageable)civilian;
            damageable.ApplyHit(Civilian.MaxHealth * 0.5f, Vector3.zero, 0.5f);
            yield return null;
            Assert.IsTrue(civilian.IsAlive, "half their health should not be fatal");

            damageable.ApplyHit(Civilian.MaxHealth, Vector3.zero, 0.5f);
            yield return null;

            Assert.IsFalse(civilian.IsAlive);
            Assert.AreEqual(0.0f, civilian.Health, 0.001f, "health went negative");
        }

        [UnityTest]
        public IEnumerator AFelledPedestrianStopsWalking()
        {
            Civilian civilian = MakeCivilian(4);

            float elapsed = 0.0f;
            while (elapsed < 8.0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            ((IDamageable)civilian).ApplyHit(Civilian.MaxHealth * 2.0f, Vector3.zero, 0.5f);
            yield return null;

            Vector3 fell = civilian.transform.position;
            elapsed = 0.0f;
            while (elapsed < 3.0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            // No ragdoll on this bare test body, so the root must simply stop.
            Assert.Less(Vector3.Distance(fell, civilian.transform.position), 0.2f,
                "a dead pedestrian carried on walking");
        }
    }
}
