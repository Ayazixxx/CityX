using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The White Sneaker (owner, 2026-10-25): a shoes-slot pair within its budget, each shoe baked in its own foot's frame so that worn it
    /// covers that foot toe to heel with its sole on the ground, the feet under it hidden, and other players shown it.
    /// </summary>
    public sealed class WhiteSneakerTests
    {
        private const string PrefabPath = "Assets/CityX/Wearables/WhiteSneaker.prefab";
        private const string BodyMeshPath = "Assets/CityX/Character/FusedBody.asset";

        /// <summary>The most triangles the pair may have (tools/model_qa.py's shoes budget).</summary>
        private const int TriangleBudget = 12000;

        /// <summary>The bare foot inside: its heel this far behind the ankle, its toes this far ahead (FusedBodyShape's foot).</summary>
        private const float HeelBehind = 0.09f;
        private const float ToeAhead = 0.225f;

        /// <summary>How far off the ground a sole may sit at rest.</summary>
        private const float SoleSlack = 0.02f;

        private static Shoes Pair()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no WhiteSneaker.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var shoes = prefab.GetComponent<Shoes>();
            Assert.IsNotNull(shoes, "the White Sneaker prefab is not a pair of shoes");
            Assert.IsNotNull(shoes.Left, "the pair has no left shoe");
            Assert.IsNotNull(shoes.Right, "the pair has no right shoe");
            return shoes;
        }

        private static Mesh MeshOf(Transform shoe) => shoe.GetComponent<MeshFilter>().sharedMesh;

        [Test]
        public void TheWhiteSneakerFillsTheShoesSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.WhiteSneaker, out ItemDefinition sneaker));
            Assert.AreEqual(EquipSlots.Shoes, sneaker.EquipSlot);
            Assert.AreEqual("White Sneaker", sneaker.DisplayName);
            Assert.IsTrue(sneaker.IsWearable);
        }

        [Test]
        public void ThePairIsWithinItsBudgetAndWearsOneSmallColourMap()
        {
            Shoes shoes = Pair();
            int triangles = 0;
            foreach (Transform shoe in new[] { shoes.Left, shoes.Right })
            {
                Mesh mesh = MeshOf(shoe);
                Assert.Greater(mesh.triangles.Length, 0, $"the {shoe.name} shoe has no model");
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, $"the {shoe.name} shoe has no texture coordinates");
                triangles += mesh.triangles.Length / 3;
                Texture texture = shoe.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
                Assert.IsNotNull(texture, $"the {shoe.name} shoe has no colour map");
                Assert.LessOrEqual(texture.width, 1024, "the sneakers' colour map is too big for many players at once");
            }
            Assert.LessOrEqual(triangles, TriangleBudget);
        }

        /// <summary>
        /// In its foot bone's frame (the ankle; the body faces +z) each shoe reaches past the toes and the heel, its sole on the ground, centred
        /// on the foot: the model came through the glb with the right axes and the right side.
        /// </summary>
        [Test]
        public void EachShoeGoesRoundItsFootWithTheSoleOnTheGround()
        {
            Shoes shoes = Pair();
            foreach (Transform shoe in new[] { shoes.Left, shoes.Right })
            {
                Bounds bounds = MeshOf(shoe).bounds;
                Assert.Greater(bounds.max.z, ToeAhead, $"the toes poke out of the {shoe.name} shoe's front (or it faces backwards)");
                Assert.Less(bounds.min.z, -HeelBehind, $"the heel pokes out of the {shoe.name} shoe's back");
                Assert.AreEqual(-BodyRig.FootY, bounds.min.y, SoleSlack, $"the {shoe.name} shoe's sole is not on the ground");
                Assert.AreEqual(0.0f, bounds.center.x, 0.03f, $"the {shoe.name} shoe is off to one side of its foot");
            }
            Assert.Less(shoes.Left.localPosition.x, 0.0f, "the left shoe is on the right foot's side");
            Assert.Greater(shoes.Right.localPosition.x, 0.0f, "the right shoe is on the left foot's side");
        }

        [Test]
        public void ThePairHidesTheFeetUnderIt()
        {
            Shoes shoes = Pair();
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            Assert.AreEqual(body.triangles.Length / 3, shoes.HiddenFromBodyTriangles, "the pair was made for another body: rebuild it");
            Assert.IsNotNull(shoes.HiddenBodyTriangles, "the pair hides nothing: the feet would poke through");
            int hidden = 0;
            foreach (byte bits in shoes.HiddenBodyTriangles)
            {
                for (int b = 0; b < 8; b++)
                {
                    hidden += (bits >> b) & 1;
                }
            }
            Assert.Greater(hidden, 0, "the pair hides nothing: the feet would poke through");
            Assert.Less(hidden, shoes.HiddenFromBodyTriangles / 4, "the pair hides far more of the body than two feet");
        }

        [Test]
        public void OtherPlayersCanBeShownTheSneakers()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            Assert.IsNotNull(networked, "no NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.WhiteSneaker);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the sneakers (NetworkedPlayerWiring.ItemPrefabPaths)");
            Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "the sneakers cannot be mounted on a copy");
        }
    }
}
