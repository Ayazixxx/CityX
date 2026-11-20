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

        /// <summary>The bone one's span tip to tip (BoomerangBuilder.GiantShape) and its blade with its wraps; "roughly the same": within these.</summary>
        private const float BoneSpan = 2.6f;
        private const float SpanSlack = 0.1f;
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

            float span = 0.0f;
            foreach (Vector3 a in Outermost(vertices))
            {
                foreach (Vector3 b in Outermost(vertices))
                {
                    span = Mathf.Max(span, Vector3.Distance(new Vector3(a.x, 0.0f, a.z), new Vector3(b.x, 0.0f, b.z)));
                }
            }
            Assert.AreEqual(BoneSpan, span, SpanSlack, "it is not as big as the Giant Boomerang tip to tip");
            float boneThick = Load(BoneThrownPath).GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y;
            Assert.AreEqual(boneThick, mesh.bounds.size.y, ThicknessSlack + 0.02f, "it is not about as thin as the Giant Boomerang");

            var material = Load(HeldPath).GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var map = material.GetTexture("_BaseMap");
            Assert.IsNotNull(map, "its paint is missing");
            Assert.LessOrEqual(Mathf.Max(map.width, map.height), MapSide);
        }

        /// <summary>
        /// Carried as the bone one is: the hand's grip on the model itself, the length from the hand toward the elbow, and the other arm off to the same side
        /// as the bone one's (its back carry, draw and throw were posed for that).
        /// </summary>
        [Test]
        public void ItIsHeldOnItsWrapAndItsOtherArmGoesTheBoneOnesWay()
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
            Assert.Greater(held.OutlineTipAndElbow.w, 0.5f, "the elbow is not ahead of the hand, along the held arm");
            var bone = Load(BonePath).GetComponent<GiantBoomerangItem>();
            Assert.AreEqual(Mathf.Sign(bone.OutlineTipAndElbow.x), Mathf.Sign(held.OutlineTipAndElbow.x), "its other arm goes off to the other side");
        }

        private static System.Collections.Generic.IEnumerable<Vector3> Outermost(Vector3[] points)
        {
            for (int k = 0; k < 64; k++)
            {
                float a = k * Mathf.PI * 2.0f / 64.0f;
                var direction = new Vector3(Mathf.Cos(a), 0.0f, Mathf.Sin(a));
                Vector3 best = points[0];
                foreach (Vector3 p in points)
                {
                    if (Vector3.Dot(p, direction) > Vector3.Dot(best, direction)) best = p;
                }
                yield return best;
            }
        }
    }
}
