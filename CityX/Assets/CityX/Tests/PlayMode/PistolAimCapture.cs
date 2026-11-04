using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the Glock as the player sees it while aiming and how the hands hold it (owner, 2026-11-03: "he doesnt grip the glock properly", "the pov when
    /// shooting the pistol doesnt show enough of the gun its covered by the body"). Writes to Logs/CityShots: pistol-aim-views.png (the real
    /// ThirdPersonCamera at the framings in <see cref="Views"/>), pistol-grip.png (close-ups of the hands from four sides), and pistol-hipfire.png
    /// (firing without aiming). Logs, per framing, how much of the gun's picture is really seen (rendered with and without it) and how wide it is
    /// on the screen. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PistolAimCapture
    {
        private const float Dt = 1.0f / 60.0f;
        private const int CellWidth = 960;
        private const int CellHeight = 600;
        private const int GunLayer = 31;

        private readonly struct View
        {
            public readonly float Distance, Shoulder, Raise, FieldOfView;

            public View(float distance, float shoulder, float raise, float fieldOfView)
            {
                Distance = distance;
                Shoulder = shoulder;
                Raise = raise;
                FieldOfView = fieldOfView;
            }
        }

        // The first is the game's own framing for a pistol, the last two its framing for other guns and a closer one still.
        private static readonly View[] Views =
        {
            new View(ThirdPersonCamera.PistolAimDistance, ThirdPersonCamera.PistolAimShoulderOffset, ThirdPersonCamera.PistolAimPivotRaise, ThirdPersonCamera.PistolAimFieldOfView),
            new View(2.4f, 1.2f, 0.3f, 40.0f),
            new View(2.0f, 1.1f, 0.3f, 45.0f),
            new View(3.0f, 1.4f, 0.3f, 36.0f),
            new View(ThirdPersonCamera.DefaultAimDistance, ThirdPersonCamera.DefaultAimShoulderOffset, ThirdPersonCamera.DefaultAimPivotRaise, 30.0f),
            new View(1.7f, 0.95f, 0.25f, 50.0f),
        };

        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private ThirdPersonCamera _follow;
        private PistolWeapon _gun;
        private GunFeel _feel;

        [UnityTest]
        public IEnumerator RenderTheAimViewsAndTheGrip()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var scenery = new GameObject("Scenery");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(scenery.transform);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(scenery.transform);
            wall.transform.position = new Vector3(0.0f, 5.0f, 40.0f);
            wall.transform.localScale = new Vector3(120.0f, 10.0f, 0.5f);
            wall.GetComponent<Renderer>().material.color = new Color(0.62f, 0.6f, 0.56f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(scenery.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);

            _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab"), new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<PistolWeapon>();
            _gun.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            _feel = _player.GetComponent<GunFeel>();

            var cameraObject = new GameObject("FollowCamera") { tag = "MainCamera" };
            Camera view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = 0.05f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            _follow = cameraObject.AddComponent<ThirdPersonCamera>();
            _follow.Target = _player.transform;
            var closeObject = new GameObject("CloseCamera");
            Camera close = closeObject.AddComponent<Camera>();
            close.enabled = false;
            close.fieldOfView = 26.0f;
            close.nearClipPlane = 0.05f;
            close.clearFlags = CameraClearFlags.SolidColor;
            close.backgroundColor = view.backgroundColor;
            yield return null;
            _follow.Yaw = 0.0f;
            _follow.Pitch = 2.0f;

            try
            {
                var images = new List<Texture2D>();
                for (int i = 0; i < Views.Length; i++)
                {
                    View framing = Views[i];
                    _follow.AimDistance = framing.Distance;
                    _follow.AimShoulderOffset = framing.Shoulder;
                    _follow.AimPivotRaise = framing.Raise;
                    _follow.AimFieldOfView = framing.FieldOfView;
                    yield return Run(true, false, 2.0f);
                    images.Add(Shot(view));
                    LogFraming(i, framing, view);
                }
                SaveSheet(images, 3, Path.Combine(directory, "pistol-aim-views.png"));
                images.Clear();

                // The game's own framing, aiming and mid-shot, as the player sees it.
                _follow.PistolAim = true;
                yield return Run(true, false, 2.0f);
                images.Add(Shot(view));
                yield return Run(true, true, 0.1f);
                images.Add(Shot(view));
                SaveSheet(images, 2, Path.Combine(directory, "pistol-aim-game.png"));
                images.Clear();

                // The hands, from four sides of the gun.
                yield return Run(true, false, 0.5f);
                Transform gun = _gun.transform;
                Vector3 grip = gun.TransformPoint(new Vector3(0.0f, -0.08f, 0.05f));
                Vector3[] from =
                {
                    gun.right,                                    // the back of the shooting hand
                    -gun.right,                                   // the support hand's side
                    (gun.forward - gun.up * 0.2f - gun.right * 0.3f).normalized,   // from the front, the fingers on the front strap
                    (gun.up - gun.forward * 0.5f + gun.right * 0.2f).normalized,   // from above and behind, the thumbs
                };
                foreach (Vector3 direction in from)
                {
                    close.transform.position = grip + direction * 1.3f;
                    close.transform.rotation = Quaternion.LookRotation(grip - close.transform.position, gun.up);
                    images.Add(Shot(close));
                }
                SaveSheet(images, 2, Path.Combine(directory, "pistol-grip.png"));

                // Carried, as the camera sees it and from the side; then fired without aiming.
                yield return Run(false, false, 2.5f);
                images.Clear();
                images.Add(Shot(view));
                Vector3 hip = _player.transform.position + Vector3.up * 1.1f;
                close.fieldOfView = 40.0f;
                close.transform.position = hip + _player.transform.right * 3.5f + _player.transform.forward * 0.6f;
                close.transform.rotation = Quaternion.LookRotation(hip - close.transform.position, Vector3.up);
                images.Add(Shot(close));
                yield return Run(false, true, 0.4f);
                images.Add(Shot(view));
                SaveSheet(images, 3, Path.Combine(directory, "pistol-hipfire.png"));
            }
            finally
            {
                Object.Destroy(_player);
                Object.Destroy(cameraObject);
                Object.Destroy(closeObject);
                Object.Destroy(scenery);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
#else
            yield break;
#endif
        }

        /// <summary>Plays <paramref name="seconds"/> of frames as the input reader would: the body ticked with the camera's aim, the camera told.</summary>
        private IEnumerator Run(bool aiming, bool firing, float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += Dt)
            {
                _follow.IsAiming = aiming;
                Transform view = _follow.transform;
                _locomotion.Tick(new LocomotionInput
                {
                    Aiming = aiming,
                    GunEquipped = true,
                    YawDegrees = _follow.Yaw,
                    AimCameraPosition = view.position,
                    AimCameraForward = view.forward,
                    GunAimPoint = _feel != null ? _feel.AimPoint : Vector3.zero,
                    HasGunAimPoint = _feel != null && _feel.HasAimPoint,
                }, Dt);
                if (firing)
                {
                    _gun.Fire(view.position, view.forward, aiming, _player);
                }
                yield return null;
            }
        }

        /// <summary>How much of the gun's picture is seen: pixels that change when it is drawn, against the pixels it covers alone; and how wide it is.</summary>
        private void LogFraming(int index, View framing, Camera view)
        {
            Renderer[] parts = _gun.GetComponentsInChildren<Renderer>();
            var layers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                layers[i] = parts[i].gameObject.layer;
                parts[i].gameObject.layer = GunLayer;
            }
            int mask = view.cullingMask;
            Color background = view.backgroundColor;
            Texture2D all = Shot(view);
            view.cullingMask = mask & ~(1 << GunLayer);
            Texture2D without = Shot(view);
            view.cullingMask = 1 << GunLayer;
            view.backgroundColor = Color.black;
            Texture2D alone = Shot(view);
            view.cullingMask = mask;
            view.backgroundColor = background;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].gameObject.layer = layers[i];
            }

            Color[] allPixels = all.GetPixels();
            Color[] withoutPixels = without.GetPixels();
            Color[] alonePixels = alone.GetPixels();
            int covers = 0;
            int seen = 0;
            int left = CellWidth;
            int right = -1;
            for (int i = 0; i < alonePixels.Length; i++)
            {
                if (alonePixels[i].maxColorComponent <= 0.001f && alonePixels[i].a >= 0.0f && allPixels[i] == withoutPixels[i])
                {
                    continue;
                }
                bool onGun = alonePixels[i].maxColorComponent > 0.001f;
                if (!onGun)
                {
                    continue;
                }
                covers++;
                int x = i % CellWidth;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                if (allPixels[i] != withoutPixels[i])
                {
                    seen++;
                }
            }
            Animator animator = _player.GetComponentInChildren<Animator>();
            Vector3 head = view.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
            Vector3 gunThere = view.transform.InverseTransformPoint(_gun.transform.position);
            Vector3 gunInBody = _player.transform.InverseTransformPoint(_gun.transform.position);
            Vector3 shoulderInBody = _player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
            Debug.Log($"CityX pistol view {index} (distance {framing.Distance} shoulder {framing.Shoulder} raise {framing.Raise} fov {framing.FieldOfView}): " +
                $"{(covers > 0 ? 100.0f * seen / covers : 0.0f):F0}% of the gun seen, {100.0f * (right - left + 1) / CellWidth:F1}% of the screen wide, {100.0f * covers / alonePixels.Length:F2}% of its area; " +
                $"head at {head.x:F2},{head.y:F2}; gun in the camera's frame {gunThere:F2}, in the body's {gunInBody:F2}, right shoulder in the body's {shoulderInBody:F2}");
            Object.Destroy(all);
            Object.Destroy(without);
            Object.Destroy(alone);
        }

        private static Texture2D Shot(Camera camera)
        {
            var target = new RenderTexture(CellWidth, CellHeight, 24) { antiAliasing = 1 };
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = target;
            var image = new Texture2D(CellWidth, CellHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            target.Release();
            Object.Destroy(target);
            return image;
        }

        /// <summary>The images in rows of <paramref name="columns"/>, the first at the top left.</summary>
        private static void SaveSheet(List<Texture2D> images, int columns, string path)
        {
            int rows = Mathf.CeilToInt(images.Count / (float)columns);
            var sheet = new Texture2D(CellWidth * columns, CellHeight * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < images.Count; i++)
            {
                int x = (i % columns) * CellWidth;
                int y = (rows - 1 - i / columns) * CellHeight;
                sheet.SetPixels(x, y, CellWidth, CellHeight, images[i].GetPixels());
                Object.Destroy(images[i]);
            }
            sheet.Apply();
            CapturePng.Write(sheet, path);
            Object.Destroy(sheet);
        }
    }
}
