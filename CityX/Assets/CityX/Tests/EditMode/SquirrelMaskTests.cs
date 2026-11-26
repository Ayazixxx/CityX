using System.Linq;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Squirrel Mask (owner, 2026-11-25: "the nose unintentionally went inward when generating. can you make it protrude ... lower it to a
    /// reasonable face count and separate the mask from the playermodel so we can use it ingame ... make the mask this orange/brown color with
    /// the eyes black and nose black"): worn over the whole head as a helmet, brown fur (the owner's #874d00) with black eyes either side and a black nose that
    /// stands out of the snout, outside the head, within its triangle count, and shown to other players.
    /// </summary>
    public sealed class SquirrelMaskTests
    {
        private const string PrefabPath = "Assets/CityX/Wearables/SquirrelMask.prefab";

        /// <summary>
        /// Over a mask's usual 8,000: the owner chose the 20,000 rebuild of their trim, smoothed, over the 12,000 ones, which kept shards
        /// (docs/SQUIRREL_MASK.md), and an inner side near its open edge (about 4,000 more). This keeps it from growing further.
        /// </summary>
        private const int TriangleBudget = 25000;

        private static GameObject Prefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no SquirrelMask.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsNotNull(prefab.GetComponent<SquirrelMask>());
            return prefab;
        }

        private static MeshRenderer Part(string name)
        {
            Transform part = Prefab().transform.Find(name);
            Assert.IsNotNull(part, $"the mask has no {name}");
            return part.GetComponent<MeshRenderer>();
        }

        private static Mesh MeshOf(string name) => Part(name).GetComponent<MeshFilter>().sharedMesh;

        [Test]
        public void ItIsWornOverTheWholeHeadAsAHelmet()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.SquirrelMask, out ItemDefinition mask));
            Assert.AreEqual("Squirrel Mask", mask.DisplayName);
            Assert.IsTrue(mask.IsWearable);
            Assert.AreEqual(EquipSlots.Helmet, mask.EquipSlot, "a hat could be worn over its ears");
        }

        [Test]
        public void ItIsWithinItsTriangleCount()
        {
            int triangles = MeshOf(SquirrelMask.FurName).triangles.Length / 3 + MeshOf(SquirrelMask.BlackName).triangles.Length / 3;
            Assert.LessOrEqual(triangles, TriangleBudget);
        }

        [Test]
        public void TheFurIsTheOwnersBrownAndTheEyesAndNoseAreBlack()
        {
            Color fur = Part(SquirrelMask.FurName).sharedMaterial.GetColor("_BaseColor");
            Assert.AreEqual("874D00", ColorUtility.ToHtmlStringRGB(fur), "the fur is not the owner's #874d00");
            Color black = Part(SquirrelMask.BlackName).sharedMaterial.GetColor("_BaseColor");
            Assert.Less(Mathf.Max(black.r, Mathf.Max(black.g, black.b)), 0.1f, "the eyes and nose are not black");
        }

        /// <summary>An eye's hollow either side of the face (+x is the wearer's right), in front, at about the head's middle height.</summary>
        [Test]
        public void TheEyesAreEitherSideOfTheFace()
        {
            Vector3[] eyes = MeshOf(SquirrelMask.BlackName).vertices.Where(v => Mathf.Abs(v.x) > 0.12f).ToArray();
            foreach (int side in new[] { -1, 1 })
            {
                Vector3[] eye = eyes.Where(v => Mathf.Sign(v.x) == side).ToArray();
                Assert.IsNotEmpty(eye, side < 0 ? "no left eye" : "no right eye");
                Vector3 middle = eye.Aggregate(Vector3.zero, (sum, v) => sum + v) / eye.Length;
                // The front half: the owner's grown eyes (their rims made black) reach round the face's side, their middles about 0.17 m forward.
                Assert.Greater(middle.z, 0.12f, "an eye is not on the front of the face");
                Assert.That(middle.y, Is.InRange(-0.08f, 0.12f), "an eye is not at the eyes' height");
            }
        }

        /// <summary>The owner's ask: the nose stands out of the fur round it, not into it as Meshy made it.</summary>
        [Test]
        public void TheNoseStandsOutOfTheSnout()
        {
            Vector3[] nose = MeshOf(SquirrelMask.BlackName).vertices.Where(v => Mathf.Abs(v.x) < 0.06f).ToArray();
            Assert.IsNotEmpty(nose, "no nose");
            float top = nose.Max(v => v.y), bottom = nose.Min(v => v.y);
            float noseFront = nose.Max(v => v.z);
            float furFront = MeshOf(SquirrelMask.FurName).vertices
                .Where(v => Mathf.Abs(v.x) < 0.1f && v.y > bottom && v.y < top + 0.03f).Max(v => v.z);
            Assert.Greater(noseFront, furFront + 0.015f, "the nose does not stand out of the fur");
            Assert.Less(top, 0.0f, "the nose is not below the eyes");
        }

        /// <summary>The fur lies outside the head (a ball round the Head bone's pivot, where it sits); only the nose's root is in the face.</summary>
        [Test]
        public void TheFurStaysOutsideTheHead()
        {
            float head = FusedBodyShape.HeadRadius;
            foreach (Vector3 vertex in MeshOf(SquirrelMask.FurName).vertices)
            {
                Assert.Greater(vertex.magnitude, head, $"the fur goes into the head at {vertex}");
            }
            Bounds fur = MeshOf(SquirrelMask.FurName).bounds;
            Assert.Less(fur.max.x - fur.min.x, head * 2.4f, "the mask stands far out from the head");
            Assert.Greater(fur.max.y, head + 0.03f, "no ears above the head");
        }

        /// <summary>Worn, it rides the Head bone at its pivot and the hair is hidden under it.</summary>
        [Test]
        public void WornItRidesTheHeadAndHidesTheHair()
        {
            var body = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            var hair = body.AddComponent<CharacterHair>();
            var mask = Object.Instantiate(Prefab()).GetComponent<SquirrelMask>();
            try
            {
                mask.OnEquip(body, ItemInstanceData.None);
                Transform head = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Assert.AreEqual(head, mask.transform.parent, "the mask is not on the head");
                Assert.AreEqual(Vector3.zero, mask.transform.localPosition);
                Assert.IsTrue(hair.IsCovered, "the hair comes out through the mask");
            }
            finally
            {
                Object.DestroyImmediate(mask.gameObject);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void OtherPlayersCanBeShownIt()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            Assert.IsNotNull(networked, "no NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.SquirrelMask);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the mask (NetworkedPlayerWiring.ItemPrefabPaths)");
            Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "the mask cannot be mounted on a copy");
        }

        [Test]
        public void TheConsoleKnowsItByName()
        {
            System.Reflection.MethodInfo alias = typeof(CityX.UI.DevConsoleView).GetMethod("AliasToItemId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(alias, "DevConsoleView.AliasToItemId is gone");
            foreach (string name in new[] { "squirrel", "squirrelmask", "squirrel_mask" })
            {
                Assert.AreEqual(ItemRegistry.SquirrelMask, alias.Invoke(null, new object[] { name }), $"/equip {name}");
            }
        }
    }
}
