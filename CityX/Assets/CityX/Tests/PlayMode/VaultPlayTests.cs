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
    }
}
