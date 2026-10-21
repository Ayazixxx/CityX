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

        public const string BloodRavenPrefabPath = "Assets/CityX/Wearables/BloodRavenTShirt.prefab";
        public const string BloodRavenPrintPath = "Assets/CityX/Wearables/Prints/BloodRavenPrint.asset";
        private const string BloodRavenLogoPath = "Assets/CityX/Wearables/Prints/BloodRaven.png";
        private const string BloodRavenMaterialPath = "Assets/CityX/Wearables/BloodRavenTShirt.mat";

        // Matches the other tops' ground pose: tipped so it reads as a dropped shirt.
        private const float GroundScale = 0.45f;
        private const float GroundPitchDegrees = -78.0f;
        private const float GroundLift = 0.15f;

        /// <summary>Cotton: white, a little off, and matte.</summary>
        private static readonly Color Cotton = new Color(0.92f, 0.92f, 0.9f);
        private const float FabricSmoothness = 0.1f;

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
            return Save(mesh, BlenderGarmentMesh.PlainMaterial(MaterialPath, Cotton, FabricSmoothness), PrefabPath, "TShirt", ItemRegistry.TShirt);
        }

        /// <summary>The Blood Raven T-Shirt: the T-Shirt (built first) printed with the Blood Raven logo where its print says.</summary>
        [MenuItem("CityX/Rebuild Blood Raven T-Shirt")]
        public static GameObject BuildBloodRaven()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                Debug.LogError($"CityX: no T-Shirt mesh at {MeshPath} -- build the T-Shirt first.");
                return null;
            }
            var print = AssetDatabase.LoadAssetAtPath<ShirtPrint>(BloodRavenPrintPath);
            if (print == null)
            {
                print = ScriptableObject.CreateInstance<ShirtPrint>();
                print.Logo = AssetDatabase.LoadAssetAtPath<Texture2D>(BloodRavenLogoPath);
                AssetDatabase.CreateAsset(print, BloodRavenPrintPath);
            }
            Print(print);
            Material material = BlenderGarmentMesh.TexturedMaterial(BloodRavenMaterialPath, ShirtPrintBaker.TexturePathFor(print), FabricSmoothness);
            return Save(mesh, material, BloodRavenPrefabPath, "BloodRavenTShirt", ItemRegistry.BloodRavenTShirt);
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
                mesh, material, prefabPath, rootName, GroundScale, GroundPitchDegrees, GroundLift,
                shirt =>
                {
                    shirt.SetItemId(itemId);
                    shirt.HiddenBodyTriangles = hidden;
                    shirt.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: {rootName} written to {prefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
            }
            return saved;
        }
    }
}
