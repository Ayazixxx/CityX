using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Black Jeans on the real player: the legs under them hidden and given back; worn with a shirt, both hide their part of the body and
    /// either comes off first without bringing back what the other covers; and through a sprint the cloth stays on the legs (no edge stretched
    /// far past its length as fitted, the hems round the ankles).
    /// </summary>
    public sealed class BlackJeansPlayTests
    {
        private const int StandFrames = 20;
        private const int SprintFrames = 170;

        /// <summary>
        /// How far past its fitted length an edge of the cloth may stretch in a sprint: a knee coming up high stretches the seat about four
        /// times over at its most, as in any skinned trousers. The tears this guards against pulled edges 7 to 24 times apart (weights jumping
        /// across the buttocks' crease, the crotch pulled half toward each leg).
        /// </summary>
        private const float MostStretch = 5.0f;

        /// <summary>
        /// Edges shorter than this are left out: across the crotch a few millimetres stretch many times over when the legs split, as in any
        /// skinned trousers; a tear (two neighbours following different bones) pulls whole centimetres apart.
        /// </summary>
        private const float ShortestEdge = 0.01f;

        /// <summary>How far from its ankle a hem may get.</summary>
        private const float HemReach = 0.22f;

        /// <summary>The hems: the cloth this low in the bind pose.</summary>
        private const float HemHeight = 0.12f;

        private GameObject _player;
        private GameObject _jeans;
        private GameObject _shirt;
        private SkinnedMeshRenderer _skin;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _jeans = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BlackJeans.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            _shirt = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/TShirt.prefab"),
                new Vector3(-30.0f, 0.5f, 30.0f), Quaternion.identity);
            _skin = TransformSearch.FindDeep(_player.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(_player);
            foreach (GameObject worn in new[] { _jeans, _shirt })
            {
                if (worn != null)
                {
                    Object.Destroy(worn);
                }
            }
            Object.Destroy(GameObject.Find("TestGround"));
        }

        private int DrawnTriangles => _skin.sharedMesh.triangles.Length / 3;

        [UnityTest]
        public IEnumerator TheJeansHideTheLegsUnderThemAndGiveThemBackWhenTakenOff()
        {
            Mesh whole = _skin.sharedMesh;
            _jeans.GetComponent<CityX.Wearables.Jeans>().OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Assert.AreNotSame(whole, _skin.sharedMesh, "the legs under the jeans are still drawn");
            Assert.Greater(DrawnTriangles, whole.triangles.Length / 3 / 2, "the jeans hide more of the body than they cover");
            _jeans.GetComponent<CityX.Wearables.Jeans>().OnUnequip(_player);
            _jeans = null;
            yield return null;
            Assert.AreSame(whole, _skin.sharedMesh, "taking the jeans off did not give the whole body back");
        }

        [UnityTest]
        public IEnumerator WornWithAShirtBothHideTheirPartAndEitherComesOffFirst([Values(true, false)] bool shirtOffFirst)
        {
            Mesh whole = _skin.sharedMesh;
            var jeans = _jeans.GetComponent<CityX.Wearables.Jeans>();
            var shirt = _shirt.GetComponent<TShirt>();
            jeans.OnEquip(_player, ItemInstanceData.None);
            int jeansOnly = DrawnTriangles;
            shirt.OnEquip(_player, ItemInstanceData.None);
            yield return null;
            int both = DrawnTriangles;
            Assert.Less(both, jeansOnly, "the shirt hid nothing with the jeans on");

            (SkinnedGarment first, SkinnedGarment second) = shirtOffFirst ? ((SkinnedGarment)shirt, (SkinnedGarment)jeans) : (jeans, shirt);
            first.OnUnequip(_player);
            yield return null;
            if (shirtOffFirst)
            {
                Assert.AreEqual(jeansOnly, DrawnTriangles, "with the shirt off, the body under the jeans came back, or the shirt's did not");
            }
            else
            {
                Assert.Greater(DrawnTriangles, both, "taking the jeans off gave nothing back");
                Assert.Less(DrawnTriangles, whole.triangles.Length / 3, "taking the jeans off brought back the body under the shirt too");
            }
            second.OnUnequip(_player);
            _jeans = _shirt = null;
            yield return null;
            Assert.AreSame(whole, _skin.sharedMesh, "with both off the body is not whole");
        }

        [UnityTest]
        public IEnumerator ThroughASprintTheJeansStayOnTheLegs()
        {
            _jeans.GetComponent<CityX.Wearables.Jeans>().OnEquip(_player, ItemInstanceData.None);
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform[] ankles = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            var worn = TransformSearch.FindDeep(_player.transform, "CombinedGarmentSkin").GetComponent<SkinnedMeshRenderer>();
            Mesh fitted = worn.sharedMesh;
            Vector3[] rest = fitted.vertices;
            int[] triangles = fitted.triangles;
            var hems = new List<int>();
            for (int v = 0; v < rest.Length; v++)
            {
                if (rest[v].y < HemHeight)
                {
                    hems.Add(v);
                }
            }
            var baked = new Mesh();
            float stretch = 0.0f, hemOff = 0.0f;
            string worst = "";
            try
            {
                for (int frame = 0; frame < SprintFrames; frame++)
                {
                    locomotion.Tick(new LocomotionInput { Move = frame > StandFrames ? Vector2.up : Vector2.zero, Sprint = frame > StandFrames },
                        1.0f / 60.0f);
                    yield return null;
                    worn.BakeMesh(baked, true);
                    Vector3[] now = baked.vertices;
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            int a = triangles[t + k], b = triangles[t + (k + 1) % 3];
                            float length = Vector3.Distance(rest[a], rest[b]);
                            if (length < ShortestEdge)
                            {
                                continue;
                            }
                            float ratio = Vector3.Distance(now[a], now[b]) / length;
                            if (ratio > stretch)
                            {
                                stretch = ratio;
                                worst = $"frame {frame}, the edge at {rest[a]:F2} (bind pose)";
                            }
                        }
                    }
                    Matrix4x4 toWorld = Matrix4x4.TRS(worn.transform.position, worn.transform.rotation, Vector3.one);
                    foreach (int v in hems)
                    {
                        Vector3 at = toWorld.MultiplyPoint3x4(now[v]);
                        hemOff = Mathf.Max(hemOff, Mathf.Min(Vector3.Distance(at, ankles[0].position), Vector3.Distance(at, ankles[1].position)));
                    }
                }
            }
            finally
            {
                Object.Destroy(baked);
            }
            Debug.Log($"BlackJeans sprint: an edge stretched to {stretch:0.00} times its fitted length at most ({worst}); a hem got " +
                $"{hemOff * 100.0f:0.0} cm from its ankle");
            Assert.Greater(hems.Count, 50, "no hems found to check");
            Assert.Less(stretch, MostStretch, $"the cloth tore along the legs: an edge stretched {stretch:0.00} times, {worst}");
            Assert.Less(hemOff, HemReach, $"a hem left its ankle: {hemOff * 100.0f:0.0} cm off");
        }
    }
}
