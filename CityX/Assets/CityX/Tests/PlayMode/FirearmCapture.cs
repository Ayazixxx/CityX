using System.Collections;
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
    /// RENDERS THE GUNS (2026-10-14): the Glock and the AK-47 on the real player, through the real ThirdPersonCamera -- carried, aimed (the
    /// owner's "i cant even see this pistol when im aiming in with it"), mid-burst with the brass flying, and through an empty reload from the
    /// side and over the shoulder -- written to Logs/CityShots/firearm-*.png to be looked at. Green tests do not mean it looks right.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FirearmCapture
    {
        private const float Dt = 1.0f / 60.0f;
        private const int Width = 960;
        private const int Height = 600;

        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private ThirdPersonCamera _follow;
        private Camera _followCamera;
        private Camera _side;
        private PistolWeapon _gun;
        private PistolWeapon _leftGun;
        private GunFeel _feel;
        private string _directory;

        [UnityTest]
        public IEnumerator RenderTheGlock()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Render("glock", "Assets/CityX/Weapons/Pistol.prefab");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RenderTheAK47()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Render("ak47", "Assets/CityX/Weapons/AK47.prefab");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RenderTheShotgun()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Render("shotgun", "Assets/CityX/Weapons/Shotgun.prefab");
#else
            yield break;
#endif
        }

        /// <summary>A Glock in each hand (Dual Pistols, docs/DUAL_PISTOLS.md): carried, raised by a shot from each, and the pair's empty reload.</summary>
        [UnityTest]
        public IEnumerator RenderTheDualPistols()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Render("dual", "Assets/CityX/Weapons/Pistol.prefab", pair: true);
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private IEnumerator Render(string label, string gunPath, bool pair = false)
        {
            Time.captureFramerate = 60;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);
            var scenery = new GameObject("Scenery");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(scenery.transform);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(80, 1, 80);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(scenery.transform);
            wall.transform.position = new Vector3(0, 2.5f, 16.0f);
            wall.transform.localScale = new Vector3(20, 5, 0.5f);
            wall.GetComponent<Renderer>().material.color = new Color(0.62f, 0.6f, 0.56f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(scenery.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40, -30, 0);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
            Assert.IsNotNull(gunPrefab, gunPath + " is missing -- rebuild the firearms (CityX > Rebuild Firearms)");
            _player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _gun = Object.Instantiate(gunPrefab, new Vector3(20, 1, 20), Quaternion.identity).GetComponent<PistolWeapon>();
            _gun.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            if (pair)
            {
                _leftGun = Object.Instantiate(gunPrefab, new Vector3(22, 1, 20), Quaternion.identity).GetComponent<PistolWeapon>();
                _leftGun.OnEquipOffHand(_player, CityX.Items.ItemInstanceData.None);
            }
            _feel = _player.GetComponent<GunFeel>();

            var cameraObject = new GameObject("FollowCamera") { tag = "MainCamera" };   // the game's camera: a tracer sizes itself to it
            _followCamera = cameraObject.AddComponent<Camera>();
            _followCamera.fieldOfView = 75.0f;   // the scene's (CityMassingSceneBuilder: player.gd's camera.fov)
            _followCamera.nearClipPlane = 0.05f;
            _followCamera.clearFlags = CameraClearFlags.SolidColor;
            _followCamera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            _follow = cameraObject.AddComponent<ThirdPersonCamera>();
            _follow.Target = _player.transform;
            var sideObject = new GameObject("SideCamera");
            _side = sideObject.AddComponent<Camera>();
            _side.enabled = false;
            _side.fieldOfView = 32.0f;
            _side.nearClipPlane = 0.05f;
            _side.clearFlags = CameraClearFlags.SolidColor;
            _side.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            yield return null;
            _follow.Yaw = 0.0f;
            _follow.Pitch = 4.0f;

            try
            {
                yield return Run(false, false, 0.8f);
                Shoot(label + "-1-carry-pov", null);
                Shoot(label + "-1-carry-side", SideView(new Vector3(3.2f, 0.4f, 1.0f)));
                if (pair)
                {
                    Shoot(label + "-1-carry-left", SideView(new Vector3(-3.2f, 0.4f, 1.0f)));
                    yield return PairShots(label);
                    yield break;
                }
                yield return Run(true, false, 0.9f);
                Shoot(label + "-2-aim-pov", null);
                Shoot(label + "-2-aim-side", SideView(new Vector3(3.0f, 0.3f, 0.8f)));
                Shoot(label + "-2-aim-front", SideView(new Vector3(1.4f, 0.5f, 3.2f)));
                Debug.Log($"CityX capture {label}: muzzle in the aim view at {_followCamera.WorldToViewportPoint(_gun.MuzzleFlashObject.transform.position).ToString("0.00")}");

                // A pump gun (docs/SHOTGUN.md): one shot, then the forend at the rear with the support hand on it and the shell on its way out.
                if (_gun.Profile.ActionDelaySeconds > 0.0f)
                {
                    yield return Run(true, true, Dt);
                    yield return Run(true, false, _gun.Profile.EjectDelaySeconds);
                    Shoot(label + "-3-pump-pov", null);
                    Shoot(label + "-3-pump-side", SideView(new Vector3(2.6f, 0.5f, 0.6f)));
                    Shoot(label + "-3-pump-left", SideView(new Vector3(-2.4f, 0.4f, 1.4f)));
                    yield return Run(true, false, 0.7f);
                }

                // A burst: a pistol's five quick shots, an automatic's trigger held for half a second.
                float burst = _gun.IsAutomatic ? 0.5f : 1.0f;
                yield return Run(true, true, burst);
                Shoot(label + "-3-burst-pov", null);
                Shoot(label + "-3-burst-side", SideView(new Vector3(2.6f, 0.5f, 0.6f)));
                Debug.Log($"CityX capture {label}: {_gun.MagazineAmmo} left after the burst, camera kick {_follow.RecoilPitch:0.0} up {_follow.RecoilYaw:0.0} right, casings {FirearmEffects.ActiveCasings}");

                // Empty it, then the empty reload, frame by frame from the side and over the shoulder.
                int guard = 0;
                while (_gun.MagazineAmmo > 0 && guard++ < 600)
                {
                    yield return Run(true, true, Dt);
                }
                yield return Run(true, false, 0.4f);
                _gun.TryReload();
                GunReloadDriver reloader = _player.GetComponent<PlayerAnimationDriver>().GunReloader;
                Assert.IsTrue(reloader.IsPlaying, "the reload motion did not start");
                if (_gun.Profile.ShellByShell)
                {
                    // Loaded a shell at a time: the hand at the shells, the shell on its way, at the port, thumbed in; then the pump of a gun run dry.
                    yield return Run(false, false, reloader.Duration + Dt);
                    Assert.AreEqual(GunReload.ShellSegment, reloader.Segment, "the first shell did not follow the start");
                    float into = 0.0f;
                    foreach (float seconds in new[] { 0.08f, 0.25f, 0.32f, 0.38f })
                    {
                        yield return Run(false, false, seconds - into);
                        into = seconds;
                        Shoot($"{label}-4-shell-{seconds * 100:00}-side", SideView(new Vector3(2.4f, 0.4f, 1.2f)));
                        Shoot($"{label}-4-shell-{seconds * 100:00}-left", SideView(new Vector3(-2.2f, 0.2f, 1.6f)));
                    }
                    int guardShells = 0;
                    while (reloader.Segment != GunReload.ShellsEndEmptySegment && guardShells++ < 600)
                    {
                        yield return Run(false, false, Dt);
                    }
                    yield return Run(false, false, 0.33f);
                    Shoot(label + "-4-dry-pump-side", SideView(new Vector3(2.4f, 0.4f, 1.2f)));
                    Shoot(label + "-4-dry-pump-left", SideView(new Vector3(-2.2f, 0.2f, 1.6f)));
                    yield return Run(false, false, 0.8f);
                    Shoot(label + "-5-after-reload-side", SideView(new Vector3(2.4f, 0.4f, 1.2f)));
                    Debug.Log($"CityX capture {label}: after the loading {_gun.GetAmmoText()}");
                    yield break;
                }
                float duration = reloader.Duration;
                float[] at = { 0.12f, 0.3f, 0.42f, 0.58f, 0.68f, 0.8f, 0.9f };
                float elapsed = 0.0f;
                foreach (float fraction in at)
                {
                    yield return Run(false, false, fraction * duration - elapsed);
                    elapsed = fraction * duration;
                    Shoot($"{label}-4-reload-{fraction * 100:00}-side", SideView(new Vector3(2.4f, 0.4f, 1.2f)));
                    Shoot($"{label}-4-reload-{fraction * 100:00}-front", SideView(new Vector3(0.9f, 0.3f, 2.4f)));
                }
                yield return Run(false, false, duration * 0.15f + 0.3f);
                Shoot(label + "-5-after-reload-side", SideView(new Vector3(2.4f, 0.4f, 1.2f)));
                Debug.Log($"CityX capture {label}: after the reload {_gun.GetAmmoText()}, dropped magazines {FirearmEffects.ActiveMagazines}");
            }
            finally
            {
                Object.Destroy(_player);
                Object.Destroy(cameraObject);
                Object.Destroy(sideObject);
                Object.Destroy(scenery);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;   // back to the steady frames every test runs on
            }
        }

        /// <summary>A pair raised by a shot from each gun, then its empty reload frame by frame.</summary>
        private IEnumerator PairShots(string label)
        {
            Transform view = _follow.transform;
            _leftGun.Fire(view.position, view.forward, false, _player);
            yield return Run(false, false, 4 * Dt);
            _gun.Fire(view.position, view.forward, false, _player);
            yield return Run(false, false, 0.15f);
            Shoot(label + "-2-raised-pov", null);
            Shoot(label + "-2-raised-side", SideView(new Vector3(3.0f, 0.3f, 0.8f)));
            Shoot(label + "-2-raised-left", SideView(new Vector3(-3.0f, 0.3f, 0.8f)));
            Shoot(label + "-2-raised-front", SideView(new Vector3(1.0f, 0.4f, 3.4f)));
            yield return Run(false, false, 0.8f);
            _gun.SetAmmo(0, 30);
            _leftGun.SetAmmo(0, 30);
            _gun.TryReload();
            GunReloadDriver reloader = _player.GetComponent<PlayerAnimationDriver>().GunReloader;
            Assert.IsTrue(reloader.IsPlaying, "the pair's reload did not start");
            float duration = reloader.Duration, elapsed = 0.0f;
            foreach (float fraction in new[] { 0.1f, 0.3f, 0.4f, 0.47f, 0.62f, 0.68f, 0.8f })
            {
                yield return Run(false, false, fraction * duration - elapsed);
                elapsed = fraction * duration;
                Shoot($"{label}-4-reload-{fraction * 100:00}-front", SideView(new Vector3(1.0f, 0.4f, 3.4f)));
                Shoot($"{label}-4-reload-{fraction * 100:00}-pov", null);
            }
            yield return Run(false, false, duration * 0.2f + 0.3f);
            Shoot(label + "-5-after-reload-pov", null);
            Debug.Log($"CityX capture {label}: after the reload {_leftGun.GetAmmoText()} and {_gun.GetAmmoText()}");
        }

        /// <summary>Plays <paramref name="seconds"/> of frames as the input reader would: the body ticked with the camera's aim, the camera told.</summary>
        private IEnumerator Run(bool aiming, bool firing, float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds - 1e-4f)
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
                elapsed += Dt;
            }
        }

        private Camera SideView(Vector3 offset)
        {
            Vector3 chest = _player.transform.position + Vector3.up * 1.75f;
            _side.transform.position = chest + _player.transform.rotation * offset;
            _side.transform.LookAt(chest + _player.transform.forward * 0.35f);
            return _side;
        }

        private void Shoot(string name, Camera camera)
        {
            camera = camera != null ? camera : _followCamera;
            var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = null;
            target.Release();
            Object.Destroy(target);
            image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            string path = Path.Combine(_directory, "firearm-" + name + ".png");
            File.WriteAllBytes(path, (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }
#endif
    }
}
