using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the pedestrian prefab.
    ///
    /// Shares the player's body, animator controller and ragdoll wholesale. The
    /// only differences are the skin and the brain: a civilian has no input
    /// reader, no stamina and no fists, because a body that walks and can be
    /// knocked down is the entire job.
    ///
    /// Run after the player rig, whose controller it reuses.
    /// </summary>
    public static class CivilianBuilder
    {
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string PrefabPath = "Assets/CityX/Character/Civilian.prefab";

        [MenuItem("CityX/Rebuild Civilian")]
        public static GameObject Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Player Rig first -- the civilian shares " +
                               "its animator controller.");
                return null;
            }

            var root = new GameObject("Civilian");
            try
            {
                // The same skeleton, avatar and fused mesh the player gets --
                // built the same way rather than copied off a prefab, so a
                // change to the body reaches the crowd without anyone
                // remembering to re-run something in the right order. The mesh
                // asset itself is shared, so a crowd of twelve is one mesh
                // twelve times.
                Material skin = CharacterSkin.Ensure("CivilianSkin", CharacterSkin.CivilianColour);
                GameObject body = FusedBodyBuilder.BuildBody(skin, controller);
                body.transform.SetParent(root.transform, false);

                Animator animator = body.GetComponent<Animator>();

                CharacterController capsule = root.AddComponent<CharacterController>();
                capsule.height = PlayerLocomotion.StandingHeightDefault;
                capsule.radius = FusedBodyShape.TorsoRadius;
                capsule.center = new Vector3(0.0f, PlayerLocomotion.StandingHeightDefault * 0.5f, 0.0f);
                capsule.slopeLimit = 50.0f;
                capsule.stepOffset = 0.4f;
                capsule.skinWidth = 0.03f;
                // Default 0.001 silently discards any move smaller than a
                // millimetre. A pedestrian walks 25mm per frame at 60fps, which
                // is fine -- but the player's own bug was exactly this, and
                // there is no reason to leave the trap armed here.
                capsule.minMoveDistance = 0.0f;

                RagdollBuilder.Build(root, animator);

                root.AddComponent<Civilian>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: civilian written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
