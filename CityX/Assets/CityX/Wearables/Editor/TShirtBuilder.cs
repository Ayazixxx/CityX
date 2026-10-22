using System.Collections.Generic;
using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the T-Shirt: Meshy's tee cut off its mannequin, fitted, evened out at the front and back, reduced and laid out for designs by
    /// SourceArt/TShirt/tshirt_build.py into Models/TShirt.json. It is plain white cloth. A printed shirt is the same shirt with its own
    /// texture (a <see cref="ShirtPrint"/> printed by <see cref="ShirtPrintBaker"/>) and its own item (docs/TSHIRT.md).
    /// </summary>
    public static class TShirtBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/TShirt.prefab";
        private const string SourcePath = "Assets/CityX/Wearables/Models/TShirt.json";
        private const string MeshPath = "Assets/CityX/Wearables/TShirtMesh.asset";
        private const string MaterialPath = "Assets/CityX/Wearables/TShirt.mat";

        private const string WearablesFolder = "Assets/CityX/Wearables";
        private const string PrintsFolder = "Assets/CityX/Wearables/Prints";
        public const string BloodRavenPrefabPath = WearablesFolder + "/BloodRavenTShirt.prefab";
        public const string BloodRavenPrintPath = PrintsFolder + "/BloodRavenPrint.asset";
        public const string RedCamoPrefabPath = WearablesFolder + "/RedCamoTShirt.prefab";
        public const string RedCamoPrintPath = PrintsFolder + "/RedCamoPrint.asset";
        public const string BlackPrefabPath = WearablesFolder + "/BlackTShirt.prefab";

        /// <summary>The shirt as it lies on the ground before it is picked up, folded (it is shown worn once equipped).</summary>
        private const string FoldedSourcePath = "Assets/CityX/Wearables/Models/TShirtFolded.glb";
        private const string FoldedMeshPath = "Assets/CityX/Wearables/TShirtFoldedMesh.asset";

        /// <summary>Cotton: white, a little off, and matte.</summary>
        private static readonly Color Cotton = new Color(0.92f, 0.92f, 0.9f);
        private const float FabricSmoothness = 0.1f;

        /// <summary>Black cotton: not quite black, so the folds and the lighting still read on it.</summary>
        private static readonly Color BlackCotton = new Color(0.05f, 0.05f, 0.055f);

        /// <summary>
        /// The body never drawn under the shirt: the torso and the upper arms, but what shows outside it (Shown). Hidden only where the shirt
        /// covers it, the upper arms came through at the back of the armholes, where the coverage rays found the seam.
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHidden = { HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };

        /// <summary>
        /// Bind pose, the bare upper arm past the cuffs (SleeveCuff), down to the elbow: further out than ArmFromX (its inner side is at 0.35
        /// there, the torso within 0.27). Hidden where covered, its inner side, facing the shirt's side, was hidden and left a hole in the arm.
        /// </summary>
        private const float ArmFromX = 0.31f;
        private const int Cuffs = 2;

        /// <summary>
        /// How far up inside a cuff the arm is drawn on, behind its rolled lip: cut off at the lip, the skin's edge showed ragged up the
        /// sleeve, white with the sleeve's inside behind it.
        /// </summary>
        private const float CuffTuck = 0.015f;

        /// <summary>
        /// The crew neck's opening (tshirt_build.py's NECK_SIDE, NECK_FRONT, NECK_BACK, NECK_BACK_SQUARE: round the neck's axis, bind pose, +z
        /// ahead, an oval in front and flatter behind), less NeckBand in front, about where the collar's band lies: the skin within it is drawn,
        /// all of it, along that clean line (the build keeps the band out of the body, so none of it comes through). Hidden there, the inside of
        /// the shirt showed through the neck; hidden only where the shirt covered it, it came through the band in jagged bits. Behind, it is
        /// drawn out to the band's seam: the band, lifted clear of the neck there, showed the edge of the skin under it in slivers.
        /// </summary>
        private const float NeckSide = 0.28f;
        private const float NeckFront = 0.2f;
        private const float NeckBack = 0.18f;
        private const float NeckBackSquare = 3.0f;
        private const float NeckBand = 0.02f;
        private const float NeckFloor = 1.75f;

        /// <summary>The skin the shirt leaves bare, by a body triangle's bind-pose centre: the upper arms past the cuffs, inside the neck.</summary>
        private static bool Shown(Vector3 centre, List<SleeveCuff> cuffs)
        {
            if (Mathf.Abs(centre.x) > ArmFromX && cuffs.Exists(cuff => cuff.IsPast(centre, CuffTuck)))
            {
                return true;
            }
            float across = Mathf.Abs(centre.x) / (NeckSide - NeckBand);
            float ahead = centre.z / (centre.z > 0.0f ? NeckFront - NeckBand : NeckBack);
            return centre.y > NeckFloor && Mathf.Pow(across, centre.z > 0.0f ? 2.0f : NeckBackSquare) + ahead * ahead < 1.0f;
        }

        /// <summary>
        /// The body hidden only where the shirt covers it (BodyUnderGarment): the hips, down to the hem. Not the neck: the crew neck opens round
        /// it.
        /// </summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered =
        {
            HumanBodyBones.Hips,
        };

        [MenuItem("CityX/Rebuild T-Shirt")]
        public static GameObject Build()
        {
            Mesh mesh = BlenderGarmentMesh.Bake(SourcePath, "TShirtMesh", MeshPath);
            if (mesh == null)
            {
                return null;
            }
            BuildFoldedMesh(mesh);
            return Save(mesh, BlenderGarmentMesh.PlainMaterial(MaterialPath, Cotton, FabricSmoothness), PrefabPath, "TShirt", ItemRegistry.TShirt);
        }

        /// <summary>
        /// The folded shirt (SourceArt/TShirtFolded/folded_build.py's model) with the worn shirt's own texture layout
        /// (<see cref="FoldedShirtLayout"/>), so a shirt's one texture dresses it folded and worn: what is printed on the worn shirt shows
        /// on the folded one in the same place.
        /// </summary>
        private static Mesh BuildFoldedMesh(Mesh worn)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FoldedSourcePath);
            MeshFilter filter = model != null ? model.GetComponentInChildren<MeshFilter>(true) : null;
            if (filter == null)
            {
                Debug.LogError($"CityX: no folded T-Shirt at {FoldedSourcePath}; the shirt lies on the ground unfolded.");
                return null;
            }
            Mesh source = filter.sharedMesh;
            Matrix4x4 bake = filter.transform.localToWorldMatrix;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = bake.MultiplyPoint3x4(vertices[i]);
                normals[i] = bake.MultiplyVector(normals[i]).normalized;
            }
            var folded = new Mesh
            {
                name = "TShirtFoldedMesh",
                vertices = vertices,
                normals = normals,
                uv = FoldedShirtLayout.Uvs(vertices, normals, ShirtPrintBaker.WornSide(worn, true), ShirtPrintBaker.WornSide(worn, false)),
                triangles = source.triangles,
            };
            folded.RecalculateBounds();
            folded.RecalculateTangents();
            Debug.Log($"CityX: folded T-Shirt -- {source.triangles.Length / 3} triangles, {folded.bounds.size.x:0.00} m across, its neckline " +
                      $"{folded.bounds.max.z - FoldedShirtLayout.NecklineZ(vertices, normals, folded.bounds.max.z):0.000} m below the collar's edge");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(FoldedMeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(folded, existing);
                Object.DestroyImmediate(folded);
                return existing;
            }
            AssetDatabase.CreateAsset(folded, FoldedMeshPath);
            return folded;
        }

        /// <summary>The Black T-Shirt (owner, 2026-10-22): the T-Shirt (built first) in black cotton.</summary>
        [MenuItem("CityX/Rebuild Black T-Shirt")]
        public static GameObject BuildBlack()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                Debug.LogError($"CityX: no T-Shirt mesh at {MeshPath} -- build the T-Shirt first.");
                return null;
            }
            Material material = BlenderGarmentMesh.PlainMaterial($"{WearablesFolder}/BlackTShirt.mat", BlackCotton, FabricSmoothness);
            return Save(mesh, material, BlackPrefabPath, "BlackTShirt", ItemRegistry.BlackTShirt);
        }

        /// <summary>The Blood Raven T-Shirt: the T-Shirt (built first) printed with the Blood Raven logo where its print says.</summary>
        [MenuItem("CityX/Rebuild Blood Raven T-Shirt")]
        public static GameObject BuildBloodRaven() => BuildPrinted("BloodRaven", ItemRegistry.BloodRavenTShirt, coverTheWholeShirt: false);

        /// <summary>The Red Camo T-Shirt: the T-Shirt (built first) covered all over in red camo.</summary>
        [MenuItem("CityX/Rebuild Red Camo T-Shirt")]
        public static GameObject BuildRedCamo() => BuildPrinted("RedCamo", ItemRegistry.RedCamoTShirt, coverTheWholeShirt: true);

        /// <summary>
        /// A printed shirt named <paramref name="name"/>: its picture Prints/&lt;name&gt;.png, placed by its print Prints/&lt;name&gt;Print.asset
        /// (made the first time, all over the shirt or once on the chest), on &lt;name&gt;TShirt.prefab.
        /// </summary>
        private static GameObject BuildPrinted(string name, string itemId, bool coverTheWholeShirt)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                Debug.LogError($"CityX: no T-Shirt mesh at {MeshPath} -- build the T-Shirt first.");
                return null;
            }
            string printPath = $"{PrintsFolder}/{name}Print.asset";
            var print = AssetDatabase.LoadAssetAtPath<ShirtPrint>(printPath);
            if (print == null)
            {
                print = ScriptableObject.CreateInstance<ShirtPrint>();
                print.CoverTheWholeShirt = coverTheWholeShirt;
                AssetDatabase.CreateAsset(print, printPath);
            }
            if (print.Logo == null)
            {
                print.Logo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PrintsFolder}/{name}.png");
                EditorUtility.SetDirty(print);
                AssetDatabase.SaveAssetIfDirty(print);
            }
            Print(print);
            Material material = BlenderGarmentMesh.TexturedMaterial($"{WearablesFolder}/{name}TShirt.mat", ShirtPrintBaker.TexturePathFor(print),
                FabricSmoothness);
            return Save(mesh, material, $"{WearablesFolder}/{name}TShirt.prefab", $"{name}TShirt", itemId);
        }

        /// <summary>Prints <paramref name="print"/> on the T-Shirt's texture, in the shirt's cotton.</summary>
        public static Texture2D Print(ShirtPrint print)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            return mesh != null ? ShirtPrintBaker.Bake(print, mesh, Cotton) : null;
        }

        /// <summary>Selects the Blood Raven print, whose Inspector places the logo.</summary>
        [MenuItem("CityX/Place the Blood Raven Logo")]
        public static void SelectBloodRavenPrint()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShirtPrint>(BloodRavenPrintPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>Selects the Red Camo print, whose Inspector sets how big the camo is.</summary>
        [MenuItem("CityX/Size the Red Camo")]
        public static void SelectRedCamoPrint()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShirtPrint>(RedCamoPrintPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>A shirt prefab of the mesh in <paramref name="material"/>, the body under it hidden as the plain T-Shirt hides it.</summary>
        private static GameObject Save(Mesh mesh, Material material, string prefabPath, string rootName, string itemId)
        {
            List<SleeveCuff> cuffs = SleeveCuff.Of(mesh, ArmFromX);
            if (cuffs.Count != Cuffs)
            {
                Debug.LogError($"CityX: the T-Shirt has {cuffs.Count} cuffs, not {Cuffs}; its sleeves' arms stay hidden down to the hands.");
            }
            byte[] hidden = BodyUnderGarment.HiddenTriangles(mesh, AlwaysHidden, new HumanBodyBones[0], new Bounds(), HiddenWhereCovered,
                new Bounds(), out int bodyTriangles, centre => Shown(centre, cuffs));
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<TShirt>(
                mesh, material, prefabPath, rootName, 1.0f, 0.0f, 0.0f,
                shirt =>
                {
                    shirt.SetItemId(itemId);
                    shirt.HiddenBodyTriangles = hidden;
                    shirt.HiddenFromBodyTriangles = bodyTriangles;
                },
                AssetDatabase.LoadAssetAtPath<Mesh>(FoldedMeshPath));
            if (saved != null)
            {
                Debug.Log($"CityX: {rootName} written to {prefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
            }
            return saved;
        }
    }
}
