using System.Collections;
using CityX.World;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A pedestrian in a real scene.
    ///
    /// THE AMBIENT CROWD IS GONE. CivilianPopulation used to recycle a pooled
    /// dozen around the viewer; it was deleted at the user's request, matching
    /// the Godot build's own retirement of civilian_population.gd. A pedestrian
    /// is still a working, punchable body -- these tests are what says so --
    /// but nothing places one on its own, which is what the last test here
    /// pins so the crowd cannot creep back in unnoticed.
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
        private CityWorld _world;
        private Vector3 _start;

        [SetUp]
        public void SetUp()
        {
            // The pedestrian walks the road graph of the published city (StreetNetwork reads CurrentWorld), and starts on a real
            // pavement, so the floor goes under that spot rather than at the origin.
            _world = new CityWorld(91736402);
            CurrentWorld.Instance = _world;
            _world.Plan.TryFindSpawn(out double sx, out double sz);
            Vector3 onPavement = StreetNetwork.SnapToPavement(new Vector3((float)sx + 6.0f, 0.0f, (float)sz + 6.0f));
            _start = new Vector3(onPavement.x, 0.1f, onPavement.z);

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(_start.x, -0.5f, _start.z);
            _floor.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.DestroyImmediate(_body);
            if (_floor != null) Object.DestroyImmediate(_floor);
            if (CurrentWorld.Instance == _world) CurrentWorld.Instance = null;
        }

        private Civilian MakeCivilian(int seed)
        {
            _body = new GameObject("Civilian");
            // On a pavement, so the brain's targets are somewhere sensible.
            _body.transform.position = _start;

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

        /// <summary>
        /// Mirrors civilian.gd's own _die() granting XP to whoever's attack
        /// parameter it was called with -- the killing punch, specifically,
        /// not any punch. Goes through a real MeleeAttacker rather than
        /// calling ApplyHit directly, since the grant lives in
        /// MeleeAttacker.LandBlow, not in Civilian itself.
        /// </summary>
        [UnityTest]
        public IEnumerator TheKillingBlowGrantsTheAttackerXpButAnEarlierOneDoesNot()
        {
            // NOT YIELDING BEFORE THE SWING. Civilian.Update() runs the wander
            // brain every real frame, and letting even one frame pass here
            // let it walk far enough on the very first (inflated-deltaTime)
            // frame after a heavy SetUp to fall out of range before the
            // swing ever landed. Awake() already ran synchronously inside
            // AddComponent, so nothing here actually needs a frame to settle.
            Civilian civilian = MakeCivilian(6);
            civilian.transform.position = new Vector3(0.0f, 0.1f, 1.5f);
            Physics.SyncTransforms();

            var attackerObject = new GameObject("Attacker");
            attackerObject.transform.position = Vector3.zero;
            attackerObject.transform.rotation = Quaternion.identity;
            var vitals = attackerObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            // Re-fetch: MeleeAttacker looked for vitals in its own Awake,
            // which has already run (see MeleeCombatPlayTests' identical fix).
            var attacker = attackerObject.AddComponent<MeleeAttacker>();

            // Softened, but not killed, by a hit that never goes through
            // MeleeAttacker -- proves a non-fatal hit alone is not what
            // grants XP.
            ((IDamageable)civilian).ApplyHit(Civilian.MaxHealth - 1.0f, Vector3.zero, 0.0f);
            Assert.IsTrue(civilian.IsAlive);
            Assert.AreEqual(0, vitals.Experience, "softening the target already granted XP");

            Assert.AreEqual(1, attacker.TargetsInReach().Count,
                "the civilian was not in reach before swinging");

            Assert.IsTrue(attacker.TryAttack(), "the attacker refused to swing");
            float slice = 1.0f / 60.0f;
            for (float t = 0.0f; t < attacker.SwingSeconds + 0.05f; t += slice)
            {
                attacker.Tick(slice);
            }

            Assert.IsFalse(civilian.IsAlive,
                $"the punch should have finished them off (health now {civilian.Health})");
            Assert.AreEqual(Civilian.XpRewardAmount, vitals.Experience,
                "the killing blow did not grant XP");

            Object.DestroyImmediate(attackerObject);
            yield return null;
        }

        /// <summary>
        /// NOTHING PUTS A CROWD IN THE STREETS. The ambient population was
        /// removed on purpose, and "on purpose" is the part worth pinning --
        /// a spawner is exactly the kind of thing that gets added back by
        /// someone who sees an empty city and assumes it is a bug.
        ///
        /// Checked by type rather than by counting bodies in a scene, because
        /// the thing being asserted is that no such component exists to be
        /// wired up at all.
        /// </summary>
        [Test]
        public void NothingSpawnsAnAmbientCrowd()
        {
            foreach (System.Reflection.Assembly assembly in
                     System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    Assert.AreNotEqual("CivilianPopulation", type.Name,
                        $"{type.FullName} brings the ambient crowd back -- pedestrians are only "
                        + "ever placed deliberately now");
                }
            }
        }
    }
}
