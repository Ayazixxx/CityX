using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: writes the body standing in its idle and the Giant Boomerang on its back as OBJ files in the Kick hairstyle's own model space
    /// (CityX/Assets/CityX/Character/Hair/KickHair.glb as Blender imports it), to Logs/HairFit, so the hair can be cut by hand in Blender round the
    /// boomerang and saved back over KickHair.glb (then CityX > Rebake Kick Hair). Also the hair as the game has it, to check the two line up.
    /// Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("writes files for Blender; needs a graphics device")]
    public sealed class HairFitExport
    {
        [UnityTest]
        public IEnumerator ExportTheBodyAndTheBoomerangInTheKickHairsSpace()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<BoomerangThrower>();
            var item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/GiantBoomerang.prefab"))
                .GetComponent<GiantBoomerangItem>();
            item.OnEquip(player, ItemInstanceData.None);
            var hair = player.GetComponent<CharacterHair>() ?? player.AddComponent<CharacterHair>();
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "head") hair.Head = child;
            }
            var meshes = new Mesh[CharacterHair.ValidStyles.Length - 1];
            Mesh kick = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/KickHairMesh.asset");
            meshes[System.Array.IndexOf(CharacterHair.ValidStyles, CharacterHair.KickHair) - 1] = kick;
            hair.StyleMeshes = meshes;
            hair.HairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            var save = CharacterSave.Create("Fit", new Color(0.95f, 0.78f, 0.6f));
            save.HairStyle = CharacterHair.KickHair;
            player.GetComponent<CharacterIdentity>().Apply(save);
            // Into the idle, the boomerang settled on the back.
            for (int i = 0; i < 90; i++) yield return null;

            Assert.IsNotNull(hair.Worn, "the Kick hair is not worn");
            // The hair's model space: where its placement on the head puts it; its vertices are the model's as imported.
            Matrix4x4 toHair = hair.Worn.transform.worldToLocalMatrix;

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "HairFit");
            Directory.CreateDirectory(folder);
            var body = new StringBuilder();
            int offset = 1;
            foreach (SkinnedMeshRenderer r in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.enabled) continue;
                var baked = new Mesh();
                r.BakeMesh(baked, false);   // in the renderer's own unscaled space: its transform below puts it in the world
                offset = Write(body, r.name, baked, toHair * r.transform.localToWorldMatrix, offset);
            }
            // The head and anything else drawn whole on a bone (not the boomerang, not the hair).
            foreach (MeshFilter f in player.GetComponentsInChildren<MeshFilter>())
            {
                if (f.transform.IsChildOf(item.transform) || f.transform.IsChildOf(hair.Worn.transform) || f.sharedMesh == null) continue;
                var shown = f.GetComponent<Renderer>();
                if (shown == null || !shown.enabled) continue;
                offset = Write(body, f.name, f.sharedMesh, toHair * f.transform.localToWorldMatrix, offset);
            }
            File.WriteAllText(Path.Combine(folder, "Body.obj"), body.ToString());

            var boomerang = new StringBuilder();
            offset = 1;
            foreach (MeshFilter f in item.GetComponentsInChildren<MeshFilter>())
            {
                offset = Write(boomerang, f.name, f.sharedMesh, toHair * f.transform.localToWorldMatrix, offset);
            }
            File.WriteAllText(Path.Combine(folder, "GiantBoomerang.obj"), boomerang.ToString());

            var reference = new StringBuilder();
            Write(reference, "KickHairAsInTheGame", kick, Matrix4x4.identity, 1);
            File.WriteAllText(Path.Combine(folder, "KickHairAsInTheGame.obj"), reference.ToString());
            Debug.Log($"CityX hair fit: wrote {folder}");

            Object.Destroy(item.gameObject);
            Object.Destroy(player);
#else
            yield break;
#endif
        }

        /// <summary>
        /// A mesh's triangles in OBJ, its points taken through <paramref name="toHair"/> into the hair's model space and from Unity's axes into glTF's
        /// (glTFast imports with x negated), so Blender's OBJ importer (y up, -z forward) puts them where its glTF importer puts the hair.
        /// </summary>
        private static int Write(StringBuilder obj, string name, Mesh mesh, Matrix4x4 toHair, int offset)
        {
            obj.Append("o ").Append(name.Replace(' ', '_')).Append('\n');
            Vector3[] vertices = mesh.vertices;
            foreach (Vector3 v in vertices)
            {
                Vector3 p = toHair.MultiplyPoint3x4(v);
                obj.Append(string.Format(CultureInfo.InvariantCulture, "v {0:R} {1:R} {2:R}\n", -p.x, p.y, p.z));
            }
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Mirrored in x, so the winding is turned back.
                obj.Append(string.Format(CultureInfo.InvariantCulture, "f {0} {1} {2}\n",
                    triangles[i] + offset, triangles[i + 2] + offset, triangles[i + 1] + offset));
            }
            return offset + vertices.Length;
        }
    }
}
