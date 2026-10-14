using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the CITY X skeleton and a humanoid avatar for it, so the Mixamo
    /// clips retarget onto the game's own proportions rather than the game's
    /// body being fitted to Mixamo's.
    ///
    /// THIS IS THE WAY ROUND THE GODOT BUILD DID IT. It built its own skeleton
    /// from the body's joints and retargeted the clips onto that. The first
    /// Unity attempt inverted it -- kept the imported skeleton and shaped the
    /// body around it -- which cost both the proportions and the skinning, for
    /// reasons recorded on BodyRig.
    ///
    /// Unity's humanoid system exists precisely to do this: an avatar describes
    /// what a rig's bones MEAN, and a clip authored on one rig plays correctly
    /// on any other with the same meaning and different proportions.
    /// </summary>
    public static class BodyRigBuilder
    {
        private const string AvatarPath = "Assets/CityX/Character/CityXAvatar.asset";

        /// <summary>
        /// Builds the skeleton under a new root and returns it, with every bone
        /// by name in <paramref name="bones"/>.
        ///
        /// <paramref name="boneList"/> defaults to <see cref="BodyRig.Bones"/>
        /// (the T-pose every LIVE, Animator-driven rig has to use) --
        /// SkinnedGarmentBuilder passes <see cref="BodyRig.RestBones"/>
        /// instead, since the garment meshes it bakes were authored against
        /// that pose, not this one. See RestBones's own doc comment for why
        /// binding a garment against the wrong one of the two is a real bug,
        /// not a cosmetic difference.
        /// </summary>
        public static GameObject BuildSkeleton(string name, out Dictionary<string, Transform> bones,
            IReadOnlyList<BodyRig.Bone> boneList = null)
        {
            var root = new GameObject(name);
            bones = new Dictionary<string, Transform>();

            foreach (BodyRig.Bone bone in boneList ?? BodyRig.Bones())
            {
                var joint = new GameObject(bone.Name);
                Transform parent = bone.Parent == null ? root.transform : bones[bone.Parent];
                joint.transform.SetParent(parent, false);
                // Positions are authored in the body's own space, so each one
                // is set in world terms and left to resolve against whatever
                // its parent already is.
                joint.transform.position = bone.Position;
                joint.transform.rotation = Quaternion.identity;
                bones[bone.Name] = joint.transform;
            }

            return root;
        }

        /// <summary>
        /// Describes the skeleton to Unity as a humanoid, and builds the avatar
        /// that lets Mixamo clips play on it.
        /// </summary>
        public static Avatar BuildAvatar(GameObject root, Dictionary<string, Transform> bones)
        {
            var humanBones = new List<HumanBone>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human == HumanBodyBones.LastBone)
                {
                    continue;   // a leaf that exists only to give its parent a direction
                }
                if (BodyRig.IsFinger(bone.Human))
                {
                    // The fingers are NOT humanoid-mapped: no clip here animates fingers (the Godot clips have none, the Mixamo ones would wave
                    // them about), and HandGrip poses them after the Animator. Left out of the avatar's human bones, they stay where they are put.
                    continue;
                }

                humanBones.Add(new HumanBone
                {
                    boneName = bone.Name,
                    // Unity matches on ITS OWN naming, which is not the enum's
                    // ToString for every bone, so it is asked rather than
                    // guessed.
                    humanName = HumanTrait.BoneName[(int)bone.Human],
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }

            // Every transform in the hierarchy, root included, with its LOCAL
            // transform. Unity treats this as the bind pose.
            var skeleton = new List<SkeletonBone>
            {
                new SkeletonBone
                {
                    name = root.name,
                    position = Vector3.zero,
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                },
            };
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                Transform joint = bones[bone.Name];
                skeleton.Add(new SkeletonBone
                {
                    name = bone.Name,
                    position = joint.localPosition,
                    rotation = joint.localRotation,
                    scale = joint.localScale,
                });
            }

            var description = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeleton.ToArray(),
                // ZERO: THIS BODY HAS NO TWIST BONES. These say how much of a limb's roll is handed to the upper and lower bone of it (Unity's default, 0.5, is for rigs
                // that have the extra twist bones to take it). Left at the default here they made a bent leg or arm spin about its own length by up to 130 degrees for
                // a few frames of every fast stride -- measured by playing one run clip on avatars built with each setting: the largest one-frame jump of a lower leg was
                // 179 degrees as it was and 40 (just fast running) with these at zero. It was the tear at the pelvis and the flung arms in every sprint capture, from any clip.
                upperArmTwist = 0.0f,
                lowerArmTwist = 0.0f,
                upperLegTwist = 0.0f,
                lowerLegTwist = 0.0f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0.0f,
                hasTranslationDoF = false,
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            avatar.name = "CityXAvatar";

            if (!avatar.isValid)
            {
                // Named loudly: an invalid avatar does not throw, it just makes
                // every clip play against nothing and the body stand still.
                Debug.LogError("CityX: the humanoid avatar came out INVALID -- check the T-pose " +
                               "and that every required bone is mapped.");
            }
            return Persist(avatar);
        }

        /// <summary>
        /// Saves the avatar as a real asset and returns the saved one.
        ///
        /// WITHOUT THIS THE PREFAB LOSES ITS AVATAR, silently. What
        /// AvatarBuilder hands back is an in-memory object with no home on
        /// disk, and a prefab cannot reference something that is not an asset
        /// -- so SaveAsPrefabAsset writes a null there and the character stands
        /// in a T-pose forever with every clip "playing". Nothing throws; it
        /// took a test loading the saved prefab to see it.
        ///
        /// Copied over the existing asset rather than replacing it, so prefabs
        /// already pointing at it keep pointing at it across a rebuild. The
        /// player and the crowd share one avatar because they share one
        /// skeleton.
        /// </summary>
        private static Avatar Persist(Avatar avatar)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(avatar, AvatarPath);
                AssetDatabase.SaveAssets();
                return avatar;
            }

            EditorUtility.CopySerialized(avatar, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(avatar);
            AssetDatabase.SaveAssets();
            return existing;
        }

        /// <summary>
        /// The body's capsules at the rig's own rest pose.
        ///
        /// Passed the AUTHORING height rather than the rig's real one, which
        /// looks odd and is deliberate: the radii in FusedBodyShape are already
        /// the numbers the body was drawn with, so the scale factor here must
        /// come out at exactly one. Handing it the real height would rescale
        /// the thicknesses that are supposed to be preserved.
        /// </summary>
        public static List<FusedBodyShape.Segment> Segments()
        {
            return FusedBodyShape.Segments(
                BodyRig.ShapePositionOf, FusedBodyShape.ReferenceHeight);
        }

        /// <summary>The body's SHAPE, one capsule per limb -- see FusedBodyShape.LimbCapsules' own comment for why this is not Segments().</summary>
        public static List<FusedBodyShape.Segment> LimbCapsules()
        {
            return FusedBodyShape.LimbCapsules(
                BodyRig.ShapePositionOf, FusedBodyShape.ReferenceHeight);
        }
    }
}
