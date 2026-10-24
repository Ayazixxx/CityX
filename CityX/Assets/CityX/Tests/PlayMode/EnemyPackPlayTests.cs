using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Enemies fighting you together (owner, 2026-10-24: "make mobs that are hostile to you work together if they are both attacking you at
    /// once. like they are aware of eachothers existance and dont just both push you at the same time completely ignoring eachother and hitting
    /// eachother with their attacks meant for you"): two real swordsmen against a player who keeps walking in at them. They take turns (never both
    /// attacking at once, and both get a go), keep apart and spread round, and no blow of one hurts the other.
    /// </summary>
    public sealed class EnemyPackPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private readonly GameObject[] _swordsmen = new GameObject[2];

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject swordsman in _swordsmen)
            {
                if (swordsman != null) Object.Destroy(swordsman);
            }
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private SwordsmanBrain[] Build()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordsmanPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            var brains = new SwordsmanBrain[2];
            for (int i = 0; i < 2; i++)
            {
                // Side by side in front of the player, as two would come at you down a street.
                _swordsmen[i] = Object.Instantiate(swordsmanPrefab, new Vector3(i == 0 ? -0.8f : 0.8f, 0.05f, 7.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
                brains[i] = _swordsmen[i].GetComponent<SwordsmanBrain>();
                brains[i].Seed = 1000 + i;
                brains[i].Target = vitals;
                _swordsmen[i].GetComponent<PlayerVitals>().InfiniteHealth = true;
            }
            return brains;
#else
            Assert.Ignore("the prefabs can only be loaded by path inside the editor");
            return null;
#endif
        }

        private static bool Attacking(SwordsmanBrain brain) =>
            brain.State == DuelistState.Attack || brain.State == DuelistState.Punish
            || (brain.Combat != null && (brain.Combat.Phase == SoulsPhase.Startup || brain.Combat.Phase == SoulsPhase.Active));

        [UnityTest]
        public IEnumerator TwoSwordsmenTakeTurnsKeepApartAndSpreadRoundTheTarget()
        {
            SwordsmanBrain[] brains = Build();
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            int bothAtOnce = 0;
            var swung = new bool[2];
            float closest = float.MaxValue;
            float widestApart = 0.0f;
            for (float t = 0.0f; t < 25.0f; t += Dt)
            {
                // The player walks at the nearest of them for two seconds in every three, as a player closing in would (a duelist waits for
                // you to come to him), and stands the third.
                Transform nearest = Vector3.Distance(_swordsmen[0].transform.position, _player.transform.position)
                                    < Vector3.Distance(_swordsmen[1].transform.position, _player.transform.position)
                    ? _swordsmen[0].transform : _swordsmen[1].transform;
                Vector3 toward = nearest.position - _player.transform.position;
                toward.y = 0.0f;
                float yaw = Quaternion.LookRotation(toward.sqrMagnitude > 1e-4f ? toward : Vector3.forward).eulerAngles.y;
                bool walking = t % 3.0f < 2.0f && toward.magnitude > 1.5f;
                locomotion.Tick(new LocomotionInput { YawDegrees = yaw, Move = walking ? new Vector2(0.0f, 0.6f) : Vector2.zero }, Dt);
                yield return null;
                bool a = Attacking(brains[0]), b = Attacking(brains[1]);
                if (a && b)
                {
                    bothAtOnce++;
                }
                swung[0] |= a;
                swung[1] |= b;
                if (t > 4.0f)
                {
                    float apart = Vector3.Distance(_swordsmen[0].transform.position, _swordsmen[1].transform.position);
                    closest = Mathf.Min(closest, apart);
                    Vector3 toA = _swordsmen[0].transform.position - _player.transform.position;
                    Vector3 toB = _swordsmen[1].transform.position - _player.transform.position;
                    toA.y = toB.y = 0.0f;
                    widestApart = Mathf.Max(widestApart, Vector3.Angle(toA, toB));
                }
            }
            Debug.Log($"CityX pack: both attacking on {bothAtOnce} frames, swung {swung[0]}/{swung[1]}, closest {closest:0.00} m, widest {widestApart:0} deg");
            Assert.IsTrue(swung[0] && swung[1], "they did not both get a turn");
            Assert.LessOrEqual(bothAtOnce, 10, $"both attacked at once on {bothAtOnce} frames");
            Assert.Greater(closest, 0.7f, $"they came within {closest:0.00} m of each other: stacked, not spread");
            Assert.Greater(widestApart, 60.0f, $"they never got more than {widestApart:0} degrees apart round the target: no spreading");
        }

        [UnityTest]
        public IEnumerator OnesBlowOnTheOtherDoesNothing()
        {
            SwordsmanBrain[] brains = Build();
            yield return null;
            var other = _swordsmen[1].GetComponent<PlayerVitals>();
            other.InfiniteHealth = false;
            float before = other.Health;
            HitResponse response = DamageContext.Strike(other, 30.0f, _swordsmen[0].transform.position, 0.4f, DamageKind.Physical, 0.0f, null,
                _swordsmen[0]);
            Assert.AreEqual(HitResponse.Ignored, response, "a swordsman's blow landed on his packmate");
            Assert.AreEqual(before, other.Health, 1e-4f);
            Assert.IsFalse(other.IsStunned, "the packmate flinched");

            HitResponse onPlayer = DamageContext.Strike(_player.GetComponent<PlayerVitals>(), 10.0f, _swordsmen[0].transform.position, 0.2f,
                DamageKind.Physical, 0.0f, null, _swordsmen[0]);
            Assert.AreNotEqual(HitResponse.Ignored, onPlayer, "a swordsman's blow on the player was ignored");
        }

        [Test]
        public void APackmateAcrossTheLineIsInTheWay()
        {
            var shooter = new GameObject("Shooter").transform;
            var mate = new GameObject("Mate").transform;
            try
            {
                EnemyPack.Join(shooter);
                EnemyPack.Join(mate);
                shooter.position = Vector3.zero;
                mate.position = new Vector3(0.2f, 0.0f, 5.0f);
                Assert.IsTrue(EnemyPack.AllyInTheWay(shooter, Vector3.zero, new Vector3(0.0f, 0.0f, 10.0f)), "a packmate on the line was not in the way");
                mate.position = new Vector3(3.0f, 0.0f, 5.0f);
                Assert.IsFalse(EnemyPack.AllyInTheWay(shooter, Vector3.zero, new Vector3(0.0f, 0.0f, 10.0f)), "a packmate off to the side was in the way");
                mate.position = new Vector3(0.0f, 0.0f, -2.0f);
                Assert.IsFalse(EnemyPack.AllyInTheWay(shooter, Vector3.zero, new Vector3(0.0f, 0.0f, 10.0f)), "a packmate behind was in the way");
            }
            finally
            {
                EnemyPack.Leave(shooter);
                EnemyPack.Leave(mate);
                Object.DestroyImmediate(shooter.gameObject);
                Object.DestroyImmediate(mate.gameObject);
            }
        }
    }
}
