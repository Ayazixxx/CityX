using System.IO;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Verifies the pistol aim POV fix (AimShoulderOffset 0.25 -> 0.65) --
    /// Bug report, with a screenshot: holding right-click put the
    /// crosshair almost exactly on the character's own head/shoulder and
    /// read as the character being "aimed left slightly." Renders from the
    /// SAME position/rotation ThirdPersonCamera.ApplyCameraTransform would
    /// compute (replicated here rather than driving the live component,
    /// which needs LateUpdate/real Play Mode -- see this method's own body
    /// for the exact pivot/shoulder-offset formula, copied verbatim), with
    /// PlayerLocomotion.Tick actually driving the real gun-aim arm pose the
    /// same way gameplay does (this part IS the real component, ticked
    /// directly the same way JetpackWallCrashTests/etc. already do in
    /// EditMode without needing full Play Mode).
    ///
    /// Two shots: the actual in-game POV (from the computed camera
    /// position, to check the crosshair/framing complaint) and an external
    /// third-person view (to check the character's own facing/pose).
    /// Needs a real graphics device -- do not run with -nographics.
    /// </summary>
    public static class PistolAimPovCapture
    {
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";

        [MenuItem("CityX/Capture Pistol Aim POV")]
        public static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"CityX: no player prefab at {PlayerPrefabPath}.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                var locomotion = instance.GetComponent<PlayerLocomotion>();
                if (locomotion == null)
                {
                    Debug.LogError("CityX: player prefab has no PlayerLocomotion.");
                    return;
                }
                locomotion.Bind();

                // Same formula as ThirdPersonCamera.ApplyCameraTransform:
                // pivot at PivotHeightFraction of standing height, camera
                // yaw/pitch at 0 (facing world +Z, matching a fresh
                // spawn -- no mouse input to simulate), shoulder pivot
                // offset to the right by AimShoulderOffset, camera pulled
                // back along -forward by Distance. Values copied from the
                // live ThirdPersonCamera field defaults/scene rather than
                // re-guessed.
                const float pivotHeightFraction = 0.73f;
                const float aimShoulderOffset = ThirdPersonCamera.DefaultAimShoulderOffset;
                const float distance = ThirdPersonCamera.DefaultAimDistance;

                float standingHeight = locomotion.StandingHeight;
                Vector3 pivot = instance.transform.position + Vector3.up * (standingHeight * pivotHeightFraction + ThirdPersonCamera.DefaultAimPivotRaise);
                Quaternion rotation = Quaternion.identity;
                Vector3 shoulderPivot = pivot + rotation * Vector3.right * aimShoulderOffset;
                Vector3 cameraPosition = shoulderPivot - rotation * Vector3.forward * distance;
                Vector3 cameraForward = rotation * Vector3.forward;

                // Drives the REAL gun-aim arm pose exactly like gameplay
                // does -- several ticks so _gunAimBlend (eases at
                // GunAimBlendPerSecond = 6/s) reaches full strength rather
                // than capturing the first, still-blending-in frame.
                var input = new LocomotionInput
                {
                    Aiming = true,
                    AimCameraPosition = cameraPosition,
                    AimCameraForward = cameraForward,
                    YawDegrees = 0.0f,
                };
                const float step = 1.0f / 60.0f;
                for (int i = 0; i < 30; i++)
                {
                    locomotion.Tick(input, step);
                }

                foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                string outputDirectory = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
                Directory.CreateDirectory(outputDirectory);

                ShootFrom(cameraPosition, cameraForward, 32.0f,
                    Path.Combine(outputDirectory, "pistol-aim-pov.png"));

                // External third-person check: pulled back and to the side,
                // looking at the same pivot, so the character's own facing
                // direction is checkable independent of the POV framing.
                Vector3 externalPos = pivot + new Vector3(3.0f, 0.5f, -3.0f);
                Vector3 externalForward = (pivot - externalPos).normalized;
                ShootFrom(externalPos, externalForward, 40.0f,
                    Path.Combine(outputDirectory, "pistol-aim-external.png"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// Bug report: "the legs move as you hold the pistol and
        /// aim in if you are walking [in Godot] which it doesnt do in
        /// unity," and separately, "he is rotated to the left [while]
        /// holding right click," reported specifically while walking.
        /// Unlike Capture() above (standing still, camera facing world +Z),
        /// this drives real forward Move input, GunEquipped=true, and a
        /// ROTATED camera yaw (40 degrees, not 0) -- the exact condition
        /// that used to freeze the Animator on the static standing aim pose
        /// (see PlayerRigBuilder.AddAimingState's own doc comment) and ease
        /// the body's facing toward the camera at TurnDegreesPerSecond
        /// instead of snapping (see PlayerLocomotion.Tick's own rotation
        /// comment). An external third-person shot (not the POV) is what
        /// actually shows both bugs: a frozen leg pose while the body
        /// visibly slides, and a body facing away from the camera's own
        /// 40-degree turn.
        /// </summary>
        [MenuItem("CityX/Capture Pistol Aim POV (Walking)")]
        public static void CaptureWalking()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"CityX: no player prefab at {PlayerPrefabPath}.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                var locomotion = instance.GetComponent<PlayerLocomotion>();
                if (locomotion == null)
                {
                    Debug.LogError("CityX: player prefab has no PlayerLocomotion.");
                    return;
                }
                locomotion.Bind();

                const float yawDegrees = 40.0f;
                const float pivotHeightFraction = 0.73f;
                const float aimShoulderOffset = ThirdPersonCamera.DefaultAimShoulderOffset;
                const float distance = ThirdPersonCamera.DefaultAimDistance;

                float standingHeight = locomotion.StandingHeight;
                Quaternion rotation = Quaternion.Euler(0.0f, yawDegrees, 0.0f);
                Vector3 pivot = instance.transform.position + Vector3.up * (standingHeight * pivotHeightFraction + ThirdPersonCamera.DefaultAimPivotRaise);
                Vector3 shoulderPivot = pivot + rotation * Vector3.right * aimShoulderOffset;
                Vector3 cameraPosition = shoulderPivot - rotation * Vector3.forward * distance;
                Vector3 cameraForward = rotation * Vector3.forward;

                var input = new LocomotionInput
                {
                    Move = new Vector2(0.0f, 1.0f),
                    YawDegrees = yawDegrees,
                    Aiming = true,
                    GunEquipped = true,
                    AimCameraPosition = cameraPosition,
                    AimCameraForward = cameraForward,
                };
                Animator animator = instance.GetComponentInChildren<Animator>();
                const float step = 1.0f / 60.0f;
                // MANY ticks -- enough for the CharacterController to have
                // travelled a visible distance, for _gunAimBlend to fully
                // ease in, and (this being an Animator, not just
                // PlayerLocomotion's own math) for the real transition
                // blend into GunLocomotion to have settled, not just started.
                //
                // animator.Update IS CALLED EXPLICITLY, NOT LEFT TO UNITY --
                // outside Play Mode there is no per-frame engine loop
                // driving MonoBehaviour LateUpdate or the Animator's own
                // state machine at all; PlayerLocomotion.Tick only ever
                // sets the Animator's PARAMETERS (Speed/Aiming/GunEquipped/
                // etc, see its own SetBool/SetFloat calls), it never
                // advances the state graph that reads them. Without this,
                // the Animator would report whatever state it happened to
                // initialize into and never move, regardless of how many
                // times Tick() runs.
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(input, step);
                    if (animator != null)
                    {
                        animator.Update(step);
                    }
                }

                foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                string outputDirectory = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
                Directory.CreateDirectory(outputDirectory);

                // External third-person view, pulled back from the CURRENT
                // (post-walk) position -- not the spawn point -- looking at
                // chest height so both the leg pose and the body's own
                // facing relative to the camera's 40-degree turn are
                // visible in frame.
                Vector3 chest = instance.transform.position + Vector3.up * (standingHeight * 0.6f);
                Vector3 externalPos = chest + new Vector3(4.0f, 1.0f, -4.0f);
                Vector3 externalForward = (chest - externalPos).normalized;
                ShootFrom(externalPos, externalForward, 45.0f,
                    Path.Combine(outputDirectory, "pistol-aim-walking-external.png"));

                if (animator != null)
                {
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    Debug.Log($"CityX: after {90} ticks, Animator state hash matches GunLocomotion: " +
                              $"{stateInfo.IsName("GunLocomotion")}, matches Aiming: {stateInfo.IsName("Aiming")}, " +
                              $"matches Locomotion: {stateInfo.IsName("Locomotion")}");
                }
                Debug.Log($"CityX: body facing after walking+aiming at yaw={yawDegrees}: " +
                          $"{instance.transform.eulerAngles.y:0.00} degrees (should equal {yawDegrees:0.00}).");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void ShootFrom(Vector3 position, Vector3 forward, float fov, string path)
        {
            var cameraObject = new GameObject("Camera");
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position = position;
                cameraObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

                var sunObject = new GameObject("Sun");
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(40.0f, 60.0f, 0.0f);

                const int width = 900;
                const int height = 700;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(sunObject);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
