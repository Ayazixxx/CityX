using CityX.Character;
using CityX.Core;
using CityX.UI;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.UIEditor
{
    /// <summary>
    /// The title's foreground (owner, 2026-11-11, with a mock-up: the logo top left, the menu under it, "on the right of the screen is your
    /// most recently played character standing on top of a building in idle stance, holding their primary weapon equipped. there is the city
    /// environment in the background"): the last played character (<see cref="TitleHero"/>) with two lamps of their own, which TitleStage
    /// stands on a real roof of the game's city when the title opens, and the 3D logo hung in the top left corner of the view
    /// (<see cref="TitleLogo"/>), its neon blooming in the game's own grade.
    /// </summary>
    public static class TitleRooftopBuilder
    {
        public const string LogoModelPath = "Assets/CityX/UI/Models/CityXLogo.fbx";

        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";


        /// <summary>The body is woken this far over the roof, clear of it.</summary>
        private const float DropOnto = 0.06f;

        private const string AssetFolder = "Assets/CityX/UI/Title";
        private const string SoundFolder = "Assets/CityX/UI/Audio";

        /// <summary>How loud the sign's hum and its flickers are (the sounds are brought up to one peak).</summary>
        private const float HumVolume = 0.012f;

        private const float FlickerVolume = 0.035f;

        /// <summary>The roof round the character, metres: how far it reaches right (off the screen) and toward the camera (past it), how far the
        /// building under it goes down, how far over the real roof it stands, and its parapet's height and thickness.</summary>
        private const float RoofRight = 14.0f;

        private const float RoofFront = 7.5f;
        private const float RoofDepthBelow = 60.0f;
        private const float RoofOver = 0.03f;
        private const float ParapetHeight = 1.0f;
        private const float ParapetThickness = 0.35f;

        private static readonly Color RoofColour = new Color(0.20f, 0.20f, 0.21f);
        private static readonly Color ParapetColour = new Color(0.33f, 0.33f, 0.34f);
        private static readonly Color KeyColour = new Color(1.0f, 0.90f, 0.80f);
        private static readonly Color RimColour = new Color(0.45f, 0.58f, 1.0f);
        private static readonly Color NeonGlowColour = new Color(1.0f, 0.2f, 1.0f);
        private static readonly Color LetterBlack = new Color(0.10f, 0.10f, 0.11f);
        private static readonly Color SignLightColour = new Color(0.80f, 0.86f, 1.0f);
        private const float SignLightIntensity = 6.0f;

        private const float KeyIntensity = 30.0f;
        private const float RimIntensity = 18.0f;

        /// <summary>Builds the character with their lamps (their feet at the returned rooftop's origin, its +Z away from the camera) and the logo.</summary>
        public static (TitleHero hero, TitleLogo logo, Transform rooftop) Build(Camera camera, string assetFolder)
        {
            Transform rooftop = BuildRooftop();
            TitleLogo logo = BuildLogo(camera, assetFolder);
            return (rooftop.GetComponentInChildren<TitleHero>(true), logo, rooftop);
        }

        private static Transform BuildRooftop()
        {
            var rooftop = new GameObject("Rooftop").transform;

            // The character, facing the camera (local -Z).
            var heroObject = new GameObject("TitleHero");
            heroObject.transform.SetParent(rooftop, false);
            heroObject.transform.localPosition = Vector3.up * DropOnto;
            heroObject.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);   // (turned toward the screen's middle by TitleStage)
            var hero = heroObject.AddComponent<TitleHero>();
            hero.Body = BuildBody(heroObject.transform, AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));

            // The roof round them, the first one's (owner, 2026-11-11, of it: "i like the positioning on this screenshot. also i like the wall
            // behind him. keep this building in this positioning"): set on the real roof the stage chooses, its edges on that roof's (its
            // left edge and the parapet behind them at TitleViewpoint's gaps), a hair over it so it covers what is there.
            Material roofMaterial = HeldModelBaker.LitMaterial($"{AssetFolder}/TitleRoof.mat", RoofColour, 0.0f, 0.15f);
            Material parapetMaterial = HeldModelBaker.LitMaterial($"{AssetFolder}/TitleParapet.mat", ParapetColour, 0.0f, 0.1f);
            float left = -TitleViewpoint.SideGap, back = TitleViewpoint.EdgeGap, top = RoofOver;
            Block(rooftop, "Roof", roofMaterial, left, RoofRight, -RoofDepthBelow, top, -RoofFront, back);
            Block(rooftop, "ParapetBack", parapetMaterial, left, RoofRight, top, top + ParapetHeight, back - ParapetThickness, back);
            Block(rooftop, "ParapetLeft", parapetMaterial, left, left + ParapetThickness, top, top + ParapetHeight, -RoofFront, back);
            // A stair housing behind them to the right, and an aerial on the far corner (the mock-up's).
            Block(rooftop, "StairHousing", parapetMaterial, 2.2f, 5.0f, top, top + 3.0f, 0.4f, back - ParapetThickness);
            Block(rooftop, "AerialMast", parapetMaterial, 6.9f, 7.0f, top + ParapetHeight, top + ParapetHeight + 4.2f, back - 0.25f, back - 0.15f);
            for (int rung = 0; rung < 3; rung++)
            {
                float height = top + ParapetHeight + 2.6f + rung * 0.5f;
                float half = 0.5f - rung * 0.12f;
                Block(rooftop, "AerialBar" + rung, parapetMaterial, 6.95f - half, 6.95f + half, height, height + 0.06f, back - 0.22f, back - 0.18f);
            }

            // Their lamps: a warm key from the front left above, a cold rim from behind on the right.
            var chest = new Vector3(0.0f, 1.7f, 0.0f);
            Lamp(rooftop, "HeroKey", KeyColour, KeyIntensity, chest + new Vector3(-2.2f, 2.4f, -3.0f), chest);
            Lamp(rooftop, "HeroRim", RimColour, RimIntensity, chest + new Vector3(2.0f, 1.2f, 3.0f), chest);
            return rooftop;
        }

        /// <summary>A whole player's body, switched off until TitleHero gives it a character: no input (the menu's keys must not walk it), with an inventory and hair.</summary>
        private static GameObject BuildBody(Transform parent, GameObject playerPrefab)
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("CityX: no player prefab -- the title's rooftop stands empty.");
                return null;
            }
            var body = (GameObject)Object.Instantiate(playerPrefab, parent, false);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            Remove<PlayerInputReader>(body);
            Remove<PlayerAudio>(body);
            Remove<AudioSource>(body);
            if (body.GetComponent<Inventory>() == null)
            {
                body.AddComponent<Inventory>().ItemPrefabs = CharacterEditor.NetworkedPlayerWiring.ItemPrefabEntries();
            }
            HairAssets.AddTo(body);
            body.SetActive(false);
            return body;
        }

        private static void Remove<T>(GameObject body) where T : Component
        {
            foreach (T found in body.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(found);
            }
        }

        /// <summary>A box from (x0, y0, z0) to (x1, y1, z1) in <paramref name="parent"/>'s space (x right, y up, z away from the camera), solid.</summary>
        private static void Block(Transform parent, string name, Material material, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            box.transform.localScale = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Lamp(Transform parent, string name, Color colour, float intensity, Vector3 at, Vector3 lookingAt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.LookRotation(lookingAt - at, Vector3.up);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = colour;
            light.intensity = intensity;
            light.range = 12.0f;
            light.spotAngle = 50.0f;
            light.innerSpotAngle = 30.0f;
            light.shadows = LightShadows.None;
            light.renderingLayerMask = (int)TitleScreenBuilder.DefaultRenderingLayer;
        }

        /// <summary>
        /// The logo model with its four parts' materials, turned so its front faces the camera (local -Z), x right and y up whatever axes the
        /// model came in with (measured: the neon stands in front of the letters, the small white tag is over their left), and held in the
        /// view's top left corner.
        /// </summary>
        private static TitleLogo BuildLogo(Camera camera, string assetFolder)
        {
            ImportLogoModel();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LogoModelPath);
            if (model == null)
            {
                Debug.LogError($"CityX: no logo model at {LogoModelPath} (SourceArt/TitleLogo/logo_build.py makes it).");
                return null;
            }
            var root = new GameObject("Logo");
            var instance = (GameObject)Object.Instantiate(model, root.transform, false);
            instance.name = "Model";

            Renderer letters = Part(instance, "Letters"), rim = Part(instance, "Rim"), face = Part(instance, "Face"), neon = Part(instance, "Neon"),
                tag = Part(instance, "Tag");
            if (letters == null || rim == null || face == null || neon == null || tag == null)
            {
                Debug.LogError("CityX: the logo model is missing a part (Letters, Rim, Face, Neon, Tag).");
                return null;
            }
            // The black a dark grey, so its carved facets read against the night sky.
            Material black = HeldModelBaker.LitMaterial($"{assetFolder}/TitleLogoBlack.mat", LetterBlack, 0.0f, 0.35f);
            Material red = HeldModelBaker.LitMaterial($"{assetFolder}/TitleLogoRed.mat", new Color(0.78f, 0.04f, 0.08f), 0.0f, 0.45f);
            Material tube = Glowing(HeldModelBaker.LitMaterial($"{assetFolder}/TitleLogoNeon.mat", new Color(0.55f, 0.25f, 0.55f), 0.0f, 0.8f));
            Material white = Glowing(HeldModelBaker.LitMaterial($"{assetFolder}/TitleLogoTag.mat", new Color(0.86f, 0.86f, 0.88f), 0.3f, 0.8f),
                new Color(0.25f, 0.25f, 0.27f));
            // The tubes are drawn after the black lines of the cel look (CityXOutline, on the opaque world): a tube a few pixels across is
            // all edge, and the lines drowned it in black.
            tube.renderQueue = (int)RenderQueue.Transparent;
            white.renderQueue = (int)RenderQueue.Transparent;
            letters.sharedMaterial = black;
            rim.sharedMaterial = red;
            face.sharedMaterial = black;
            neon.sharedMaterial = tube;
            tag.sharedMaterial = white;
            Renderer[] parts = { letters, rim, face, neon, tag };
            foreach (Renderer part in parts)
            {
                part.shadowCastingMode = ShadowCastingMode.Off;
                part.renderingLayerMask = TitleScreenBuilder.DefaultRenderingLayer;
            }

            Orient(instance.transform, letters, neon, tag);
            var logo = root.AddComponent<TitleLogo>();
            logo.View = camera;
            logo.Neon = neon;
            logo.Bounds = LocalBounds(root.transform, parts);
            // A floodlight on the sign from above its top left, as a sign at night has: the letters read when the neon falters.
            var flood = new GameObject("SignLight");
            flood.transform.SetParent(root.transform, false);
            Bounds b = logo.Bounds;
            flood.transform.localPosition = new Vector3(b.min.x + 0.2f * b.size.x, b.max.y + 0.5f * b.size.y, b.min.z - 1.2f * b.size.y);
            flood.transform.localRotation = Quaternion.LookRotation(b.center - flood.transform.localPosition, Vector3.up);
            Light signLight = flood.AddComponent<Light>();
            signLight.type = LightType.Spot;
            signLight.color = SignLightColour;
            signLight.spotAngle = 90.0f;
            signLight.innerSpotAngle = 60.0f;
            signLight.shadows = LightShadows.None;
            signLight.renderingLayerMask = (int)TitleScreenBuilder.DefaultRenderingLayer;
            var glow = new GameObject("NeonGlow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(logo.Bounds.center.x, logo.Bounds.center.y, logo.Bounds.min.z - 0.6f * logo.Bounds.size.y);
            logo.Glow = glow.AddComponent<Light>();
            logo.Glow.type = LightType.Point;
            logo.Glow.color = NeonGlowColour;
            logo.Glow.shadows = LightShadows.None;
            logo.Glow.renderingLayerMask = (int)TitleScreenBuilder.DefaultRenderingLayer;
            // The rooftop at night (the sign humming, the wind), low and looped, and a flicker sound as each fault begins
            // (SourceArt/TitleSound/title_sounds.py makes them).
            logo.Hum = root.AddComponent<AudioSource>();
            logo.Hum.clip = Sound($"{SoundFolder}/TitleRooftopNight.wav", uncompressed: true);
            logo.Hum.loop = true;
            logo.Hum.playOnAwake = true;
            logo.Hum.volume = HumVolume;
            logo.Hum.spatialBlend = 0.0f;
            logo.Faulting = root.AddComponent<AudioSource>();
            logo.Faulting.playOnAwake = false;
            logo.Faulting.volume = FlickerVolume;
            logo.Faulting.spatialBlend = 0.0f;
            logo.FlickerSounds = new[]
            {
                Sound($"{SoundFolder}/TitleNeonFlicker1.wav", uncompressed: false), Sound($"{SoundFolder}/TitleNeonFlicker2.wav", uncompressed: false),
            };
            logo.Hold();
            logo.Flicker(0.0f);
            // Reaching across the letters and no further (not over to the rooftop).
            float across = logo.Bounds.size.x * root.transform.localScale.x;
            logo.Glow.range = across;
            signLight.range = 1.6f * across;
            signLight.intensity = SignLightIntensity;
            return logo;
        }

        /// <summary>
        /// A sound, <paramref name="uncompressed"/> for a loop (a compressed one can open a hair of a gap where it comes round), kept in memory
        /// either way (they are short).
        /// </summary>
        private static AudioClip Sound(string path, bool uncompressed)
        {
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                AudioCompressionFormat format = uncompressed ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
                if (settings.compressionFormat != format || settings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    settings.compressionFormat = format;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError($"CityX: no sound at {path} (SourceArt/TitleSound/title_sounds.py makes it).");
            }
            return clip;
        }

        private static Material Glowing(Material material, Color? glow = null)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // with none, the keyword is dropped as the material is saved
            material.SetColor("_EmissionColor", glow ?? Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Renderer Part(GameObject model, string name)
        {
            Transform part = TransformSearch.FindDeep(model.transform, name);
            return part != null ? part.GetComponent<Renderer>() : null;
        }

        private static void Orient(Transform model, Renderer letters, Renderer neon, Renderer tag)
        {
            Bounds slab = letters.bounds;
            Vector3 size = slab.size;
            int depthAxis = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int widthAxis = -1, heightAxis = -1;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == depthAxis)
                {
                    continue;
                }
                if (widthAxis < 0 || size[axis] > size[widthAxis])
                {
                    heightAxis = widthAxis;
                    widthAxis = axis;
                }
                else
                {
                    heightAxis = axis;
                }
            }
            Vector3 toNeon = neon.bounds.center - slab.center, toTag = tag.bounds.center - slab.center;
            Vector3 front = Axis(depthAxis) * Mathf.Sign(toNeon[depthAxis]);
            Vector3 up = Axis(heightAxis) * Mathf.Sign(toTag[heightAxis]);
            Vector3 rightward = -Axis(widthAxis) * Mathf.Sign(toTag[widthAxis]);
            // Front to -Z and up to +Y; if its right then lands on -X the model came in mirrored, and is mirrored back across the logo's
            // own x (a holder between it and the logo, scaled -1 across).
            Quaternion turn = Quaternion.Inverse(Quaternion.LookRotation(-front, up));
            model.localRotation = turn * model.localRotation;
            if ((turn * rightward).x < 0.0f)
            {
                var mirror = new GameObject("Mirrored").transform;
                mirror.SetParent(model.parent, false);
                mirror.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
                model.SetParent(mirror, false);
            }
        }

        private static Vector3 Axis(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static Bounds LocalBounds(Transform root, Renderer[] parts)
        {
            var bounds = new Bounds(root.InverseTransformPoint(parts[0].bounds.center), Vector3.zero);
            foreach (Renderer part in parts)
            {
                Bounds world = part.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(corner % 2 == 0 ? world.min.x : world.max.x, (corner / 2) % 2 == 0 ? world.min.y : world.max.y,
                        corner / 4 == 0 ? world.min.z : world.max.z);
                    bounds.Encapsulate(root.InverseTransformPoint(point));
                }
            }
            return bounds;
        }

        /// <summary>The logo comes in as geometry alone: no materials of its own, no animation, its normals as made (its creases).</summary>
        private static void ImportLogoModel()
        {
            if (!(AssetImporter.GetAtPath(LogoModelPath) is ModelImporter importer))
            {
                return;
            }
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation
                || importer.animationType != ModelImporterAnimationType.None || importer.importNormals != ModelImporterNormals.Import
                || importer.importCameras || importer.importLights || importer.isReadable;
            if (!changed)
            {
                return;
            }
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
