using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Claw Skull (owner, 2026-11-08: "make it an item ingame "Claw Skull" and wearable as a helmet", and of its back: "too long for it to be
    /// functional ingame"): a helmet-slot item centred on the head, its back drawn in, wearing the Crimson Claw's own colour.
    /// </summary>
    public sealed class ClawSkullTests
    {
        private const string PrefabPath = "Assets/CityX/Wearables/ClawSkull.prefab";
        private const string NetworkedPlayerPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";

        /// <summary>The most triangles a helmet should have: the top of the budget's "aim" (the budget given to the owner on 2026-10-18).</summary>
        private const int TriangleBudget = 5000;

        /// <summary>How far behind the head's centre it may reach, metres: the Claw's own head reached 0.70, twice the head's radius.</summary>
        private const float FurthestBack = 0.46f;

        private static Transform Model()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no ClawSkull.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsNotNull(prefab.GetComponent<ClawSkull>());
            return prefab.transform.Find(ClawSkull.ModelName);
        }

        [Test]
        public void TheClawSkullFillsTheHelmetSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.ClawSkull, out ItemDefinition skull));
            Assert.AreEqual(EquipSlots.Helmet, skull.EquipSlot);
            Assert.AreEqual("Claw Skull", skull.DisplayName);
        }

        [Test]
        public void ItIsWithinItsTriangleBudgetGoesAroundTheHeadAndFacesForward()
        {
            Mesh mesh = Model().GetComponent<MeshFilter>().sharedMesh;
            Assert.LessOrEqual(mesh.triangles.Length / 3, TriangleBudget);

            // The model's origin is the head's centre, so the head sphere has to sit inside its bounds on every side but the open bottom.
            Bounds bounds = mesh.bounds;
            float head = FusedBodyShape.HeadRadius;
            Assert.Greater(bounds.max.y, head - 0.01f, "the skull's top is inside the head");
            Assert.Less(bounds.min.x, -head, "the head pokes out of the skull's side");
            Assert.Greater(bounds.max.x, head, "the head pokes out of the skull's side");
            Assert.Less(bounds.min.z, -head, "the head pokes out of the skull's back");
            Assert.Greater(bounds.max.z, head, "the head pokes out of the skull's face");
            Assert.Greater(bounds.max.z, -bounds.min.z, "the snout should reach further forward than the back reaches behind: it is on backwards");
        }

        [Test]
        public void ItsBackIsDrawnInToTheHead()
        {
            Bounds bounds = Model().GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Greater(bounds.min.z, -FurthestBack, "the back of the skull still sticks out too far to play with");
        }

        /// <summary>The owner's numbers from the Inspector (2026-11-09: "make this the new positioning"), on the wearer and on other players' copies alike.</summary>
        [Test]
        public void ItSitsOnTheHeadWhereTheOwnerFittedIt()
        {
            var fittedPosition = new Vector3(0.0f, 0.00169f, 0.0f);
            Quaternion fittedRotation = Quaternion.Euler(13.26f, 0.0f, 0.0f);

            GameObject skullPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject body = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            GameObject worn = Object.Instantiate(skullPrefab);
            GameObject onACopy = Object.Instantiate(skullPrefab);
            try
            {
                Transform head = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                worn.GetComponent<ClawSkull>().OnEquip(body, ItemInstanceData.None);
                onACopy.GetComponent<ClawSkull>().MountCosmetic(body);

                foreach (GameObject skull in new[] { worn, onACopy })
                {
                    Assert.AreSame(head, skull.transform.parent, "it is not riding the Head bone");
                    Assert.Less(Vector3.Distance(fittedPosition, skull.transform.localPosition), 0.0001f, "it moved off the owner's position");
                    Assert.Less(Quaternion.Angle(fittedRotation, skull.transform.localRotation), 0.01f, "it turned off the owner's angle");
                }
            }
            finally
            {
                Object.DestroyImmediate(worn);
                Object.DestroyImmediate(onACopy);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ItWearsTheClawsOwnColourAndAddsNoTexture()
        {
            Material material = Model().GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/CrimsonClawBody.mat"), material);
            Assert.IsNull(material.GetTexture("_BaseMap"), "a colour map was added to it");
        }

        [Test]
        public void ItIsVeryRareAndMediumArmour()
        {
            Assert.AreEqual(Rarity.VeryRare, ItemRarity.Of(ItemRegistry.ClawSkull));
            Assert.AreEqual(ArmourTier.Medium, ArmourRatings.TierOf(ItemRegistry.ClawSkull));
        }

        [Test]
        public void OtherPlayersCanBeShownIt()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath);
            Assert.IsNotNull(networked, "no NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.ClawSkull);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the skull: rebuild the scene (NetworkedPlayerWiring.ItemPrefabPaths)");
            Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "the skull cannot be mounted on a copy");
        }

        [Test]
        public void TheConsoleKnowsItByName()
        {
            System.Reflection.MethodInfo alias = typeof(CityX.UI.DevConsoleView).GetMethod("AliasToItemId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(alias, "DevConsoleView.AliasToItemId is gone");
            foreach (string name in new[] { "clawskull", "claw_skull" })
            {
                Assert.AreEqual(ItemRegistry.ClawSkull, alias.Invoke(null, new object[] { name }), $"/equip {name}");
            }
        }
    }
}
