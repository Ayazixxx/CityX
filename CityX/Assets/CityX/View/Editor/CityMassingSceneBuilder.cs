using System.IO;
using CityX.Character;
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

            CityFacadeTextures.Build();

            // The mesh builder writes UVs in METRES, so tiling is simply the
            // reciprocal of how many metres of world each texture covers. That
            // is what makes the facade line up with real floors and window
            // columns on every face of every building with no per-building UV
            // work -- and it is why these numbers are derived from the texture
            // rather than typed in.
            Material ground = EnsureMaterial("CityGround", "CityRoad", 8.0f, 0.10f);
            // Also carries roofs and walkway decks -- same material in the real
            // world, and the buffer is named flatConcrete for that reason.
            Material pavement = EnsureMaterial("CityPavement", "CityPavement", 4.0f, 0.05f);
            Material structure = EnsureMaterial("CityConcrete", "CityFacade",
                CityFacadeTextures.FacadeTileWidth, 0.04f);

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
            // 1.15, not the 1.7 this started at. With no tonemapping in the
            // pipeline yet, anything much above 1 drives every sunlit face to
            // pure white and the concrete reads as painted render. Dropping the
            // albedo instead was the wrong lever -- it only made the shadowed
            // faces muddy while the lit ones stayed blown out.
            sun.intensity = 1.15f;
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

            // --- the player. The flycam stays on the same object but disabled,
            // --- so switching between walking the city and flying over it is a
            // --- checkbox rather than a scene edit.
            GameObject playerPrefab = CityX.CharacterEditor.PlayerRigBuilder.Build();
            if (playerPrefab != null)
            {
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Player";
                // A little above the road so the controller settles onto it
                // rather than starting intersecting it.
                player.transform.position = new Vector3(
                    CityLayout.ChunkSize * 0.5f, groundHeight + 0.5f, CityLayout.ChunkSize * 0.5f);

                cameraObject.GetComponent<FlyCamera>().enabled = false;
                var follow = cameraObject.AddComponent<ThirdPersonCamera>();
                follow.Target = player.transform;
            }
            else
            {
                Debug.LogWarning("CityX: no player prefab -- leaving the flycam enabled.");
            }

            GameObject streamerObject = new GameObject("CityStreamer");
            CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = 20260810;
            streamer.RadiusChunks = 14;
            streamer.GroundMaterial = ground;
            streamer.PavementMaterial = pavement;
            streamer.StructureMaterial = structure;
            streamer.Viewer = cameraObject.transform;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"CityX: massing scene written to {ScenePath}");
        }

        /// <summary>
        /// Creates or updates a URP Lit material using one of the generated
        /// textures. Updating in place rather than replacing keeps the scene's
        /// reference to it valid across rebuilds.
        /// </summary>
        /// <param name="metresPerTile">
        /// How much world the texture covers. Tiling is its reciprocal, because
        /// UVs are in metres.
        /// </param>
        private static Material EnsureMaterial(string name, string textureName,
            float metresPerTile, float smoothness)
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

            string textureDirectory = "Assets/CityX/View/Textures";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureDirectory}/{textureName}.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureDirectory}/{textureName}Normal.png");

            if (albedo == null)
            {
                Debug.LogError($"CityX: texture {textureName} missing -- run CityX/Rebuild City Textures.");
            }

            float tiling = 1.0f / metresPerTile;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.0f);

            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                material.SetFloat("_BumpScale", 1.0f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
