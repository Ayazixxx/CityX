using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityX.Tests
{
    /// <summary>
    /// Every saved body that draws the shared FusedBody mesh lists the SAME bones in the SAME order as the mesh was built for.
    ///
    /// THE MESH IS UPDATED IN PLACE, THE BONE LISTS ARE NOT. FusedBodyBuilder rewrites FusedBody.asset where it lies, so every prefab and scene
    /// pointing at it picks up the new shape at once -- but each of them saved its own renderer.bones list when it was last built. The tube body
    /// changed which bone each tube sits on (2026-10-10), the main scene builder rebuilt the player, and the title scene and the civilian kept
    /// their old lists: the character creator showed the arms, head and torso strewn about on the wrong bones (owner: "character creation looks
    /// all messed up now"). The player prefab is rebuilt with the mesh every time, so every other body must match it.
    /// </summary>
    public sealed class SharedBodyBindingTests
    {
        private const string MeshPath = "Assets/CityX/Character/FusedBody.asset";

        private static readonly string[] Prefabs =
        {
            "Assets/CityX/Character/Civilian.prefab",
            "Assets/CityX/Character/OldGuy.prefab",
            "Assets/CityX/Character/FlyingNpc.prefab",
            "Assets/CityX/Character/NetworkedPlayer.prefab",
        };

        private const string TitleScene = "Assets/CityX/Scenes/Title.unity";

        private static Mesh BodyMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            Assert.IsNotNull(mesh, $"no mesh at {MeshPath}");
            return mesh;
        }

        private static List<SkinnedMeshRenderer> BodiesIn(GameObject root, Mesh mesh)
        {
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh == mesh).ToList();
        }

        private static string[] PlayerBones(Mesh mesh)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(player);
            List<SkinnedMeshRenderer> bodies = BodiesIn(player, mesh);
            Assert.AreEqual(1, bodies.Count, "the player should draw the body once");
            Assert.AreEqual(mesh.bindposes.Length, bodies[0].bones.Length, "the player's own bone list does not match the mesh -- rebuild");
            return bodies[0].bones.Select(b => b != null ? b.name : "<missing>").ToArray();
        }

        private static void AssertSameBones(string where, SkinnedMeshRenderer body, Mesh mesh, string[] expected)
        {
            string[] names = body.bones.Select(b => b != null ? b.name : "<missing>").ToArray();
            Assert.AreEqual(mesh.bindposes.Length, names.Length,
                $"{where}: its body lists {names.Length} bones but the mesh has {mesh.bindposes.Length} -- it was built against an older body; rebuild it");
            CollectionAssert.AreEqual(expected, names,
                $"{where}: its body lists the bones in a different order from the mesh -- every tube lands on the wrong bone; rebuild it");
        }

        [Test]
        public void EveryCharacterPrefabBindsTheBodyToTheBonesItWasBuiltFor()
        {
            Mesh mesh = BodyMesh();
            string[] expected = PlayerBones(mesh);
            foreach (string path in Prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }
                foreach (SkinnedMeshRenderer body in BodiesIn(prefab, mesh))
                {
                    AssertSameBones(path, body, mesh, expected);
                }
            }
        }

        [Test]
        public void TheCharacterCreatorBindsTheBodyToTheBonesItWasBuiltFor()
        {
            Mesh mesh = BodyMesh();
            string[] expected = PlayerBones(mesh);
            Scene scene = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Additive);
            try
            {
                int found = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (SkinnedMeshRenderer body in BodiesIn(root, mesh))
                    {
                        AssertSameBones(TitleScene + " " + body.name, body, mesh, expected);
                        found++;
                    }
                }
                Assert.Greater(found, 0, "the title scene has no body drawing the shared mesh");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
