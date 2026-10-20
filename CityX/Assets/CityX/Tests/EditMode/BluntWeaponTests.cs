using System.Reflection;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The owner's metal crowbar (one-handed, metallic, crowbar.glb renamed metalcrowbar) and baseball bat (two-handed, wooden), 2026-10-15: each an
    /// item of its own, built upright in the sword frame from a model that came in lying diagonally, equippable, and seen by other players.
    /// </summary>
    public sealed class BluntWeaponTests
    {
        private const string CrowbarPath = "Assets/CityX/Weapons/MetalCrowbar.prefab";
        private const string BatPath = "Assets/CityX/Weapons/BaseballBat.prefab";

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} is missing -- rebuild the scene (CityMassingSceneBuilder builds it)");
            return prefab;
        }

        /// <summary>The model's vertices in the weapon root's own space.</summary>
        private static Vector3[] RootSpaceVertices(GameObject prefab, out MeshFilter filter)
        {
            filter = prefab.GetComponentInChildren<MeshFilter>();
            Vector3[] vertices = filter.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
            }
            return vertices;
        }

        private static void Span(Vector3[] vertices, out float bottom, out float top)
        {
            bottom = float.MaxValue;
            top = float.MinValue;
            foreach (Vector3 v in vertices)
            {
                bottom = Mathf.Min(bottom, v.y);
                top = Mathf.Max(top, v.y);
            }
        }

        /// <summary>The mean distance from the root's Y axis of the vertices between two heights.</summary>
        private static float Thickness(Vector3[] vertices, float from, float to, out Vector3 centre)
        {
            float sum = 0.0f;
            int count = 0;
            centre = Vector3.zero;
            foreach (Vector3 v in vertices)
            {
                if (v.y < from || v.y > to) continue;
                centre += v;
                count++;
            }
            Assert.Greater(count, 4, $"no vertices between {from:0.00} and {to:0.00} m");
            centre /= count;
            foreach (Vector3 v in vertices)
            {
                if (v.y < from || v.y > to) continue;
                sum += new Vector2(v.x - centre.x, v.z - centre.z).magnitude;
            }
            return sum / count;
        }

        [Test]
        public void TheCrowbarIsAOneHandedMetalWeaponOfItsOwn()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.MetalCrowbar, out ItemDefinition definition));
            Assert.AreEqual("hand_r", definition.EquipSlot);
            GameObject prefab = Load(CrowbarPath);
            SwordWeapon weapon = prefab.GetComponent<SwordWeapon>();
            Assert.IsNotNull(weapon, "not a weapon the sword combat can swing");
            Assert.AreEqual(ItemRegistry.MetalCrowbar, weapon.ItemId, "picking it up would give another weapon");
            Assert.IsTrue(weapon.OneHanded, "it should be held in one hand");

            Vector3[] vertices = RootSpaceVertices(prefab, out MeshFilter filter);
            Span(vertices, out float bottom, out float top);
            Assert.AreEqual(MetalCrowbarLength, top - bottom, 0.03f, "the crowbar should be scaled to its length");
            Assert.AreEqual(top, weapon.BladeLength, 0.01f, "a swing should reach as far as the model does");
            Assert.Less(bottom, -0.1f, "the straight end should stand out below the hand");
            Assert.Greater(bottom, -0.35f, "the hand should hold it near its straight end");

            // The shaft runs through the root: the hand's hold is on it.
            Thickness(vertices, bottom + 0.05f, 0.2f, out Vector3 shaft);
            Assert.Less(new Vector2(shaft.x, shaft.z).magnitude, 0.02f, $"the shaft is {shaft} off the root's axis");

            // The claw curls toward -X at the top, the swing's leading side (where a sword's edge is).
            Vector3 clawEnd = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in vertices)
            {
                if (v.y > top - 0.15f) { clawEnd += v; count++; }
            }
            clawEnd /= count;
            Assert.Less(clawEnd.x, -0.02f, $"the claw should curl toward -X, its middle is at {clawEnd}");

            Material steel = filter.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.GreaterOrEqual(steel.GetFloat("_Metallic"), 0.8f, "the owner asked for it to be metallic");
        }

        /// <summary>
        /// The one-handed hold the owner saved was set on the combat sword (edge on +X). The crowbar's edge is on -X, so its hold is turned half round
        /// the handle: in the hand its edge faces where the combat sword's does, and the poses do not turn the hand half round to lead with it (owner,
        /// 2026-10-19: "after you swing the crowbar it leave it in this weird arm position").
        /// </summary>
        [Test]
        public void TheCrowbarsEdgeSitsInTheHandWhereTheCombatSwordsDoes()
        {
            var crowbar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/MetalCrowbar.prefab").GetComponent<SwordWeapon>();
            var combat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab").GetComponent<SwordWeapon>();
            Assert.AreNotEqual(crowbar.EdgeOnPlusX, combat.EdgeOnPlusX, "the two have their edges on the same side now: this test no longer applies");
            Vector3 crowbarEdge = crowbar.HoldRotation * crowbar.EdgeLocal;
            Vector3 combatEdge = combat.HoldRotation * combat.EdgeLocal;
            Assert.Less(Vector3.Angle(crowbarEdge, combatEdge), 1.0f, "the crowbar's edge faces another way in the hand than the combat sword's");
            Assert.Less(Vector3.Angle(crowbar.HoldRotation * Vector3.up, combat.HoldRotation * Vector3.up), 1.0f, "the crowbar is not along the same line in the hand");
        }

        private const float MetalCrowbarLength = 1.0f;
        private const float BatLength = 1.6f;

        [Test]
        public void TheBatIsATwoHandedWoodenWeaponOfItsOwnHeldAtTheKnob()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.BaseballBat, out ItemDefinition definition));
            Assert.AreEqual("hand_r", definition.EquipSlot);
            GameObject prefab = Load(BatPath);
            SwordWeapon weapon = prefab.GetComponent<SwordWeapon>();
            Assert.IsNotNull(weapon, "not a weapon the sword combat can swing");
            Assert.AreEqual(ItemRegistry.BaseballBat, weapon.ItemId);
            Assert.IsFalse(weapon.OneHanded, "it should be held in both hands");

            Vector3[] vertices = RootSpaceVertices(prefab, out MeshFilter filter);
            Span(vertices, out float bottom, out float top);
            Assert.AreEqual(BatLength, top - bottom, 0.03f, "the bat should be scaled to its length");
            Assert.AreEqual(top, weapon.BladeLength, 0.01f, "a swing should reach as far as the model does");
            // Both hands on the handle: the support hand SwordWeapon.DefaultSupportGripDown below the root, just over the knob.
            Assert.AreEqual(-(SwordWeapon.DefaultSupportGripDown + 0.08f), bottom, 0.03f, "the knob should sit just under the bottom hand");

            // Knob down, barrel up: the barrel is far thicker than the handle.
            float handle = Thickness(vertices, -0.2f, 0.0f, out Vector3 handleCentre);
            float barrel = Thickness(vertices, top - 0.3f, top - 0.1f, out _);
            Assert.Greater(barrel, handle * 1.6f, $"the barrel ({barrel:0.000} m) should be up and the handle ({handle:0.000} m) down");
            Assert.Less(new Vector2(handleCentre.x, handleCentre.z).magnitude, 0.02f, "the handle runs through the root");

            // Unwrapped for the wood grain, which is drawn when it appears.
            Mesh mesh = filter.sharedMesh;
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "the bat has no texture coordinates for its grain");
            Assert.IsNotNull(filter.GetComponent<WoodGrain>(), "nothing draws the wood");
            Material wood = filter.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.Less(wood.GetFloat("_Metallic"), 0.1f, "wood is not metal");
        }

        [Test]
        public void TheGrainIsWoodColouredWithDarkerLinesAndWrapsRound()
        {
            Texture2D grain = WoodGrain.Texture();
            Assert.AreEqual(WoodGrain.Width, grain.width);
            Assert.AreEqual(WoodGrain.Height, grain.height);
            Assert.AreEqual(TextureWrapMode.Repeat, grain.wrapModeU);
            float darkest = float.MaxValue, lightest = float.MinValue;
            for (int y = 0; y < WoodGrain.Height; y += 7)
            {
                for (int x = 0; x < WoodGrain.Width; x++)
                {
                    Color c = WoodGrain.Colour((x + 0.5f) / WoodGrain.Width, (y + 0.5f) / WoodGrain.Height, x, y);
                    Assert.Greater(c.r, c.b, "wood is warm: more red than blue");
                    float luminance = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    darkest = Mathf.Min(darkest, luminance);
                    lightest = Mathf.Max(lightest, luminance);
                }
            }
            Assert.Greater(lightest - darkest, 0.15f, "no grain: the wood is one flat colour");
            // Round the bat the texture meets itself: the last column and the first are neighbours, and they may differ no more than any two
            // neighbouring columns do (a grain line ends sharply anywhere it falls).
            float seam = 0.0f, neighbours = 0.0f;
            for (int y = 0; y < WoodGrain.Height; y += 5)
            {
                float v = (y + 0.5f) / WoodGrain.Height;
                for (int x = 0; x < WoodGrain.Width; x++)
                {
                    int next = (x + 1) % WoodGrain.Width;
                    Color a = WoodGrain.Colour((x + 0.5f) / WoodGrain.Width, v, x, y);
                    Color b = WoodGrain.Colour((next + 0.5f) / WoodGrain.Width, v, next, y);
                    float step = Mathf.Abs(a.r - b.r);
                    if (next == 0)
                    {
                        seam = Mathf.Max(seam, step);
                    }
                    else
                    {
                        neighbours = Mathf.Max(neighbours, step);
                    }
                }
            }
            Assert.LessOrEqual(seam, neighbours + 0.02f, $"the grain jumps where the texture wraps round the bat ({seam:0.00}, against {neighbours:0.00} anywhere else)");
        }

        [Test]
        public void EveryBodyCanEquipBothSoOthersSeeThem()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponentInChildren<Inventory>(true);
            foreach (string id in new[] { ItemRegistry.MetalCrowbar, ItemRegistry.BaseballBat })
            {
                bool found = false;
                foreach (Inventory.ItemPrefabEntry entry in inventory.ItemPrefabs)
                {
                    found |= entry.ItemId == id && entry.Prefab != null;
                }
                Assert.IsTrue(found, $"the networked player cannot build {id}, so other players would not see it");
            }
        }

        [Test]
        public void TheConsoleEquipsThemByName()
        {
            MethodInfo alias = typeof(CityX.UI.DevConsoleView).GetMethod("AliasToItemId", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(alias, "DevConsoleView.AliasToItemId is gone");
            foreach (string name in new[] { "crowbar", "metalcrowbar" })
            {
                Assert.AreEqual(ItemRegistry.MetalCrowbar, alias.Invoke(null, new object[] { name }), $"/equip {name}");
            }
            foreach (string name in new[] { "bat", "baseballbat" })
            {
                Assert.AreEqual(ItemRegistry.BaseballBat, alias.Invoke(null, new object[] { name }), $"/equip {name}");
            }
        }
    }
}
