using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Fireball's look (docs/FIRE.md, "The fire spells") on the real prefab: a wake of burning gas left behind it as it flies, its core
    /// heating; its burst on a wall throwing fire, sparks and cinders and leaving a scorch facing out of the wall with molten cracks that cool
    /// and go dark -- the same from a copy of another player's fireball, which hurts nothing; no scorch on a body; and none of the fire spells'
    /// look making garbage a frame.
    /// </summary>
    public sealed class FireSpellLookPlayTests
    {
        private const string FireballPath = "Assets/CityX/Weapons/FireballProjectile.prefab";
        private static readonly int ChargeId = Shader.PropertyToID("_Charge");
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds) => HitResponse.Landed;
        }

        [TearDown]
        public void TearDown()
        {
            FireScene.Clear();
            if (FireScene.Instance != null)
            {
                Object.Destroy(FireScene.Instance.gameObject);
            }
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private FireballProjectile Launch(Vector3 from, Vector3 direction, bool cosmetic)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FireballPath);
            Assert.IsNotNull(prefab, "no FireballProjectile.prefab -- rebuild the scene");
            FireballProjectile ball = FireballProjectile.Spawn(prefab, from, direction * FireballCaster.FireballSpeed, 0.0f, null, cosmetic);
            _made.Add(ball.gameObject);
            return ball;
#else
            return null;
#endif
        }

        private GameObject Wall(Vector3 at, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = at;
            wall.transform.localScale = size;
            _made.Add(wall);
            Physics.SyncTransforms();
            return wall;
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        /// <summary>The live particles of one of the fire's shared systems (FireLook: Gas, Blast, Jet, Sparks, Cinders, Flames...).</summary>
        internal static ParticleSystem.Particle[] Live(string system)
        {
            Assert.IsNotNull(FireScene.Instance, "no fire in the world");
            Transform child = FireScene.Instance.transform.Find(system);
            Assert.IsNotNull(child, $"the fire has no {system}");
            var particles = child.GetComponent<ParticleSystem>();
            var all = new ParticleSystem.Particle[particles.main.maxParticles];
            int count = particles.GetParticles(all);
            System.Array.Resize(ref all, count);
            return all;
        }

        private static List<GroundMark> Marks(string name)
        {
            var marks = new List<GroundMark>();
            foreach (GroundMark mark in Object.FindObjectsByType<GroundMark>(FindObjectsSortMode.None))
            {
                if (mark.name == name)
                {
                    marks.Add(mark);
                }
            }
            return marks;
        }

        private static float CoreHeat(FireballProjectile ball)
        {
            var block = new MaterialPropertyBlock();
            ball.Core.GetPropertyBlock(block);
            return block.GetFloat(ChargeId);
        }

        [UnityTest]
        public IEnumerator AFireballLeavesAWakeOfBurningGasBehindItAndItsCoreHeatsAsItFlies()
        {
            Vector3 from = new Vector3(0.0f, 40.0f, 0.0f);
            FireballProjectile ball = Launch(from, Vector3.forward, false);
            Assert.IsNotNull(ball.Core, "the fireball has no molten core");
            yield return null;
            float leaving = CoreHeat(ball);
            yield return Seconds(0.3f);
            Assert.Greater(CoreHeat(ball), leaving + 0.3f, "the core did not heat as it flew");

            ParticleSystem.Particle[] wake = Live("Gas");
            Assert.Greater(wake.Length, 10, "no wake of burning gas behind the fireball");
            float ballZ = ball.transform.position.z;
            float furthestBack = ballZ;
            foreach (ParticleSystem.Particle puff in wake)
            {
                Assert.Less(puff.position.z, ballZ + 0.6f, "a puff of the wake ahead of the ball");
                Assert.Less(Mathf.Abs(puff.position.x), 2.0f, "a puff of the wake off to the side of its line");
                Assert.Less(Mathf.Abs(puff.position.y - from.y), 2.5f, "a puff of the wake far off its line");
                furthestBack = Mathf.Min(furthestBack, puff.position.z);
            }
            Assert.Less(furthestBack, ballZ - 2.0f, "the wake does not trail behind the ball");
        }

        [UnityTest]
        public IEnumerator ABurstOnAWallScorchesItWithCracksThatCoolAndACopysBurstsTheSame()
        {
            Wall(new Vector3(0.0f, 2.0f, 8.0f), new Vector3(10.0f, 4.0f, 0.5f));
            foreach (bool copy in new[] { false, true })
            {
                int before = Marks("BurstMark").Count;
                FireballProjectile ball = Launch(new Vector3(copy ? 2.0f : -2.0f, 2.0f, 0.0f), Vector3.forward, copy);
                for (int i = 0; i < 120 && ball != null; i++)
                {
                    yield return null;
                }
                Assert.IsTrue(ball == null, "the fireball never burst on the wall");
                yield return null;
                string who = copy ? "a copy's" : "the caster's";
                List<GroundMark> marks = Marks("BurstMark");
                Assert.AreEqual(before + 1, marks.Count, $"{who} burst left no scorch on the wall");
                GroundMark mark = marks[marks.Count - 1];
                Assert.Greater(Vector3.Dot(mark.transform.up, Vector3.back), 0.95f, $"{who} scorch does not lie on the wall's face");
                Assert.AreEqual(7.75f, mark.transform.position.z, 0.05f, $"{who} scorch is not on the wall");
                var cracks = mark.GetComponentInChildren<CoolingCracks>();
                Assert.IsNotNull(cracks, $"{who} scorch has no molten cracks");
                Assert.IsTrue(cracks.Glowing, $"{who} cracks are not glowing");
                Assert.Greater(Live("Blast").Length, 10, $"{who} burst threw no fire");
                Assert.Greater(Live("Sparks").Length, 5, $"{who} burst threw no sparks");
                Assert.Greater(Live("Cinders").Length, 2, $"{who} burst threw no cinders");
                if (copy)
                {
                    yield return Seconds(MoltenCracks.CoolSeconds + 0.2f);
                    Assert.IsFalse(cracks.Glowing, "the cracks never cooled");
                    Assert.IsTrue(mark != null && mark.Alpha > 0.5f, "the scorch went with the cracks' glow");
                }
            }
        }

        [UnityTest]
        public IEnumerator ABurstOnABodyInTheOpenLeavesNoScorch()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.position = new Vector3(0.0f, 30.0f, 6.0f);
            body.AddComponent<Dummy>();
            _made.Add(body);
            Physics.SyncTransforms();
            FireballProjectile ball = Launch(new Vector3(0.0f, 30.0f, 0.0f), Vector3.forward, false);
            for (int i = 0; i < 120 && ball != null; i++)
            {
                yield return null;
            }
            Assert.IsTrue(ball == null, "the fireball never burst on the body");
            yield return null;
            Assert.AreEqual(0, Marks("BurstMark").Count, "a scorch on a body, or in the air round it");
            Assert.Greater(Live("Blast").Length, 10, "the burst on a body threw no fire");
        }

        [UnityTest]
        public IEnumerator TheFireSpellsLookMakesNoGarbage()
        {
            FireScene.Ensure();
            float wakeDebt = 0.0f;
            float gatherDebt = 0.0f;
            var debts = new FireJetDebts();
            Vector3 at = new Vector3(0.0f, 40.0f, 0.0f);
            for (int warm = 0; warm < 2; warm++)
            {
                Frame(at, ref wakeDebt, ref gatherDebt, ref debts);
            }
            yield return null;
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                Frame(at, ref wakeDebt, ref gatherDebt, ref debts);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "the fire spells' look made garbage");
        }

        private static void Frame(Vector3 at, ref float wakeDebt, ref float gatherDebt, ref FireJetDebts debts)
        {
            FireScene.Wake(at, at + Vector3.forward * 0.33f, Vector3.forward, 1.0f, ref wakeDebt, 1.0f / 60.0f);
            FireScene.Jet(at, Vector3.forward, 9.0f, 4.0f, true, at + Vector3.forward * 4.0f, Vector3.back, 1.0f, 1.0f, Vector3.zero, ref debts, 1.0f / 60.0f);
            FireScene.Gather(at, 0.7f, ref gatherDebt, 1.0f / 60.0f);
            FireScene.Burst(at, Vector3.up, 1.0f);
        }
    }
}
