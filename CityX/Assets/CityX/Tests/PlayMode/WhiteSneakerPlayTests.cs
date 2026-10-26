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
    /// The White Sneaker on the real player: each shoe on its own foot, the feet under them hidden and given back; worn with jeans, taking the
    /// shoes off leaves the legs under the jeans hidden; standing, the soles rest on the ground; and on another player's copy the same.
    /// </summary>
    public sealed class WhiteSneakerPlayTests
    {
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";
        private const int StandFrames = 30;

        /// <summary>How far above or into the ground a sole may be with the player standing still.</summary>
        private const float SoleSlack = 0.04f;

        /// <summary>
        /// The back of the ankle down its middle: this far either side of the foot bone, from this high up (over the foot, under the collar).
        /// </summary>
        private const float AnkleMiddle = 0.02f;
        private const float CollarLow = -0.04f;

        /// <summary>How far back a ray looks for the heel, and the most of a gap that does not show.</summary>
        private const float HeelReach = 0.1f;
        private const float HeelGap = 0.005f;

        private GameObject _player;
        private GameObject _sneakers;
        private SkinnedMeshRenderer _skin;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), new Vector3(0.0f, 0.05f, 0.0f),
                Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _sneakers = Spawn("WhiteSneaker", new Vector3(30.0f, 0.5f, 30.0f));
            _skin = TransformSearch.FindDeep(_player.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Object.Destroy(_player);
            if (_sneakers != null)
            {
                Object.Destroy(_sneakers);
            }
            Object.Destroy(GameObject.Find("TestGround"));
        }

        private static GameObject Spawn(string prefab, Vector3 at)
        {
#if UNITY_EDITOR
            return Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab"), at,
                Quaternion.identity);
#else
            return null;
#endif
        }

        private int DrawnTriangles => _skin.sharedMesh.triangles.Length / 3;

        [UnityTest]
        public IEnumerator EachShoeGoesOnItsFootAndTheFeetComeBackWhenTakenOff()
        {
            Mesh whole = _skin.sharedMesh;
            var shoes = _sneakers.GetComponent<Shoes>();
            Transform left = shoes.Left, right = shoes.Right;
            shoes.OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Animator animator = _player.GetComponentInChildren<Animator>();
            Assert.AreSame(animator.GetBoneTransform(HumanBodyBones.LeftFoot), left.parent, "the left shoe is not on the left foot");
            Assert.AreSame(animator.GetBoneTransform(HumanBodyBones.RightFoot), right.parent, "the right shoe is not on the right foot");
            Assert.AreNotSame(whole, _skin.sharedMesh, "the feet under the shoes are still drawn");
            Assert.Greater(DrawnTriangles, whole.triangles.Length / 3 * 3 / 4, "the shoes hide far more than the feet");

            shoes.OnUnequip(_player);
            _sneakers = null;
            yield return null;
            Assert.AreSame(whole, _skin.sharedMesh, "taking the shoes off did not give the feet back");
            Assert.IsTrue(left == null && right == null, "the shoes stayed on the feet after being taken off");
        }

        [UnityTest]
        public IEnumerator WornWithJeansTakingTheShoesOffLeavesTheLegsHidden()
        {
            GameObject jeansObject = Spawn("BlackJeans", new Vector3(-30.0f, 0.5f, 30.0f));
            var jeans = jeansObject.GetComponent<Jeans>();
            var shoes = _sneakers.GetComponent<Shoes>();
            jeans.OnEquip(_player, ItemInstanceData.None);
            int jeansOnly = DrawnTriangles;
            shoes.OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Assert.Less(DrawnTriangles, jeansOnly, "the shoes hid nothing with the jeans on");
            shoes.OnUnequip(_player);
            _sneakers = null;
            yield return null;
            Assert.AreEqual(jeansOnly, DrawnTriangles, "with the shoes off, the legs under the jeans came back, or the feet did not");
            jeans.OnUnequip(_player);
            yield return null;
        }

        /// <summary>
        /// Owner, 2026-10-26: "when pants and shoes are being worn you can make the feet underneath invisible, when i jump a bit of the skin
        /// pokes through the shoes model". In jeans and shoes, put on in either order, no skin is drawn below the knees: all of it is in the
        /// jeans' legs or in the shoes (bare-footed, the jeans leave the ankles under their hems drawn).
        /// </summary>
        [UnityTest]
        public IEnumerator InJeansAndShoesNoSkinIsDrawnBelowTheKnees([Values(true, false)] bool shoesFirst)
        {
            GameObject jeansObject = Spawn("BlackJeans", new Vector3(-30.0f, 0.5f, 30.0f));
            var jeans = jeansObject.GetComponent<Jeans>();
            var shoes = _sneakers.GetComponent<Shoes>();
            foreach (IEquippableItem item in shoesFirst ? new IEquippableItem[] { shoes, jeans } : new IEquippableItem[] { jeans, shoes })
            {
                item.OnEquip(_player, ItemInstanceData.None);
            }
            yield return null;
            Mesh drawn = _skin.sharedMesh;
            Vector3[] rest = drawn.vertices;
            int[] triangles = drawn.triangles;
            float knee = Mathf.Lerp(BodyRig.HipY, BodyRig.FootY, BodyRig.KneeT);
            int below = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                float centre = (rest[triangles[t]].y + rest[triangles[t + 1]].y + rest[triangles[t + 2]].y) / 3.0f;
                below += centre < knee ? 1 : 0;
            }
            Assert.AreEqual(0, below, "in jeans and shoes, skin is still drawn below the knees");
            jeans.OnUnequip(_player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StandingTheSolesRestOnTheGround()
        {
            _sneakers.GetComponent<Shoes>().OnEquip(_player, ItemInstanceData.None);
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < StandFrames; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            var shoes = _sneakers.GetComponent<Shoes>();
            float ground = _player.transform.position.y;
            foreach (Transform shoe in new[] { shoes.Left, shoes.Right })
            {
                float lowest = float.MaxValue;
                foreach (Vector3 vertex in shoe.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    lowest = Mathf.Min(lowest, shoe.TransformPoint(vertex).y);
                }
                Assert.AreEqual(ground, lowest, SoleSlack, $"standing, the {shoe.name} shoe's sole is not on the ground");
            }
        }

        /// <summary>
        /// Owner, 2026-10-26: "slide it forward so the back of the heel it touching the inner wall of the shoe" (the tongue clipped into the
        /// front of the ankle). Standing, from the back of each ankle down the middle straight back to the heel: no gap to see; and nowhere round
        /// the back of the ankle does the skin come out through the heel. The heel is one surface, seen from inside by its back: rays hit both
        /// sides.
        /// </summary>
        [UnityTest]
        public IEnumerator StandingTheBackOfEachAnkleRestsOnItsHeel()
        {
            var shoes = _sneakers.GetComponent<Shoes>();
            shoes.OnEquip(_player, ItemInstanceData.None);
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < StandFrames; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            var posed = new Mesh();
            _skin.BakeMesh(posed);
            Vector3[] skin = posed.vertices;
            bool hitBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                foreach (Transform shoe in new[] { shoes.Left, shoes.Right })
                {
                    var heel = shoe.gameObject.AddComponent<MeshCollider>();
                    heel.sharedMesh = shoe.GetComponent<MeshFilter>().sharedMesh;
                    Transform foot = shoe.parent;
                    float collarTop = heel.sharedMesh.bounds.max.y;
                    float gap = float.MaxValue, through = 0.0f;
                    foreach (Vector3 vertex in skin)
                    {
                        Vector3 world = _skin.transform.TransformPoint(vertex);
                        Vector3 local = foot.InverseTransformPoint(world);
                        if (local.z > 0.0f || local.y < CollarLow || local.y > collarTop)
                        {
                            continue;
                        }
                        if (Mathf.Abs(local.x) < AnkleMiddle && heel.Raycast(new Ray(world, -foot.forward), out RaycastHit back, HeelReach))
                        {
                            gap = Mathf.Min(gap, back.distance);
                        }
                        if (heel.Raycast(new Ray(world, foot.forward), out RaycastHit ahead, HeelReach)
                            && foot.InverseTransformPoint(ahead.point).z < -HeelReach / 2.0f)
                        {
                            through = Mathf.Max(through, ahead.distance);
                        }
                    }
                    Object.DestroyImmediate(heel);
                    Assert.Less(gap, HeelGap, $"standing, the back of the ankle stands {gap * 100.0f:0.0} cm off the {shoe.name} shoe's heel " +
                        $"(and {through * 100.0f:0.0} cm out through it)");
                    Assert.Less(through, HeelGap, $"standing, the ankle comes {through * 100.0f:0.0} cm out through the {shoe.name} shoe's heel");
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
                Object.Destroy(posed);
            }
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyWearsThemOnItsFeet()
        {
            var shoes = _sneakers.GetComponent<Shoes>();
            Mesh whole = _skin.sharedMesh;
            shoes.MountCosmetic(_player);
            yield return null;
            Animator animator = _player.GetComponentInChildren<Animator>();
            Assert.AreSame(animator.GetBoneTransform(HumanBodyBones.LeftFoot), shoes.Left.parent, "the copy's left shoe is not on its left foot");
            Assert.AreSame(animator.GetBoneTransform(HumanBodyBones.RightFoot), shoes.Right.parent, "the copy's right shoe is not on its right foot");
            Assert.AreNotSame(whole, _skin.sharedMesh, "the copy's feet show through its shoes");

            Transform left = shoes.Left;
            shoes.UnmountCosmetic(_player);
            Object.Destroy(_sneakers);
            _sneakers = null;
            yield return null;
            Assert.AreSame(whole, _skin.sharedMesh, "the copy's feet did not come back");
            Assert.IsTrue(left == null, "the copy's shoe outlived its model");
        }
    }
}
