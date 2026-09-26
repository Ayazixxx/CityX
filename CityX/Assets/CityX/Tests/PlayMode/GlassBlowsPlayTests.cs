using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The things that break a window besides a fist and a bullet: a sword swung through it, and a body going
    /// through it at a sprint. Both are HEAVY blows. The glass here is a stand-in that records what hit it.
    /// </summary>
    public sealed class GlassBlowsPlayTests
    {
        private sealed class FakeGlass : MonoBehaviour, IBreakableSurface
        {
            public readonly List<BlowStrength> Blows = new List<BlowStrength>();
            public readonly List<Vector3> Directions = new List<Vector3>();

            public bool TakePunch(Vector3 point)
            {
                Blows.Add(BlowStrength.Light);
                return true;
            }

            public bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength)
            {
                Blows.Add(strength);
                Directions.Add(direction);
                return true;
            }
        }

        /// <summary>A blade that moves the way a swing does: sweeping across in front of the wielder.</summary>
        private sealed class SweepingBlade : ISwordBlade
        {
            public Vector3 Base = new Vector3(-0.6f, 1.4f, 0.4f);
            public Vector3 Tip = new Vector3(-0.6f, 1.4f, 2.2f);

            public Vector3 BladeBaseWorld => Base;
            public Vector3 BladeTipWorld => Tip;
            public float GetMeleeDamage() => 10.0f;

            public void SweepTo(float x)
            {
                Base = new Vector3(x, 1.4f, 0.4f);
                Tip = new Vector3(x, 1.4f, 2.2f);
            }
        }

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private FakeGlass Wall(Vector3 centre, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.transform.position = centre;
            wall.transform.localScale = size;
            Physics.SyncTransforms();
            return wall.AddComponent<FakeGlass>();
        }

        private SoulsSwordCombat SwordWielder(SweepingBlade blade)
        {
            var player = new GameObject("SwordPlayer");
            _made.Add(player);
            player.AddComponent<PlayerVitals>().Revive();
            var combat = player.AddComponent<SoulsSwordCombat>();
            combat.Equip(blade);
            combat.HitStopSeconds = 0.0f;
            return combat;
        }

        [UnityTest]
        public IEnumerator ASwordSweptThroughAWindowTakesItOutWithAHeavyBlow()
        {
            var blade = new SweepingBlade();
            SoulsSwordCombat combat = SwordWielder(blade);

            // A pane 1.3m ahead; the blade's line runs in front of it, and the sweep carries it across the glass.
            FakeGlass glass = Wall(new Vector3(0.0f, 1.4f, 1.3f), new Vector3(0.2f, 3.0f, 3.0f));

            combat.RequestLight(0.0f);
            // Stepped in fixed 1/60s ticks (a batch run has no frame rate to speak of): held still through the
            // wind-up, then swept sideways through the glass at 15 m/s once the blade is live.
            float x = -0.6f;
            for (int tick = 0; tick < 90; tick++)
            {
                if (combat.Phase == SoulsPhase.Active)
                {
                    x += 0.25f;
                }
                blade.SweepTo(x);
                combat.Tick(1.0f / 60.0f);
                yield return null;
            }

            Assert.IsNotEmpty(glass.Blows, "a blade swung through the glass never touched it");
            Assert.AreEqual(BlowStrength.Heavy, glass.Blows[0], "a blade is a heavy blow: the pane goes at once");
        }

        [UnityTest]
        public IEnumerator AWindUpBrushingTheGlassBreaksNothing()
        {
            var blade = new SweepingBlade();
            SoulsSwordCombat combat = SwordWielder(blade);
            FakeGlass glass = Wall(new Vector3(0.0f, 1.4f, 1.3f), new Vector3(0.2f, 3.0f, 3.0f));

            combat.RequestLight(0.0f);
            // Only the start-up (0.2s): the blade is still winding up, so it must not be a blow.
            float x = -0.6f;
            float elapsed = 0.0f;
            while (elapsed < 0.12f)
            {
                float dt = Time.deltaTime;
                x += 6.0f * dt;
                blade.SweepTo(x);
                combat.Tick(dt);
                yield return null;
                elapsed += dt;
            }

            Assert.IsEmpty(glass.Blows, "the wind-up broke the window");
        }

        private GameObject BuildPlayer()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(ground);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            _made.Add(player);
            var reader = player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            return player;
#else
            return null;
#endif
        }

        private IEnumerator Run(PlayerLocomotion locomotion, bool sprint, float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint, YawDegrees = 0.0f }, dt);
                yield return null;
                elapsed += dt;
            }
        }

        [UnityTest]
        public IEnumerator SprintingIntoAWindowGoesThroughItWithAHeavyBlow()
        {
#if UNITY_EDITOR
            GameObject player = BuildPlayer();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            FakeGlass glass = Wall(new Vector3(0.0f, 1.5f, 14.0f), new Vector3(20.0f, 4.0f, 0.4f));

            yield return Run(locomotion, sprint: true, seconds: 3.0f);

            Assert.IsNotEmpty(glass.Blows, "a sprint into the glass never broke it");
            Assert.AreEqual(BlowStrength.Heavy, glass.Blows[0]);
            Assert.Greater(glass.Directions[0].z, 0.3f, "the blow should go the way the body was going");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WalkingIntoAWindowDoesNotBreakIt()
        {
#if UNITY_EDITOR
            GameObject player = BuildPlayer();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            FakeGlass glass = Wall(new Vector3(0.0f, 1.5f, 8.0f), new Vector3(20.0f, 4.0f, 0.4f));

            yield return Run(locomotion, sprint: false, seconds: 4.0f);

            Assert.IsEmpty(glass.Blows, "walking up to a window broke it");
#else
            yield break;
#endif
        }
    }
}
