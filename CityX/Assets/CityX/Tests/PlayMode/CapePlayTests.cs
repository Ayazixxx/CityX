using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The cape on the real body (docs/CAPE.md): standing, its top edge sits on the shoulders, it hangs behind and nothing of it is inside the body;
    /// sprinting, it streams out behind and lifts; a body put somewhere new takes its cape along at rest; another player's copy wears it, moving.
    /// </summary>
    public sealed class CapePlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _copy;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (GameObject made in new[] { _player, _copy, _ground })
            {
                if (made != null) Object.Destroy(made);
            }
        }

        private static GameObject Asset(string path)
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, $"{path} is missing -- rebuild the scene");
            return asset;
#else
            return null;
#endif
        }

        private (Cape cape, PlayerLocomotion body) Wear()
        {
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _player = Object.Instantiate(Asset("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            GameObject capeObject = Object.Instantiate(Asset(CapeColourways.PrefabPath(CapeColourways.RedItemId)), new Vector3(5.0f, 0.0f, 5.0f), Quaternion.identity);
            var cape = capeObject.GetComponent<Cape>();
            cape.OnEquip(_player, ItemInstanceData.None);
            return (cape, _player.GetComponent<PlayerLocomotion>());
        }

        private static IEnumerator Run(PlayerLocomotion body, LocomotionInput input, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                body.Tick(input, Dt);
                yield return null;
            }
        }

        private static int Hem => CapeShape.GridIndex(CapeShape.Columns / 2, CapeShape.Rows - 1);

        [UnityTest]
        public IEnumerator StandingItSitsOnTheShouldersAndHangsBehindOutOfTheBody()
        {
            (Cape cape, PlayerLocomotion body) = Wear();
            Assert.IsNotNull(cape.Cloth, "the cape has no cloth");
            yield return Run(body, new LocomotionInput(), 150);

            Transform root = _player.transform;
            for (int column = 0; column < CapeShape.Columns; column++)
            {
                Vector3 expected = root.TransformPoint(CapeShape.GridPoint(column, 0));
                Assert.Less(Vector3.Distance(cape.Cloth.Position[CapeShape.GridIndex(column, 0)], expected), 0.06f,
                    "the top edge is not on the shoulders standing");
            }
            Vector3 hem = root.InverseTransformPoint(cape.Cloth.Position[Hem]);
            Assert.Less(hem.y, BodyRig.HipY, "the hem does not hang below the hips");
            Assert.Less(hem.z, -0.15f, "the cape does not hang behind");
            int inside = 0;
            for (int i = 0; i < cape.Cloth.Position.Length; i++)
            {
                if (cape.Cloth.Pinned[i]) continue;   // the top edge and the knot are fastened on the body, at the shoulders and the throat
                Vector3 p = cape.Cloth.Position[i];
                foreach (CapeCloth.Capsule capsule in cape.Capsules)
                {
                    if (Vector3.Distance(CapeCloth.PushOut(p, capsule), p) > capsule.Radius * 0.35f + 0.02f) inside++;
                }
            }
            Assert.AreEqual(0, inside, "parts of the cape are deep inside the body");
            Assert.IsNotNull(cape.transform.Find("CapeCloth"), "the cloth is not drawn");
        }

        [UnityTest]
        public IEnumerator SprintingItStreamsOutBehindAndLifts()
        {
            (Cape cape, PlayerLocomotion body) = Wear();
            yield return Run(body, new LocomotionInput(), 120);
            Transform root = _player.transform;
            Vector3 standing = root.InverseTransformPoint(cape.Cloth.Position[Hem]);
            yield return Run(body, new LocomotionInput { Move = Vector2.up, Sprint = true }, 150);
            Vector3 sprinting = root.InverseTransformPoint(cape.Cloth.Position[Hem]);
            Assert.Greater(body.CurrentSpeed, 4.0f, "the body did not sprint");
            Assert.Less(sprinting.z, standing.z - 0.25f, $"sprinting, the cape does not stream out behind ({standing} then {sprinting})");
            Assert.Greater(sprinting.y, standing.y + 0.1f, "sprinting, the cape's hem does not lift");
        }

        [UnityTest]
        public IEnumerator PutSomewhereNewTheCapeComesAlongAtRest()
        {
            (Cape cape, PlayerLocomotion body) = Wear();
            yield return Run(body, new LocomotionInput(), 30);
            _player.GetComponent<CharacterController>().enabled = false;
            _player.transform.position += new Vector3(60.0f, 0.0f, 40.0f);
            _player.GetComponent<CharacterController>().enabled = true;
            yield return Run(body, new LocomotionInput(), 3);
            foreach (Vector3 p in cape.Cloth.Position)
            {
                Assert.Less(Vector3.Distance(p, _player.transform.position), 3.0f, "part of the cape was left behind (or flung) when the body was moved");
            }
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyWearsItMoving()
        {
            LogAssert.ignoreFailingMessages = true;   // a networked body made outside a session says so
            Time.captureFramerate = 60;
            _copy = Object.Instantiate(Asset("Assets/CityX/Character/NetworkedPlayer.prefab"), Vector3.zero, Quaternion.identity);
            yield return null;
            _copy.GetComponent<NetworkedEquipmentSync>().Show(new[] { CapeColourways.BlackItemId });
            yield return null;
            var cape = _copy.GetComponentInChildren<Cape>();
            Assert.IsNotNull(cape, "the copy wears no cape");
            Assert.IsNotNull(cape.Cloth, "the copy's cape is not hung on it");
            Vector3 before = cape.Cloth.Position[Hem];
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(Vector3.Distance(before, cape.Cloth.Position[Hem]), 0.001f, "the copy's cape does not move (its cloth is not running)");
        }
    }
}
