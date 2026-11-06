using System;
using System.IO;
using CityX.Character;
using CityX.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CityX.UIEditor
{
    /// <summary>
    /// Bakes the creator HAIR tab's pictures (HairIcons): the title's own creator, with its white head, its layer, its lights and its
    /// backdrop, photographed wearing every style in brown, all from one camera so the pictures compare. Needs a graphics device:
    ///
    ///     Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.UIEditor.HairIconBaker.Bake
    ///
    /// The title's tiles take their pictures when the title is built (HairPage), so a first bake is followed by a title rebuild
    /// (RebuildAndBake does both); a later bake rewrites the same assets, which the built title already points at. The title scene is
    /// opened and left as the bake leaves it, never saved.
    /// </summary>
    public static class HairIconBaker
    {
        private const string TitleScenePath = "Assets/CityX/Scenes/Title.unity";
        private const string ParentFolder = "Assets/CityX/UI";
        private const string ResourcesFolder = ParentFolder + "/Resources";
        public const string IconFolder = ResourcesFolder + "/" + HairIcons.Folder;

        /// <summary>A long lens: the creator's own 75 degrees, a metre from a head, swells whatever is nearest the camera.</summary>
        public const float FieldOfView = 30.0f;

        /// <summary>The camera stands this far round to the body's left, the side the creator's key light comes from (CharacterCreatorBuilder), so the side in view is lit and dark hair stands off the dark backdrop.</summary>
        public const float TurnDegrees = 0.0f;

        /// <summary>And this far above level: a little above the eyes.</summary>
        public const float ElevationDegrees = 10.0f;

        /// <summary>Room left round the framed box, as a share of its size on each side.</summary>
        public const float MarginShare = 0.05f;

        /// <summary>How far past the head a style's hair can be framed, in the head's own size.</summary>
        public const float HairReachHeads = 0.12f;

        /// <summary>Rendered at twice the picture's size and averaged down, on top of the MSAA, so the small picture's edges are smooth.</summary>
        private const int Supersample = 2;
        private const int RenderSize = HairIcons.Size * Supersample;

        /// <summary>
        /// Pixelated (owner, 2026-11-05: "make the icons pixelated"): drawn this many pixels across, each shown as a hard square block of the
        /// <see cref="HairIcons.Size"/> picture and imported unsmoothed.
        /// </summary>
        public const int Pixels = 32;

        /// <summary>The black lines this many of those pixels wide.</summary>
        private const float LinePixels = 1.0f;
        private const int MsaaSamples = 4;

        /// <summary>The height the game's own line width is given for (CityXOutline.shader): at that width the lines would thin to nothing at this size.</summary>
        private const float OutlineReferenceHeight = 1080.0f;

        /// <summary>One hair colour for every picture: HairPage's brown swatch.</summary>
        /// <summary>A pixel is hair where the white-haired and black-haired pictures differ by more than this (brightness, 0 to 1).</summary>
        private const float HairDifference = 0.04f;

        public static string PathFor(string style) => IconFolder + "/" + style + ".png";

        /// <summary>Where the hair layer of <paramref name="style"/> is written (none for the bald head).</summary>
        public static string HairPathFor(string style) => IconFolder + "/" + style + HairIcons.HairSuffix + ".png";

        [MenuItem("CityX/Bake Hair Icons")]
        public static void Bake()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException("CityX: the hair icons are rendered, so the bake needs a graphics device: run Unity without -nographics.");
            }
            // The title scene replaces whatever is open: offer to save that first (a batch run has nothing of anybody's open).
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

            // The title has one creator (the character list's carousel carries none); its component sits on its stage.
            var creator = Object.FindFirstObjectByType<CharacterCreator>(FindObjectsInactive.Include);
            if (creator == null || creator.Camera == null || creator.HeadRenderer == null || creator.Hair == null)
            {
                throw new InvalidOperationException($"CityX: {TitleScenePath} has no complete character creator -- rebuild the title screen (TitleScreenBuilder.Build).");
            }

            // As TitleScreenCapture: the sky (the ambient on the body) is the cycle's to set, and the cycle does not run outside play mode.
            var cycle = Object.FindFirstObjectByType<View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.Apply(View.SkyCycle.At(cycle.TimeOfDay));
            }

            GameObject stage = creator.gameObject;
            stage.SetActive(true);
            // Awake's job, which edit mode skips: the body in its white paint, not its world material over the paint's UVs.
            creator.Initialise();
            // Arms at the sides (owner, 2026-11-05: "have the arms down to his side"): the creator's T-pose put them across every picture.
            StillPose.ArmsDown(creator.transform);

            Camera camera = creator.Camera;
            camera.fieldOfView = FieldOfView;
            Bounds box = HeadAndEveryStyle(creator);
            // The hair is worn by the body's root, so its transform faces the way the body does.
            Pose pose = Framing(box, creator.Hair.transform.forward, FieldOfView);
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);

            EnsureFolders();
            try
            {
                Render(creator, camera);
            }
            finally
            {
                stage.SetActive(false);
            }

            foreach (string style in CharacterHair.ValidStyles)
            {
                Import(PathFor(style), seeThrough: false);
                if (style != CharacterHair.None)
                {
                    Import(HairPathFor(style), seeThrough: true);
                }
            }
            RemoveStale();
            Debug.Log($"CityX: {CharacterHair.ValidStyles.Length} hair icons baked into {IconFolder}, framed alike on the head and every style " +
                      $"({box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00} m) from {Vector3.Distance(pose.position, box.center):0.00} m.");
        }

        /// <summary>The title rebuilt, its pictures baked from it, and the title rebuilt again so its HAIR tab carries them: one editor start.</summary>
        public static void RebuildAndBake()
        {
            TitleScreenBuilder.Build();
            Bake();
            TitleScreenBuilder.Build();
        }

        /// <summary>The box every picture is framed on: the head and every style's hair on it, so the longest style fits the frame the shortest has.</summary>
        private static Bounds HeadAndEveryStyle(CharacterCreator creator)
        {
            Bounds head = creator.HeadRenderer.bounds;
            Bounds box = head;
            // Hair reaches at most a head's size past the head. A skinned style's bounds can come back body-sized in the editor before it is
            // ever drawn, and framed on those every picture was the whole body with a speck of hair.
            Bounds reach = head;
            reach.Expand(head.size * (2.0f * HairReachHeads));
            foreach (string style in CharacterHair.ValidStyles)
            {
                creator.SetHairStyle(style);
                Transform worn = creator.Hair.Worn;
                if (worn != null)
                {
                    Bounds hair = worn.GetComponent<Renderer>().bounds;
                    box.Encapsulate(Vector3.Max(Vector3.Min(hair.min, reach.max), reach.min));
                    box.Encapsulate(Vector3.Max(Vector3.Min(hair.max, reach.max), reach.min));
                }
                else if (style != CharacterHair.None)
                {
                    throw new InvalidOperationException($"CityX: \"{style}\" puts no hair on the creator's head -- rebuild the title screen (TitleScreenBuilder.Build).");
                }
            }
            box.Expand(box.size * (2.0f * MarginShare));
            return box;
        }

        /// <summary>
        /// Where the camera stands for every picture: <see cref="TurnDegrees"/> round to the body's left of <paramref name="facing"/> and
        /// <see cref="ElevationDegrees"/> above level, looking at <paramref name="box"/>'s centre from just far enough away that all eight of its
        /// corners are in a square picture <paramref name="fieldOfView"/> degrees high. Pure, so a test checks the fit without a scene.
        /// </summary>
        public static Pose Framing(Bounds box, Vector3 facing, float fieldOfView)
        {
            facing.y = 0.0f;
            facing = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
            Vector3 round = Quaternion.AngleAxis(-TurnDegrees, Vector3.up) * facing;
            float lift = ElevationDegrees * Mathf.Deg2Rad;
            Vector3 toCamera = round * Mathf.Cos(lift) + Vector3.up * Mathf.Sin(lift);
            Quaternion rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            // Square, so a corner is in when it is within the half-angle's tangent of the axis both across and up.
            float halfTan = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = 0.0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(box.extents,
                    new Vector3((corner & 1) == 0 ? -1.0f : 1.0f, (corner & 2) == 0 ? -1.0f : 1.0f, (corner & 4) == 0 ? -1.0f : 1.0f));
                float nearer = Vector3.Dot(offset, toCamera);
                float across = Mathf.Max(Mathf.Abs(Vector3.Dot(offset, right)), Mathf.Abs(Vector3.Dot(offset, up)));
                distance = Mathf.Max(distance, across / halfTan + nearer);
            }
            return new Pose(box.center + toCamera * distance, rotation);
        }

        /// <summary>The camera's picture, halved a step at a time (each bilinear blit averaging 2x2 blocks) down to the pixel art's size.</summary>
        private static Color[] Shoot(Camera camera, RenderTexture shot, RenderTexture half, RenderTexture quarter, RenderTexture small)
        {
            camera.Render();
            Graphics.Blit(shot, half);
            Graphics.Blit(half, quarter);
            Graphics.Blit(quarter, small);
            RenderTexture.active = small;
            var read = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
            Color[] colours = read.GetPixels();
            for (int i = 0; i < colours.Length; i++)
            {
                colours[i].a = 1.0f;
            }
            Object.DestroyImmediate(read);
            return colours;
        }

        /// <summary>The hair alone: the white-haired picture where it differs from the black-haired one, see-through everywhere else.</summary>
        public static Color[] HairLayer(Color[] white, Color[] black)
        {
            var layer = new Color[white.Length];
            for (int i = 0; i < white.Length; i++)
            {
                Color difference = white[i] - black[i];
                bool hair = difference.grayscale > HairDifference;
                layer[i] = hair ? new Color(white[i].r, white[i].g, white[i].b, 1.0f) : Color.clear;
            }
            return layer;
        }

        /// <summary><paramref name="pixels"/> blown up into <paramref name="picture"/> in hard blocks and written as a PNG at <paramref name="path"/>.</summary>
        private static void Write(Texture2D pixels, Texture2D picture, string path)
        {
            Blocky(pixels, picture);
            File.WriteAllBytes(OnDisk(path), picture.EncodeToPNG());
        }

        /// <summary>Each of <paramref name="pixels"/> as a square block of <paramref name="picture"/>, unblended.</summary>
        public static void Blocky(Texture2D pixels, Texture2D picture)
        {
            int block = picture.width / pixels.width;
            for (int y = 0; y < picture.height; y++)
            {
                for (int x = 0; x < picture.width; x++)
                {
                    picture.SetPixel(x, y, pixels.GetPixel(x / block, y / block));
                }
            }
            picture.Apply();
        }

        /// <summary>Every style photographed and written as a PNG, in the game's cel look (which only play mode switches on, CelShading).</summary>
        private static void Render(CharacterCreator creator, Camera camera)
        {
            var shot = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = MsaaSamples,
                filterMode = FilterMode.Bilinear,
            };
            var small = new RenderTexture(Pixels, Pixels, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var half = new RenderTexture(RenderSize / 2, RenderSize / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var quarter = new RenderTexture(RenderSize / 4, RenderSize / 4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
            var hairPixels = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
            var picture = new Texture2D(HairIcons.Size, HairIcons.Size, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            bool celLook = View.CelShading.Enabled;
            float lineWidth = View.CelShading.OutlineWidth;
            try
            {
                // A shader still compiling in the background draws as a cyan stand-in, which a picture would keep for good.
                ShaderUtil.allowAsyncCompilation = false;
                View.CelShading.SetOutlineWidth(LinePixels * OutlineReferenceHeight / Pixels);
                View.CelShading.Set(true);
                camera.targetTexture = shot;
                foreach (string style in CharacterHair.ValidStyles)
                {
                    creator.SetHairStyle(style);
                    // The hair white, then black: the black picture is the base, and where the two differ is the hair, its white shading a layer
                    // the tile tints with the hair colour.
                    creator.SetHairColour(Color.white);
                    Color[] white = Shoot(camera, shot, half, quarter, small);
                    creator.SetHairColour(Color.black);
                    Color[] black = Shoot(camera, shot, half, quarter, small);
                    pixels.SetPixels(black);
                    pixels.Apply();
                    Write(pixels, picture, PathFor(style));
                    if (style != CharacterHair.None)
                    {
                        hairPixels.SetPixels(HairLayer(white, black));
                        hairPixels.Apply();
                        Write(hairPixels, picture, HairPathFor(style));
                    }
                    Debug.Log($"CityX: hair icon \"{style}\" written to {PathFor(style)}");
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                View.CelShading.SetOutlineWidth(lineWidth);
                View.CelShading.Set(celLook);
                shot.Release();
                small.Release();
                half.Release();
                quarter.Release();
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(half);
                Object.DestroyImmediate(quarter);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(hairPixels);
                Object.DestroyImmediate(picture);
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder(ParentFolder, "Resources");
            }
            if (!AssetDatabase.IsValidFolder(IconFolder))
            {
                AssetDatabase.CreateFolder(ResourcesFolder, HairIcons.Folder);
            }
        }

        /// <summary>Imported as the tab draws it: sharp at its own size (no mipmaps, no compression), not repeated past its edge; a hair layer see-through.</summary>
        private static void Import(string path, bool seeThrough)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                throw new InvalidOperationException($"CityX: Unity did not import {path} (is another program holding the folder?).");
            }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.maxTextureSize = HairIcons.Size;
            importer.alphaIsTransparency = seeThrough;
            importer.SaveAndReimport();
        }

        /// <summary>Takes away the pictures of styles there are no longer: a Resources folder goes whole into every build.</summary>
        private static void RemoveStale()
        {
            foreach (string file in Directory.GetFiles(OnDisk(IconFolder), "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string style = name.EndsWith(HairIcons.HairSuffix) ? name.Substring(0, name.Length - HairIcons.HairSuffix.Length) : name;
                if (Array.IndexOf(CharacterHair.ValidStyles, style) < 0)
                {
                    string asset = IconFolder + "/" + name + ".png";
                    AssetDatabase.DeleteAsset(asset);
                    Debug.Log($"CityX: removed {asset}: there is no \"{style}\" style any more.");
                }
            }
        }

        private static string OnDisk(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }
}
