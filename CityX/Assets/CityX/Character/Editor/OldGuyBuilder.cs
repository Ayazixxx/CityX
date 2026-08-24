using System;
using System.IO;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the Old Guy prefab. Follows CivilianBuilder's own precedent:
    /// shares the player's body, animator controller and rig wholesale, a
    /// distinct skin is the only visual difference.
    ///
    /// A REAL DRAWN PORTRAIT, PULLED FROM THE GODOT DESIGN. Godot's own
    /// npc/old_guy.gd wears a design baked in the game's own character
    /// creator (npc/designs/old_guy.tres). It is a Godot Resource (.tres),
    /// not a ConfigFile, so its own atlas_png is base64 inside a quoted
    /// string rather than GodotCharacterImport.Bytes' comma-separated
    /// integer literal -- LoadDesignAtlasPng below is that format's own
    /// reader. Same "CharacterSkin.Ensure a flat colour" fallback as
    /// before if the design is missing or its atlas will not decode, so a
    /// checkout without the Godot repo alongside it still builds something
    /// rather than failing outright.
    ///
    /// NO RAGDOLL. Old Guy never takes damage (see UniqueNpc's own
    /// ApplyHit) and so never has a reason to go limp -- unlike Civilian,
    /// which can be felled and needs one.
    ///
    /// TAKES THE DIALOGUE BOX PREFAB AS A PARAMETER, rather than building
    /// it here. CityX.Character.Editor's own .asmdef does not reference
    /// CityX.UI.Editor (Character never depends on UI, the same rule the
    /// runtime layers already follow -- see UniqueNpc's own IDialogueBox
    /// comment) -- only the top-level orchestrator, CityMassingSceneBuilder,
    /// references both, so it is the one that has to build the dialogue
    /// box and hand the result down.
    /// </summary>
    public static class OldGuyBuilder
    {
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string PrefabPath = "Assets/CityX/Character/OldGuy.prefab";

        /// <summary>Fallback only -- used when the drawn design can't be found or decoded.</summary>
        private static readonly Color SkinColour = new Color(0.62f, 0.55f, 0.47f);

        public static GameObject Build(GameObject dialogueBoxPrefab)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Player Rig first -- Old Guy shares its animator controller.");
                return null;
            }

            var root = new GameObject("OldGuy");
            try
            {
                Material skin = BuildSkin();
                GameObject body = FusedBodyBuilder.BuildBody(skin, controller);
                body.transform.SetParent(root.transform, false);

                CharacterController capsule = root.AddComponent<CharacterController>();
                capsule.height = PlayerLocomotion.StandingHeightDefault;
                capsule.radius = FusedBodyShape.TorsoRadius;
                capsule.center = new Vector3(0.0f, PlayerLocomotion.StandingHeightDefault * 0.5f, 0.0f);
                capsule.slopeLimit = 50.0f;
                capsule.stepOffset = 0.4f;
                capsule.skinWidth = 0.03f;
                capsule.minMoveDistance = 0.0f;

                OldGuy oldGuy = root.AddComponent<OldGuy>();
                oldGuy.SpeakerNameOverride = "Old Guy";
                oldGuy.DialogueBoxPrefab = dialogueBoxPrefab;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: Old Guy written to {PrefabPath}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The painted material if the Godot design decodes, otherwise the
        /// flat fallback colour -- never a hard failure, since a checkout
        /// without the Godot repo sitting next to this one should still
        /// build a usable (if plainer) Old Guy.
        /// </summary>
        private static Material BuildSkin()
        {
            byte[] png = LoadDesignAtlasPng("old_guy");
            if (png == null)
            {
                Debug.LogWarning("CityX: no npc/designs/old_guy.tres found (or its atlas would " +
                                  "not decode) -- Old Guy is getting the flat fallback colour.");
                return CharacterSkin.Ensure("OldGuySkin", SkinColour);
            }

            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(image, png, markNonReadable: false))
                {
                    Debug.LogWarning("CityX: old_guy.tres's atlas_png would not decode as a PNG " +
                                      "-- falling back to the flat colour.");
                    return CharacterSkin.Ensure("OldGuySkin", SkinColour);
                }
                if (image.width != PaintAtlas.Width || image.height != PaintAtlas.Height)
                {
                    Debug.LogWarning($"CityX: old_guy.tres's atlas is {image.width}x{image.height}, " +
                                      $"not {PaintAtlas.Width}x{PaintAtlas.Height} -- falling back " +
                                      "to the flat colour rather than stretching it onto the wrong rows.");
                    return CharacterSkin.Ensure("OldGuySkin", SkinColour);
                }

                var paint = new BodyPaint(Color.white);
                if (!paint.Adopt(image.GetPixels32()))
                {
                    Debug.LogWarning("CityX: old_guy.tres's atlas would not adopt -- falling back " +
                                      "to the flat colour.");
                    return CharacterSkin.Ensure("OldGuySkin", SkinColour);
                }

                return CharacterSkin.EnsurePainted("OldGuySkin", paint.Atlas);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// Reads a design's own atlas_png straight out of npc/designs/&lt;slug&gt;
        /// .tres. A .tres is a Godot Resource, saved through ResourceSaver --
        /// a DIFFERENT serialization from characters.cfg's ConfigFile (see
        /// GodotCharacterImport.Bytes' own comma-separated integer literal):
        /// a Resource's own PackedByteArray comes out as base64 inside a
        /// quoted string instead. Null on anything short of a clean decode
        /// -- a missing repo, a missing design, a field that will not parse
        /// -- rather than throwing, since this runs as part of an ordinary
        /// rebuild that has to keep working without the Godot half of the
        /// project checked out alongside it.
        /// </summary>
        public static byte[] LoadDesignAtlasPng(string slug)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
            string path = Path.Combine(projectRoot, "npc", "designs", slug + ".tres");
            if (!File.Exists(path))
            {
                return null;
            }
            return ParseAtlasPngFromTres(File.ReadAllText(path));
        }

        /// <summary>
        /// Separated from the file read above so a test can drive it against
        /// an inline fixture, the same reasoning GodotCharacterImport.Import
        /// gives for its own split from ImportMenu.
        /// </summary>
        public static byte[] ParseAtlasPngFromTres(string text)
        {
            const string marker = "atlas_png = PackedByteArray(\"";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }
            start += marker.Length;
            int end = text.IndexOf('"', start);
            if (end < 0)
            {
                return null;
            }

            string base64 = text.Substring(start, end - start);
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
