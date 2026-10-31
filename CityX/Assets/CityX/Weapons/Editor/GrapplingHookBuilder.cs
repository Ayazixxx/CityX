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
    /// THE EMBEDDED TEXTURE IS UNUSABLE, a known glTFast quirk with Meshy
    /// exports: confirmed directly that
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
        /// colour stands in for the whole texture.
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
        /// long with the muzzle at its +Z end. Requested: "just replace it
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

                StillPose.ArmsDown(player.transform);

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

#if CITYX_LEGACY_GRID_DIAGNOSTICS
        // The three diagnostics below were written against the old square-grid city (sloped terrace streets, district boundaries,
        // multi-chunk wings). None of those features exist any more; the code is kept, switched off, for reference.


        /// <summary>
        /// DIAGNOSTIC, NOT A PERMANENT TOOL -- a street-level look at
        /// CityMeshBuilder.AddSlopedStreetRamp in the REAL streamed city
        /// (Requested: "make these cut off streets sloped like san
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
                CityWorld world = new CityWorld(61256619);
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
        /// on either side of the same arterial street (Requested:
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

                CityWorld world = new CityWorld(61256619);
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
        /// multi-wing building in the streamed city (Requested: "you can
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
                CityWorld world = new CityWorld(61256619);
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
    }
}
