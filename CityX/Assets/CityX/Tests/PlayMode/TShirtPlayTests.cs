using System.Collections;
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
    /// The T-Shirt worn on the real player: the body under it is hidden and given back when it comes off, and through a sprint (the clip lowers
    /// the head 3.4 cm along the neck) the head never sinks into the collar, whose sides follow the neck. The Long Sleeve T-Shirt the same, its
    /// sleeves bending at the elbows.
    /// </summary>
    [TestFixture("TShirt")]
    [TestFixture("LongSleeveTShirt")]
    public sealed class TShirtPlayTests
    {
        private readonly string _prefab;

        public TShirtPlayTests(string prefab)
        {
            _prefab = prefab;
        }

        private const int StandFrames = 20;
        private const int SprintFrames = 170;

        /// <summary>How much deeper than standing the head may go into the collar's rib and still read as resting on it.</summary>
        private const float CollarSink = 0.015f;

        /// <summary>
        /// How far the cloth may be drawn out in a sprint (an edge's length over its length as made), at worst, and past Strained on no more
        /// than MostStrained of its triangles' sides. The armpits can do no better with this rig: the body's own skin, bound arms down and
        /// swung through a sprint, is drawn out up to 12x, with some 460 of its triangles' sides past 2x round the armpits (the shirt's, before
        /// its underarms were made anew, 16x). Bunching up (shorter) is how cloth folds, and is not counted.
        /// </summary>
        private const float MostStretch = 2.3f;
        private const float Strained = 2.0f;
        private const int MostStrained = 80;

        /// <summary>Edges shorter than this are left out of the stretch: the rolled cuff's and collar's tight turns.</summary>
        private const float MeasuredFrom = 0.01f;

        private GameObject _player;
        private GameObject _shirt;
        private PlayerLocomotion _locomotion;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _shirt = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{_prefab}.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Object.Destroy(_player);
            if (_shirt != null)
            {
                Object.Destroy(_shirt);
            }
            Object.Destroy(GameObject.Find("TestGround"));
        }

        [UnityTest]
        public IEnumerator TheShirtHidesTheBodyUnderItAndGivesItBackWhenTakenOff()
        {
            var skin = TransformSearch.FindDeep(_player.transform, "Skin").GetComponent<SkinnedMeshRenderer>();
            Mesh whole = skin.sharedMesh;
            var shirt = _shirt.GetComponent<TShirt>();
            shirt.OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Assert.AreNotSame(whole, skin.sharedMesh, "the body under the shirt is still drawn whole");
            Assert.Less(skin.sharedMesh.triangles.Length, whole.triangles.Length);
            Assert.Greater(skin.sharedMesh.triangles.Length, whole.triangles.Length / 2, "the shirt hides more of the body than it covers");
            shirt.OnUnequip(_player);
            _shirt = null;
            yield return null;
            Assert.AreSame(whole, skin.sharedMesh, "taking the shirt off did not give the whole body back");
        }

        [UnityTest]
        public IEnumerator TheHeadNeverSinksIntoTheCollarInASprint()
        {
            _shirt.GetComponent<TShirt>().OnEquip(_player, ItemInstanceData.None);
            Transform head = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var worn = TransformSearch.FindDeep(_player.transform, "CombinedGarmentSkin").GetComponent<SkinnedMeshRenderer>();
            var baked = new Mesh();
            float[] standing = null;
            float sank = 0.0f;
            Vector3 where = Vector3.zero;
            try
            {
                for (int frame = 0; frame < SprintFrames; frame++)
                {
                    _locomotion.Tick(new LocomotionInput { Move = frame > StandFrames ? Vector2.up : Vector2.zero, Sprint = frame > StandFrames },
                        1.0f / 60.0f);
                    yield return null;
                    if (frame < StandFrames)
                    {
                        continue;
                    }
                    worn.BakeMesh(baked, true);
                    Matrix4x4 toWorld = Matrix4x4.TRS(worn.transform.position, worn.transform.rotation, Vector3.one);
                    Vector3[] vertices = baked.vertices;
                    standing ??= new float[vertices.Length];
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        float off = Vector3.Distance(toWorld.MultiplyPoint3x4(vertices[v]), head.position) - FusedBodyShape.HeadRadius;
                        if (frame == StandFrames)
                        {
                            standing[v] = off;
                            continue;
                        }
                        float deeper = Mathf.Max(0.0f, -off) - Mathf.Max(0.0f, -standing[v]);
                        if (deeper > sank)
                        {
                            sank = deeper;
                            where = worn.sharedMesh.vertices[v];
                        }
                    }
                }
            }
            finally
            {
                Object.Destroy(baked);
            }
            Debug.Log($"TShirt sprint: the head went {sank * 100.0f:0.0} cm deeper into the collar than standing");
            Assert.Less(sank, CollarSink, $"the head sank {sank * 100.0f:0.0} cm deeper into the shirt than standing, at {where:F3} (bind pose)");
        }

        /// <summary>
        /// Owner, 2026-10-20: "it still stretches around the armpits". Through a sprint, the shirt's cloth (each edge between two of its points,
        /// a centimetre or longer) is never drawn out past MostStretch, and few edges past Strained.
        /// </summary>
        [UnityTest]
        public IEnumerator TheClothNeverStretchesFarInASprint()
        {
            var shirt = _shirt.GetComponent<TShirt>();
            shirt.OnEquip(_player, ItemInstanceData.None);
            var worn = TransformSearch.FindDeep(_player.transform, "CombinedGarmentSkin").GetComponent<SkinnedMeshRenderer>();
            Vector3[] bind = shirt.GarmentMesh.vertices;
            int[] triangles = shirt.GarmentMesh.triangles;
            int offset = worn.sharedMesh.vertexCount - bind.Length;   // the combined skin holds the shirt, worn last, at its end
            var baked = new Mesh();
            float worst = 1.0f;
            var edgeWorst = new float[triangles.Length];
            Vector3 where = Vector3.zero;
            int when = 0;
            try
            {
                for (int frame = 0; frame < SprintFrames; frame++)
                {
                    _locomotion.Tick(new LocomotionInput { Move = frame > StandFrames ? Vector2.up : Vector2.zero, Sprint = frame > StandFrames },
                        1.0f / 60.0f);
                    yield return null;
                    worn.BakeMesh(baked, true);
                    Vector3[] now = baked.vertices;
                    for (int t = 0; t < triangles.Length; t++)
                    {
                        int a = triangles[t], b = triangles[t % 3 == 2 ? t - 2 : t + 1];
                        float made = Vector3.Distance(bind[a], bind[b]);
                        if (made < MeasuredFrom)
                        {
                            continue;
                        }
                        float stretch = Vector3.Distance(now[offset + a], now[offset + b]) / made;
                        edgeWorst[t] = Mathf.Max(edgeWorst[t], stretch);
                        if (stretch > worst)
                        {
                            worst = stretch;
                            where = (bind[a] + bind[b]) * 0.5f;
                            when = frame;
                        }
                    }
                }
            }
            finally
            {
                Object.Destroy(baked);
            }
            int strained = System.Array.FindAll(edgeWorst, s => s > Strained).Length;
            Debug.Log($"{_prefab} sprint: the cloth drawn out {worst:0.00}x at the most, at {where:F3} (bind pose), frame {when}; " +
                $"{strained} of its triangles' {triangles.Length} sides past {Strained}x");
            Assert.Less(worst, MostStretch, $"the shirt's cloth was drawn out {worst:0.00}x at {where:F3} (bind pose), frame {when}");
            Assert.LessOrEqual(strained, MostStrained, $"{strained} edges of the shirt were drawn out past {Strained}x");
        }
    }
}
