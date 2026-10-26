using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Farmer Hat (owner, 2026-10-26): a helmet-slot straw hat within the hat budget, its brim wider than the head and the head wholly
    /// inside its crown (the model's origin is the head's centre), and other players shown it.
    /// </summary>
    public sealed class FarmerHatTests
    {
        private const string PrefabPath = "Assets/CityX/Wearables/FarmerHat.prefab";

        /// <summary>The most triangles a hat may have (tools/model_qa.py's hat budget).</summary>
        private const int TriangleBudget = 6000;

        /// <summary>The head is checked inside the crown along directions this far up from level (the sine): below, the brim's opening.</summary>
        private const float CrownFromHeight = 0.3f;

        /// <summary>Rays come in from this far out from the head's centre, past the whole hat, so the first surface met is its outside.</summary>
        private const float OutsideTheHat = 2.0f;

        private static Mesh Model()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no FarmerHat.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsNotNull(prefab.GetComponent<FarmerHat>());
            return prefab.transform.Find(FarmerHat.ModelName).GetComponent<MeshFilter>().sharedMesh;
        }

        [Test]
        public void TheFarmerHatFillsTheHelmetSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.FarmerHat, out ItemDefinition hat));
            Assert.AreEqual(EquipSlots.Helmet, hat.EquipSlot);
            Assert.AreEqual("Farmer Hat", hat.DisplayName);
            Assert.IsTrue(hat.IsWearable);
        }

        [Test]
        public void TheHatIsWithinItsBudgetAndWearsOneSmallColourMap()
        {
            Mesh mesh = Model();
            Assert.LessOrEqual(mesh.triangles.Length / 3, TriangleBudget);
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "the hat has no texture coordinates");
            Material material = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.Find(FarmerHat.ModelName)
                .GetComponent<MeshRenderer>().sharedMaterial;
            Texture texture = material.GetTexture("_BaseMap");
            Assert.IsNotNull(texture, "the hat has no colour map");
            Assert.LessOrEqual(texture.width, 1024, "the hat's colour map is too big for many players at once");
        }

        /// <summary>
        /// The brim stands out past the head all round and the crown over it; every way up through the crown, the hat's outside stands past the
        /// head's surface, so none of the head comes through (its inside rests on the head: Meshy's hat was modelled on one).
        /// </summary>
        [Test]
        public void TheBrimIsWiderThanTheHeadAndTheHeadIsInsideTheCrown()
        {
            Mesh mesh = Model();
            Bounds bounds = mesh.bounds;
            float head = FusedBodyShape.HeadRadius;
            Assert.Greater(bounds.max.y, head, "the crown's top is inside the head");
            Assert.Greater(bounds.min.y, -head, "the hat hangs below the chin");
            Assert.Less(bounds.min.x, -head * 1.5f, "the brim is no wider than the head");
            Assert.Greater(bounds.max.x, head * 1.5f, "the brim is no wider than the head");
            Assert.Less(bounds.min.z, -head * 1.5f, "the brim is no wider than the head");
            Assert.Greater(bounds.max.z, head * 1.5f, "the brim is no wider than the head");

            var hat = new GameObject("FarmerHatCheck");
            bool hitBackfaces = Physics.queriesHitBackfaces;
            try
            {
                var crown = hat.AddComponent<MeshCollider>();
                crown.sharedMesh = mesh;
                Physics.SyncTransforms();
                Physics.queriesHitBackfaces = true;
                int checkedWays = 0;
                for (int i = 0; i < 400; i++)
                {
                    // Spread evenly over the sphere (a Fibonacci spiral), the ones through the crown kept.
                    float y = 1.0f - (i + 0.5f) / 200.0f;
                    if (y < CrownFromHeight)
                    {
                        continue;
                    }
                    float around = i * Mathf.PI * (3.0f - Mathf.Sqrt(5.0f));
                    float ring = Mathf.Sqrt(1.0f - y * y);
                    var way = new Vector3(Mathf.Cos(around) * ring, y, Mathf.Sin(around) * ring);
                    Assert.IsTrue(crown.Raycast(new Ray(way * OutsideTheHat, -way), out RaycastHit outside, OutsideTheHat),
                        $"the crown is open towards {way}");
                    Assert.Greater(OutsideTheHat - outside.distance, head, $"the head comes through the crown towards {way}");
                    checkedWays++;
                }
                Assert.Greater(checkedWays, 50);
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
                Object.DestroyImmediate(hat);
            }
        }

        /// <summary>The fuller hairstyles come out through the crown: the wearer goes bald under it, and gets the hair back taking it off.</summary>
        [Test]
        public void TheHairGoesUnderTheHatAndComesBack()
        {
            var body = new GameObject("Body");
            var hair = body.AddComponent<CharacterHair>();
            var hat = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<FarmerHat>();
            try
            {
                hat.OnEquip(body, ItemInstanceData.None);
                Assert.IsTrue(hair.IsCovered, "the hair comes through the Farmer Hat");
                hat.UnmountCosmetic(body);
                Assert.IsFalse(hair.IsCovered, "taking the Farmer Hat off left the wearer bald");
            }
            finally
            {
                Object.DestroyImmediate(hat.gameObject);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void OtherPlayersCanBeShownTheHat()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            Assert.IsNotNull(networked, "no NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.FarmerHat);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the hat (NetworkedPlayerWiring.ItemPrefabPaths)");
            Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "the hat cannot be mounted on a copy");
        }
    }
}
