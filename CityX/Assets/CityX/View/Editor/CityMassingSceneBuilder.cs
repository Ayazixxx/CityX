using System.IO;
using CityX.View;
using CityX.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the massing scene from scratch, including its materials.
    ///
    /// WHY THIS IS A SCRIPT AND NOT A HAND-AUTHORED SCENE. A .unity file is a
    /// large opaque YAML blob that cannot be reviewed in a diff and cannot be
    /// regenerated if it breaks. Everything here is a dozen lines of readable
    /// setup instead, and re-running it repairs the scene rather than requiring
    /// somebody to remember what was in it.
    ///
    /// Runs headlessly:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.ViewEditor.CityMassingSceneBuilder.Build
    /// </summary>
    public static class CityMassingSceneBuilder
    {
        private const string SceneDirectory = "Assets/CityX/Scenes";
        private const string ScenePath = SceneDirectory + "/CityMassing.unity";
        private const string MaterialDirectory = "Assets/CityX/View/Materials";

        [MenuItem("CityX/Rebuild Massing Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDirectory);
            Directory.CreateDirectory(MaterialDirectory);

            // Ground is darker than the buildings but not by much. The first
            // pass used 0.20 and the streets rendered as a black void -- with
            // no sky light bouncing into a narrow canyon, a dark albedo down
            // there receives almost nothing and the whole lower half of every
            // shot went to mud.
            Material ground = EnsureMaterial("CityGround", new Color(0.44f, 0.45f, 0.46f), 0.95f);
            Material structure = EnsureMaterial("CityConcrete", new Color(0.62f, 0.63f, 0.64f), 0.85f);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // An empty scene has no environment lighting, so every surface
            // facing away from the sun goes almost black. In a city of narrow
            // canyons that is most surfaces.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.40f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.20f, 0.21f);

            // --- light. Angled rather than straight down so the massing casts
            // --- readable shadows; a flat top light makes every box look like a
            // --- flat grey rectangle and hides the thing being judged.
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.97f, 0.92f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(38.0f, 145.0f, 0.0f);

            // --- camera, standing in a street rather than above the city, since
            // --- street level is where the reference lives.
            GameObject cameraObject = new GameObject("FlyCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 2000.0f;
            camera.nearClipPlane = 0.15f;
            camera.backgroundColor = new Color(0.55f, 0.60f, 0.66f);
            cameraObject.AddComponent<FlyCamera>();
            cameraObject.AddComponent<AudioListener>();

            TerrainField terrain = new TerrainField(20260810);
            // Chunk (0,0) is a street on both axes, so this is a road junction
            // with buildings on every side -- the view worth opening on.
            float groundHeight = terrain.PadHeightFor(0, 0);
            cameraObject.transform.position = new Vector3(
                CityLayout.ChunkSize * 0.5f, groundHeight + 1.7f, CityLayout.ChunkSize * 0.5f);
            cameraObject.transform.rotation = Quaternion.Euler(4.0f, 35.0f, 0.0f);

            GameObject streamerObject = new GameObject("CityStreamer");
            CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = 20260810;
            streamer.RadiusChunks = 14;
            streamer.GroundMaterial = ground;
            streamer.StructureMaterial = structure;
            streamer.Viewer = cameraObject.transform;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"CityX: massing scene written to {ScenePath}");
        }

        /// <summary>
        /// Creates or updates a URP Lit material. Updating in place rather than
        /// replacing keeps the scene's reference to it valid across rebuilds.
        /// </summary>
        private static Material EnsureMaterial(string name, Color colour, float smoothnessInverse)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    // Naming the cause matters here: a missing URP shader means
                    // the render pipeline is not set up, and the symptom would
                    // otherwise be a city rendered in flat magenta.
                    Debug.LogError("CityX: URP Lit shader not found -- is the Universal Render Pipeline " +
                                   "asset assigned in Graphics settings?");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 1.0f - smoothnessInverse);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
