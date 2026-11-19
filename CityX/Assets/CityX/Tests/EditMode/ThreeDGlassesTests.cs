using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The 3D Glasses (owner, 2026-11-19: "make a 3D Glasses model and equippable item. make it shaped like the black one but white ingame. make
    /// the blue and red part's transparent"): worn on the face beside any hat, small, a white frame with red and blue lenses seen through, the red
    /// before the left eye and the blue before the right, all of it outside the head with the arms reaching round to behind its widest, and shown
    /// to other players.
    /// </summary>
    public sealed class ThreeDGlassesTests
    {
        private const string PrefabPath = "Assets/CityX/Wearables/ThreeDGlasses.prefab";

        /// <summary>The most triangles the glasses may have, all parts: well under a hat's.</summary>
        private const int TriangleBudget = 3000;

        private static GameObject Prefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no ThreeDGlasses.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsNotNull(prefab.GetComponent<ThreeDGlasses>());
            return prefab;
        }

        private static MeshRenderer Part(string name)
        {
            Transform part = Prefab().transform.Find(name);
            Assert.IsNotNull(part, $"the glasses have no {name}");
            return part.GetComponent<MeshRenderer>();
        }

        private static Mesh MeshOf(string name) => Part(name).GetComponent<MeshFilter>().sharedMesh;

        [Test]
        public void TheGlassesAreWornOnTheFaceBesideAHat()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.ThreeDGlasses, out ItemDefinition glasses));
            Assert.AreEqual("3D Glasses", glasses.DisplayName);
            Assert.IsTrue(glasses.IsWearable);
            Assert.AreEqual(EquipSlots.Mask, glasses.EquipSlot, "the glasses are not worn on the face");
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.FarmerHat, out ItemDefinition hat));
            Assert.AreNotEqual(hat.EquipSlot, glasses.EquipSlot, "putting the glasses on takes a hat off");
        }

        [Test]
        public void TheyAreSmall()
        {
            int triangles = 0;
            foreach (string part in new[] { ThreeDGlasses.FrameName, ThreeDGlasses.RedLensName, ThreeDGlasses.BlueLensName })
            {
                triangles += MeshOf(part).triangles.Length / 3;
            }
            Assert.LessOrEqual(triangles, TriangleBudget);
        }

        /// <summary>The frame white and solid, the lenses red and blue film the face shows through (drawn after the cel look's black lines).</summary>
        [Test]
        public void TheFrameIsWhiteAndTheLensesAreSeenThrough()
        {
            Material frame = Part(ThreeDGlasses.FrameName).sharedMaterial;
            Color white = frame.GetColor("_BaseColor");
            Assert.Greater(Mathf.Min(white.r, Mathf.Min(white.g, white.b)), 0.9f, "the frame is not white");
            Assert.Less(frame.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent, "the frame is see-through");

            Color red = Part(ThreeDGlasses.RedLensName).sharedMaterial.GetColor("_BaseColor");
            Color blue = Part(ThreeDGlasses.BlueLensName).sharedMaterial.GetColor("_BaseColor");
            Assert.Greater(red.r, Mathf.Max(red.g, red.b) + 0.5f, "the red lens is not red");
            Assert.Greater(blue.b, Mathf.Max(blue.r, blue.g) + 0.5f, "the blue lens is not blue");
            foreach (string lens in new[] { ThreeDGlasses.RedLensName, ThreeDGlasses.BlueLensName })
            {
                Material film = Part(lens).sharedMaterial;
                Assert.That(film.GetColor("_BaseColor").a, Is.InRange(0.2f, 0.8f), $"{lens} cannot be seen through");
                Assert.GreaterOrEqual(film.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent, $"{lens} is drawn solid");
                Assert.IsTrue(film.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), $"{lens} is drawn solid");
            }
        }

        /// <summary>As real ones are, and as the owner's pictures show: red before the left eye, blue before the right (+x is the wearer's right).</summary>
        [Test]
        public void RedIsBeforeTheLeftEyeAndBlueBeforeTheRight()
        {
            Bounds red = MeshOf(ThreeDGlasses.RedLensName).bounds;
            Bounds blue = MeshOf(ThreeDGlasses.BlueLensName).bounds;
            Assert.Less(red.center.x, -0.05f, "the red lens is not before the left eye");
            Assert.Greater(blue.center.x, 0.05f, "the blue lens is not before the right eye");
            foreach (Bounds lens in new[] { red, blue })
            {
                Assert.Greater(lens.center.z, FusedBodyShape.HeadRadius, "a lens is not in front of the face");
                Assert.That(lens.center.y, Is.InRange(-0.05f, 0.15f), "a lens is not at the eyes' height");
            }
        }

        /// <summary>Nothing of them inside the head (a ball round the Head bone's pivot, where they sit); the arms reach round it to behind its widest.</summary>
        [Test]
        public void TheyStayOutsideTheHeadAndTheArmsReachRoundIt()
        {
            float head = FusedBodyShape.HeadRadius;
            foreach (string part in new[] { ThreeDGlasses.FrameName, ThreeDGlasses.RedLensName, ThreeDGlasses.BlueLensName })
            {
                foreach (Vector3 vertex in MeshOf(part).vertices)
                {
                    Assert.Greater(vertex.magnitude, head, $"{part} goes into the head at {vertex}");
                }
            }
            Bounds frame = MeshOf(ThreeDGlasses.FrameName).bounds;
            Assert.Less(frame.min.z, 0.0f, "the arms stop short of the head's sides");
            Assert.Greater(frame.max.x, head, "the arms do not go round the head");
            Assert.Less(frame.min.x, -head, "the arms do not go round the head");
            Assert.Less(frame.max.x - frame.min.x, head * 2.4f, "the glasses stand far out from the head");
        }

        /// <summary>Worn, they ride the Head bone at its pivot, and the hair stays (glasses do not cover it).</summary>
        [Test]
        public void WornTheyRideTheHeadAndLeaveTheHair()
        {
            var body = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            var hair = body.AddComponent<CharacterHair>();
            var glasses = Object.Instantiate(Prefab()).GetComponent<ThreeDGlasses>();
            try
            {
                glasses.OnEquip(body, ItemInstanceData.None);
                Transform head = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Assert.AreEqual(head, glasses.transform.parent, "the glasses are not on the head");
                Assert.AreEqual(Vector3.zero, glasses.transform.localPosition);
                Assert.IsFalse(hair.IsCovered, "the glasses took the hair away");
            }
            finally
            {
                Object.DestroyImmediate(glasses.gameObject);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void OtherPlayersCanBeShownTheGlasses()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            Assert.IsNotNull(networked, "no NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.ThreeDGlasses);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the glasses (NetworkedPlayerWiring.ItemPrefabPaths)");
            Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "the glasses cannot be mounted on a copy");
        }
    }
}
