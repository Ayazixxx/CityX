using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A blow that never reached a body does not throw it. The heavy punch knocks down whom it lands on, and it threw the body down as well
    /// when an Arcane Barrier, a held guard or a raised shield had soaked the blow (all three answer Guarded).
    /// </summary>
    public sealed class HeavyPunchOnAGuardPlayTests
    {
        private const float Slice = 1.0f / 60.0f;

        private GameObject _attackerObject;
        private GameObject _ground;
        private GameObject _defender;

        private sealed class WallOfAura : IBarrier
        {
            public int Absorbed;

            public bool Covers(Vector3 fromPosition) => true;

            public void Absorb(float amount, Vector3 fromPosition)
            {
                Absorbed++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _attackerObject = new GameObject("Attacker");
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackerObject != null) Object.DestroyImmediate(_attackerObject);
            if (_defender != null) Object.Destroy(_defender);
            if (_ground != null) Object.Destroy(_ground);
        }

        private static void Step(System.Action<float> tick, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Slice)
            {
                tick(Slice);
            }
        }

        /// <summary>The heavy punch (the overhand) knocks down whom it lands on; one soaked by a barrier (or a held guard, or a shield: all answer Guarded) landed on nobody.</summary>
        [UnityTest]
        public IEnumerator AHeavyPunchTheBarrierTookDoesNotThrowTheBodyBehindIt()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            _defender = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 1.3f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            _defender.GetComponent<PlayerInputReader>().enabled = false;
            var defenderVitals = _defender.GetComponent<PlayerVitals>();
            defenderVitals.Roll = () => 1.0f;
            var ragdoll = _defender.GetComponent<Ragdoll>();
            var attacker = _attackerObject.AddComponent<MeleeAttacker>();
            yield return null;
            Physics.SyncTransforms();

            var wall = new WallOfAura();
            defenderVitals.Barrier = wall;
            Assert.IsTrue(attacker.TryAttack(heavy: true));
            Step(attacker.Tick, 1.6f);
            Assert.Greater(wall.Absorbed, 0, "setup: the heavy punch never reached the barrier");
            Assert.IsFalse(ragdoll.IsLimp, "a heavy punch the barrier took threw the body behind it to the ground");

            // And with no barrier, it is the knockdown it always was.
            defenderVitals.Barrier = null;
            Step(attacker.Tick, 3.0f);   // well past the overhand's follow-up, so the next heavy is an overhand again
            Assert.IsTrue(attacker.TryAttack(heavy: true));
            Step(attacker.Tick, 1.6f);
            Assert.IsTrue(ragdoll.IsLimp, "setup: a heavy punch that lands no longer knocks down");
#else
            yield break;
#endif
        }
    }
}
