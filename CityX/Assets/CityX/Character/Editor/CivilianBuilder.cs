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
        private const string ModelPath = "Assets/CityX/Character/Animations/idle.fbx";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string PrefabPath = "Assets/CityX/Character/Civilian.prefab";

        [MenuItem("CityX/Rebuild Civilian")]
        public static GameObject Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);

            if (model == null || controller == null || avatar == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Player Rig first -- the civilian shares " +
                               "its model, avatar and animator controller.");
                return null;
            }

            var root = new GameObject("Civilian");
            try
            {
                GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);

                Material skin = CharacterSkin.Ensure("CivilianSkin", CharacterSkin.CivilianColour);
                CharacterSkin.Apply(body, skin);

                Animator animator = body.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = body.AddComponent<Animator>();
                }
                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.applyRootMotion = false;

                // Same body as the player, different skin. The mesh asset is
                // shared, so a crowd of twelve is one mesh twelve times.
                FusedBodyBuilder.ApplyTo(body, animator, skin);

                // Same 2.2m body as the player. A crowd at a different height
                // would make the city read as two different scales at once,
                // which is the exact question these are here to answer.
                float scale = PlayerLocomotion.StandingHeightDefault /
                              PlayerLocomotion.MixamoRigHeight;
                body.transform.localScale = new Vector3(scale, scale, scale);

                CharacterController capsule = root.AddComponent<CharacterController>();
                capsule.height = PlayerLocomotion.StandingHeightDefault;
                capsule.radius = 0.35f * scale;
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
