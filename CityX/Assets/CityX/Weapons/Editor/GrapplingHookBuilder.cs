using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the grappling hook prefab from a Meshy AI "Grapple Blaster"
    /// export -- a real pistol-grip sci-fi launcher with an open claw/hook
    /// mechanism at the muzzle, replacing an earlier procedural
    /// body+barrel+muzzle build that read as a plain grey cylinder with no
    /// real grip shape. Same "load the real model, override its broken
    /// material, normalize its scale" pattern PistolBuilder's own
    /// Beretta_M9.obj already uses.
    ///
    /// THE EMBEDDED TEXTURE IS UNUSABLE, for the exact reason the
    /// meshy-ai-import-quirks memory already documents twice over
    /// (FarmerHat, MidnightPatchworkCoat): confirmed directly that
    /// glTFast imports this file's base_color/normal/metallicRoughness
    /// textures as silently stubbed 4x4 Texture2Ds, not the real (extracted
    /// from the .glb's own binary chunk by scanning for JPEG signatures,
    /// then viewed directly) full-resolution art the source actually
    /// carries -- a dark charcoal gunmetal with lighter worn-steel
    /// highlights, colour-matched here the same flat-material way.
    ///
    /// THE MODEL'S OWN NATIVE FORWARD AXIS IS +X, NOT +Z -- confirmed by a
    /// raw render before assuming otherwise (a lesson the same memory
    /// already paid for once): the pistol grip sits at one end, the open
    /// claw mechanism at the other, spread out along the source mesh's own
    /// local X axis. Beretta_M9.obj's own muzzle already sits at +Z
    /// natively (confirmed by PistolBuilder's own fixed +Z muzzle-flash
    /// offset, applied with no rotation), so BuildVisual applies a fixed
    /// corrective -90 degree yaw here to match that same +Z-forward
    /// convention -- the point of doing so is that GrapplingHookItem's own
    /// GripHandPosition/GripHandRotationEuler can then be the literal same
    /// numbers as PistolWeapon's, satisfying "hold it the same as the
    /// pistol" exactly rather than needing its own independently-tuned grip.
    /// </summary>
    public static class GrapplingHookBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/GrapplingHook.prefab";
        private const string ModelPath = "Assets/CityX/Weapons/Models/GrappleBlaster.glb";

        /// <summary>
        /// Claw-tip to grip-butt. A bit longer than the Beretta's real
        /// 21.7cm -- this is a chunky two-part sci-fi launcher with an open
        /// claw mechanism riding past the end of an ordinary pistol's own
        /// barrel, not a real-world sidearm to match exactly.
        /// </summary>
        private const float TargetLengthMeters = 0.34f;

        /// <summary>
        /// Eyeballed off the source .glb's own real base_color JPEG (see
        /// this file's own class comment) -- a dark, near-black charcoal
        /// gunmetal with lighter worn-steel highlights. This single flat
        /// colour stands in for the whole texture, the same fix
        /// FarmerHatBuilder/MidnightPatchworkCoatBuilder already use for
        /// the identical glTFast stub bug.
        /// </summary>
        private static readonly Color MetalColour = new Color(0.11f, 0.11f, 0.12f);

        [MenuItem("CityX/Rebuild Grappling Hook")]
        public static GameObject Build()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"CityX: {ModelPath} missing -- no grappling hook built.");
                return null;
            }

            var root = new GameObject("GrapplingHook");
            try
            {
                BuildVisual(root.transform, model, out Bounds rotatedBounds);

                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform, false);
                // The claw-tip end, not a guessed half-length -- see
                // BuildVisual's own doc comment for why an asymmetric mesh
                // (grip at one end, claw at the other) means this cannot
                // just be TargetLengthMeters * 0.5.
                muzzle.transform.localPosition = new Vector3(0.0f, 0.0f, rotatedBounds.max.z);

                var collision = root.AddComponent<CapsuleCollider>();
                collision.radius = Mathf.Max(rotatedBounds.extents.x, rotatedBounds.extents.y) * 0.6f;
                collision.height = rotatedBounds.size.z;
                collision.direction = 2; // Z axis, matching the muzzle's own aim axis.
                collision.center = new Vector3(0.0f, 0.0f, rotatedBounds.center.z);

                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = GrapplingHookItem.HookMass;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GrapplingHookItem.InteractRange;

                var item = root.AddComponent<GrapplingHookItem>();
                item.Muzzle = muzzle.transform;
                item.RopeMaterial = CharacterSkin.Ensure("GrappleRope", GrapplingHookItem.RopeColour);
                item.RopeMaterial.SetFloat("_Smoothness", 0.1f);
                item.HookHeadMaterial = CharacterSkin.Ensure("GrappleHookHead", GrapplingHookItem.HookColour);
                item.HookHeadMaterial.SetFloat("_Metallic", 0.8f);
                item.HookHeadMaterial.SetFloat("_Smoothness", 0.65f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: grappling hook written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Instantiates the real model, applies the fixed corrective yaw
        /// (native +X-forward -> +Z-forward, see this file's own class
        /// comment), normalizes scale and recentres exactly the way
        /// PistolBuilder's own NormalizeScale does, and overrides the
        /// broken imported material with a flat gunmetal one. Returns the
        /// visual's own rotated-and-scaled bounds, in <paramref name="parent"/>'s
        /// local space, so Build can place the muzzle/collider from real
        /// geometry instead of a guessed half-length -- the mesh is not
        /// symmetric front-to-back (grip at one end, claw at the other),
        /// so its own local origin after recentring is not its midpoint
        /// along the aim axis either.
        /// </summary>
        private static void BuildVisual(Transform parent, GameObject model, out Bounds rotatedBounds)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localRotation = Quaternion.Euler(0.0f, -90.0f, 0.0f);

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            float largestDimension = Mathf.Max(worldBounds.size.x, Mathf.Max(worldBounds.size.y, worldBounds.size.z));
            float normalizeScale = largestDimension > 0.0001f ? TargetLengthMeters / largestDimension : 1.0f;

            Vector3 centreLocal = visual.transform.InverseTransformPoint(worldBounds.center);
            visual.transform.localScale = Vector3.one * normalizeScale;
            visual.transform.localPosition = -centreLocal * normalizeScale;

            Material metal = CharacterSkin.Ensure("GrappleBlasterMetal", MetalColour);
            metal.SetFloat("_Metallic", 0.75f);
            metal.SetFloat("_Smoothness", 0.6f);
            foreach (MeshRenderer meshRenderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                meshRenderer.sharedMaterial = metal;
            }

            Bounds newWorldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                newWorldBounds.Encapsulate(renderers[i].bounds);
            }
            rotatedBounds = new Bounds(
                parent.InverseTransformPoint(newWorldBounds.center),
                newWorldBounds.size);
        }

        /// <summary>
        /// Renders the grappling hook equipped on the real player prefab,
        /// close on the hand -- the only way to actually judge whether
        /// reusing PistolWeapon's own grip numbers on a differently-shaped
        /// mesh actually looks held, not just numerically identical. Copied
        /// from PistolBuilder's own CaptureGrip almost verbatim; not shared,
        /// since that one is private to its own file.
        /// </summary>
        [MenuItem("CityX/Capture Grapple Grip")]
        public static void CaptureGrip()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var hookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null || hookPrefab == null)
            {
                Debug.LogError("CityX: need both Player.prefab and GrapplingHook.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("GrappleGripRig");
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

                PoseArmsDown(player.transform);

                var hookInstance = (GameObject)PrefabUtility.InstantiatePrefab(hookPrefab);
                var hook = hookInstance.GetComponent<GrapplingHookItem>();
                hook.OnEquip(player, CityX.Items.ItemInstanceData.None);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                Vector3 gripPosition = hookInstance.transform.position;

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
                string path = System.IO.Path.Combine(outputDirectory, "grapple-grip.png");
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

        private static void PoseArmsDown(Transform root)
        {
            SetLocalRotation(root, "arm_l", new Vector3(0.0f, 0.0f, 78.0f));
            SetLocalRotation(root, "forearm_l", new Vector3(0.0f, 0.0f, 12.0f));
            SetLocalRotation(root, "arm_r", new Vector3(0.0f, 0.0f, -78.0f));
            SetLocalRotation(root, "forearm_r", new Vector3(0.0f, 0.0f, -12.0f));
        }

        private static void SetLocalRotation(Transform root, string boneName, Vector3 euler)
        {
            Transform bone = FindDeep(root, boneName);
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(euler);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
