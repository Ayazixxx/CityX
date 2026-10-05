using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The reported bug, on the real rig with its Animator: climbing to a real edge mantles onto the top and ends standing there (not in the wall, with the
    /// controller back on); climbing past a gap in the wall (no real edge) never mantles and never leaves the body inside the wall.
    /// </summary>
    public sealed class LedgeMantlePlayTests
    {
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = centre;
            go.transform.localScale = size;
            _made.Add(go);
            return go;
        }

#if UNITY_EDITOR
        private PlayerClimbing Spawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            // Facing +Z, 0.7 m clear of a wall whose near face is at z = 1.0.
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.3f), Quaternion.identity);
            return _player.GetComponent<PlayerClimbing>();
        }
#endif

        private IEnumerator Climb(PlayerClimbing climbing, PlayerVitals vitals, float seconds, System.Action<float> eachStep = null)
        {
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds; t += dt)
            {
                if (climbing.IsClimbing) climbing.Tick(false, true, false, vitals, dt);
                else if (climbing.IsToppingOut) climbing.TickMantle(dt);
                else break;
                eachStep?.Invoke(t);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ClimbingToARealEdgeMantlesOntoTheTopAndStandsThereInFreeSpace()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Box(new Vector3(0.0f, 2.0f, 2.0f), new Vector3(10.0f, 4.0f, 2.0f));   // a 4 m wall, near face z = 1, top y = 4
            Physics.SyncTransforms();
            PlayerClimbing climbing = Spawn();
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return null;
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f), "setup: could not grab the wall");

            bool mantled = false;
            yield return Climb(climbing, vitals, 12.0f, t => mantled |= climbing.IsToppingOut);
            Assert.IsTrue(mantled, "it never mantled");
            Assert.IsFalse(climbing.IsToppingOut, "the mantle never finished");

            Assert.IsTrue(_player.GetComponent<CharacterController>().enabled, "the controller was left off");
            Assert.AreEqual(4.0f, _player.transform.position.y, 0.15f, "not standing on the top of the wall");
            Assert.Greater(_player.transform.position.z, 1.0f, "not over the wall's top");
            Assert.IsTrue(LedgeProbe.HasRoom(_player.transform.position, BodyRig.StandingHeight, 0.3f, _player.transform),
                "the body ended up inside geometry");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ANakedGapInTheWallIsNotAnEdgeAndNeverLeavesTheBodyInsideIt()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            // A wall with an opening between y = 2.2 and y = 4: solid below, solid above, nothing in between (a window / recess).
            Box(new Vector3(0.0f, 1.1f, 2.0f), new Vector3(10.0f, 2.2f, 2.0f));
            Box(new Vector3(0.0f, 14.0f, 2.0f), new Vector3(10.0f, 20.0f, 2.0f));   // from y = 4 up
            Physics.SyncTransforms();
            PlayerClimbing climbing = Spawn();
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return null;
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            bool mantled = false;
            yield return Climb(climbing, vitals, 8.0f, t => mantled |= climbing.IsToppingOut);
            Assert.IsFalse(mantled, "it mantled at a gap in the wall, where there is no edge");
            Assert.IsTrue(LedgeProbe.HasRoom(_player.transform.position, BodyRig.StandingHeight, 0.3f, _player.transform),
                "the body is inside the wall");
            Assert.IsTrue(_player.GetComponent<CharacterController>().enabled);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ControlDropsOffTheWallWithNoPush()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Box(new Vector3(0.0f, 15.0f, 2.0f), new Vector3(10.0f, 30.0f, 2.0f));
            Physics.SyncTransforms();
            PlayerClimbing climbing = Spawn();
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            climbing.Tick(false, false, false, vitals, 1.0f / 60.0f);
            climbing.Tick(false, false, false, vitals, 1.0f / 60.0f, dropPressed: true);
            Assert.IsFalse(climbing.IsClimbing, "Control did not let go");
            Assert.AreEqual(Vector3.zero, climbing.LastEndVelocity, "a drop must not push off the wall");
#else
            yield break;
#endif
        }
    }
}
