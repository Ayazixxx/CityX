using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the pistol prefab: THE GLOCK (2026-10-14), made in Blender from scratch (CityX/SourceArt/Glock/glock_model.py) as separate
    /// parts -- frame, slide, barrel, magazine, trigger -- so the slide cycles on a shot and is racked in an empty reload, the magazine
    /// drops out and a fresh one goes in, and the trigger is pulled. It replaced the placeholder Beretta model (Models/Beretta_M9.obj, which
    /// the owner made in Meshy and called temporary). The item is still "pistol" and the prefab still Pistol.prefab, so every inventory,
    /// network list and test that names them keeps working. See FirearmBuilder for how a model becomes a prefab.
    /// </summary>
    public static class PistolBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Pistol.prefab";
        private const string ModelPath = "Assets/CityX/Weapons/Glock.glb";
        private const string MeshFolder = "Assets/CityX/Weapons/GlockMeshes";

        /// <summary>The Blender materials' looks: black polymer, dark nitrided steel, a brighter barrel, the magazine, white sight dots.</summary>
        public static readonly Dictionary<string, FirearmBuilder.Finish> Finishes = new Dictionary<string, FirearmBuilder.Finish>
        {
            ["GlockPolymer"] = new FirearmBuilder.Finish(new Color(0.07f, 0.07f, 0.075f), 0.0f, 0.3f),
            ["GlockSteel"] = new FirearmBuilder.Finish(new Color(0.11f, 0.115f, 0.125f), 0.8f, 0.55f),
            ["GlockBarrel"] = new FirearmBuilder.Finish(new Color(0.34f, 0.35f, 0.37f), 0.9f, 0.65f),
            ["GlockMag"] = new FirearmBuilder.Finish(new Color(0.09f, 0.09f, 0.1f), 0.5f, 0.45f),
            ["GlockSightDot"] = new FirearmBuilder.Finish(new Color(0.92f, 0.92f, 0.86f), 0.0f, 0.4f),
        };

        [MenuItem("CityX/Rebuild Pistol")]
        public static GameObject Build()
        {
            return FirearmBuilder.Build(ModelPath, PrefabPath, MeshFolder, "Pistol", "Glock", FirearmProfile.Glock(), Finishes);
        }

        /// <summary>
        /// Renders the pistol equipped on the real player prefab, close on
        /// the hand -- the only way to actually judge GripHandPosition/
        /// GripHandRotationEuler, which are a starting guess (see their own
        /// doc comment) rather than a live-tuned number.
        /// </summary>
        [MenuItem("CityX/Capture Pistol Grip")]
        public static void CaptureGrip()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null || pistolPrefab == null)
            {
                Debug.LogError("CityX: need both Player.prefab and Pistol.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("PistolGripRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                player.transform.rotation = Quaternion.Euler(0.0f, 200.0f, 0.0f);

                // Arms down, matching CharacterCapture's own Rest pose --
                // a pistol held at the hip, not mid-swing.
                StillPose.ArmsDown(player.transform);

                var pistolInstance = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab);
                var pistol = pistolInstance.GetComponent<PistolWeapon>();
                pistol.OnEquip(player, ItemInstanceData.None);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                // Framed FROM the pistol's own live world position, not a
                // guessed fixed coordinate -- a hardcoded camera position
                // landed on empty air the first time this ran, framing legs
                // instead of the gun. A generous distance straight in front
                // of the character, not hugging the grip, so a wrong grip
                // pose is still clearly visible rather than clipped into.
                Vector3 gripPosition = pistolInstance.transform.position;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position =
                    player.transform.position + Vector3.up * 1.0f - player.transform.forward * 1.6f;
                cameraObject.transform.LookAt(gripPosition);

                target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "pistol-grip.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Same idea as CaptureGrip, but posed against the REAL pistol_aim_in
        /// clip rather than a plain arms-down rest -- "the pistol is upside
        /// down as you are shooting it" is a claim about that specific pose,
        /// not the idle hip-carry one CaptureGrip already covers, and the
        /// wrist rotation an actual aiming clip drives can differ enough
        /// from a manual arms-down pose that a fixed GripHandRotationEuler
        /// looking right in one says nothing about the other.
        /// </summary>
        [MenuItem("CityX/Capture Pistol Aiming")]
        public static void CaptureAiming()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null || pistolPrefab == null)
            {
                Debug.LogError("CityX: need both Player.prefab and Pistol.prefab built first.");
                return;
            }

            AnimationClip aimClip = FindClip("pistol_aim_in");
            if (aimClip == null)
            {
                Debug.LogError("CityX: no pistol_aim_in clip found.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("PistolAimRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                player.transform.rotation = Quaternion.Euler(0.0f, 210.0f, 0.0f);

                SampleClip(player, aimClip, 0.9f);

                var pistolInstance = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab);
                var pistol = pistolInstance.GetComponent<PistolWeapon>();
                pistol.OnEquip(player, ItemInstanceData.None);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                // Framed FROM THE GUN'S OWN LIVE WORLD POSITION, not the
                // player root's -- a clip's own hip track can carry real
                // root-adjacent travel (see CharacterCapture.SampleClip's
                // own "recentre on hips" comment, deliberately not repeated
                // in this file's own copy), which left the body's actual
                // mesh well away from player.transform.position the first
                // time this ran and framed a close-up of the neck instead
                // of the gun. The gun's own transform is parented to the
                // live hand bone, so it is exactly where the posed body
                // actually put it, independent of whatever the root did.
                Vector3 gripPosition = pistolInstance.transform.position;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position = gripPosition + new Vector3(0.3f, 0.15f, -0.9f);
                cameraObject.transform.LookAt(gripPosition);

                target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "pistol-aiming.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Pulls the animation clip out of a model file, skipping Unity's own
        /// __preview__ duplicates -- same shape CharacterCapture's own
        /// FindClip already uses.
        /// </summary>
        private static AnimationClip FindClip(string modelName)
        {
            string path = $"Assets/CityX/Character/Animations/{modelName}.fbx";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// Poses <paramref name="instance"/> against <paramref name="clip"/>
        /// at a given fraction through it -- copied from CharacterCapture's
        /// own SampleClip rather than shared, since that one is private to
        /// its own file and this is the only other place that needs it.
        /// </summary>
        private static void SampleClip(GameObject instance, AnimationClip clip, float fraction)
        {
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            Transform[] bones = animator.GetComponentsInChildren<Transform>();
            var rotation = new Quaternion[bones.Length];
            var position = new Vector3[bones.Length];
            Vector3 rootPosition = animator.transform.localPosition;
            Quaternion rootRotation = animator.transform.localRotation;

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, fraction * clip.length);
                AnimationMode.EndSampling();

                for (int i = 0; i < bones.Length; i++)
                {
                    rotation[i] = bones[i].localRotation;
                    position[i] = bones[i].localPosition;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localRotation = rotation[i];
                bones[i].localPosition = position[i];
            }

            animator.transform.localPosition = rootPosition;
            animator.transform.localRotation = rootRotation;
        }
    }
}
