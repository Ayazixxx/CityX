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
    /// A FLAT SKIN COLOUR, NOT A DRAWN PORTRAIT. Godot's own npc/old_guy.gd
    /// wears a design baked in the game's own character creator
    /// (npc/designs/old_guy.tres) -- there is no equivalent bake pipeline
    /// on the Unity side yet (confirmed: nothing resembling a saved
    /// design/paint asset exists anywhere in this project outside the
    /// live in-game creator itself). Building that whole pipeline as a
    /// side effect of one NPC's own look would be a much larger, separate
    /// piece of work; CharacterSkin.Ensure is this project's own
    /// established fallback for exactly this gap, already used by
    /// Civilian for the identical reason.
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
                Material skin = CharacterSkin.Ensure("OldGuySkin", SkinColour);
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
                Object.DestroyImmediate(root);
            }
        }
    }
}
