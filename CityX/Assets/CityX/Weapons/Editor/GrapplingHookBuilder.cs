using CityX.Character;
using CityX.CharacterEditor;
using CityX.Items;
using CityX.Wearables;
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

        /// <summary>
        /// Renders the player mid-swing after real simulated Tick()s, not a
        /// static pose -- a live bug report ("the body stays still and
        /// starts stretching, the camera keeps moving") described something
        /// no single-frame capture could ever show, and CaptureGrip's own
        /// static arms-down pose never runs GrappleSwing.Tick at all. This
        /// drives the exact same rope/pose code a real swing does for
        /// several simulated seconds -- CharacterController.Move works
        /// outside Play Mode the same way GrappleSwingTests' own EditMode
        /// suite already relies on -- then renders once from a
        /// third-person-ish angle so the WHOLE body is visible, not just the
        /// grip.
        /// </summary>
        [MenuItem("CityX/Capture Grapple Swing")]
        public static void CaptureSwing()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("GrappleSwingRig");
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
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                player.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();
                swing.Attach(player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));

                // Held forward with a slowly sweeping yaw, the same "pump
                // the swing while looking around" a real player actually
                // does -- 240 ticks at a real 1/60 step is a genuine 4
                // seconds of swinging, long enough for the sway smoothing
                // and the arm's own RotateTowards easing to fully settle
                // rather than being caught mid-transition.
                for (int i = 0; i < 240; i++)
                {
                    float yaw = i * 0.5f;
                    swing.Tick(new Vector2(0.0f, 1.0f), yaw, false, false, false, false, 1.0f / 60.0f);
                }

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 50.0f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500.0f;
                cameraObject.transform.position =
                    player.transform.position + Vector3.up * 1.4f - player.transform.forward * 4.0f;
                cameraObject.transform.LookAt(player.transform.position + Vector3.up * 1.0f);

                target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "grapple-swing.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path} -- swing position {player.transform.position:F2}, velocity {swing.Velocity:F2}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Same as CaptureSwing, but with a Hooded Cloak equipped first --
        /// a live bug report of the swing pose looking "contorted" persisted
        /// after CaptureSwing's own bare-body render already confirmed the
        /// arm reach pose itself was fixed. HoodedCloak.ApplySleeveArmSpread
        /// runs every LateUpdate regardless of what else is posing the arm,
        /// and its sleeve mesh is a SEPARATE SkinnedMeshRenderer bound to the
        /// same arm bones but authored only against ordinary locomotion's
        /// narrow swing range -- worth checking directly whether the reach
        /// pose's own ~150 degree rotation, which the bare body tolerates
        /// fine, crumples a sleeve tube never built to flex that far.
        /// </summary>
        [MenuItem("CityX/Capture Grapple Swing With Cloak")]
        public static void CaptureSwingWithCloak()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var cloakPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/HoodedCloak.prefab");
            if (playerPrefab == null || cloakPrefab == null)
            {
                Debug.LogError("CityX: need both Player.prefab and HoodedCloak.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("GrappleSwingCloakRig");
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
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                player.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();

                // GrappleSwing added BEFORE the cloak is equipped --
                // HoodedCloak.OnEquipped caches player.GetComponent<GrappleSwing>()
                // once, at equip time, the same way it caches the arm bones;
                // equipping the cloak first left that cache permanently null
                // and silently defeated UpdateSwingHiding, exactly the kind
                // of ordering mistake a real Player.prefab (built with
                // GrappleSwing already on it) would never hit.
                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();

                var cloakInstance = (GameObject)PrefabUtility.InstantiatePrefab(cloakPrefab);
                cloakInstance.transform.SetParent(root.transform);
                var cloak = cloakInstance.GetComponent<HoodedCloak>();
                cloak.OnEquip(player, ItemInstanceData.None);

                swing.Attach(player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));

                for (int i = 0; i < 240; i++)
                {
                    float yaw = i * 0.5f;
                    swing.Tick(new Vector2(0.0f, 1.0f), yaw, false, false, false, false, 1.0f / 60.0f);
                    // HoodedCloak's own LateUpdate never runs from this batch
                    // loop -- driven by hand, in the same order LateUpdate
                    // itself uses, so the capture actually reflects what a
                    // real frame would show. See UpdateSwingHiding's own doc
                    // comment for why both are public in the first place.
                    cloak.UpdateSwingHiding();
                    if (!swing.IsActive)
                    {
                        cloak.ApplySleeveArmSpread();
                    }
                }

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.updateWhenOffscreen = true;
                }
                foreach (SkinnedMeshRenderer skin in cloakInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.updateWhenOffscreen = true;
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 50.0f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500.0f;
                cameraObject.transform.position =
                    player.transform.position + Vector3.up * 1.4f - player.transform.forward * 4.0f;
                cameraObject.transform.LookAt(player.transform.position + Vector3.up * 1.0f);

                target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "grapple-swing-cloak.png");
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
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- unlike CaptureSwing/
        /// CaptureSwingWithCloak, this exists to answer one live question:
        /// the user's own explicit report is "it gets more distorted the
        /// more I move around when the grapple rope is attached to wall",
        /// which neither of the other two capture rigs can ever reproduce --
        /// both swing in open air with nothing to collide with. This anchors
        /// the rope ON a real wall right beside the player and drives a real
        /// varied swing (sweeping yaw AND alternating push toward/away from
        /// the wall) through the REAL CharacterController.Move(), logging
        /// the numbers every 10 ticks so a runaway shows up in the log even
        /// if the final rendered frame does not happen to catch it.
        /// </summary>
        [MenuItem("CityX/Diagnose Grapple Swing Against A Wall")]
        public static void DiagnoseSwingAgainstWall()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("GrappleWallDiagnosticRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                // A real wall, 2m to the player's own +X, tall and wide
                // enough that the swing cannot possibly swing around its
                // edges -- the anchor sits directly on its face.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(2.0f, 24.0f, 0.0f);
                wall.transform.localScale = new Vector3(0.5f, 30.0f, 30.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                player.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();

                // Equip a real sword too -- visible in the user's own
                // screenshots during a swing, and SwordWeapon.OnEquip
                // parents it directly onto RightHand, the same bone
                // GrappleSwing's own reach pose rotates -- worth ruling in
                // or out directly rather than only reasoning about it from
                // the source.
                GameObject swordPrefab = CityX.WeaponsEditor.SwordBuilder.Build();
                if (swordPrefab != null)
                {
                    var swordInstance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
                    swordInstance.transform.SetParent(root.transform);
                    SwordWeapon sword = swordInstance.GetComponent<SwordWeapon>();
                    sword.OnEquip(player, ItemInstanceData.None);
                }

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

                // ANCHOR DIRECTLY ON THE WALL'S FACE -- this is what "the
                // grapple rope is attached to wall" means: the wall's -X
                // face sits at x=1.75 (centre 2.0 minus half-scale 0.25).
                Vector3 anchor = new Vector3(1.75f, 22.0f, 0.0f);
                swing.Attach(anchor);

                Debug.Log($"CityX DIAGNOSTIC: anchor={anchor:F2} ropeLength={swing.RopeLength:F2} " +
                    $"start pos={player.transform.position:F2}");

                float maxRigOffset = 0.0f;
                float maxHeadDistanceFromRoot = 0.0f;
                float maxHandDistanceFromRoot = 0.0f;
                float maxVelocityMagnitude = 0.0f;

                // A REAL VARIED SWING AGAINST THE WALL: sweeping yaw so the
                // player keeps trying to swing past the wall's own face, and
                // alternating push toward/away from it every half-second --
                // exactly "moving around" while attached, the one input
                // shape neither of the other two capture rigs ever drives.
                for (int i = 0; i < 600; i++)
                {
                    float yaw = i * 0.7f;
                    float pushZ = Mathf.Sin(i * 0.05f);
                    float pushX = ((i / 30) % 2 == 0) ? 1.0f : -1.0f;
                    swing.Tick(new Vector2(pushX, pushZ), yaw, false, false, false, false, 1.0f / 60.0f);

                    if (!swing.IsActive)
                    {
                        Debug.Log($"CityX DIAGNOSTIC: swing released itself at tick {i}");
                        break;
                    }

                    float rigOffset = animator.transform.localPosition.magnitude;
                    float headDistance = headBone != null
                        ? Vector3.Distance(headBone.position, player.transform.position) : 0.0f;
                    float handDistance = rightHand != null
                        ? Vector3.Distance(rightHand.position, player.transform.position) : 0.0f;
                    float velocityMagnitude = swing.Velocity.magnitude;

                    maxRigOffset = Mathf.Max(maxRigOffset, rigOffset);
                    maxHeadDistanceFromRoot = Mathf.Max(maxHeadDistanceFromRoot, headDistance);
                    maxHandDistanceFromRoot = Mathf.Max(maxHandDistanceFromRoot, handDistance);
                    maxVelocityMagnitude = Mathf.Max(maxVelocityMagnitude, velocityMagnitude);

                    if (i % 10 == 0)
                    {
                        Debug.Log($"CityX DIAGNOSTIC tick={i} pos={player.transform.position:F2} " +
                            $"velocity={swing.Velocity:F2} (mag {velocityMagnitude:F2}) " +
                            $"rigOffset={rigOffset:F3} headDist={headDistance:F2} handDist={handDistance:F2} " +
                            $"distToAnchor={Vector3.Distance(player.transform.position, anchor):F2} " +
                            $"ropeLength={swing.RopeLength:F2}");
                    }
                }

                Debug.Log($"CityX DIAGNOSTIC SUMMARY: maxRigOffset={maxRigOffset:F3} " +
                    $"maxHeadDistanceFromRoot={maxHeadDistanceFromRoot:F2} " +
                    $"maxHandDistanceFromRoot={maxHandDistanceFromRoot:F2} " +
                    $"maxVelocityMagnitude={maxVelocityMagnitude:F2} " +
                    $"finalPos={player.transform.position:F2}");

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 60.0f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500.0f;
                cameraObject.transform.position =
                    player.transform.position + Vector3.up * 1.4f - Vector3.forward * 5.0f - Vector3.right * 2.0f;
                cameraObject.transform.LookAt(player.transform.position + Vector3.up * 1.0f);

                target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "grapple-swing-wall-diagnostic.png");
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
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- tests a hypothesis GrappleSwing's
        /// own AttachGraceSeconds doc comment names directly: "Repeated firing
        /// attempts... toggle the Animator off and back on every couple of
        /// frames, each time capturing a fresh, never-fully-settled base pose".
        /// AttachGraceSeconds only fixes the INSTANT no-op grounded case;
        /// nothing has ever tested a player rapid-firing the grapple against a
        /// wall/ground where each attempt runs a short but real few frames
        /// before auto-releasing, then immediately fires again -- exactly what
        /// a frustrated real player does when a swing does not go anywhere,
        /// and exactly what "gets more distorted the more I move around"
        /// would look like if each cycle's captured base pose drifts a little
        /// from the last. Logs the right-arm base rotation captured at EVERY
        /// Attach across many rapid cycles.
        /// </summary>
        [MenuItem("CityX/Diagnose Repeated Grapple Fire")]
        public static void DiagnoseRepeatedFire()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            var root = new GameObject("GrappleRepeatedFireDiagnosticRig");
            try
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                // On solid ground, not airborne -- the exact "normal" case
                // the AttachGraceSeconds fix targets, and the one a real
                // player spam-firing near a wall/rooftop edge is actually in.
                player.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
                Physics.SyncTransforms();

                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.SetParent(root.transform);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Quaternion firstCapturedBase = Quaternion.identity;
                float maxDriftFromFirst = 0.0f;

                // 40 rapid attach/release cycles: fire at a point 4m up and
                // ahead, let the swing run for HALF A SECOND (past
                // AttachGraceSeconds -- this is not the instant no-op case),
                // then release exactly like a plain-release keypress would,
                // and fire again immediately -- a real player mashing the
                // grapple key when it does not seem to be working.
                for (int cycle = 0; cycle < 40; cycle++)
                {
                    Vector3 anchor = player.transform.position + new Vector3(3.0f, 4.0f, 0.0f);
                    swing.Attach(anchor);

                    for (int i = 0; i < 30; i++)
                    {
                        swing.Tick(new Vector2(0.0f, 1.0f), 0.0f, false, false, false, false, 1.0f / 60.0f);
                        if (!swing.IsActive) break;
                    }
                    if (swing.IsActive)
                    {
                        swing.Release(false);
                    }

                    if (rightUpperArm != null)
                    {
                        if (cycle == 0)
                        {
                            firstCapturedBase = rightUpperArm.localRotation;
                        }
                        float drift = Quaternion.Angle(firstCapturedBase, rightUpperArm.localRotation);
                        maxDriftFromFirst = Mathf.Max(maxDriftFromFirst, drift);
                        Debug.Log($"CityX DIAGNOSTIC cycle={cycle} rightUpperArm.localRotation={rightUpperArm.localRotation.eulerAngles:F1} " +
                            $"driftFromFirstCapture={drift:F2}deg playerPos={player.transform.position:F2}");
                    }

                    // A real player's next press does not wait for the
                    // Animator to settle back into a clean idle frame --
                    // it can land the very next frame after release.
                }

                Debug.Log($"CityX DIAGNOSTIC SUMMARY: maxDriftFromFirstCapture={maxDriftFromFirst:F2}deg " +
                    $"over 40 rapid attach/release cycles, finalPos={player.transform.position:F2}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- renders exactly what a
        /// player sees the instant the console opens mid-stride, from the
        /// real ThirdPersonCamera's own computed shot rather than an
        /// external angle, to directly judge whether "frozen at whatever
        /// frame the walk cycle happened to be on" is itself a visible
        /// problem worth chasing further, separately from any of the
        /// compounding bugs already fixed.
        /// </summary>
        [MenuItem("CityX/Diagnose Console Freeze Mid Walk")]
        public static void DiagnoseConsoleFreezeMidWalk()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("ConsoleFreezeDiagnosticRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.SetParent(root.transform);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                Physics.SyncTransforms();

                PlayerLocomotion locomotion = player.GetComponent<PlayerLocomotion>();
                locomotion.Bind();
                Animator animator = player.GetComponentInChildren<Animator>();

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                ThirdPersonCamera orbitCamera = cameraObject.AddComponent<ThirdPersonCamera>();
                orbitCamera.Target = player.transform;

                // Real sustained forward walking through the real component,
                // exactly like real WASD input held for a while -- long
                // enough that the walk cycle is well underway rather than
                // still on its own opening transition.
                var lateUpdate = typeof(PlayerLocomotion).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f }, 1.0f / 60.0f);
                    // THE PIECE A PURE EDITOR BATCH SCRIPT NEVER GETS FOR
                    // FREE: real Play Mode calls Animator.Update() itself,
                    // automatically, every frame -- nothing here does that
                    // outside Play Mode, so without this explicit call the
                    // Animator never evaluates a single frame at all and
                    // the skeleton just sits at its raw import bind pose
                    // (confirmed directly: the first version of this
                    // diagnostic, missing this line, rendered a T-pose after
                    // 90 full ticks of "walking").
                    animator.Update(1.0f / 60.0f);
                    lateUpdate.Invoke(locomotion, null);
                }

                Debug.Log($"CityX DIAGNOSTIC: mid-walk pose before freeze, player pos={player.transform.position:F2}");

                // THE FREEZE ITSELF -- exactly DevConsoleView.Open's own job.
                animator.enabled = false;

                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var cameraStart = typeof(ThirdPersonCamera).GetMethod("Start", flags);
                var cameraLateUpdate = typeof(ThirdPersonCamera).GetMethod("LateUpdate", flags);
                cameraStart.Invoke(orbitCamera, null);
                for (int i = 0; i < 10; i++)
                {
                    cameraLateUpdate.Invoke(orbitCamera, null);
                }

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "console-freeze-mid-walk.png");
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
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- renders the single frame
        /// right BEFORE and the single frame right AFTER a grapple release,
        /// to directly measure whether handing control back from the
        /// swing's own reach pose to the Animator's live pose is a visible,
        /// instant snap rather than a smooth handoff -- something none of
        /// the earlier diagnostics checked, since they all measured
        /// SUSTAINED states (mid-swing, mid-freeze), never the transition
        /// moment itself.
        /// </summary>
        [MenuItem("CityX/Diagnose Grapple Release Snap")]
        public static void DiagnoseReleaseSnap()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("ReleaseSnapDiagnosticRig");
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
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                Physics.SyncTransforms();

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();
                Animator animator = player.GetComponentInChildren<Animator>();

                swing.Attach(player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));
                for (int i = 0; i < 90; i++)
                {
                    swing.Tick(new Vector2(0.0f, 1.0f), i * 0.5f, false, false, false, false, 1.0f / 60.0f);
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 50.0f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500.0f;
                cameraObject.transform.position =
                    player.transform.position + Vector3.up * 1.4f - Vector3.forward * 4.0f;
                cameraObject.transform.LookAt(player.transform.position + Vector3.up * 1.0f);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;

                void RenderTo(string filename)
                {
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                    image.Apply();
                    string path = System.IO.Path.Combine(outputDirectory, filename);
                    System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                    Debug.Log($"CityX: wrote {path}");
                }

                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Quaternion beforeRotation = rightUpperArm.localRotation;
                Debug.Log($"CityX DIAGNOSTIC BEFORE: rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} " +
                    $"leftUpperArm={leftUpperArm.localRotation.eulerAngles:F1} animatorEnabled={animator.enabled}");
                RenderTo("release-snap-before.png");

                swing.Release(false);
                player.GetComponent<CharacterController>().Move(swing.LastReleaseVelocity * (1.0f / 60.0f));

                Debug.Log($"CityX DIAGNOSTIC RIGHT AFTER RELEASE (frame 0 of blend): " +
                    $"rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} " +
                    $"jumpFromBefore={Quaternion.Angle(beforeRotation, rightUpperArm.localRotation):F1}deg " +
                    $"animatorEnabled={animator.enabled}");
                RenderTo("release-snap-after.png");

                // Step the blend forward by hand -- LateUpdate never runs
                // outside Play Mode, same reasoning every other bone-posing
                // class in this project already documents its own public
                // Tick/Update wrapper for.
                var lateUpdate = typeof(GrappleSwing).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Quaternion previous = rightUpperArm.localRotation;
                for (int i = 0; i < 12; i++)
                {
                    lateUpdate.Invoke(swing, null);
                    float step = Quaternion.Angle(previous, rightUpperArm.localRotation);
                    Debug.Log($"CityX DIAGNOSTIC blend frame={i} rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} stepDelta={step:F2}deg");
                    previous = rightUpperArm.localRotation;
                }
                RenderTo("release-snap-blended.png");
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
