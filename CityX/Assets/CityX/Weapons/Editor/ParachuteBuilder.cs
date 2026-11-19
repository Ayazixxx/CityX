using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the parachute prefab (docs/PARACHUTE.md): the pack on the back, a basic one for now (owner, 2026-11-19: "id like to design the
    /// backpack myself later but just make a basic one that works for now"): the container, its top flap, the pilot chute's pouch at the bottom
    /// and the orange handle the hand pulls; and the canopy (CanopyShape, saved as a mesh) with its lines, hidden until the body's
    /// ParachuteRider shows it.
    /// </summary>
    public static class ParachuteBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Parachute.prefab";
        private const string CanopyMeshPath = "Assets/CityX/Weapons/ParachuteCanopy.asset";
        private const string LinesMaterialPath = "Assets/CityX/Weapons/ParachuteLines.mat";
        private const string WindClipPath = "Assets/CityX/Weapons/Audio/jetpack_wind_loop.wav";

        private static readonly Color ContainerColour = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color FlapColour = new Color(0.23f, 0.25f, 0.31f);
        private static readonly Color HandleColour = new Color(0.95f, 0.42f, 0.08f);
        private static readonly Color CanopyColourA = new Color(0.95f, 0.40f, 0.06f);
        private static readonly Color CanopyColourB = new Color(0.93f, 0.91f, 0.86f);
        private static readonly Color MouthColour = new Color(0.10f, 0.09f, 0.10f);
        private static readonly Color LineColour = new Color(0.14f, 0.14f, 0.15f);

        /// <summary>The container: wide, tall and deep, metres, and where its middle sits behind the mount point.</summary>
        private static readonly Vector3 ContainerSize = new Vector3(0.42f, 0.62f, 0.18f);

        private static readonly Vector3 ContainerCentre = new Vector3(0.0f, 0.02f, -0.02f);

        private static readonly Vector3 FlapSize = new Vector3(0.40f, 0.16f, 0.20f);

        private static readonly Vector3 FlapCentre = new Vector3(0.0f, 0.34f, -0.03f);

        /// <summary>The pilot chute's pouch at the bottom of the container, and the handle sticking out of it.</summary>
        private static readonly Vector3 PouchSize = new Vector3(0.16f, 0.06f, 0.16f);

        private static readonly Vector3 PouchCentre = new Vector3(0.08f, -0.30f, -0.04f);

        private const float HandleSize = 0.06f;

        private static readonly Vector3 HandleCentre = new Vector3(0.12f, -0.33f, -0.13f);

        [MenuItem("CityX/Rebuild Parachute")]
        public static GameObject Build()
        {
            var root = new GameObject("Parachute");
            try
            {
                var pack = new GameObject("Pack");
                pack.transform.SetParent(root.transform, false);
                Box(pack.transform, "Container", ContainerCentre, ContainerSize, Matte("ParachuteContainer", ContainerColour));
                Box(pack.transform, "Flap", FlapCentre, FlapSize, Matte("ParachuteFlap", FlapColour));
                Box(pack.transform, "Pouch", PouchCentre, PouchSize, Matte("ParachuteFlap", FlapColour));
                var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                handle.name = "Handle";
                Object.DestroyImmediate(handle.GetComponent<Collider>());
                handle.transform.SetParent(pack.transform, false);
                handle.transform.localPosition = HandleCentre;
                handle.transform.localScale = Vector3.one * HandleSize;
                handle.GetComponent<MeshRenderer>().sharedMaterial = Matte("ParachuteHandle", HandleColour);

                var canopy = new GameObject("Canopy");
                canopy.transform.SetParent(root.transform, false);
                canopy.AddComponent<MeshFilter>().sharedMesh = SaveCanopyMesh();
                var canopyRenderer = canopy.AddComponent<MeshRenderer>();
                var sheets = new Material[3];
                sheets[CanopyShape.ColourA] = Matte("ParachuteCanopyA", CanopyColourA);
                sheets[CanopyShape.ColourB] = Matte("ParachuteCanopyB", CanopyColourB);
                sheets[CanopyShape.Mouths] = Matte("ParachuteMouths", MouthColour);
                canopyRenderer.sharedMaterials = sheets;
                var shown = canopy.AddComponent<ParachuteCanopy>();

                var lines = new GameObject("Lines");
                lines.transform.SetParent(canopy.transform, false);
                shown.Lines = lines.AddComponent<MeshFilter>();
                var lineRenderer = lines.AddComponent<MeshRenderer>();
                lineRenderer.sharedMaterial = LinesMaterial();
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                canopy.SetActive(false);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = Parachute.InteractRange;

                var parachute = root.AddComponent<Parachute>();
                parachute.PackVisual = pack;
                parachute.Canopy = shown;
                parachute.WindClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WindClipPath);
                if (parachute.WindClip == null)
                {
                    Debug.LogWarning($"CityX: {WindClipPath} missing -- a fall under a parachute will have no wind.");
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: parachute written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material Matte(string name, Color colour)
        {
            Material material = CharacterSkin.Ensure(name, colour);
            material.SetFloat("_Smoothness", 0.1f);
            return material;
        }

        private static void Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh SaveCanopyMesh()
        {
            Mesh built = CanopyShape.Build();
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CanopyMeshPath);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(built, CanopyMeshPath);
                return built;
            }
            EditorUtility.CopySerialized(built, mesh);
            mesh.name = "ParachuteCanopy";
            EditorUtility.SetDirty(mesh);
            Object.DestroyImmediate(built);
            return mesh;
        }

        /// <summary>The lines: thin and dark, unlit (they are drawn a pixel wide).</summary>
        private static Material LinesMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(LinesMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, LinesMaterialPath);
            }
            material.SetColor("_BaseColor", LineColour);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
