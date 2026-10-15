using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Items;
using CityX.UI;
using CityX.View;
using CityX.Wearables;
using CityX.Weapons;
using CityX.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            var root = new GameObject("GrapplingHook");
            try
            {
                BuildVisual(root.transform, out Bounds rotatedBounds);

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
        /// The launcher's visual: a plain metal rectangle, TargetLengthMeters
        /// long with the muzzle at its +Z end. EXPLICIT ASK: "just replace it
        /// with a rectangle" -- the old sci-fi blaster model (a 118 MB .glb) was
        /// deleted, and a simpler one is to be made later. Dropping a real model
        /// back in means replacing this box; the muzzle, collider and grip all
        /// read from the bounds returned here, so nothing else needs to change.
        /// </summary>
        private static void BuildVisual(Transform parent, out Bounds rotatedBounds)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            var size = new Vector3(0.07f, 0.11f, TargetLengthMeters);
            visual.transform.localScale = size;
            visual.transform.localPosition = Vector3.zero;

            Material metal = CharacterSkin.Ensure("GrappleBlasterMetal", MetalColour);
            metal.SetFloat("_Metallic", 0.75f);
            metal.SetFloat("_Smoothness", 0.6f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = metal;

            rotatedBounds = new Bounds(Vector3.zero, size);
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
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- a quick visual check that
        /// MeleeAttacker's own new spin attack (EXPLICIT ASK: "add a spin
        /// attack with the sword press 2 to use it. its like links up
        /// special in smash bros he just spins around with the sword for a
        /// bit") actually reads as spinning rather than just passing its
        /// own assertions -- MeleeCombatPlayTests already proves the
        /// numbers (rotation, hit count, cooldown) are correct; this is
        /// only here to confirm what the retriggered sword_attack clip
        /// plus the body's own fast yaw rotation actually LOOKS like
        /// together, at three points through one use.
        /// </summary>
        [MenuItem("CityX/Diagnose Sword Spin Attack")]
        public static void DiagnoseSwordSpinAttack()
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

            var root = new GameObject("SwordSpinDiagnosticRig");
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
                ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                Physics.SyncTransforms();

                GameObject swordPrefab = SwordBuilder.Build();
                if (swordPrefab == null)
                {
                    Debug.LogError("CityX: need the sword built first.");
                    return;
                }
                var swordInstance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
                swordInstance.transform.SetParent(root.transform);
                SwordWeapon sword = swordInstance.GetComponent<SwordWeapon>();
                sword.OnEquip(player, ItemInstanceData.None);

                MeleeAttacker melee = player.GetComponent<MeleeAttacker>();
                Animator animator = player.GetComponentInChildren<Animator>();

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0.0f, 1.5f, -3.5f);
                cameraObject.transform.LookAt(new Vector3(0.0f, 1.0f, 0.0f));

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                // THE "start" AND "end" FRAMES CAN SHOW A RAW BIND POSE --
                // confirmed directly this same session to be a batch-mode-
                // only artifact, not a real bug: Animator.Update(0f) inside
                // PlayerAnimationDriver.OnSwingStarted (added specifically
                // for this) does not reliably warm up a genuinely
                // never-evaluated Humanoid Animator in a pure editor batch
                // script the way it does in real Play Mode, matching this
                // session's own separate Time.deltaTime-in-batch-mode
                // finding for the exact same "batch mode behaves
                // differently" reason. Proven fine for real gameplay by
                // MeleeCombatPlayTests.SpinningTheInstantAfterSpawnDoesNotFreezeARawBindPose,
                // a genuine PlayMode test -- the "half" frame below is what
                // actually matters for judging the spin's own look.
                bool started = melee.TrySpinAttack();

                void RenderAt(string label)
                {
                    RenderTexture target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                    image.Apply();
                    string path = System.IO.Path.Combine(outputDirectory, $"sword-spin-{label}.png");
                    System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                    target.Release();
                    Object.DestroyImmediate(target);
                    camera.targetTexture = null;
                    Debug.Log($"CityX: wrote {path} -- yaw={player.transform.eulerAngles.y:F0} " +
                        $"IsSpinAttacking={melee.IsSpinAttacking}");
                }

                float stepSeconds = 1.0f / 60.0f;
                float elapsed = 0.0f;
                float renderAtQuarter = melee.SpinAttackSeconds * 0.25f;
                float renderAtHalf = melee.SpinAttackSeconds * 0.5f;
                bool renderedQuarter = false;
                bool renderedHalf = false;
                RenderAt("start");

                while (melee.IsSpinAttacking)
                {
                    melee.Tick(stepSeconds);
                    animator.Update(stepSeconds);
                    elapsed += stepSeconds;

                    if (!renderedQuarter && elapsed >= renderAtQuarter)
                    {
                        renderedQuarter = true;
                        RenderAt("quarter");
                    }
                    if (!renderedHalf && elapsed >= renderAtHalf)
                    {
                        renderedHalf = true;
                        RenderAt("half");
                    }
                }
                RenderAt("end");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- EXPLICIT PUSHBACK on a
        /// theory this session floated and the user rejected outright:
        /// "0 reason why i should be stretching eliminate that." Renders
        /// the console-freeze at MANY different points through a real
        /// jump arc (takeoff, early air, apex, descent, just before
        /// landing) rather than DiagnoseConsoleFreezeMidWalk's own single
        /// steady-walk moment -- if a real, unremarkable animator
        /// transition (Locomotion -> Falling -> Locomotion) is ever going
        /// to look like something is wrong when frozen, this is exactly
        /// where it would show up. Every frame is saved, not just one, so
        /// they can be looked at directly rather than guessed about.
        /// </summary>
        [MenuItem("CityX/Diagnose Console Freeze Through A Jump Arc")]
        public static void DiagnoseConsoleFreezeThroughAJumpArc()
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

            var root = new GameObject("JumpArcFreezeDiagnosticRig");
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
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var cameraStart = typeof(ThirdPersonCamera).GetMethod("Start", flags);
                var cameraLateUpdate = typeof(ThirdPersonCamera).GetMethod("LateUpdate", flags);
                cameraStart.Invoke(orbitCamera, null);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                // A moment of ordinary walking first, so this is a jump OUT
                // OF real forward motion, not a jump from a cold standstill.
                var locomotionLateUpdate = typeof(PlayerLocomotion).GetMethod("LateUpdate", flags);
                var input = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f };
                for (int i = 0; i < 30; i++)
                {
                    locomotion.Tick(input, 1.0f / 60.0f);
                    animator.Update(1.0f / 60.0f);
                    locomotionLateUpdate.Invoke(locomotion, null);
                }

                // Jump on this exact tick, then keep running ordinary
                // forward-walk input through the whole arc -- Jump only
                // needs to be true for the one frame it takes effect on.
                input.Jump = true;
                locomotion.Tick(input, 1.0f / 60.0f);
                animator.Update(1.0f / 60.0f);
                locomotionLateUpdate.Invoke(locomotion, null);
                input.Jump = false;

                // JumpVelocity/Gravity puts the apex/landing well inside a
                // 100-tick (1.6s) window -- freezing and rendering at 10
                // evenly spread points across it covers takeoff, early air,
                // apex, descent and landing without guessing which single
                // tick matters.
                const int totalTicks = 100;
                int[] freezeAtTick = { 2, 6, 12, 20, 30, 45, 60, 75, 88, 96 };
                int nextFreezeIndex = 0;

                for (int tick = 0; tick < totalTicks; tick++)
                {
                    if (nextFreezeIndex < freezeAtTick.Length && tick == freezeAtTick[nextFreezeIndex])
                    {
                        // THE FREEZE ITSELF -- exactly DevConsoleView.Open's own job.
                        animator.enabled = false;
                        for (int i = 0; i < 5; i++)
                        {
                            cameraLateUpdate.Invoke(orbitCamera, null);
                        }

                        RenderTexture target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                        image.Apply();
                        string path = System.IO.Path.Combine(outputDirectory, $"console-freeze-jump-tick{tick:D3}.png");
                        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                        target.Release();
                        Object.DestroyImmediate(target);
                        camera.targetTexture = null;
                        Debug.Log($"CityX: wrote {path} -- grounded={locomotion.IsGrounded} playerY={player.transform.position.y:F2}");

                        // THE UNFREEZE -- back to ordinary play before the next tick.
                        animator.enabled = true;
                        animator.Update(0.0f);
                        nextFreezeIndex++;
                    }

                    locomotion.Tick(input, 1.0f / 60.0f);
                    animator.Update(1.0f / 60.0f);
                    locomotionLateUpdate.Invoke(locomotion, null);
                    cameraLateUpdate.Invoke(orbitCamera, null);
                }
            }
            finally
            {
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

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- EXPLICIT BUG REPORT:
        /// "stretched elongated sometimes head gets detached." "Sometimes"
        /// points at something timing/input-dependent that none of the
        /// earlier smooth, predictable diagnostics (steady sine sweeps)
        /// ever drove -- a real player's mouse produces much more sudden,
        /// erratic yaw changes than any of those. Zooms in tight on the
        /// neck/head junction specifically, across many varied erratic
        /// swings, to look directly for a skinning gap rather than
        /// inferring one from bone rotation numbers alone.
        /// </summary>
        [MenuItem("CityX/Diagnose Head Detachment")]
        public static void DiagnoseHeadDetachment()
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

            var root = new GameObject("HeadDetachDiagnosticRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(2.0f, 24.0f, 0.0f);
                wall.transform.localScale = new Vector3(0.5f, 30.0f, 30.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                Physics.SyncTransforms();

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

                Vector3 anchor = new Vector3(1.75f, 22.0f, 0.0f);
                swing.Attach(anchor);

                var random = new System.Random(20260901);
                float maxHeadGap = 0.0f;
                Vector3 lastHeadPos = head.position;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = 500.0f;

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;

                // ERRATIC, NOT SMOOTH -- random large yaw jumps and random
                // WASD combinations every few ticks, mimicking a real mouse
                // whipped around fast rather than a steady predictable
                // sweep, over 400 ticks (6.6 real seconds).
                float yaw = 0.0f;
                Vector2 move = Vector2.zero;
                for (int i = 0; i < 400; i++)
                {
                    if (i % 5 == 0)
                    {
                        yaw += (float)(random.NextDouble() * 180.0 - 90.0);
                        move = new Vector2((float)(random.NextDouble() * 2.0 - 1.0), (float)(random.NextDouble() * 2.0 - 1.0));
                    }
                    bool reel = random.NextDouble() < 0.05;
                    bool payout = !reel && random.NextDouble() < 0.05;
                    swing.Tick(move, yaw, false, false, reel, payout, 1.0f / 60.0f);
                    if (!swing.IsActive)
                    {
                        swing.Attach(anchor);
                    }

                    // Neck-to-chest and chest-to-hips gap, measured directly
                    // in world space rather than inferred from rotation --
                    // this is what a real skinning separation would show up
                    // as: adjacent joints ending up further apart than the
                    // rig's own bone LENGTHS allow, which a pure rotation
                    // reading could never catch on its own.
                    if (neck != null && chest != null)
                    {
                        float gap = Vector3.Distance(neck.position, chest.position);
                        maxHeadGap = Mathf.Max(maxHeadGap, gap);
                    }

                    if (i % 40 == 0)
                    {
                        Debug.Log($"CityX DIAGNOSTIC tick={i} yaw={yaw:F0} " +
                            $"headPos={head.position:F2} neckPos={(neck != null ? neck.position : Vector3.zero):F2} " +
                            $"chestPos={(chest != null ? chest.position : Vector3.zero):F2} " +
                            $"spinePos={(spine != null ? spine.position : Vector3.zero):F2} " +
                            $"hipsPos={(hips != null ? hips.position : Vector3.zero):F2} " +
                            $"headToChest={(chest != null ? Vector3.Distance(head.position, chest.position) : -1.0f):F3}");

                        // TRACKS THE HIPS, NOT THE HEAD -- a camera centred
                        // on the head itself hides exactly the thing being
                        // checked for, since it re-frames every shot around
                        // wherever the head is regardless of whether that is
                        // still attached to the body. Framed from a fixed
                        // offset behind the whole upper body instead, so a
                        // real separation shows up as the head visibly NOT
                        // where a neck/shoulders would put it.
                        cameraObject.transform.position = hips.position + new Vector3(0.0f, 1.0f, -2.5f);
                        cameraObject.transform.LookAt(hips.position + Vector3.up * 1.0f);
                        camera.Render();
                        RenderTexture.active = target;
                        var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                        image.Apply();
                        string path = System.IO.Path.Combine(outputDirectory, $"head-detach-tick{i:D3}.png");
                        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                    }
                }

                Debug.Log($"CityX DIAGNOSTIC SUMMARY: maxNeckToChestDistance={maxHeadGap:F3} " +
                    "(compare against the rig's own resting neck-to-chest bone length to judge a real gap)");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- the Neck bone (distinct from
        /// Head) has no protection anywhere in this project: no identity
        /// lock like hips/spine, no captured base like GrappleSwing's own
        /// head sway. hips/spine needed their own lock specifically because
        /// this rig has a KNOWN retargeting wiggle; if Neck has an
        /// analogous one, freezing mid-animation (console or grapple, at
        /// an arbitrary real moment -- including mid-TRANSITION between two
        /// states, which a steady walk-cycle diagnostic never catches)
        /// could catch it at an extreme, never-normally-visible angle. The
        /// head is a separate, non-skinned rigid sphere that "slides
        /// through the torso" (HeadMesh's own doc comment) rather than
        /// blending into it, so a real neck excursion would show as an
        /// actual visible gap, not just deformation. Freezes at MANY
        /// random real moments across varied real input (walk, stop, turn,
        /// jump) via the real PlayerLocomotion + Animator, logging the
        /// neck's own rotation and the head-to-chest world distance each
        /// time, to catch a transition-timing issue no steady-state test
        /// would ever trigger.
        /// </summary>
        [MenuItem("CityX/Diagnose Neck Freeze Angle")]
        public static void DiagnoseNeckFreezeAngle()
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

            var root = new GameObject("NeckFreezeDiagnosticRig");
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
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

                if (neck == null)
                {
                    Debug.LogError("CityX DIAGNOSTIC: this rig has no mapped Neck bone at all.");
                    return;
                }

                var lateUpdateMethod = typeof(PlayerLocomotion).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var random = new System.Random(20260901);

                float worstNeckAngle = 0.0f;
                float worstHeadToHipsRatio = 0.0f;
                float restingHeadToHips = Vector3.Distance(head.position, hips.position);

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 45.0f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500.0f;
                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }
                target = new RenderTexture(700, 700, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;

                // VARIED, RANDOM REAL INPUT -- walk, stop, turn, jump,
                // sprint, all switching unpredictably, exactly what would
                // put the AnimatorController through real transitions
                // rather than settling into one steady-state loop.
                int worstFreezeTick = -1;
                for (int trial = 0; trial < 60; trial++)
                {
                    for (int i = 0; i < random.Next(5, 40); i++)
                    {
                        var input = new LocomotionInput
                        {
                            Move = new Vector2((float)(random.NextDouble() * 2.0 - 1.0), (float)(random.NextDouble() * 2.0 - 1.0)),
                            YawDegrees = (float)(random.NextDouble() * 360.0),
                            Sprint = random.NextDouble() < 0.3,
                            Jump = random.NextDouble() < 0.05,
                        };
                        locomotion.Tick(input, 1.0f / 60.0f);
                        animator.Update(1.0f / 60.0f);
                        lateUpdateMethod.Invoke(locomotion, null);
                    }

                    // THE FREEZE ITSELF, at this unpredictable real moment --
                    // exactly DevConsoleView.Open's own job.
                    animator.enabled = false;
                    lateUpdateMethod.Invoke(locomotion, null);

                    float neckAngle = Quaternion.Angle(Quaternion.identity, neck.localRotation);
                    float headToHips = Vector3.Distance(head.position, hips.position);
                    float ratio = headToHips / Mathf.Max(restingHeadToHips, 0.001f);
                    if (neckAngle > worstNeckAngle)
                    {
                        worstNeckAngle = neckAngle;
                        worstFreezeTick = trial;
                    }
                    worstHeadToHipsRatio = Mathf.Max(worstHeadToHipsRatio, ratio);

                    if (trial % 10 == 0 || neckAngle > 30.0f)
                    {
                        Debug.Log($"CityX DIAGNOSTIC trial={trial} neckLocalRot={neck.localRotation.eulerAngles:F1} " +
                            $"neckAngleFromIdentity={neckAngle:F1}deg headToHips={headToHips:F2} " +
                            $"(resting {restingHeadToHips:F2}, ratio {ratio:F2})");

                        cameraObject.transform.position = hips.position + new Vector3(0.0f, 1.2f, -2.2f);
                        cameraObject.transform.LookAt(hips.position + Vector3.up * 1.2f);
                        camera.Render();
                        RenderTexture.active = target;
                        var image = new Texture2D(700, 700, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 700, 700), 0, 0);
                        image.Apply();
                        string path = System.IO.Path.Combine(outputDirectory, $"neck-freeze-trial{trial:D2}.png");
                        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                    }

                    // Re-enable for the next trial's own real animation to run.
                    animator.enabled = true;
                }

                Debug.Log($"CityX DIAGNOSTIC SUMMARY: worstNeckAngleFromIdentity={worstNeckAngle:F1}deg " +
                    $"(at trial {worstFreezeTick}), worstHeadToHipsRatio={worstHeadToHipsRatio:F2} " +
                    $"(restingDistance={restingHeadToHips:F2})");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- RagdollPlayTests' own doc
        /// comment admits it never tests the REAL imported skeleton's own
        /// ragdoll rig at all, only a synthetic 3-box stand-in ("a PlayMode
        /// test cannot load the real skeleton"), so the real 15-body rig
        /// with its real joint limits has never actually been stress-tested
        /// under a real, violent velocity. Getting hit or dying WHILE
        /// swinging feeds the swing's own velocity (up to MaxSwingSpeed,
        /// 35 m/s -- far more violent than an ordinary fall or sprint)
        /// straight into Ragdoll.Activate. This project's own documented
        /// history is six separate failed attempts at driving THIS SAME
        /// multi-joint rig through fast, forced motion, every one ending
        /// in joint instability -- worth checking directly whether a
        /// single violent Activate (no forcing afterward, just the real
        /// PhysX solver settling it) can violate the neck joint's own
        /// limits badly enough to look like the head separating.
        /// </summary>
        [MenuItem("CityX/Diagnose Ragdoll At Swing Speed")]
        public static void DiagnoseRagdollAtSwingSpeed()
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

            var root = new GameObject("RagdollSwingSpeedDiagnosticRig");
            RenderTexture target = null;
            var previousSimMode = Physics.simulationMode;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                // CLOSE ENOUGH TO GUARANTEE IMPACT WITHIN THE TEST WINDOW --
                // the classic ragdoll failure point is the moment of ground
                // contact, not free-fall, and a first pass of this
                // diagnostic (ground far below, never reached in 120 ticks)
                // only ever checked free-fall stability.
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.SetParent(root.transform);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(0.0f, 3.0f, 0.0f);
                Physics.SyncTransforms();

                Ragdoll ragdoll = player.GetComponent<Ragdoll>();
                if (ragdoll == null || ragdoll.Bodies == null || ragdoll.Bodies.Length == 0)
                {
                    Debug.LogError("CityX DIAGNOSTIC: Player.prefab has no built ragdoll rig -- run the player rig builder first.");
                    return;
                }

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                // FALLS BACK TO SPINE -- matching RagdollBuilder.Build's own
                // "chest ?? spine" exactly: this rig has no bone mapped to
                // HumanBodyBones.Chest at all, so the actual "chest body"
                // Ragdoll.cs joins the head/neck chain onto IS the spine
                // bone's own rigidbody on this specific rig.
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                Rigidbody headBody = head.GetComponent<Rigidbody>();
                Rigidbody chestBody = chest != null ? chest.GetComponent<Rigidbody>() : null;
                float restingHeadToChest = chest != null ? Vector3.Distance(head.position, chest.position) : -1.0f;

                Debug.Log($"CityX DIAGNOSTIC: restingHeadToChest={restingHeadToChest:F3} " +
                    $"headBody={(headBody != null ? "found" : "MISSING")} chestBody={(chestBody != null ? "found" : "MISSING")}");

                // THE EXACT SCENARIO: swinging at full MaxSwingSpeed (35 m/s)
                // horizontally when a killing hit lands -- Ragdoll.Activate
                // carries that same velocity into every body in the rig,
                // matching OnDied's own "_locomotion.Velocity" hand-off.
                Vector3 swingVelocity = new Vector3(35.0f, 0.0f, 0.0f);
                ragdoll.Activate(swingVelocity);

                Physics.simulationMode = SimulationMode.Script;
                float worstHeadToChest = restingHeadToChest;
                float worstHeadSpeed = 0.0f;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 60.0f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500.0f;
                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }
                target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;

                for (int i = 0; i < 120; i++)
                {
                    Physics.Simulate(1.0f / 60.0f);

                    if (chest != null)
                    {
                        float headToChest = Vector3.Distance(head.position, chest.position);
                        worstHeadToChest = Mathf.Max(worstHeadToChest, headToChest);
                    }
                    if (headBody != null)
                    {
                        worstHeadSpeed = Mathf.Max(worstHeadSpeed, headBody.linearVelocity.magnitude);
                    }

                    if (i % 3 == 0)
                    {
                        float headToChestNow = chest != null ? Vector3.Distance(head.position, chest.position) : -1.0f;
                        Debug.Log($"CityX DIAGNOSTIC tick={i} headPos={head.position:F2} " +
                            $"headToChest={headToChestNow:F3} (resting {restingHeadToChest:F3}, ratio {headToChestNow / Mathf.Max(restingHeadToChest, 0.001f):F2}) " +
                            $"headVelocity={(headBody != null ? headBody.linearVelocity.magnitude : -1.0f):F2}");

                        cameraObject.transform.position = player.transform.position + new Vector3(0.0f, 2.0f, -6.0f);
                        cameraObject.transform.LookAt(head.position);
                        camera.Render();
                        RenderTexture.active = target;
                        var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                        image.Apply();
                        string path = System.IO.Path.Combine(outputDirectory, $"ragdoll-swing-speed-tick{i:D3}.png");
                        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                    }
                }

                Debug.Log($"CityX DIAGNOSTIC SUMMARY: restingHeadToChest={restingHeadToChest:F3} " +
                    $"worstHeadToChest={worstHeadToChest:F3} (ratio {worstHeadToChest / Mathf.Max(restingHeadToChest, 0.001f):F2}) " +
                    $"worstHeadSpeed={worstHeadSpeed:F2}");
            }
            finally
            {
                Physics.simulationMode = previousSimMode;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- EXPLICIT BUG REPORT, the most
        /// specific one yet: "gets more distorted the more you move... gets
        /// LESS distorted if you move back to original position while
        /// grapple rope is still attached... as soon as grapple rope is
        /// detached... the character goes back to normal." That is a
        /// REVERSIBLE, DISTANCE-DEPENDENT signature -- not a one-way
        /// compounding bug (which cannot un-happen by moving back) and not
        /// a frozen-bad-pose bug (which would not care about position at
        /// all). Something scales with how far the player has travelled
        /// from wherever the swing/freeze began, in both directions. Fully
        /// instrumented: swings OUT for half the run, then
        /// explicitly steers back toward the start for the other half,
        /// logging distance-from-start ALONGSIDE every candidate metric
        /// (visual rig offset, camera-to-pivot distance, right arm
        /// rotation, sway angles, raw velocity) every single tick, so
        /// whichever one actually tracks distance-from-start shows up
        /// directly in the correlation rather than needing to be guessed.
        /// </summary>
        [MenuItem("CityX/Diagnose Distance Based Distortion")]
        public static void DiagnoseDistanceBasedDistortion()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            var root = new GameObject("DistanceDistortionDiagnosticRig");
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                // A real wall FAR ENOUGH AWAY for a genuinely long rope --
                // a first pass of this diagnostic anchored only 2.66m away,
                // which let the player travel almost nowhere before
                // settling at the rope's own taut limit right next to the
                // wall, nothing like a real swing's actual tens-of-metres
                // arc. 20m gives real room to swing out and back.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(20.0f, 24.0f, 0.0f);
                wall.transform.localScale = new Vector3(0.5f, 30.0f, 30.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                Physics.SyncTransforms();
                Vector3 startPosition = player.transform.position;

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();

                var cameraObject = new GameObject("Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                ThirdPersonCamera orbitCamera = cameraObject.AddComponent<ThirdPersonCamera>();
                orbitCamera.Target = player.transform;
                swing.OrbitCamera = orbitCamera;
                swing.Bind();

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

                var cameraLateUpdate = typeof(ThirdPersonCamera).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Vector3 anchor = new Vector3(19.75f, 22.0f, 0.0f);
                swing.Attach(anchor);
                cameraLateUpdate.Invoke(orbitCamera, null);

                // OUT for 300 ticks (push consistently away from the wall
                // and sideways -- a real player swinging further and
                // further from where they started), then BACK for 300
                // ticks (push consistently the OPPOSITE way, steering
                // toward the start position) -- exactly the reported
                // repro shape, not a smooth sine sweep. Yaw held fixed
                // within each half so "push" stays a consistent world
                // direction throughout that half, rather than sweeping
                // through arbitrary directions the way earlier diagnostics
                // deliberately did to maximise chaos -- this one needs
                // controlled, monotonic outbound-then-inbound travel.
                for (int i = 0; i < 600; i++)
                {
                    bool outbound = i < 300;
                    Vector2 move = outbound ? new Vector2(-1.0f, -1.0f) : new Vector2(1.0f, 1.0f);
                    float yaw = 0.0f;
                    swing.Tick(move, yaw, false, false, false, false, 1.0f / 60.0f);
                    if (!swing.IsActive)
                    {
                        break;
                    }
                    cameraLateUpdate.Invoke(orbitCamera, null);

                    if (i % 20 == 0)
                    {
                        float distanceFromStart = Vector3.Distance(player.transform.position, startPosition);
                        float rigOffset = animator.transform.localPosition.magnitude;
                        float cameraDistance = Vector3.Distance(cameraObject.transform.position, head.position);
                        Debug.Log($"CityX DIAGNOSTIC {(outbound ? "OUT" : "BACK")} tick={i} " +
                            $"distanceFromStart={distanceFromStart:F2} rigOffset={rigOffset:F3} " +
                            $"cameraToHead={cameraDistance:F2} cameraDistanceField={orbitCamera.Distance:F2} " +
                            $"velocity={swing.Velocity.magnitude:F2} " +
                            $"rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} " +
                            $"headLocalRot={head.localRotation.eulerAngles:F1} " +
                            $"playerPos={player.transform.position:F2}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- every earlier diagnostic
        /// tonight tested "swinging" and "console open" as two SEPARATE
        /// scenarios, and both came back stable in isolation. Re-reading
        /// the user's own exact report closely: "it does the same glitch
        /// when console is open then goes back" could equally describe
        /// opening the console WHILE ALREADY MID-SWING (the console-open
        /// itself, not a separate standing-still repro), with "the more
        /// you move" describing how far into the swing's own arc it was
        /// opened at -- a genuinely different, never-yet-tested combined
        /// scenario, since GrappleSwing itself is never disabled by
        /// DevConsoleView (only InputReader/OrbitCamera/the Animator are),
        /// so GrappleSwing's own LateUpdate (including the new release
        /// blend) keeps running throughout. Fires a real swing, lets it
        /// run out to a real distance, freezes it exactly like opening the
        /// console would (disable InputReader/Animator, leave GrappleSwing
        /// itself alone), holds the freeze for a while, then unfreezes --
        /// logging every candidate metric through the whole sequence.
        /// </summary>
        [MenuItem("CityX/Diagnose Console Opened Mid Swing")]
        public static void DiagnoseConsoleOpenedMidSwing()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            var root = new GameObject("ConsoleMidSwingDiagnosticRig");
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(20.0f, 24.0f, 0.0f);
                wall.transform.localScale = new Vector3(0.5f, 30.0f, 30.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                Physics.SyncTransforms();

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();
                Animator animator = player.GetComponentInChildren<Animator>();
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

                var lateUpdateMethod = typeof(GrappleSwing).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Vector3 anchor = new Vector3(19.75f, 22.0f, 0.0f);
                swing.Attach(anchor);

                void LogState(string phase, int tick)
                {
                    Debug.Log($"CityX DIAGNOSTIC {phase} tick={tick} " +
                        $"animatorEnabled={animator.enabled} swingActive={swing.IsActive} " +
                        $"rigOffset={animator.transform.localPosition.magnitude:F3} " +
                        $"playerPos={player.transform.position:F2} velocity={swing.Velocity.magnitude:F2} " +
                        $"rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} " +
                        $"headLocalRot={head.localRotation.eulerAngles:F1}");
                }

                // Real swing, moving well away from the start, for 150
                // ticks (2.5 real seconds) -- a genuine mid-swing moment,
                // not the very first frame.
                for (int i = 0; i < 150; i++)
                {
                    swing.Tick(new Vector2(-1.0f, -1.0f), 0.0f, false, false, false, false, 1.0f / 60.0f);
                    if (i % 30 == 0) LogState("SWINGING", i);
                }

                // THE FREEZE ITSELF -- exactly DevConsoleView.Open's own
                // job. GrappleSwing itself is NOT disabled (DevConsoleView
                // never touches it directly) -- only InputReader (which
                // means nothing calls swing.Tick any more), OrbitCamera and
                // the Animator, matching the real console exactly.
                animator.enabled = false;
                Debug.Log("CityX DIAGNOSTIC: console OPENED mid-swing here");

                // Held frozen for 60 ticks (1 real second) -- GrappleSwing's
                // own LateUpdate (which the console never disables) keeps
                // running throughout, same as real gameplay.
                for (int i = 0; i < 60; i++)
                {
                    lateUpdateMethod.Invoke(swing, null);
                    if (i % 15 == 0) LogState("CONSOLE_OPEN", 150 + i);
                }

                // THE UNFREEZE -- exactly DevConsoleView.Close's own job
                // (Swing.IsActive is still true, so the real Close() would
                // also leave the Animator off, matching this).
                animator.enabled = swing.IsActive ? false : true;
                Debug.Log($"CityX DIAGNOSTIC: console CLOSED here (animator re-enabled={animator.enabled})");

                for (int i = 0; i < 90; i++)
                {
                    swing.Tick(new Vector2(-1.0f, -1.0f), 0.0f, false, false, false, false, 1.0f / 60.0f);
                    if (i % 15 == 0) LogState("RESUMED", 210 + i);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- every diagnostic tonight
        /// used a plain flat box for a wall. The real city is built from
        /// FusedBodyBuilder-style facade meshes with real decorative
        /// geometry hanging off them (fins, balconies, railings, window
        /// ribs) -- worth checking directly whether firing at a REAL
        /// building's real collision mesh, in the REAL saved scene,
        /// behaves any differently than a simple box ever could.
        /// </summary>
        [MenuItem("CityX/Diagnose Swing In Real City")]
        public static void DiagnoseSwingInRealCity()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/CityX/Scenes/CityMassing.unity", OpenSceneMode.Single);
            try
            {
                GameObject playerObject = null;
                foreach (GameObject rootObj in scene.GetRootGameObjects())
                {
                    if (rootObj.name == "Player")
                    {
                        playerObject = rootObj;
                        break;
                    }
                }
                if (playerObject == null)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no \"Player\" root object found in CityMassing.unity.");
                    return;
                }

                GrappleSwing swing = playerObject.GetComponent<GrappleSwing>();
                if (swing == null) swing = playerObject.AddComponent<GrappleSwing>();
                swing.Bind();
                Animator animator = playerObject.GetComponentInChildren<Animator>();
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

                Vector3 origin = playerObject.transform.position + Vector3.up * 1.5f;
                Debug.Log($"CityX DIAGNOSTIC: player found at {playerObject.transform.position:F2}");

                // Raycast forward and to each side looking for REAL nearby
                // building geometry to actually anchor on, rather than
                // assuming a fixed offset that might land in open air in
                // this specific generated layout.
                Vector3 anchor = Vector3.zero;
                bool found = false;
                foreach (Vector3 direction in new[]
                {
                    playerObject.transform.forward, playerObject.transform.right,
                    -playerObject.transform.right, -playerObject.transform.forward
                })
                {
                    if (Physics.Raycast(origin, direction, out RaycastHit hit, 40.0f, ~0, QueryTriggerInteraction.Ignore)
                        && !hit.transform.IsChildOf(playerObject.transform))
                    {
                        anchor = hit.point;
                        found = true;
                        Debug.Log($"CityX DIAGNOSTIC: real anchor found at {anchor:F2} on \"{hit.collider.name}\", " +
                            $"{hit.distance:F1}m away");
                        break;
                    }
                }
                if (!found)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no real building surface found nearby to anchor on.");
                    return;
                }

                swing.Attach(anchor);
                Vector3 startPos = playerObject.transform.position;
                float worstRigOffset = 0.0f;

                for (int i = 0; i < 400; i++)
                {
                    bool outbound = i < 200;
                    Vector2 move = outbound ? new Vector2(-1.0f, -1.0f) : new Vector2(1.0f, 1.0f);
                    swing.Tick(move, 0.0f, false, false, false, false, 1.0f / 60.0f);
                    if (!swing.IsActive)
                    {
                        Debug.Log($"CityX DIAGNOSTIC: swing ended itself at tick {i}");
                        break;
                    }

                    float rigOffset = animator.transform.localPosition.magnitude;
                    worstRigOffset = Mathf.Max(worstRigOffset, rigOffset);

                    if (i % 25 == 0)
                    {
                        Debug.Log($"CityX DIAGNOSTIC {(outbound ? "OUT" : "BACK")} tick={i} " +
                            $"distanceFromStart={Vector3.Distance(playerObject.transform.position, startPos):F2} " +
                            $"rigOffset={rigOffset:F3} velocity={swing.Velocity.magnitude:F2} " +
                            $"rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} " +
                            $"headLocalRot={head.localRotation.eulerAngles:F1} " +
                            $"playerPos={playerObject.transform.position:F2}");
                    }
                }

                Debug.Log($"CityX DIAGNOSTIC SUMMARY: worstRigOffset={worstRigOffset:F3}");
            }
            finally
            {
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- EXPLICIT BUG REPORT: "just
        /// imagine the character immediately glitches out... upon opening
        /// menu and grapple swinging." Every earlier diagnostic tonight
        /// drove the Animator through many real ticks of a SETTLED
        /// steady-state clip before freezing it -- none of them ever froze
        /// within the first few frames after a fresh spawn, which is
        /// exactly when a Mecanim AnimatorController is still running its
        /// own ENTRY TRANSITION (default/empty state blending into real
        /// idle/locomotion), a real, normal-duration blend that a
        /// consistent "spawn in, immediately open console to gear up"
        /// habit would reliably catch every single time -- not a rare
        /// fluke, a habitually-hit window. Freezes at ticks 0 through 20
        /// after a fresh Instantiate, logging bone rotations at each one,
        /// to check directly whether an early freeze catches a genuinely
        /// different (and possibly much worse) pose than a settled one.
        /// </summary>
        [MenuItem("CityX/Diagnose Freeze Immediately After Spawn")]
        public static void DiagnoseFreezeImmediatelyAfterSpawn()
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

            // A FRESH INSTANTIATE FOR EVERY SINGLE FREEZE POINT -- each
            // trial has to be a genuinely fresh spawn (its own Awake/Start,
            // its own from-scratch AnimatorController state), not the same
            // instance ticked further and further, or this would just
            // re-measure "settled pose at different times" all over again.
            for (int freezeAtTick = 0; freezeAtTick <= 20; freezeAtTick += 2)
            {
                var root = new GameObject($"FreezeAfterSpawnRig_{freezeAtTick}");
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
                    ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);

                    var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    player.transform.SetParent(root.transform);
                    player.transform.position = Vector3.zero;
                    Physics.SyncTransforms();

                    PlayerLocomotion locomotion = player.GetComponent<PlayerLocomotion>();
                    locomotion.Bind();
                    Animator animator = player.GetComponentInChildren<Animator>();
                    Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

                    var lateUpdate = typeof(PlayerLocomotion).GetMethod("LateUpdate",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                    // STANDING STILL, not walking -- matching "just spawned
                    // in, immediately opened console" before ever pressing
                    // a movement key at all.
                    for (int i = 0; i < freezeAtTick; i++)
                    {
                        locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                        animator.Update(1.0f / 60.0f);
                        lateUpdate.Invoke(locomotion, null);
                    }

                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                    bool inTransition = animator.IsInTransition(0);

                    // THE FREEZE ITSELF -- exactly DevConsoleView.Open's own job.
                    animator.enabled = false;
                    lateUpdate.Invoke(locomotion, null);

                    Debug.Log($"CityX DIAGNOSTIC freezeAtTick={freezeAtTick} " +
                        $"animatorState={state.shortNameHash} normalizedTime={state.normalizedTime:F2} " +
                        $"inTransition={inTransition} " +
                        $"rightUpperArm={rightUpperArm.localRotation.eulerAngles:F1} " +
                        $"leftUpperArm={leftUpperArm.localRotation.eulerAngles:F1} " +
                        $"headPos={head.position:F2}");

                    var cameraObject = new GameObject("Camera");
                    cameraObject.transform.SetParent(root.transform);
                    Camera camera = cameraObject.AddComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                    camera.fieldOfView = 50.0f;
                    camera.nearClipPlane = 0.05f;
                    camera.farClipPlane = 500.0f;
                    cameraObject.transform.position = player.transform.position + new Vector3(0.0f, 1.4f, -4.0f);
                    cameraObject.transform.LookAt(player.transform.position + Vector3.up * 1.0f);
                    foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.updateWhenOffscreen = true;
                    }
                    target = new RenderTexture(700, 700, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(700, 700, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 700, 700), 0, 0);
                    image.Apply();
                    string path = System.IO.Path.Combine(outputDirectory, $"freeze-after-spawn-tick{freezeAtTick:D2}.png");
                    System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally
                {
                    if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                    Object.DestroyImmediate(root);
                }
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- verifies the fix for the
        /// unevaluated-Animator bind-pose glitch directly: fires a rope the
        /// INSTANT a fresh Player.prefab instance exists (freezeAtTick=0,
        /// zero real ticks, zero Animator.Update calls before Attach), and
        /// logs the captured base rotations Attach's own CaptureBase reads
        /// right after. Before the fix these were a raw all-zero bind
        /// pose; after it, they should already be a real, sensible idle
        /// pose.
        /// </summary>
        [MenuItem("CityX/Diagnose Attach Immediately After Spawn")]
        public static void DiagnoseAttachImmediatelyAfterSpawn()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: need Player.prefab built first.");
                return;
            }

            var root = new GameObject("AttachAfterSpawnDiagnosticRig");
            try
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
                Physics.SyncTransforms();

                GrappleSwing swing = player.GetComponent<GrappleSwing>();
                if (swing == null) swing = player.AddComponent<GrappleSwing>();
                swing.Bind();
                Animator animator = player.GetComponentInChildren<Animator>();
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

                // ZERO real ticks, zero Animator.Update calls -- fires
                // literally the instant the rig exists, exactly the
                // spawn-in-then-immediately-fire repro.
                swing.Attach(player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));

                Debug.Log($"CityX DIAGNOSTIC: immediately after Attach on a fresh spawn -- " +
                    $"rightUpperArm.localRotation={rightUpperArm.localRotation.eulerAngles:F1} " +
                    $"leftUpperArm.localRotation={leftUpperArm.localRotation.eulerAngles:F1} " +
                    $"(all-zero would mean the raw bind pose was captured as the swing's own base)");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
#if CITYX_LEGACY_GRID_DIAGNOSTICS
        // The three diagnostics below were written against the old square-grid city (sloped terrace streets, district boundaries,
        // multi-chunk wings). None of those features exist any more; the code is kept, switched off, for reference.


        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- a street-level look at
        /// CityMeshBuilder.AddSlopedStreetRamp in the REAL streamed city
        /// (EXPLICIT ASK: "make these cut off streets sloped like san
        /// francisco... never so steep that you can walk or run up it").
        /// Mirrors AddStreetEdgeFeatures's own roll to find a real
        /// street-vs-street terrace drop that actually came out sloped,
        /// rather than pointing the camera at an arbitrary spot and hoping.
        /// </summary>
        [MenuItem("CityX/Diagnose Sloped Street In Real Scene")]
        public static void DiagnoseSlopedStreetInRealScene()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/CityX/Scenes/CityMassing.unity", OpenSceneMode.Single);

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            RenderTexture target = null;
            try
            {
                CityWorld world = new CityWorld(70485213);
                int foundX = 0, foundY = 0, foundNx = 0, foundNy = 0;
                float foundPad = 0.0f, foundNeighbourPad = 0.0f;
                bool found = false;
                double bestDistance = double.MaxValue;
                int totalRamps = 0;
                for (int x = -20; x <= 20; x++)
                {
                    for (int y = -20; y <= 20; y++)
                    {
                        if (!CityLayout.IsStreetChunk(x, y)) continue;
                        float pad = world.Terrain.PadHeightFor(x, y);
                        foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                        {
                            float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                            if (neighbourPad >= pad) continue;
                            bool sloped = new DeterministicRandom(world.ChunkSeed(nx, ny)).NextDouble() <
                                CityMeshBuilder.SlopedStreetChance;
                            if (!sloped) continue;
                            totalRamps++;
                            double cx = CityLayout.ChunkCentreX(x);
                            double cz = CityLayout.ChunkCentreZ(y);
                            double distance = System.Math.Sqrt(cx * cx + cz * cz);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                foundX = x; foundY = y; foundNx = nx; foundNy = ny;
                                foundPad = pad; foundNeighbourPad = neighbourPad;
                                found = true;
                            }
                        }
                    }
                }
                if (!found)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no sloped street edge found within the search radius.");
                    return;
                }
                Debug.Log($"CityX DIAGNOSTIC: {totalRamps} sloped ramps within the +/-20 chunk search " +
                    $"({20 * 48}m box) around spawn -- closest is {bestDistance:F0}m away at chunk " +
                    $"({foundX},{foundY}) dropping to ({foundNx},{foundNy}).");

                Vector3 chunkCentre = new Vector3(
                    (float)CityLayout.ChunkCentreX(foundX), foundPad, (float)CityLayout.ChunkCentreZ(foundY));

                CityStreamer streamer = Object.FindFirstObjectByType<CityStreamer>();
                if (streamer == null)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no CityStreamer in the real saved scene.");
                    return;
                }
                streamer.StreamAround(chunkCentre, 400);
                Physics.SyncTransforms();

                // Elevated three-quarter view, offset a little to the side
                // of the centre line -- a steep ramp seen dead-on from eye
                // height just reads as a brink with sky beyond it, the same
                // way a real cliff edge does. Looking down and slightly
                // across is what actually shows the sloped FACE.
                Vector3 towardNeighbour = new Vector3(foundNx - foundX, 0.0f, foundNy - foundY).normalized;
                Vector3 perp = Vector3.Cross(Vector3.up, towardNeighbour);
                Vector3 cameraPos = chunkCentre - towardNeighbour * 8.0f + perp * 3.0f + new Vector3(0.0f, 18.0f, 0.0f);
                Vector3 lookTarget = chunkCentre + towardNeighbour * 10.0f;
                lookTarget.y = foundNeighbourPad - 3.0f;

                var cameraObject = new GameObject("Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 500.0f;
                camera.nearClipPlane = 0.1f;
                cameraObject.transform.position = cameraPos;
                cameraObject.transform.LookAt(lookTarget);

                target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "sloped-street-real-scene.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path} -- chunk ({foundX},{foundY}) drops to " +
                    $"({foundNx},{foundNy}), camera at {cameraObject.transform.position:F2}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- a birds-eye look at a real
        /// district boundary in the streamed city, framed to show two
        /// neighbouring districts with visibly different local block sizes
        /// on either side of the same arterial street (EXPLICIT ASK:
        /// "separate areas by neighborhood and having main streets
        /// connecting them. not everything should be a blocky grid").
        /// </summary>
        [MenuItem("CityX/Diagnose District Boundary In Real Scene")]
        public static void DiagnoseDistrictBoundaryInRealScene()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/CityX/Scenes/CityMassing.unity", OpenSceneMode.Single);

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            RenderTexture target = null;
            try
            {
                int bestDistrictX = 0, bestDistrictY = 0, bestPeriodA = 0, bestPeriodB = 0;
                double bestScore = -1.0;
                for (int dx = -6; dx <= 6; dx++)
                {
                    for (int dy = -6; dy <= 6; dy++)
                    {
                        // Districts are irregular now; probe each nominal
                        // district at its middle and its eastern neighbour
                        // just past the arterial that bounds it.
                        int probeX = dx * CityLayout.DistrictChunks + CityLayout.DistrictChunks / 2;
                        int probeY = dy * CityLayout.DistrictChunks + CityLayout.DistrictChunks / 2;
                        CityLayout.GetDistrictExtent(probeX, probeY, out _, out int eastEdge, out _, out _);
                        int periodA = CityLayout.DistrictPeriod(probeX, probeY);
                        int periodB = CityLayout.DistrictPeriod(eastEdge + 1, probeY);
                        double score = System.Math.Abs(periodA - periodB) -
                            0.01 * (dx * dx + dy * dy); // prefer contrast, then closeness to spawn
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestDistrictX = dx;
                            bestDistrictY = dy;
                            bestPeriodA = periodA;
                            bestPeriodB = periodB;
                        }
                    }
                }

                CityWorld world = new CityWorld(70485213);
                // The arterial line between district (bestDistrictX, *) and
                // (bestDistrictX+1, *), at a chunk comfortably inside the
                // district's own Y span so both sides show real interior.
                int bestProbeX = bestDistrictX * CityLayout.DistrictChunks + CityLayout.DistrictChunks / 2;
                int chunkY = bestDistrictY * CityLayout.DistrictChunks + CityLayout.DistrictChunks / 2;
                CityLayout.GetDistrictExtent(bestProbeX, chunkY, out _, out int arterialChunkX, out _, out _);
                float pad = world.Terrain.PadHeightFor(arterialChunkX, chunkY);
                Vector3 centre = new Vector3(
                    (float)CityLayout.ChunkCentreX(arterialChunkX), pad, (float)CityLayout.ChunkCentreZ(chunkY));

                CityStreamer streamer = Object.FindFirstObjectByType<CityStreamer>();
                if (streamer == null)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no CityStreamer in the real saved scene.");
                    return;
                }
                streamer.StreamAround(centre, 3000);
                Physics.SyncTransforms();

                var cameraObject = new GameObject("Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 2000.0f;
                camera.nearClipPlane = 0.1f;
                cameraObject.transform.position = centre + new Vector3(0.0f, 260.0f, -1.0f);
                cameraObject.transform.rotation = Quaternion.Euler(89.0f, 0.0f, 0.0f);

                target = new RenderTexture(1280, 1280, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "district-boundary-real-scene.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path} -- district ({bestDistrictX},{bestDistrictY}) period " +
                    $"{bestPeriodA} next to district ({bestDistrictX + 1},{bestDistrictY}) period {bestPeriodB}, " +
                    $"arterial at chunk x={arterialChunkX}, camera at {cameraObject.transform.position:F2}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- a street-level look at a real
        /// multi-wing building in the streamed city (EXPLICIT ASK: "you can
        /// make different sized buildings too dont have to have 4 corners").
        /// Searches for a building that grew wings on two or three walls at
        /// once, not just one, since that is the case most likely to actually
        /// read as a T or U shape rather than a plain L.
        /// </summary>
        [MenuItem("CityX/Diagnose Multi Wing Building In Real Scene")]
        public static void DiagnoseMultiWingBuildingInRealScene()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/CityX/Scenes/CityMassing.unity", OpenSceneMode.Single);

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            RenderTexture target = null;
            try
            {
                CityWorld world = new CityWorld(70485213);
                BuildingSite bestSite = default;
                Bounds bestWingBox = default;
                int bestWingCount = 0;
                for (int x = -25; x <= 25; x++)
                {
                    for (int y = -25; y <= 25; y++)
                    {
                        BuildingSite site = world.BuildingAt(x, y);
                        if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                        {
                            continue;
                        }
                        // Prefer a corner/edge lot: IsStreetChunk next door on
                        // two sides means open sky to shoot the wing against,
                        // not another tower's own facade filling the frame.
                        int openSides = 0;
                        if (CityLayout.IsStreetChunk(x - 1, y)) openSides++;
                        if (CityLayout.IsStreetChunk(x + 1, y)) openSides++;
                        if (CityLayout.IsStreetChunk(x, y - 1)) openSides++;
                        if (CityLayout.IsStreetChunk(x, y + 1)) openSides++;

                        List<Bounds> wings = CityMeshBuilder.WingBoxesFor(world, site);
                        if (wings.Count == 0 || openSides < 2)
                        {
                            continue;
                        }
                        int score = wings.Count * 10 + openSides;
                        if (score > bestWingCount)
                        {
                            bestWingCount = score;
                            bestSite = site;
                            bestWingBox = wings[0];
                        }
                    }
                }
                if (bestWingCount == 0)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no open-sided multi-wing building found within the search radius.");
                    return;
                }
                Debug.Log($"CityX DIAGNOSTIC: building at ({bestSite.AnchorX},{bestSite.AnchorY}) has " +
                    $"footprint {bestSite.Params.Width:F1}x{bestSite.Params.Depth:F1}, wing box {bestWingBox}.");

                Vector3 centre = new Vector3((float)bestSite.CentreX, (float)bestSite.BaseHeight,
                    (float)bestSite.CentreZ);

                CityStreamer streamer = Object.FindFirstObjectByType<CityStreamer>();
                if (streamer == null)
                {
                    Debug.LogError("CityX DIAGNOSTIC: no CityStreamer in the real saved scene.");
                    return;
                }
                streamer.StreamAround(centre, 800);
                Physics.SyncTransforms();

                var cameraObject = new GameObject("Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 1000.0f;
                camera.nearClipPlane = 0.1f;
                // High and well back, looking down at an angle -- a ground-
                // level dolly shot this close to a dense downtown block just
                // as often lands inside or against the NEXT building over
                // (confirmed directly, live: a first attempt at this exact
                // shot rendered nothing but a neighbour's ribbed facade
                // filling the frame). Height clears every neighbour instead
                // of gambling on which diagonal happens to be open street.
                // Straight down from well above the roof -- the one framing
                // that does not need to guess which direction is open sky
                // versus another tower's own wall (confirmed directly, live:
                // two different angled approaches each ended up aimed
                // straight into a neighbour instead). A true plan view shows
                // the footprint's own outline directly regardless.
                cameraObject.transform.position =
                    centre + Vector3.up * (bestSite.Params.Height + 45.0f);
                cameraObject.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);

                target = new RenderTexture(1280, 960, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 960, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "multi-wing-building-real-scene.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path} -- camera at {cameraObject.transform.position:F2}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

#endif // CITYX_LEGACY_GRID_DIAGNOSTICS
        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- confirms PlayerChat's own
        /// Ragdoll field actually points at the PLAYER's own Ragdoll, not
        /// some other Civilian-based body in the scene.
        ///
        /// EXPLICIT BUG REPORT this exists for: "/sleep isnt working" --
        /// root-caused directly: the scene ends up with SEVERAL Ragdoll
        /// components (the sparring dummies are Civilian.prefab-based too,
        /// each carrying its own), and the original wiring used
        /// Object.FindFirstObjectByType&lt;Ragdoll&gt;() AFTER all of them
        /// already existed in the scene -- genuinely ambiguous about which
        /// one it would find, and it found some NPC's, not the player's.
        /// Fixed by capturing the player's own Ragdoll directly off the
        /// player GameObject at the moment it's built, before any other
        /// Ragdoll exists to be confused with it.
        /// </summary>
        [MenuItem("CityX/Diagnose Sleep Command")]
        public static void DiagnoseSleepCommand()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/CityX/Scenes/CityMassing.unity", OpenSceneMode.Single);

            GameObject playerObject = null;
            PlayerChat chat = null;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                if (rootObj.name == "Player") playerObject = rootObj;
                if (chat == null) chat = rootObj.GetComponentInChildren<PlayerChat>(true);
            }

            if (playerObject == null || chat == null)
            {
                Debug.LogError($"CityX DIAGNOSTIC: playerObject={playerObject}, chat={chat} -- can't verify.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Ragdoll playerRagdoll = playerObject.GetComponent<Ragdoll>();
            bool wiredCorrectly = chat.Ragdoll == playerRagdoll;
            Debug.Log($"CityX DIAGNOSTIC: chat.Ragdoll == player's own Ragdoll? {wiredCorrectly} " +
                $"(chat.Ragdoll={chat.Ragdoll}, player's={playerRagdoll})");

            chat.Ragdoll.Bind();
            chat.Submit("/sleep");
            Debug.Log($"CityX DIAGNOSTIC: after /sleep, PLAYER's own Ragdoll.IsLimp={playerRagdoll.IsLimp}");

            if (Application.isBatchMode) EditorApplication.Exit(wiredCorrectly && playerRagdoll.IsLimp ? 0 : 1);
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
