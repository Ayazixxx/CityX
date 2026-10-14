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
    /// <summary>The vault on the real rig: running at a low wall takes the player over it and lands them on the far side, controller back on, in free space.</summary>
    public sealed class VaultPlayTests
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
        private PlayerVaulting Spawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.2f), Quaternion.identity);
            PlayerVaulting vaulting = _player.GetComponent<PlayerVaulting>();
            return vaulting != null ? vaulting : _player.AddComponent<PlayerVaulting>();
        }
#endif

        [UnityTest]
        public IEnumerator RunningAtALowWallVaultsOverItAndLandsOnTheFarSide()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Box(new Vector3(0.0f, 0.7f, 1.2f), new Vector3(10.0f, 1.4f, 0.4f));   // a 1.4 m fence, near face z = 1.0, far face z = 1.4
            Physics.SyncTransforms();
            PlayerVaulting vaulting = Spawn();
            yield return null;
            yield return null;

            Assert.IsTrue(vaulting.TryStart(true, true, true, 5.0f, 1.0f / 60.0f), "setup: the wall was not vaultable");
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < 2.0f && vaulting.IsVaulting; t += dt)
            {
                vaulting.Tick(dt);
                yield return null;
            }
            Assert.IsFalse(vaulting.IsVaulting, "the vault never finished");
            Assert.IsTrue(_player.GetComponent<CharacterController>().enabled, "the controller was left off");
            Assert.Greater(_player.transform.position.z, 1.4f, "not on the far side of the wall");
            Assert.AreEqual(0.0f, _player.transform.position.y, 0.15f, "not on the ground");
            Assert.IsTrue(LedgeProbe.HasRoom(_player.transform.position, BodyRig.StandingHeight, 0.3f, _player.transform), "landed inside geometry");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WalkingOrStandingStillNeverVaults()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Box(new Vector3(0.0f, 0.7f, 1.2f), new Vector3(10.0f, 1.4f, 0.4f));
            Physics.SyncTransforms();
            PlayerVaulting vaulting = Spawn();
            yield return null;
            Assert.IsFalse(vaulting.TryStart(true, false, true, 5.0f, 0.016f), "vaulted without running");
            Assert.IsFalse(vaulting.TryStart(false, true, true, 5.0f, 0.016f), "vaulted without moving forward");
            Assert.IsFalse(vaulting.TryStart(true, true, false, 5.0f, 0.016f), "vaulted in mid-air");
#else
            yield break;
#endif
        }

        /// <summary>
        /// A window too high to vault from the street (sill 3 m up, a room behind it with its floor 2.1 m up) in a 0.3 m wall, near face z = 1.0, with
        /// jambs either side of a 1.2 m opening; with <paramref name="glass"/>, the pane whole.
        /// </summary>
        private void HighWindow(bool glass)
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Box(new Vector3(0.0f, 1.5f, 1.15f), new Vector3(12.0f, 3.0f, 0.3f));                // under the sill
            Box(new Vector3(0.0f, 4.86f + 5.0f, 1.15f), new Vector3(12.0f, 10.0f, 0.3f));       // over the lintel
            Box(new Vector3(-3.3f, 3.93f, 1.15f), new Vector3(5.4f, 1.86f, 0.3f));              // the jambs
            Box(new Vector3(3.3f, 3.93f, 1.15f), new Vector3(5.4f, 1.86f, 0.3f));
            Box(new Vector3(0.0f, 1.05f, 6.3f), new Vector3(12.0f, 2.1f, 10.0f));               // the room's floor, 2.1 m up
            if (glass)
            {
                Box(new Vector3(0.0f, 3.93f, 1.14f), new Vector3(1.2f, 1.86f, 0.02f));
            }
            Physics.SyncTransforms();
        }

        /// <summary>Climbs (W held) for up to <paramref name="seconds"/>, the way the input reader does: a vault owns the frame once it starts.</summary>
        private static IEnumerator ClimbUp(PlayerClimbing climbing, PlayerVaulting vaulting, PlayerVitals vitals, float seconds, System.Action each = null)
        {
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds; t += dt)
            {
                if (vaulting.IsVaulting) vaulting.Tick(dt);
                else if (climbing.IsClimbing) climbing.Tick(false, true, false, vitals, dt);
                else if (climbing.IsToppingOut) climbing.TickMantle(dt);
                else break;
                each?.Invoke();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ClimbingUpToABrokenWindowTooHighToVaultDivesInThroughIt()
        {
#if UNITY_EDITOR
            // Owner, 2026-10-14: "the vaulting through window function isnt working on most windows". A window this high is climbed to, then dived through.
            HighWindow(glass: false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.2f, 0.05f, 0.3f), Quaternion.identity);
            _made.Add(player);
            var climbing = player.GetComponent<PlayerClimbing>();
            var vaulting = player.GetComponent<PlayerVaulting>();
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsFalse(VaultProbe.TryFind(player.transform.position, Vector3.forward, BodyRig.StandingHeight, 0.3f, player.transform, out _),
                "setup: the window should be out of a vault's reach from the street");
            yield return null;
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f), "setup: could not grab the wall");
            bool dived = false;
            yield return ClimbUp(climbing, vaulting, vitals, 12.0f, () => dived |= vaulting.IsVaulting);
            Assert.IsTrue(dived, "climbing up to a broken window never dived through it");
            Assert.IsFalse(vaulting.IsVaulting || climbing.IsClimbing, "it never finished");
            Vector3 p = player.transform.position;
            Assert.Greater(p.z, 1.6f, "the body did not end up inside the room");
            Assert.AreEqual(2.1f, p.y, 0.15f, "not standing on the room's floor");
            Assert.IsTrue(player.GetComponent<CharacterController>().enabled, "the controller was left off");
            Assert.IsTrue(LedgeProbe.HasRoom(p, BodyRig.StandingHeight, 0.3f, player.transform), "the body ended up inside geometry");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ClimbingPastAWholeWindowNeverDivesThroughTheGlass()
        {
#if UNITY_EDITOR
            HighWindow(glass: true);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.2f, 0.05f, 0.3f), Quaternion.identity);
            _made.Add(player);
            var climbing = player.GetComponent<PlayerClimbing>();
            var vaulting = player.GetComponent<PlayerVaulting>();
            var vitals = player.GetComponent<PlayerVitals>();
            yield return null;
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f), "setup: could not grab the wall");
            bool dived = false;
            yield return ClimbUp(climbing, vaulting, vitals, 8.0f, () => dived |= vaulting.IsVaulting);
            Assert.IsFalse(dived, "dived through a WHOLE pane of glass");
            Assert.Less(player.transform.position.z, 1.0f, "the body went through the wall");
#else
            yield break;
#endif
        }
    }
}
