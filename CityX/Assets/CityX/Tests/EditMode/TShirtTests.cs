using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The T-Shirt (owner, 2026-10-19/20: "a wearable top called T-Shirt ... flatten out the lump and wrinks at the front so we can cleanly put
    /// a various textures and logos on here ... lower the face count"; "detatch the sleeve from the body"): a top in the registry, within its
    /// budget, worn from the shoulders to the hips with short sleeves, skinned only to what a shirt covers, its sleeves sewn to its body
    /// along seams that never open, and laid out so a design's front lands on the chest and its back on the back. The Long Sleeve T-Shirt
    /// (owner, 2026-10-22: "make a longsleeve version of the tshirt both black and white") is held to all of it, its sleeves down to the wrists.
    /// </summary>
    [TestFixture("TShirt", ItemRegistry.TShirt, "T-Shirt", 8000, false)]
    [TestFixture("LongSleeveTShirt", ItemRegistry.LongSleeveTShirt, "Long Sleeve T-Shirt", 10000, true)]
    public sealed class TShirtTests
    {
        private readonly string _prefab;
        private readonly string _itemId;
        private readonly string _displayName;
        private readonly int _triangleBudget;
        private readonly bool _longSleeves;

        public TShirtTests(string prefab, string itemId, string displayName, int triangleBudget, bool longSleeves)
        {
            _prefab = prefab;
            _itemId = itemId;
            _displayName = displayName;
            _triangleBudget = triangleBudget;
            _longSleeves = longSleeves;
        }

        /// <summary>A long sleeve's cuff lies no further than this up the forearm from the wrist (tshirt_build.py's LONG_CUFF: 3 cm along the arm).</summary>
        private const float LongCuffAboveWrist = 0.06f;

        /// <summary>Where on the template each piece lies (tshirt_build.py's PIECE_BOXES): the front on the left of the top, the back on the right.</summary>
        private static readonly Rect FrontPiece = ShirtPrintLayout.FrontPiece;
        private static readonly Rect BackPiece = ShirtPrintLayout.BackPiece;

        /// <summary>The middle of the chest, bind pose (+z ahead).</summary>
        private const float ChestHeight = 1.62f;

        /// <summary>Bind pose: the arms lie further out to the sides than this (the torso within 0.27), and a sleeve sits nearer them.</summary>
        private const float ArmFromX = 0.31f;
        private const float SleeveOffArm = 0.05f;

        /// <summary>How far up inside a cuff the arm may be drawn on, behind its lip (the shirt draws it 1.5 cm up).</summary>
        private const float InsideACuff = 0.025f;

        /// <summary>
        /// The sleeves lie below this on the template (tshirt_build.py's PIECE_BOXES); a ring round one is taken at this height, this far up
        /// and down, and its points are never this many degrees apart round the arm (they are at most 16 apart; across the flat side, 57).
        /// </summary>
        private const float SleevePieces = 0.4f;
        private const float SleeveRingHeight = 1.6f;
        private const float SleeveRingDepth = 0.02f;
        private const float SleeveGap = 45.0f;

        private SkinnedGarment Shirt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{_prefab}.prefab");
            Assert.IsNotNull(prefab, $"no {_prefab}.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var shirt = prefab.GetComponent<TShirt>();
            Assert.IsNotNull(shirt.GarmentMesh, "the shirt has no mesh");
            return shirt;
        }

        [Test]
        public void TheShirtIsATopCalledByItsName()
        {
            Assert.IsTrue(ItemRegistry.TryGet(_itemId, out ItemDefinition shirt));
            Assert.AreEqual(_displayName, shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
            Assert.AreEqual(_itemId, Shirt().ItemId);
        }

        [Test]
        public void TheShirtIsWithinItsBudgetAndHidesTheBodyItCovers()
        {
            SkinnedGarment shirt = Shirt();
            Assert.LessOrEqual(shirt.GarmentMesh.triangles.Length / 3, _triangleBudget);
            Assert.AreEqual(shirt.GarmentMesh.vertexCount, shirt.GarmentMesh.uv.Length, "the shirt has no texture coordinates for a design");
            Assert.IsNotNull(shirt.HiddenBodyTriangles, "the shirt hides none of the body under it");
            Assert.AreEqual((shirt.HiddenFromBodyTriangles + 7) / 8, shirt.HiddenBodyTriangles.Length);
        }

        [Test]
        public void TheShirtFollowsOnlyWhatAShirtCoversAndEveryWeightAddsUp()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Assert.AreEqual(SkinnedGarment.Bones.Length + GarmentHelperBones.Helpers.Length, mesh.bindposes.Length,
                "the shirt is not bound to the body's bones and the helper bones (shoulders, collar)");
            var never = new System.Collections.Generic.List<int>
            {
                Bone(HumanBodyBones.Head), Bone(HumanBodyBones.LeftHand), Bone(HumanBodyBones.RightHand),
                Bone(HumanBodyBones.LeftUpperLeg), Bone(HumanBodyBones.RightUpperLeg), Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.RightLowerLeg),
                HelperIndex("skirt_front_l"), HelperIndex("skirt_back_l"), HelperIndex("skirt_front_r"), HelperIndex("skirt_back_r"),
            };
            foreach (BoneWeight w in mesh.boneWeights)
            {
                Assert.AreEqual(1.0f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 0.01f);
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.001f)
                    {
                        CollectionAssert.DoesNotContain(never, bone, "part of the shirt follows the head, a hand, a leg or a skirt bone");
                    }
                }
            }
        }

        /// <summary>
        /// Owner, 2026-10-20: "the shirt stretches around the armpits please detatch the sleeve from the body". Every point is either on a
        /// sleeve, following its arm (the upper arm, the forearm near the cuff and, round the armhole, that side's half-turning shoulder bone)
        /// and nothing of the torso, or on the shirt's body, following no arm: nothing is blended between the torso and an arm to stretch.
        /// </summary>
        [Test]
        public void EachSleeveFollowsItsArmAndTheBodyNoArm()
        {
            Mesh mesh = Shirt().GarmentMesh;
            int[] left = { Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), HelperIndex("shoulder_l") };
            int[] right = { Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), HelperIndex("shoulder_r") };
            var arms = new System.Collections.Generic.HashSet<int>
            {
                Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand),
                Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand),
            };
            int onSleeves = 0;
            foreach (BoneWeight w in mesh.boneWeights)
            {
                var used = new System.Collections.Generic.List<int>();
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.001f)
                    {
                        used.Add(bone);
                    }
                }
                if (!used.Exists(arms.Contains))
                {
                    continue;   // the shirt's body
                }
                onSleeves++;
                Assert.IsTrue(used.TrueForAll(b => System.Array.IndexOf(left, b) >= 0) || used.TrueForAll(b => System.Array.IndexOf(right, b) >= 0),
                    "a point of the shirt is shared between the torso and an arm (it stretches)");
            }
            Assert.Greater(onSleeves, mesh.vertexCount / 8, "the sleeves do not follow the arms");
        }

        /// <summary>
        /// The texture's pieces are cut apart (the sleeves from the shirt's body at the armholes among them) by points doubled along their
        /// seams: every two at one place move alike, so no seam ever opens (skinned apart, a swinging arm once pulled the armhole 25 cm open
        /// and the arm showed through the slit at rest).
        /// </summary>
        [Test]
        public void NoSeamEverOpens()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            var first = new System.Collections.Generic.Dictionary<Vector3, int>();
            int doubled = 0;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!first.TryGetValue(vertices[v], out int other))
                {
                    first[vertices[v]] = v;
                    continue;
                }
                doubled++;
                Assert.IsTrue(Alike(weights[v], weights[other]), $"the shirt's seam at {vertices[v]:F3} (bind pose) opens: its two sides are skinned apart");
            }
            Assert.Greater(doubled, 100, "the shirt has no seams: the texture's pieces are not apart");
        }

        private static bool Alike(BoneWeight a, BoneWeight b)
        {
            var share = new System.Collections.Generic.Dictionary<int, float>();
            foreach ((int bone, float weight) in new[] { (a.boneIndex0, a.weight0), (a.boneIndex1, a.weight1), (a.boneIndex2, a.weight2), (a.boneIndex3, a.weight3) })
            {
                share[bone] = share.TryGetValue(bone, out float s) ? s + weight : weight;
            }
            foreach ((int bone, float weight) in new[] { (b.boneIndex0, b.weight0), (b.boneIndex1, b.weight1), (b.boneIndex2, b.weight2), (b.boneIndex3, b.weight3) })
            {
                share[bone] = (share.TryGetValue(bone, out float s) ? s : 0.0f) - weight;
            }
            foreach (float difference in share.Values)
            {
                if (Mathf.Abs(difference) > 0.002f)
                {
                    return false;
                }
            }
            return true;
        }

        private static int Bone(HumanBodyBones bone)
        {
            return System.Array.IndexOf(SkinnedGarment.Bones, bone);
        }

        private static int HelperIndex(string name)
        {
            return SkinnedGarment.Bones.Length + System.Array.FindIndex(GarmentHelperBones.Helpers, h => h.name == name);
        }

        [Test]
        public void TheShirtCoversTheShouldersToTheHipsAndItsSleevesEndWhereTheyShould()
        {
            Bounds bounds = Shirt().GarmentMesh.bounds;
            Assert.Greater(bounds.max.y, BodyRig.ShoulderY, "the shirt does not reach the shoulders");
            Assert.Less(bounds.max.y, BodyRig.ShoulderY + 0.15f, "the shirt's collar climbs up the neck");
            Assert.Less(bounds.min.y, BodyRig.HipY + 0.1f, "the shirt does not reach the hips");
            Assert.Greater(bounds.min.y, BodyRig.HipY - 0.1f, "the shirt hangs down the thighs");
            Assert.Greater(bounds.max.x, BodyRig.ShoulderX + 0.1f, "no sleeve reaches out along the arm");
            Assert.Greater(bounds.min.y, 0.0f);
            float lowestSleeve = float.MaxValue;
            foreach (Vector3 v in Shirt().GarmentMesh.vertices)
            {
                if (Mathf.Abs(v.x) > BodyRig.ShoulderX + 0.05f)
                {
                    lowestSleeve = Mathf.Min(lowestSleeve, v.y);
                }
            }
            float elbow = BodyRig.ShoulderY + BodyRig.ElbowT * (BodyRig.HandY - BodyRig.ShoulderY);
            float wrist = BodyRig.ShoulderY + BodyRig.WristT * (BodyRig.HandY - BodyRig.ShoulderY);
            if (_longSleeves)
            {
                Assert.Less(lowestSleeve, wrist + LongCuffAboveWrist, "a long sleeve stops short of the wrist");
                Assert.Greater(lowestSleeve, wrist, "a long sleeve reaches down over the hand");
            }
            else
            {
                Assert.Greater(lowestSleeve, elbow, "a sleeve reaches down past the elbow: it is a short-sleeved shirt");
            }
        }

        /// <summary>
        /// Owner, 2026-10-20, of the skin at the back of a cuff: "the sleeve is clipping through the arm a bit here ... make the parts of the
        /// body being covered invisible". No skin the shirt leaves drawn out at the sides lies under a sleeve (straight out from it, the shirt
        /// nearer than a sleeve ever sits off the arm) but just inside a cuff, where a little further down the arm it is bare.
        /// </summary>
        [Test]
        public void NoArmSkinIsDrawnInsideASleeve()
        {
            var under = DrawnSkinWhere((centre, normal) => Mathf.Abs(centre.x) > ArmFromX &&
                Physics.Raycast(centre + normal * 0.002f, normal, SleeveOffArm) &&
                Physics.Raycast(centre + Vector3.down * InsideACuff + normal * 0.002f, normal, SleeveOffArm));
            Assert.IsEmpty(under, $"{under.Count} drawn triangles of the arms lie under a sleeve, the first at {(under.Count > 0 ? under[0] : Vector3.zero):F3}");
        }

        /// <summary>The torso's skin between these heights (bind pose) lies under the shirt's body: above the hem, below the armpits and the collar.</summary>
        private const float TorsoFrom = BodyRig.HipY + 0.15f;
        private const float TorsoTo = 1.7f;
        private const float TorsoHalfWidth = 0.22f;

        /// <summary>The shirt's body stands off the torso no further than this anywhere (1.5 cm at the least).</summary>
        private const float ClothOffTorso = 0.1f;

        /// <summary>
        /// Owner, 2026-11-19, of the Blood Raven shirt's back in a sword swing: "some sword animations have a small bit of the players back
        /// clipping through the shirt". No torso skin is drawn under the shirt's body: a band round the waist, where the skin is shared between
        /// the hips and the spine, was neither the spine's to hide always nor enough the hips' to hide where covered, and a twisting swing pushed
        /// it through the cloth.
        /// </summary>
        [Test]
        public void NoTorsoSkinIsDrawnUnderTheShirtsBody()
        {
            var under = DrawnSkinWhere((centre, normal) => Mathf.Abs(centre.x) < TorsoHalfWidth && centre.y > TorsoFrom && centre.y < TorsoTo &&
                Physics.Raycast(centre + normal * 0.002f, normal, ClothOffTorso));
            float lowest = float.MaxValue, highest = float.MinValue;
            under.ForEach(p => { lowest = Mathf.Min(lowest, p.y); highest = Mathf.Max(highest, p.y); });
            Assert.IsEmpty(under, $"{under.Count} drawn triangles of the torso lie under the shirt, from {lowest:F3} to {highest:F3} m up");
        }

        /// <summary>The centres of the body's triangles the shirt leaves drawn that <paramref name="under"/> (centre, normal; bind pose, the shirt's collider in place) accepts.</summary>
        private System.Collections.Generic.List<Vector3> DrawnSkinWhere(System.Func<Vector3, Vector3, bool> under)
        {
            SkinnedGarment shirt = Shirt();
            var body = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Assert.AreEqual(shirt.HiddenFromBodyTriangles, body.triangles.Length / 3, "the shirt's hidden skin was made for another body");
            var cover = new GameObject("ShirtCover");
            bool hitBackfaces = Physics.queriesHitBackfaces;
            var found = new System.Collections.Generic.List<Vector3>();
            try
            {
                cover.AddComponent<MeshCollider>().sharedMesh = shirt.GarmentMesh;
                Physics.SyncTransforms();
                Physics.queriesHitBackfaces = true;
                Vector3[] vertices = body.vertices;
                int[] triangles = body.triangles;
                for (int t = 0; t < triangles.Length / 3; t++)
                {
                    if ((shirt.HiddenBodyTriangles[t / 8] & (1 << (t % 8))) != 0)
                    {
                        continue;
                    }
                    Vector3 a = vertices[triangles[t * 3]], b = vertices[triangles[t * 3 + 1]], c = vertices[triangles[t * 3 + 2]];
                    Vector3 centre = (a + b + c) / 3.0f;
                    if (under(centre, Vector3.Cross(b - a, c - a).normalized))
                    {
                        found.Add(centre);
                    }
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
                Object.DestroyImmediate(cover);
            }
            return found;
        }

        /// <summary>
        /// Owner, 2026-10-20: "it like rolls in at the side on both sleeves, make it shaped like a real shirt". Round each sleeve, partway down
        /// it, its points are never far apart: a quarter of it was one flat face, cutting into the arm.
        /// </summary>
        [Test]
        public void EachSleeveGoesRoundTheArmWithNoFlatSide()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            foreach (float side in new[] { -1.0f, 1.0f })
            {
                var ring = new System.Collections.Generic.List<Vector3>();
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (uvs[v].y < SleevePieces && vertices[v].x * side > 0.0f && Mathf.Abs(vertices[v].y - SleeveRingHeight) < SleeveRingDepth)
                    {
                        ring.Add(vertices[v]);
                    }
                }
                Vector3 middle = Vector3.zero;
                ring.ForEach(p => middle += p / ring.Count);
                var turns = ring.ConvertAll(p => Mathf.Atan2(p.z - middle.z, p.x - middle.x) * Mathf.Rad2Deg);
                turns.Sort();
                float widest = 360.0f + turns[0] - turns[turns.Count - 1];
                for (int i = 1; i < turns.Count; i++)
                {
                    widest = Mathf.Max(widest, turns[i] - turns[i - 1]);
                }
                Assert.Less(widest, SleeveGap, $"the sleeve on the {(side > 0.0f ? "+x" : "-x")} side has no cloth across {widest:0} degrees round the arm");
            }
        }

        [Test]
        public void ADesignsFrontLandsOnTheChestAndItsBackOnTheBack()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int front = -1, back = -1;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (Mathf.Abs(v.x) > 0.03f || Mathf.Abs(v.y - ChestHeight) > 0.05f)
                {
                    continue;
                }
                if (v.z > 0.0f && (front < 0 || v.z > vertices[front].z))
                {
                    front = i;
                }
                if (v.z < 0.0f && (back < 0 || v.z < vertices[back].z))
                {
                    back = i;
                }
            }
            Assert.GreaterOrEqual(front, 0, "no point of the shirt on the middle of the chest");
            Assert.GreaterOrEqual(back, 0, "no point of the shirt on the middle of the back");
            Assert.IsTrue(FrontPiece.Contains(uvs[front]), $"the chest is at {uvs[front]} on the texture, not on its front piece");
            Assert.IsTrue(BackPiece.Contains(uvs[back]), $"the back is at {uvs[back]} on the texture, not on its back piece");
            // Upright and the right way round: higher on the chest is higher on the texture, and the wearer's left (-x) is a viewer's right.
            int above = Nearest(vertices, new Vector3(0.0f, ChestHeight + 0.2f, vertices[front].z));
            int left = Nearest(vertices, new Vector3(-0.15f, ChestHeight, vertices[front].z));
            Assert.Greater(uvs[above].y, uvs[front].y, "the front is upside down on the texture");
            Assert.Greater(uvs[left].x, uvs[front].x, "the front is mirrored on the texture: a logo's writing would read backwards");
        }

        private static int Nearest(Vector3[] vertices, Vector3 to)
        {
            int best = 0;
            for (int i = 1; i < vertices.Length; i++)
            {
                if ((vertices[i] - to).sqrMagnitude < (vertices[best] - to).sqrMagnitude)
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
