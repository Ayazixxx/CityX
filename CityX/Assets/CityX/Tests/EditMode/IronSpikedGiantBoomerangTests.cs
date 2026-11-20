using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Iron Spiked Giant Boomerang (owner, 2026-11-19, with Meshy's "Twin Fang Crescent": "make this another giant boomerang weapon called "Iron Spiked
    /// Giant Boomerang" make it the roughly the same size as the other giant boomerang and shrink the y axis to roughly the same size as the giant boomerang
    /// as well so. make this a very rare item and adjust the damage how you think would be best", "also lower the face count to a reasonable amount"): its
    /// item, its rarity, its blow, and the built model's size, thickness, faces, map and grip against the bone one's.
    /// </summary>
    public sealed class IronSpikedGiantBoomerangTests
    {
        private const string HeldPath = "Assets/CityX/Weapons/IronSpikedGiantBoomerang.prefab";
        private const string ThrownPath = "Assets/CityX/Weapons/ThrownIronSpikedGiantBoomerang.prefab";
        private const string BonePath = "Assets/CityX/Weapons/GiantBoomerang.prefab";
        private const string BoneThrownPath = "Assets/CityX/Weapons/ThrownGiantBoomerang.prefab";

        /// <summary>A weapon's triangles at most for "fine" (tools/model_qa.py), and the map's side at most (past it the check warns).</summary>
        private const int TriangleBudget = 6000;
        private const int MapSide = 1024;

        /// <summary>
        /// "The same size" as the bone one (owner, 2026-11-19, after a first fit tip to tip: "scale it down slightly so its the same size as the other giant
        /// boomerang i feel like it a bit bigger"): its arms, elbow to each end, as long as the bone one's, within this; as thin as its blade with its wraps.
        /// </summary>
        private const float ArmsSlack = 0.15f;
        private const float ThicknessSlack = 0.03f;

        /// <summary>The hand closes on the model: some of it lies this near the grip.</summary>
        private const float GripOnTheModel = 0.06f;

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- rebuild the scene");
            return prefab;
        }

        [Test]
        public void ItIsAVeryRareGiantBoomerangCalledByItsName()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.IronSpikedGiantBoomerang, out ItemDefinition item));
            Assert.AreEqual("Iron Spiked Giant Boomerang", item.DisplayName);
            Assert.AreEqual(EquipSlots.Primary, item.EquipSlot);
            Assert.AreEqual(Rarity.VeryRare, ItemRarity.Of(ItemRegistry.IronSpikedGiantBoomerang));
            var held = Load(HeldPath).GetComponent<GiantBoomerangItem>();
            Assert.IsNotNull(held, "it is not a giant boomerang");
            Assert.AreEqual(GiantBoomerangModel.IronSpiked, held.Model);
            Assert.AreEqual(ItemRegistry.IronSpikedGiantBoomerang, held.ItemId);
            Assert.AreEqual(ItemRegistry.GiantBoomerang, Load(BonePath).GetComponent<GiantBoomerangItem>().ItemId, "the bone one changed");
        }

        /// <summary>Owner, 2026-11-19, with GiantMetalBoomerangImpact.mp3: "make this the noise it plays when it lands on an enemy ... or terrain for now".</summary>
        [Test]
        public void ItStrikesWithTheOwnersImpactSoundAndTheBoneOneKeepsItsOwn()
        {
            AudioClip impact = Load(ThrownPath).GetComponent<ThrownBoomerang>().ImpactSound;
            Assert.IsNotNull(impact, "the iron spiked one has no impact sound");
            Assert.AreEqual(CityX.WeaponsEditor.BoomerangBuilder.IronSpikedImpactSoundPath, AssetDatabase.GetAssetPath(impact));
            Assert.Greater(impact.length, 0.05f, "the impact sound is empty");
            Assert.IsNull(Load(BoneThrownPath).GetComponent<ThrownBoomerang>().ImpactSound, "the bone one lost its own bang");
        }

        [Test]
        public void ItHitsHarderThanTheBoneOneAndTwoThrowsDropALowLevelEnemy()
        {
            Assert.Greater(GiantBoomerangRules.DamageOf(GiantBoomerangModel.IronSpiked), GiantBoomerangRules.DamageOf(GiantBoomerangModel.Bone));
            Assert.AreEqual(GiantBoomerangRules.Damage, GiantBoomerangRules.DamageOf(GiantBoomerangModel.Bone), "the bone one's blow changed");
            Assert.GreaterOrEqual(2.0f * GiantBoomerangRules.IronSpikedDamage, CityX.Character.PlayerVitals.BaseMaxHealth);
        }

        [Test]
        public void ItIsAsBigAndAsThinAsTheBoneOneWithAReasonableFaceCountAndASmallMap()
        {
            Mesh mesh = Load(ThrownPath).GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.IsTrue(Load(ThrownPath).GetComponent<ThrownBoomerang>().Giant, "it does not fly as a giant one");
            Assert.LessOrEqual(mesh.triangles.Length / 3, TriangleBudget);
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "it has no texture coordinates for its paint");
            Vector3[] vertices = mesh.vertices;
            // It spins about its middle: the middle of its area (a crescent's is not the middle of the box round it).
            int[] triangles = mesh.triangles;
            Vector3 middle = Vector3.zero;
            float area = 0.0f;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                float size = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                middle += (a + b + c) / 3.0f * size;
                area += size;
            }
            middle /= area;
            Assert.Less(new Vector2(middle.x, middle.z).magnitude, 0.05f, "it does not spin about its middle");

            Assert.AreEqual(Arms(Load(BonePath).GetComponent<GiantBoomerangItem>()), Arms(Load(HeldPath).GetComponent<GiantBoomerangItem>()), ArmsSlack,
                "its arms are not as long as the Giant Boomerang's");
            float boneThick = Load(BoneThrownPath).GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y;
            Assert.AreEqual(boneThick, mesh.bounds.size.y, ThicknessSlack + 0.02f, "it is not about as thin as the Giant Boomerang");

            var material = Load(HeldPath).GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var map = material.GetTexture("_BaseMap");
            Assert.IsNotNull(map, "its paint is missing");
            Assert.LessOrEqual(Mathf.Max(map.width, map.height), MapSide);
        }

        /// <summary>
        /// In the hand, held on its wrap: the hand's grip on the model itself, the length from the hand toward the elbow. On the back, where the bone one
        /// lies, turned over (owner, 2026-11-19: "flip it so its facing the other way so the spikes dont block your view when playing", "i want it like the
        /// same position as the giant boomerang on back just flipped so the spikes are facing the other way"): its tip-to-tip line along the bone one's,
        /// its middle (what it spins about) on the bone one's, its elbow on the other side of that line from the bone one's, so its hollow and its spikes
        /// face the other way.
        /// </summary>
        [Test]
        public void ItIsHeldOnItsWrapAndOnTheBackLiesWhereTheBoneOneDoesTurnedOver()
        {
            var held = Load(HeldPath).GetComponent<GiantBoomerangItem>();
            Transform model = held.transform.Find(GiantBoomerangItem.ModelName);
            Mesh mesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
            float nearest = float.MaxValue;
            foreach (Vector3 v in mesh.vertices)
            {
                nearest = Mathf.Min(nearest, (model.localPosition + model.localRotation * v).magnitude);
            }
            Assert.Less(nearest, GripOnTheModel, "the grip is not on the model: the hand would close on air");

            var bone = Load(BonePath).GetComponent<GiantBoomerangItem>();
            Assert.IsFalse(bone.HasBackPose, "the bone one no longer lies on the back where the hand holds it");
            Assert.IsTrue(held.HasBackPose, "it lies on the back where the hand holds it, not where the bone one lies");
            Outline(bone, out Vector2 boneNear, out Vector2 boneFar, out Vector2 boneElbow);
            Outline(held, out Vector2 near, out Vector2 far, out Vector2 elbow);
            Assert.Less(Vector2.Angle(far - near, boneFar - boneNear), 2.0f, "on the back it does not lie along the bone one's line");
            // Its model's origin is its middle (what it spins about), so where the model lies is where its middle is.
            // Flat on the back as the bone one lies (owner, 2026-11-20, of it leant off the back: "now its floating on his back").
            Vector3 boneMiddle = bone.transform.Find(GiantBoomerangItem.ModelName).localPosition;
            Assert.Less(Vector3.Distance(held.BackModelPosition, boneMiddle), 0.03f, "on the back its middle is not where the bone one's is");
            Assert.Greater(Mathf.Abs(Vector3.Dot(held.BackModelRotation * Vector3.up, Vector3.up)), Mathf.Cos(1.0f * Mathf.Deg2Rad),
                "on the back it does not lie flat against the back: it leans off it");
            Assert.AreEqual(-Mathf.Sign(Side(boneNear, boneFar, boneElbow)), Mathf.Sign(Side(near, far, elbow)),
                "on the back its spikes face the bone one's way (toward the camera), not the other way");
        }

        /// <summary>
        /// It hangs on the back where the owner set it by hand in the editor (2026-11-20: "this is the new positioning i want for the boomerang"), upside down
        /// across the back; the bone one where it always has.
        /// </summary>
        [Test]
        public void ItHangsOnTheBackWhereTheOwnerSetIt()
        {
            var held = Load(HeldPath).GetComponent<GiantBoomerangItem>();
            Assert.IsTrue(held.HasOwnHang, "it hangs where the bone one does");
            Assert.Less(Vector3.Distance(new Vector3(-0.92f, 0.15f, -0.36f), held.HangPosition), 0.001f, "it does not hang where the owner set it");
            Assert.Less(Quaternion.Angle(Quaternion.Euler(516.5f, -90.0f, 90.0f), Quaternion.Euler(held.HangEuler)), 0.1f, "it is not turned as the owner set it");
            Assert.IsFalse(Load(BonePath).GetComponent<GiantBoomerangItem>().HasOwnHang, "the bone one no longer hangs where it always has");
        }

        /// <summary>Of its faces on its flat sides, at most this share may look into it (a crease of the wraps), not out of it.</summary>
        private const float InwardFacesAtMost = 0.05f;

        /// <summary>
        /// Its faces look out of it (owner, 2026-11-20, of it inside out: "its like transparent on one side but i can see the texture on the other side"):
        /// only a face's front is drawn, so with its faces looking in, the near side of it was not drawn and its far side was seen from inside.
        /// </summary>
        [Test]
        public void ItsFacesLookOutOfItSoItIsNotSeenThrough()
        {
            Mesh mesh = Load(HeldPath).GetComponentInChildren<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            int outward = 0, inward = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a).normalized;
                // Only the flat sides (it lies flat, its thin y out of its faces): which way one looks is plain there.
                if (Mathf.Abs(face.y) < 0.5f) continue;
                if (Mathf.Sign(face.y) == Mathf.Sign(a.y + b.y + c.y)) outward++;
                else inward++;
            }
            Assert.Greater(outward, 0, "it has no flat sides");
            Assert.LessOrEqual((float)inward / (inward + outward), InwardFacesAtMost, $"{inward} of its faces look into it, {outward} out of it: it is inside out");
        }

        /// <summary>Its outline as it lies on the back (BoomerangBuilder): the near tip, the far tip and the elbow, across and along its carried frame.</summary>
        private static void Outline(GiantBoomerangItem item, out Vector2 near, out Vector2 far, out Vector2 elbow)
        {
            far = new Vector2(item.OutlineTipAndElbow.x, item.OutlineTipAndElbow.y);
            elbow = new Vector2(item.OutlineTipAndElbow.z, item.OutlineTipAndElbow.w);
            near = new Vector2(item.OutlineTipAndWidths.x, item.OutlineTipAndWidths.y);
        }

        /// <summary>Which side of the line from <paramref name="a"/> to <paramref name="b"/> <paramref name="p"/> is on.</summary>
        private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        /// <summary>A giant boomerang's two arms, elbow to each tip, from its outline in the carried frame (BoomerangBuilder).</summary>
        private static float Arms(GiantBoomerangItem item)
        {
            var farTip = new Vector2(item.OutlineTipAndElbow.x, item.OutlineTipAndElbow.y);
            var elbow = new Vector2(item.OutlineTipAndElbow.z, item.OutlineTipAndElbow.w);
            var nearTip = new Vector2(item.OutlineTipAndWidths.x, item.OutlineTipAndWidths.y);
            return Vector2.Distance(farTip, elbow) + Vector2.Distance(nearTip, elbow);
        }
    }
}
