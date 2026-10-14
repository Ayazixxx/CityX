using System;
using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The reticle's reactions to shooting -- hit marker, kill marker,
    /// "barrel blocked" and "reloading" states (see GunReticleView) -- driven
    /// by a fake gun so each state can be set exactly. Built by hand, like
    /// PlayerHudPlayTests, so it fails for one reason.
    /// </summary>
    public sealed class GunReticleViewPlayTests
    {
        private sealed class FakeGun : IEquippableGun
        {
            public bool Blocked;
            public bool Reloading;
            public bool IsReloading => Reloading;
            public event Action<GunShot> ShotResolved;

            public bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder) => true;
            public void TryReload() { }
            public string GetAmmoText() => "0 / 0";
            public bool IsBarrelBlocked(Vector3 origin, Vector3 direction, GameObject wielder) => Blocked;
            public void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder, out Vector3 point, out bool barrelBlocked)
            {
                point = origin + direction * 10.0f;
                barrelBlocked = Blocked;
            }
            public bool IsAutomatic => false;
            public void Raise(GunShot shot) => ShotResolved?.Invoke(shot);
        }

        private GameObject _body;
        private GameObject _hudObject;
        private GameObject _cameraObject;
        private FakeGun _gun;
        private GunFeel _feel;
        private GunReticleView _view;
        private CursorLockMode _lockBefore;
        private bool _visibleBefore;

        [SetUp]
        public void SetUp()
        {
            _lockBefore = Cursor.lockState;
            _visibleBefore = Cursor.visible;

            _body = new GameObject("Body");
            var vitals = _body.AddComponent<PlayerVitals>();
            _feel = _body.AddComponent<GunFeel>();
            _feel.FeedbackVolume = 0.0f;   // silent

            // GunFeel only checks the barrel once a camera exists to look through.
            _cameraObject = new GameObject("Camera");
            _cameraObject.AddComponent<ThirdPersonCamera>().Target = _body.transform;

            _hudObject = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            var hud = _hudObject.AddComponent<PlayerHud>();
            hud.DeathScreen = new GameObject("DeathScreen");
            hud.DeathScreen.transform.SetParent(_hudObject.transform, false);
            hud.DeathScreen.SetActive(false);

            var crosshair = new GameObject("Crosshair", typeof(RectTransform));
            crosshair.transform.SetParent(_hudObject.transform, false);
            foreach (string bar in new[] { "Horizontal", "Vertical" })
            {
                var go = new GameObject(bar, typeof(RectTransform));
                go.transform.SetParent(crosshair.transform, false);
                go.AddComponent<Image>().color = GunReticleView.NormalColour;
            }
            hud.Crosshair = crosshair;
            hud.Target = vitals;

            _gun = new FakeGun();
            _feel.Attach(_gun);
            _view = _hudObject.GetComponent<GunReticleView>();
        }

        [TearDown]
        public void TearDown()
        {
            _feel.Detach(_gun);
            UnityEngine.Object.DestroyImmediate(_hudObject);
            UnityEngine.Object.DestroyImmediate(_cameraObject);
            UnityEngine.Object.DestroyImmediate(_body);
            Cursor.lockState = _lockBefore;
            Cursor.visible = _visibleBefore;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f &&
            Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

        [Test]
        public void TheHudGrowsTheReticleViewOnItsOwn()
        {
            Assert.IsNotNull(_view, "PlayerHud did not add its GunReticleView");
        }

        [UnityTest]
        public IEnumerator AHitFlashesAWhiteMarkerThatThenFades()
        {
            yield return Frames(2);
            Assert.IsFalse(_view.MarkerVisible, "a marker was showing with nothing hit");

            _gun.Raise(new GunShot { Fired = true, HitSomething = true, HitDamageable = true });
            yield return null;

            Assert.IsTrue(_view.MarkerVisible, "a hit did not show a marker");
            Assert.IsTrue(Near(_view.MarkerColour, GunReticleView.HitColour), $"hit marker was {_view.MarkerColour}");

            yield return new WaitForSeconds(GunReticleView.HitMarkerSeconds + 0.1f);
            Assert.IsFalse(_view.MarkerVisible, "the hit marker never faded");
        }

        [UnityTest]
        public IEnumerator AKillFlashesARedMarkerThatLastsLonger()
        {
            yield return Frames(2);

            _gun.Raise(new GunShot { Fired = true, HitSomething = true, HitDamageable = true, Killed = true });
            yield return null;

            Assert.IsTrue(_view.MarkerVisible);
            Assert.IsTrue(Near(_view.MarkerColour, GunReticleView.KillColour), $"kill marker was {_view.MarkerColour}");

            // Still up after a plain hit's whole duration.
            yield return new WaitForSeconds(GunReticleView.HitMarkerSeconds + 0.05f);
            Assert.IsTrue(_view.MarkerVisible, "a kill marker faded no slower than a hit marker");

            yield return new WaitForSeconds(GunReticleView.KillMarkerSeconds);
            Assert.IsFalse(_view.MarkerVisible);
        }

        [UnityTest]
        public IEnumerator AMissDoesNotShowAMarkerAndNeitherDoesADryClick()
        {
            yield return Frames(2);

            _gun.Raise(new GunShot { Fired = true });                    // shot at nothing
            _gun.Raise(new GunShot { Fired = true, HitSomething = true }); // shot a wall
            _gun.Raise(new GunShot { DryClick = true });
            yield return null;

            Assert.IsFalse(_view.MarkerVisible, "a miss, a wall or a dry click showed a hit marker");
        }

        [UnityTest]
        public IEnumerator TheCrosshairTurnsRedWhileTheBarrelIsBlockedAndBackWhenClear()
        {
            yield return Frames(3);
            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.NormalColour), $"started as {_view.CrosshairColour}");

            _gun.Blocked = true;
            yield return Frames(3);
            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.BlockedColour),
                $"the crosshair looked clear ({_view.CrosshairColour}) while the barrel was blocked");

            _gun.Blocked = false;
            yield return Frames(3);
            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.NormalColour), "the crosshair stayed red after the barrel cleared");
        }

        [UnityTest]
        public IEnumerator TheCrosshairDimsWhileReloading()
        {
            yield return Frames(3);

            _gun.Reloading = true;
            yield return Frames(3);
            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.ReloadingColour), $"was {_view.CrosshairColour}");

            _gun.Reloading = false;
            yield return Frames(3);
            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.NormalColour));
        }

        [UnityTest]
        public IEnumerator ABlockedBarrelOutranksReloading()
        {
            yield return Frames(3);

            _gun.Reloading = true;
            _gun.Blocked = true;
            yield return Frames(3);

            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.BlockedColour));
        }

        [UnityTest]
        public IEnumerator WithNoGunTheReticleIsLeftAlone()
        {
            _feel.Detach(_gun);
            _gun.Blocked = true;
            yield return Frames(3);

            Assert.IsTrue(Near(_view.CrosshairColour, GunReticleView.NormalColour),
                "the reticle reacted to a gun that is no longer equipped");
        }

        /// <summary>
        /// THE RESEARCH'S OWN WARNING: never give feedback that misleads about
        /// where the bullet goes. Bullets land exactly on the crosshair, so the
        /// crosshair must not swell with fire or movement.
        /// </summary>
        [UnityTest]
        public IEnumerator FiringDoesNotWidenTheCrosshairBeyondATinyPulseThatSettles()
        {
            yield return Frames(3);
            RectTransform rect = _hudObject.GetComponentInChildren<RectTransform>()
                .Find("Crosshair") as RectTransform;
            Assert.IsNotNull(rect);

            _gun.Raise(new GunShot { Fired = true });
            yield return null;
            Assert.LessOrEqual(rect.localScale.x, 1.3f, "the reticle swelled like a spread indicator");

            yield return new WaitForSeconds(GunReticleView.FirePulseSeconds + 0.1f);
            Assert.AreEqual(1.0f, rect.localScale.x, 0.01f, "the reticle stayed enlarged after firing");
        }
    }
}
