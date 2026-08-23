using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// The clip whose skeleton defines the avatar. Must be one whose rest
        /// pose is a NEUTRAL STAND -- every other clip is retargeted through
        /// it, so a crouched or braced source bends the whole library.
        /// </summary>
        private const string AvatarSourceModel = "idle.fbx";

        [MenuItem("CityX/Import Character Clips")]
        public static void Import()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimationDirectory });
            if (guids.Length == 0)
            {
                Debug.LogError($"CityX: no models found under {AnimationDirectory}");
                return;
            }

            // One clip is the avatar source and every other copies from it.
            // Without a shared avatar each clip builds its own, and two avatars
            // from the same skeleton are not guaranteed to map bones
            // identically -- which shows up as limbs popping between states.
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            paths.Sort();

            // NAMED, NOT "first alphabetically". That was the original rule and
            // it broke the moment punch clips arrived: boxing_left_jab sorts
            // before idle, so the avatar started being built from a clip whose
            // rest pose is a BOXING STANCE. The measured skeleton dropped from
            // 1.78m to 1.65m because the source was crouched. An avatar
            // configured from a bent pose retargets everything else onto that
            // bend, and nothing would have reported an error.
            string avatarSourcePath = $"{AnimationDirectory}/{AvatarSourceModel}";
            if (!paths.Contains(avatarSourcePath))
            {
                Debug.LogWarning($"CityX: {AvatarSourceModel} is missing, so the avatar will come " +
                                 $"from {paths[0]} instead. Check its rest pose is a neutral stand.");
                avatarSourcePath = paths[0];
            }
            ConfigureModel(avatarSourcePath, null);

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarSourcePath);
            if (avatar == null)
            {
                Debug.LogError($"CityX: {avatarSourcePath} produced no avatar -- the FBX may not " +
                               "contain a skeleton Unity recognises as humanoid.");
                return;
            }

            // SKIPPED BY PATH, NOT BY INDEX. This loop used to start at 1,
            // which was only ever correct while the avatar source was
            // guaranteed to be paths[0]. Pinning the source to idle.fbx broke
            // that silently: idle sorts fourth, so it fell into this loop and
            // was set to copy from its OWN avatar -- the cycle described in
            // ConfigureModel -- while boxing_left_jab at index 0 was skipped
            // and kept building a second, competing avatar of its own.
            foreach (string path in paths)
            {
                if (path == avatarSourcePath)
                {
                    continue;
                }
                ConfigureModel(path, avatar);
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
                // CLEARING THIS MATTERS. Importer settings persist in the .meta,
                // so a model that was previously copying from another still
                // holds that reference. Switching it to create-from-self while
                // the old sourceAvatar points at a model that now copies from
                // THIS one is a cycle, and Unity resolves it by producing no
                // avatar at all -- silently, with the character T-posing.
                importer.sourceAvatar = null;
            }

            // Mixamo ships its own placeholder materials; the game supplies its
            // own, and importing theirs just litters the project.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;

            ConfigureVerticalMotion(importer, path);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Clips whose whole point is that the body CHANGES HEIGHT.
        ///
        /// The rig runs with root motion off, so by default a clip's vertical
        /// travel is handed to the root and then thrown away -- which is
        /// exactly right for a walk cycle and exactly wrong for a crouch. A
        /// crouch imported that way bends the knees and folds the torso while
        /// the hips stay at standing height: a character stooping rather than
        /// crouching. Baking Y into the POSE instead keeps the lowering.
        ///
        /// AND MEASURED FROM THE FEET, which fixes a real problem Godot hit and
        /// solved by hand. The three crouch clips are separately authored and
        /// do not agree on how deep a crouch is -- standing_to_crouch ends with
        /// the hips about 0.4m lower than crouch_walk holds -- so a character
        /// who crouches and then walks pops upright. Anchoring each clip to its
        /// own feet normalises them against the floor rather than against
        /// whatever height the animator happened to author.
        /// </summary>
        private static void ConfigureVerticalMotion(ModelImporter importer, string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            // charged_jump WAS ADDED HERE, SPECULATIVELY, AND WAS WRONG. It
            // looked like the same shape as the other three -- a body that
            // has to actually lower rather than stoop -- but it is a lean
            // into a coiled, weight-shifted stance with one leg crossed at
            // an angle, not a clean symmetric vertical crouch, and
            // heightFromFeet's own "anchor to the lowest point" measurement
            // assumes exactly the shape a real crouch has. Anchored to the
            // wrong reference point on this clip, it sank the whole body
            // below the floor for as long as space was held -- confirmed
            // by the user, reported as "clips through the floor" the moment
            // this shipped. Left at the DEFAULT treatment instead (Y
            // motion discarded, matching jump.fbx and every other non-
            // crouch clip), which is the same choice this file already
            // made for every clip that is not one of the three real
            // crouches.
            bool lowers = name == "standing_to_crouch" || name == "crouch_to_idle" ||
                          name == "crouch_walk";

            // ALWAYS WRITTEN, NEVER JUST RETURNED PAST. A clip that used to
            // qualify (charged_jump, briefly, before that turned out to be
            // wrong) and reads `importer.defaultClipAnimations` again on a
            // later reimport does NOT get a clean slate for free -- that
            // property re-derives frame ranges from the take, but Unity
            // still carries the LAST EXPLICITLY-SAVED per-clip settings
            // forward underneath it, because those live in this importer's
            // own persisted state, not in the source file. Returning early
            // here left lockRootHeightY/heightFromFeet=true sitting in
            // charged_jump.fbx.meta from the earlier, wrong attempt even
            // after the C# said it should no longer apply -- confirmed
            // directly in the .meta file after this method's own early
            // return ran on it. Every clip gets an explicit value for both
            // fields every time now, so which one is true is never a
            // function of import history.
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].lockRootHeightY = lowers;    // bake Y into the pose
                clips[i].keepOriginalPositionY = false;
                clips[i].heightFromFeet = lowers;     // measured from the feet
            }
            importer.clipAnimations = clips;
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
