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
    /// texture (a <see cref="ShirtPrint"/> printed by <see cref="ShirtPrintBaker"/>) and its own item (docs/TSHIRT.md). The long-sleeved shirt
    /// is the same build with its sleeves down to the wrists (--long-sleeves into Models/LongSleeveTShirt.json), in white and in black.
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
        public const string CityXPrefabPath = WearablesFolder + "/CityXTShirt.prefab";
        public const string CityXPrintPath = PrintsFolder + "/CityXPrint.asset";

        /// <summary>The long-sleeved shirt: its own mesh (tshirt_build.py --long-sleeves), in white and in black.</summary>
        public const string LongSleevePrefabPath = WearablesFolder + "/LongSleeveTShirt.prefab";
        public const string BlackLongSleevePrefabPath = WearablesFolder + "/BlackLongSleeveTShirt.prefab";
        private const string LongSleeveSourcePath = "Assets/CityX/Wearables/Models/LongSleeveTShirt.json";
        public const string LongSleeveMeshPath = "Assets/CityX/Wearables/LongSleeveTShirtMesh.asset";

        /// <summary>The shirt as it lies on the ground before it is picked up, folded (it is shown worn once equipped).</summary>
        private const string FoldedSourcePath = "Assets/CityX/Wearables/Models/TShirtFolded.glb";
        private const string FoldedMeshPath = "Assets/CityX/Wearables/TShirtFoldedMesh.asset";

        /// <summary>Cotton, white and black, matte.</summary>
        private static readonly Color Cotton = ShirtPrint.WhiteCotton;
        private static readonly Color BlackCotton = ShirtPrint.BlackCotton;
        private const float FabricSmoothness = 0.1f;

        /// <summary>Where a new print starts on the chest: the size and height the owner gave the Blood Raven logo.</summary>
        private const float StartWidthCm = 86.5f;
        private const float StartUpCm = 0.85f;

        /// <summary>
        /// The body never drawn under the shirt: the torso and the upper arms, but what shows outside it (Shown). Hidden only where the shirt
        /// covers it, the upper arms came through at the back of the armholes, where the coverage rays found the seam.
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHidden = { HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };

        /// <summary>Under long sleeves the forearms too, but for the wrist past each cuff (Shown).</summary>
        private static readonly HumanBodyBones[] AlwaysHiddenUnderLongSleeves =
        {
            HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        };

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

        /// <summary>
        /// Within the crew neck's opening, the skin this near the body's middle (either side) is drawn all of it, in front and behind, along the clean
        /// line the band lies along; further out, over the tops of the shoulders, only where the shirt does not cover it (BodyUnderGarment's
        /// shownWhereBare). Kept only this near the neck's axis all round, the skin's edge under the band behind showed through it in spikes. Drawn all of it, an arm swung
        /// out (the Giant Boomerang's catch, its aim) moved the cloth over it and it came through the back and front of the shoulder (owner,
        /// 2026-11-20: "there is a bit of the shoulder that clips through", "just make his body underneath the shirt invisible at that area").
        /// </summary>
        private const float NeckKeep = 0.12f;

        /// <summary>The skin the shirt leaves bare, by a body triangle's bind-pose centre: the upper arms past the cuffs, inside the neck near it.</summary>
        private static bool Shown(Vector3 centre, List<SleeveCuff> cuffs)
        {
            if (Mathf.Abs(centre.x) > ArmFromX && cuffs.Exists(cuff => cuff.IsPast(centre, CuffTuck)))
            {
                return true;
            }
            return InsideTheNeck(centre) && Mathf.Abs(centre.x) < NeckKeep;
        }

        /// <summary>The skin within the crew neck's opening (its oval, round the neck's axis, bind pose).</summary>
        private static bool InsideTheNeck(Vector3 centre)
        {
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
            return Save(mesh, BlenderGarmentMesh.PlainMaterial(MaterialPath, Cotton, FabricSmoothness), PrefabPath, "TShirt", ItemRegistry.TShirt,
                AlwaysHidden);
        }

        /// <summary>
        /// The Long Sleeve T-Shirt (owner, 2026-10-22: "make a longsleeve version of the tshirt both black and white"): the T-Shirt with its
        /// sleeves down to the wrists (tshirt_build.py --long-sleeves into Models/LongSleeveTShirt.json), in the T-Shirt's white cotton. It lies
        /// on the ground as the T-Shirt's folded model, its long sleeves folded behind.
        /// </summary>
        [MenuItem("CityX/Rebuild Long Sleeve T-Shirt")]
        public static GameObject BuildLongSleeve()
        {
            Mesh mesh = BlenderGarmentMesh.Bake(LongSleeveSourcePath, "LongSleeveTShirtMesh", LongSleeveMeshPath);
            if (mesh == null)
            {
                return null;
            }
            Material material = BlenderGarmentMesh.PlainMaterial($"{WearablesFolder}/LongSleeveTShirt.mat", Cotton, FabricSmoothness);
            return Save(mesh, material, LongSleevePrefabPath, "LongSleeveTShirt", ItemRegistry.LongSleeveTShirt, AlwaysHiddenUnderLongSleeves);
        }

        /// <summary>The Black Long Sleeve T-Shirt: the Long Sleeve T-Shirt (built first) in the Black T-Shirt's black cotton.</summary>
        [MenuItem("CityX/Rebuild Black Long Sleeve T-Shirt")]
        public static GameObject BuildBlackLongSleeve()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LongSleeveMeshPath);
            if (mesh == null)
            {
                Debug.LogError($"CityX: no Long Sleeve T-Shirt mesh at {LongSleeveMeshPath} -- build the Long Sleeve T-Shirt first.");
                return null;
            }
            Material material = BlenderGarmentMesh.PlainMaterial($"{WearablesFolder}/BlackLongSleeveTShirt.mat", BlackCotton, FabricSmoothness);
            return Save(mesh, material, BlackLongSleevePrefabPath, "BlackLongSleeveTShirt", ItemRegistry.BlackLongSleeveTShirt,
                AlwaysHiddenUnderLongSleeves);
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
            return Save(mesh, material, BlackPrefabPath, "BlackTShirt", ItemRegistry.BlackTShirt, AlwaysHidden);
        }

        /// <summary>The Blood Raven T-Shirt: the T-Shirt (built first) printed with the Blood Raven logo where its print says.</summary>
        [MenuItem("CityX/Rebuild Blood Raven T-Shirt")]
        public static GameObject BuildBloodRaven() => BuildPrinted("BloodRaven", ItemRegistry.BloodRavenTShirt, coverTheWholeShirt: false);

        /// <summary>The Red Camo T-Shirt: the T-Shirt (built first) covered all over in red camo.</summary>
        [MenuItem("CityX/Rebuild Red Camo T-Shirt")]
        public static GameObject BuildRedCamo() => BuildPrinted("RedCamo", ItemRegistry.RedCamoTShirt, coverTheWholeShirt: true);

        /// <summary>The cityXshirt (owner, 2026-11-07): the game's own logo on the T-Shirt in black cotton.</summary>
        [MenuItem("CityX/Rebuild cityXshirt")]
        public static GameObject BuildCityX() => BuildPrinted("CityX", ItemRegistry.CityXShirt, coverTheWholeShirt: false, ShirtPrint.BlackCotton);

        /// <summary>
        /// A printed shirt named <paramref name="name"/>: its picture Prints/&lt;name&gt;.png, placed by its print Prints/&lt;name&gt;Print.asset
        /// (made the first time, all over the shirt or once on the chest, on <paramref name="cotton"/> or white), on &lt;name&gt;TShirt.prefab.
        /// </summary>
        private static GameObject BuildPrinted(string name, string itemId, bool coverTheWholeShirt, Color? cotton = null)
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
                print.Cotton = cotton ?? Cotton;
                if (!coverTheWholeShirt)
                {
                    print.WidthCm = StartWidthCm;
                    print.UpCm = StartUpCm;
                }
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
            return Save(mesh, material, $"{WearablesFolder}/{name}TShirt.prefab", $"{name}TShirt", itemId, AlwaysHidden);
        }

        /// <summary>Prints <paramref name="print"/> on the T-Shirt's texture, in its cotton.</summary>
        public static Texture2D Print(ShirtPrint print)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            return mesh != null ? ShirtPrintBaker.Bake(print, mesh, print.Cotton) : null;
        }

        /// <summary>Selects the Blood Raven print, whose Inspector places the logo.</summary>
        [MenuItem("CityX/Place the Blood Raven Logo")]
        public static void SelectBloodRavenPrint()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShirtPrint>(BloodRavenPrintPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>Selects the cityXshirt's print, whose Inspector places the logo.</summary>
        [MenuItem("CityX/Place the cityXshirt Logo")]
        public static void SelectCityXPrint()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShirtPrint>(CityXPrintPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>Selects the Red Camo print, whose Inspector sets how big the camo is.</summary>
        [MenuItem("CityX/Size the Red Camo")]
        public static void SelectRedCamoPrint()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShirtPrint>(RedCamoPrintPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>
        /// A shirt prefab of the mesh in <paramref name="material"/>, the body under it hidden as the plain T-Shirt hides it: the
        /// <paramref name="alwaysHidden"/> bones' skin, but for what the shirt leaves bare (Shown).
        /// </summary>
        private static GameObject Save(Mesh mesh, Material material, string prefabPath, string rootName, string itemId, HumanBodyBones[] alwaysHidden)
        {
            List<SleeveCuff> cuffs = SleeveCuff.Of(mesh, ArmFromX);
            if (cuffs.Count != Cuffs)
            {
                Debug.LogError($"CityX: {rootName} has {cuffs.Count} cuffs, not {Cuffs}; its sleeves' arms stay hidden down to the hands.");
            }
            byte[] hidden = BodyUnderGarment.HiddenTriangles(mesh, alwaysHidden, new HumanBodyBones[0], new Bounds(), HiddenWhereCovered,
                new Bounds(), out int bodyTriangles, centre => Shown(centre, cuffs), InsideTheNeck);
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
