using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// With nobody to fight the enemies wander (owner, 2026-11-14: "make sure that the mobs have patrol behaviors or in other words they
    /// wander around in a natural way rather than awkwardly stand still"): the real swordsman, Shoota and Crimson Claw, each alone in a
    /// walled yard with no player in the world, stroll and stand by turns, turn before they go, and stay inside the walls. (The Wraith
    /// drifts by its own rules: WraithPlayTests.)
    /// </summary>
    public sealed class MobWanderPlayTests
    {
        private const float HalfYard = 9.0f;

        private GameObject _yard;
        private GameObject _body;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_body != null) Object.Destroy(_body);
            if (_yard != null) Object.Destroy(_yard);
        }

#if UNITY_EDITOR
        private void BuildYard()
        {
            _yard = new GameObject("Yard");
            void Slab(Vector3 at, Vector3 size)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.transform.SetParent(_yard.transform, false);
                slab.transform.position = at;
                slab.transform.localScale = size;
            }
            Slab(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(HalfYard * 2.0f + 4.0f, 1.0f, HalfYard * 2.0f + 4.0f));
            Slab(new Vector3(HalfYard + 0.5f, 3.0f, 0.0f), new Vector3(1.0f, 6.0f, HalfYard * 2.0f + 2.0f));
            Slab(new Vector3(-HalfYard - 0.5f, 3.0f, 0.0f), new Vector3(1.0f, 6.0f, HalfYard * 2.0f + 2.0f));
            Slab(new Vector3(0.0f, 3.0f, HalfYard + 0.5f), new Vector3(HalfYard * 2.0f + 2.0f, 6.0f, 1.0f));
            Slab(new Vector3(0.0f, 3.0f, -HalfYard - 0.5f), new Vector3(HalfYard * 2.0f + 2.0f, 6.0f, 1.0f));
        }
#endif

        [UnityTest]
        public IEnumerator WithNobodyToFightTheSwordsmanTheShootaAndTheCrimsonClawWanderInsteadOfStandingStill()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            BuildYard();
            Assert.IsNull(Object.FindFirstObjectByType<PlayerInputReader>(), "there is a player in the world: the enemies would fight it");
            foreach ((string name, float height) in new[] { ("Swordsman", 0.05f), ("Shoota", 0.05f), ("CrimsonClaw", CrimsonClaw.HoverHeightAboveGround) })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Character/{name}.prefab");
                Assert.IsNotNull(prefab, $"{name}.prefab is missing -- rebuild the scene");
                _body = Object.Instantiate(prefab, new Vector3(0.0f, height, 0.0f), Quaternion.identity);
                for (int i = 0; i < 30; i++) yield return null;   // onto the ground

                Vector3 last = _body.transform.position;
                float lastYaw = _body.transform.eulerAngles.y;
                float walked = 0.0f, still = 0.0f, longestStill = 0.0f, mostTurned = 0.0f, strolling = 0.0f;
                const float Seconds = 60.0f;
                for (float t = 0.0f; t < Seconds; t += Time.deltaTime)
                {
                    yield return null;
                    Vector3 now = _body.transform.position;
                    float moved = new Vector2(now.x - last.x, now.z - last.z).magnitude;
                    walked += moved;
                    float speed = moved / Mathf.Max(Time.deltaTime, 1e-5f);
                    still = speed < 0.15f ? still + Time.deltaTime : 0.0f;
                    longestStill = Mathf.Max(longestStill, still);
                    if (speed > 0.5f) strolling += Time.deltaTime;
                    float yaw = _body.transform.eulerAngles.y;
                    mostTurned = Mathf.Max(mostTurned, Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / Mathf.Max(Time.deltaTime, 1e-5f));
                    last = now;
                    lastYaw = yaw;
                    Assert.Less(Mathf.Max(Mathf.Abs(now.x), Mathf.Abs(now.z)), HalfYard + 0.1f, $"the {name} went through the yard's wall at {t:0.0} s");
                    Assert.Greater(now.y, -0.3f, $"the {name} fell through the ground");
                }
                Debug.Log($"CityX wandering, {name}: {walked:0.0} m in {Seconds:0} s, strolling {strolling:0.0} s of it, stood at most {longestStill:0.0} s, turned at most {mostTurned:0} deg/s");
                Assert.Greater(walked, 12.0f, $"the {name} hardly moved in a minute with nobody to fight: it stands about");
                Assert.Greater(longestStill, 1.0f, $"the {name} never stood a moment: it marches round");
                Assert.Less(longestStill, NpcWander.PauseSecondsMax + 6.0f, $"the {name} stood {longestStill:0.0} s: it stands about");
                Assert.Less(mostTurned, NpcWander.TurnDegreesPerSecond * 1.6f, $"the {name} snapped round");
                Object.Destroy(_body);
                _body = null;
                yield return null;
            }
#else
            yield break;
#endif
        }
    }
}
