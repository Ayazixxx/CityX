using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Wraith Staff (owner, 2026-11-08: "make an item called "Wraith Staff" it will be a rare drop from the wraiths like a 3% drop chance,
    /// and it summons 1 wraith that follows you around and fights for you ... make the red orb the same color as the wraith's eyes and make it
    /// glow as well. also lower it to a reasonable face count"): the item, its model and its orb, its drop chance, and a summon's side.
    /// </summary>
    public sealed class WraithStaffTests
    {
        private const string PrefabPath = "Assets/CityX/Weapons/WraithStaff.prefab";

        [Test]
        public void ItIsAVeryRareStaff()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.WraithStaff, out ItemDefinition item));
            Assert.AreEqual("Wraith Staff", item.DisplayName);
            Assert.AreEqual(EquipSlots.Primary, item.EquipSlot);
            Assert.AreEqual(Rarity.VeryRare, ItemRarity.Of(ItemRegistry.WraithStaff));
        }

        [Test]
        public void OneWildWraithInThirtyThreeDropsIt()
        {
            Assert.AreEqual(0.03f, Wraith.StaffDropChance, 1e-6f);
        }

        /// <summary>Cut to the weapon budget, its paint on one small map, the orb at the head where spells leave a staff.</summary>
        [Test]
        public void ItsModelIsCutDownAndHeldAsTheOtherStaffsAre()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no Wraith Staff prefab: rebuild the massing scene");
            var staff = prefab.GetComponent<WraithStaff>();
            Assert.IsNotNull(staff);
            Assert.IsNotNull(staff.Tip);
            Assert.AreEqual(CityX.WeaponsEditor.StaffBuilder.OrbHeight, staff.Tip.localPosition.y, 0.01f);
            Mesh mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.LessOrEqual(mesh.triangles.Length / 3, 6000, "over the weapon budget (tools/model_qa.py: 6k fine)");
            Bounds bounds = prefab.GetComponentInChildren<MeshRenderer>().bounds;
            Assert.AreEqual(2.36f, bounds.size.y, 0.05f, "not as long as the other staffs");
            Material body = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials[0];
            var map = body.GetTexture("_BaseMap");
            Assert.IsNotNull(map, "the staff is not painted");
            Assert.LessOrEqual(map.width, 512, "its map is too big");
        }

        /// <summary>The orb is its own part, in the Wraith's eyes' red, glowing as they do; nothing else on the staff glows.</summary>
        [Test]
        public void ItsOrbGlowsTheRedOfTheWraithsEyes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var renderer = prefab.GetComponentInChildren<MeshRenderer>();
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            int glowing = 0;
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material material = renderer.sharedMaterials[i];
                bool glows = material.IsKeywordEnabled("_EMISSION") && material.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                if (!glows)
                {
                    continue;
                }
                glowing++;
                Assert.AreEqual(CityX.CharacterEditor.WraithBuilder.EyeGlowColour, material.GetColor("_EmissionColor"), "not the Wraith's eyes' glow");
                Assert.Greater(mesh.GetTriangles(i).Length / 3, 300, "the glowing part is not the orb");
                Bounds orb = OrbBounds(mesh, i);
                Assert.AreEqual(CityX.WeaponsEditor.StaffBuilder.OrbHeight, renderer.transform.TransformPoint(orb.center).y - prefab.transform.position.y, 0.05f,
                    "the glow is not at the head");
            }
            Assert.AreEqual(1, glowing, "the orb alone should glow");
        }

        /// <summary>No player's blow lands on a summon on their side (its master's included); anything else's does.</summary>
        [Test]
        public void ASummonIsSparedItsSidesBlows()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Wraith.prefab");
            GameObject wraithObject = Object.Instantiate(prefab);
            var master = new GameObject("Master").AddComponent<PlayerVitals>();
            var stranger = new GameObject("Stranger");
            try
            {
                var wraith = wraithObject.GetComponent<Wraith>();
                wraith.Summon(master);
                Opponents.Join(wraith);   // OnEnable does not run in an edit-mode test
                Assert.IsTrue(Opponents.IsSummon(wraithObject));
                Assert.AreEqual(Wraith.SummonedMaxHealth, wraith.Health);

                Assert.AreEqual(HitResponse.Ignored, DamageContext.Strike(wraith, 50.0f, Vector3.zero, 0.0f, DamageKind.Physical, 0.0f, null, master.gameObject),
                    "its summoner's blow landed on it");
                Assert.AreEqual(Wraith.SummonedMaxHealth, wraith.Health);
                Assert.AreEqual(HitResponse.Landed, DamageContext.Strike(wraith, 50.0f, Vector3.zero, 0.0f, DamageKind.Physical, 0.0f, null, stranger),
                    "an enemy's blow did not land on it");
                Assert.Less(wraith.Health, Wraith.SummonedMaxHealth);
            }
            finally
            {
                Opponents.Leave(wraithObject.GetComponent<Wraith>());
                Object.DestroyImmediate(wraithObject);
                Object.DestroyImmediate(master.gameObject);
                Object.DestroyImmediate(stranger);
            }
        }

        private static Bounds OrbBounds(Mesh mesh, int submesh)
        {
            int[] triangles = mesh.GetTriangles(submesh);
            Vector3[] vertices = mesh.vertices;
            var bounds = new Bounds(vertices[triangles[0]], Vector3.zero);
            foreach (int index in triangles)
            {
                bounds.Encapsulate(vertices[index]);
            }
            return bounds;
        }
    }
}
