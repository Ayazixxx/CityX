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
    /// The cape on the real body (docs/CAPE.md), as it is drawn (its skinned mesh, baked): standing, its top edge sits on the shoulders, it hangs
    /// behind and nothing of it is inside the body; sprinting, it lifts out behind; jumping, it flies up; over a shirt nothing of the shirt comes
    /// through; a body put somewhere new takes its cape along; another player's copy wears it, moving.
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
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
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

        /// <summary>The cape as it is drawn now, in the world: each point's outside.</summary>
        private static Vector3[] Drawn(Cape cape)
        {
            var baked = new Mesh();
            cape.Look.BakeMesh(baked, true);
            Matrix4x4 toWorld = Matrix4x4.TRS(cape.Look.transform.position, cape.Look.transform.rotation, Vector3.one);
            Vector3[] vertices = baked.vertices;
            Object.Destroy(baked);
            var points = new Vector3[CapeShape.Points];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            }
            return points;
        }

        [UnityTest]
        public IEnumerator StandingItSitsOnTheShouldersAndHangsBehindOutOfTheBody()
        {
            (Cape cape, PlayerLocomotion body) = Wear();
            Assert.IsNotNull(cape.Look, "the cape is not drawn");
            yield return Run(body, new LocomotionInput(), 150);

            Transform root = _player.transform;
            Vector3[] drawn = Drawn(cape);
            for (int column = 0; column < CapeShape.Columns; column++)
            {
                Vector3 expected = root.TransformPoint(cape.Rest[CapeShape.GridIndex(column, 0)]);
                Assert.Less(Vector3.Distance(drawn[CapeShape.GridIndex(column, 0)], expected), 0.06f, "the top edge is not on the shoulders standing");
            }
            Vector3 hem = root.InverseTransformPoint(drawn[Hem]);
            Assert.Less(hem.y, BodyRig.HipY, "the hem does not hang below the hips");
            Assert.Less(hem.z, -0.15f, "the cape does not hang behind");
            // Nothing of the hanging part deep in the torso (a capsule from the hips to the neck).
            Animator animator = _player.GetComponentInChildren<Animator>();
            Vector3 a = animator.GetBoneTransform(HumanBodyBones.Hips).position, b = animator.GetBoneTransform(HumanBodyBones.Neck).position;
            int inside = 0;
            for (int i = CapeShape.GridIndex(0, CapeShape.YokeRows); i < CapeShape.GridPoints; i++)
            {
                Vector3 p = drawn[i];
                float t = Mathf.Clamp01(Vector3.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                if (Vector3.Distance(p, a + (b - a) * t) < FusedBodyShape.TorsoRadius * 0.65f) inside++;
            }
            Assert.AreEqual(0, inside, "parts of the cape are deep inside the body");
        }

        /// <summary>
        /// Over a worn shirt (owner, 2026-11-25: "the shoulders on the cape still clip through and i saw some of the lower back as well"), put
        /// on before the cape or after it: standing, nothing of the shirt comes out through the cape's yoke, collar or back.
        /// </summary>
        [UnityTest]
        public IEnumerator OverAShirtNothingOfItComesThrough([Values(true, false)] bool shirtFirst)
        {
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _player = Object.Instantiate(Asset("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var shirt = Object.Instantiate(Asset("Assets/CityX/Wearables/TShirt.prefab"), new Vector3(5.0f, 0.0f, 5.0f), Quaternion.identity).GetComponent<SkinnedGarment>();
            var cape = Object.Instantiate(Asset(CapeColourways.PrefabPath(CapeColourways.RedItemId)), new Vector3(6.0f, 0.0f, 6.0f), Quaternion.identity).GetComponent<Cape>();
            if (shirtFirst) shirt.OnEquip(_player, ItemInstanceData.None);
            cape.OnEquip(_player, ItemInstanceData.None);
            if (!shirtFirst) shirt.OnEquip(_player, ItemInstanceData.None);
            var body = _player.GetComponent<PlayerLocomotion>();
            yield return Run(body, new LocomotionInput(), 150);

            SkinnedMeshRenderer worn = _player.GetComponent<SkinnedGarmentCombiner>().GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            worn.BakeMesh(baked, true);
            Matrix4x4 toWorld = Matrix4x4.TRS(worn.transform.position, worn.transform.rotation, Vector3.one);
            Vector3[] shirtPoints = baked.vertices, shirtFacing = baked.normals;
            for (int j = 0; j < shirtPoints.Length; j++)
            {
                shirtPoints[j] = toWorld.MultiplyPoint3x4(shirtPoints[j]);
                shirtFacing[j] = toWorld.MultiplyVector(shirtFacing[j]).normalized;
            }
            Object.Destroy(baked);
            int through = 0;
            var where = new System.Collections.Generic.List<string>();
            float hips = _player.transform.position.y + BodyRig.HipY;
            Vector3[] drawn = Drawn(cape);
            for (int i = 0; i < drawn.Length; i++)
            {
                Vector3 p = drawn[i];
                if (p.y < hips) continue;   // below the shirt
                float best = float.MaxValue;
                int k = -1;
                for (int j = 0; j < shirtPoints.Length; j++)
                {
                    float d = (shirtPoints[j] - p).sqrMagnitude;
                    if (d < best) { best = d; k = j; }
                }
                if (Mathf.Sqrt(best) > 0.05f) continue;   // the shirt is not just under this point
                if (Vector3.Dot(p - shirtPoints[k], shirtFacing[k]) < -0.003f)
                {
                    through++;
                    if (where.Count < 6) where.Add($"{_player.transform.InverseTransformPoint(p):F3}");
                }
            }
            Assert.AreEqual(0, through, $"the shirt comes out through the cape at {string.Join(", ", where)}");
            Object.Destroy(shirt.gameObject);
        }

        [UnityTest]
        public IEnumerator SprintingItLiftsOutBehind()
        {
            (Cape cape, PlayerLocomotion body) = Wear();
            yield return Run(body, new LocomotionInput(), 120);
            Transform root = _player.transform;
            Vector3 standing = root.InverseTransformPoint(Drawn(cape)[Hem]);
            yield return Run(body, new LocomotionInput { Move = Vector2.up, Sprint = true }, 150);
            Vector3 sprinting = root.InverseTransformPoint(Drawn(cape)[Hem]);
            Assert.Greater(body.CurrentSpeed, 4.0f, "the body did not sprint");
            Assert.Less(sprinting.z, standing.z - 0.25f, $"sprinting, the cape does not lift out behind ({standing} then {sprinting})");
            Assert.Greater(sprinting.y, standing.y + 0.08f, "sprinting, the cape's hem does not lift");
        }

        /// <summary>The owner: "maybe the back flies up a bit when you jump".</summary>
        [UnityTest]
        public IEnumerator JumpingTheBackFliesUp()
        {
            (Cape cape, PlayerLocomotion body) = Wear();
            yield return Run(body, new LocomotionInput(), 120);
            Transform root = _player.transform;
            float standing = root.InverseTransformPoint(Drawn(cape)[Hem]).z;
            float most = float.MaxValue;
            body.Tick(new LocomotionInput { Jump = true, JumpHeld = true }, Dt);
            yield return null;
            for (int i = 0; i < 50; i++)
            {
                body.Tick(new LocomotionInput { JumpHeld = true }, Dt);
                yield return null;
                most = Mathf.Min(most, root.InverseTransformPoint(Drawn(cape)[Hem]).z);
            }
            Assert.Less(most, standing - 0.15f, $"jumping, the cape's back does not fly up (behind {standing:0.00}, at most {most:0.00})");
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
            foreach (Vector3 p in Drawn(cape))
            {
                Assert.Less(Vector3.Distance(p, _player.transform.position), 3.0f, "part of the cape was left behind (or flung) when the body was moved");
            }
            Assert.Less(Mathf.Abs(cape.Swing.Lift), 5.0f, "moved somewhere new, the cape was thrown");
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
            Assert.IsNotNull(cape.Look, "the copy's cape is not hung on it");
            Vector3 before = Drawn(cape)[Hem];
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(Vector3.Distance(before, Drawn(cape)[Hem]), 0.0005f, "the copy's cape does not move (its swing is not running)");
        }
    }
}
