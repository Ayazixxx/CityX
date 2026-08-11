using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Configures the Mixamo clips for Humanoid retargeting and reports what is
    /// actually inside them.
    ///
    /// WHY THIS IS A SCRIPT AND NOT INSPECTOR CLICKS. There will eventually be
    /// forty-odd clips, every one needing the same three settings, and a single
    /// clip imported as Generic instead of Humanoid fails much later as an
    /// animation that plays on nothing. Doing it in code means it is done the
    /// same way every time and can be re-run after any reimport.
    ///
    /// The measurement half exists because the Godot build's own numbers
    /// disagree about how big this character is -- fused_body.gd puts shoulders
    /// at 1.9m, which implies something well over 2m tall, while player.gd
    /// pivots its camera at 1.6m and punches from 1.5m, which is ordinary human
    /// scale. That gap matters right now: the whole point of getting a body
    /// into the city is to judge whether 3.2m floors and 14m streets feel
    /// right, and that judgement is worthless if the body is the wrong size.
    /// So measure the skeleton rather than trusting either number.
    /// </summary>
    public static class CharacterImportSetup
    {
        private const string AnimationDirectory = "Assets/CityX/Character/Animations";

        [MenuItem("CityX/Import Character Clips")]
        public static void Import()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimationDirectory });
            if (guids.Length == 0)
            {
                Debug.LogError($"CityX: no models found under {AnimationDirectory}");
                return;
            }

            // The first clip alphabetically becomes the avatar source and every
            // other clip copies from it. Without a shared avatar each clip
            // builds its own, and two avatars from the same skeleton are not
            // guaranteed to map bones identically -- which shows up as limbs
            // popping between animation states.
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            paths.Sort();

            string avatarSourcePath = paths[0];
            ConfigureModel(avatarSourcePath, null);

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarSourcePath);
            if (avatar == null)
            {
                Debug.LogError($"CityX: {avatarSourcePath} produced no avatar -- the FBX may not " +
                               "contain a skeleton Unity recognises as humanoid.");
                return;
            }

            for (int i = 1; i < paths.Count; i++)
            {
                ConfigureModel(paths[i], avatar);
            }

            Debug.Log($"CityX: configured {paths.Count} clips against the avatar from {avatarSourcePath}");
            Measure(avatarSourcePath);
        }

        private static void ConfigureModel(string path, Avatar sharedAvatar)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"CityX: no importer for {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            if (sharedAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sharedAvatar;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            // Mixamo ships its own placeholder materials; the game supplies its
            // own, and importing theirs just litters the project.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Reports the character's real dimensions, and whether the FBX carries
        /// a mesh at all or only a skeleton -- which decides whether there is
        /// something to look at or whether a stand-in body has to be built.
        /// </summary>
        private static void Measure(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogError($"CityX: could not load {path}");
                return;
            }

            GameObject instance = Object.Instantiate(model);
            try
            {
                var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var plain = instance.GetComponentsInChildren<MeshRenderer>();
                int bones = instance.GetComponentsInChildren<Transform>().Length;

                Debug.Log($"CityX: {System.IO.Path.GetFileName(path)} contains " +
                          $"{skinned.Length} skinned mesh(es), {plain.Length} static mesh(es), " +
                          $"{bones} transforms");

                var animator = instance.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    Transform shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

                    Debug.Log("CityX: MEASURED SKELETON -- " +
                              $"foot {Height(foot):0.00}m, hips {Height(hips):0.00}m, " +
                              $"shoulder {Height(shoulder):0.00}m, head {Height(head):0.00}m");

                    if (head != null && foot != null)
                    {
                        // Head bone sits at the base of the skull, so the crown
                        // is roughly a further eighth of body height up.
                        float headBone = Height(head);
                        Debug.Log($"CityX: approximate standing height {headBone * 1.13f:0.00}m " +
                                  "(head bone + skull)");
                    }
                }
                else
                {
                    Debug.LogWarning("CityX: model has no humanoid animator -- retargeting will not work.");
                }

                if (skinned.Length > 0)
                {
                    Bounds bounds = skinned[0].bounds;
                    for (int i = 1; i < skinned.Length; i++)
                    {
                        bounds.Encapsulate(skinned[i].bounds);
                    }
                    Debug.Log($"CityX: mesh bounds {bounds.size.x:0.00} x {bounds.size.y:0.00} x " +
                              $"{bounds.size.z:0.00} m");
                }
                else
                {
                    Debug.Log("CityX: SKELETON ONLY, no mesh in this FBX -- a stand-in body will have " +
                              "to be built and parented to the bones.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static float Height(Transform bone)
        {
            return bone == null ? 0.0f : bone.position.y;
        }
    }
}
