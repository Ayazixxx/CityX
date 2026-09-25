using System.Collections;
using System.Reflection;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The third-person-pistol mechanics from the research
    /// (docs/THIRD_PERSON_PISTOL.md): the three-trace shot, recoil, kill/hit
    /// reporting, auto-reload and the dry click. Bare GameObjects, stepped by
    /// hand -- the scene wiring is covered by GunFeelRealInputPlayTests.
    /// </summary>
    public sealed class PistolThirdPersonPlayTests
    {
        private GameObject _wielder;
        private GameObject _pistolObject;
        private GameObject _muzzle;
        private PistolWeapon _pistol;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<GunShot> _shots = new System.Collections.Generic.List<GunShot>();

        [SetUp]
        public void SetUp()
        {
            _wielder = new GameObject("Wielder");
            _wielder.AddComponent<PlayerVitals>();

            _pistolObject = new GameObject("Pistol");
            _pistolObject.AddComponent<SphereCollider>().isTrigger = true;
            _pistol = _pistolObject.AddComponent<PistolWeapon>();

            // The barrel sits off to the right of the body and a little ahead,
            // where a held pistol does -- and away from the camera, which is
            // the whole point of the three-trace shot.
            _muzzle = new GameObject("Muzzle");
            _muzzle.transform.position = new Vector3(0.3f, 1.5f, 0.3f);
            _pistol.MuzzleFlashObject = _muzzle;

            _pistol.ShotResolved += _shots.Add;
            DestroyAllTracers();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wielder);
            Object.DestroyImmediate(_pistolObject);
            Object.DestroyImmediate(_muzzle);
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            DestroyAllTracers();
        }

        private static void DestroyAllTracers()
        {
            foreach (BulletTracer stale in Object.FindObjectsByType<BulletTracer>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(stale.gameObject);
            }
        }

        private GameObject Make(PrimitiveType type, Vector3 position, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.position = position;
            go.transform.localScale = scale;
            _made.Add(go);
            return go;
        }

        private SparringDummy MakeDummy(Vector3 position, float maxHealth = 100.0f)
        {
            GameObject go = Make(PrimitiveType.Cube, position, Vector3.one);
            var dummy = go.AddComponent<SparringDummy>();
            dummy.MaxHealth = maxHealth;
            dummy.Reset();
            return dummy;
        }

        private void ReadyToFire()
        {
            typeof(PistolWeapon)
                .GetField("_fireCooldownRemaining", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_pistol, 0.0f);
        }

        private bool Fire(Vector3 origin, Vector3 direction, bool aiming = false)
        {
            ReadyToFire();
            Physics.SyncTransforms();
            return _pistol.Fire(origin, direction, aiming, _wielder);
        }

        private GunShot LastShot => _shots[_shots.Count - 1];

        // --- the three-trace shot ---------------------------------------------

        [Test]
        public void OpenGroundTheShotGoesExactlyWhereTheCrosshairIs()
        {
            SparringDummy target = MakeDummy(new Vector3(0.0f, 1.5f, 10.0f));

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.AreEqual(1, target.TimesHit, "a clear shot missed the crosshair's target");
            Assert.IsFalse(LastShot.BarrelBlocked, "reported a blocked barrel in open ground");
            Assert.IsTrue(LastShot.HitDamageable);
        }

        /// <summary>
        /// THE THIRD-PERSON PROBLEM ITSELF, from the Monday Night Combat
        /// write-up: the camera looks past a wall's edge at a target, but the
        /// GUN is behind the wall. The bullet must hit the wall, not pass
        /// through it to the target the crosshair is on.
        /// </summary>
        [Test]
        public void AWallBetweenTheBarrelAndTheTargetTakesTheBullet()
        {
            SparringDummy target = MakeDummy(new Vector3(2.0f, 1.5f, 10.0f));
            GameObject wall = Make(PrimitiveType.Cube, new Vector3(0.0f, 1.5f, 2.0f), new Vector3(2.0f, 4.0f, 0.2f));

            // The camera is out to the right, level with the target, so ITS ray
            // slips past the wall's edge (x = 1) -- but the muzzle at x = 0.3
            // is looking straight through the wall.
            Fire(new Vector3(2.0f, 1.5f, -4.0f), Vector3.forward);

            Assert.AreEqual(0, target.TimesHit, "the bullet went through the wall to the target");
            Assert.IsTrue(LastShot.BarrelBlocked, "the shot did not report that scenery took it");
            Assert.IsTrue(LastShot.HitSomething);
            Assert.IsFalse(LastShot.HitDamageable, "a wall is not a body");
            Assert.Less(LastShot.Point.z, 2.0f, $"the bullet ended at {LastShot.Point}, past the wall");
            Assert.IsNotNull(wall);
        }

        [Test]
        public void TheSameShotWithNoWallHitsTheTarget()
        {
            SparringDummy target = MakeDummy(new Vector3(2.0f, 1.5f, 10.0f));

            Fire(new Vector3(2.0f, 1.5f, -4.0f), Vector3.forward);

            Assert.AreEqual(1, target.TimesHit);
            Assert.IsFalse(LastShot.BarrelBlocked);
        }

        [Test]
        public void TheReticleIsToldTheBarrelIsBlockedBeforeYouFire()
        {
            MakeDummy(new Vector3(2.0f, 1.5f, 10.0f));
            Physics.SyncTransforms();
            Assert.IsFalse(_pistol.IsBarrelBlocked(new Vector3(2.0f, 1.5f, -4.0f), Vector3.forward, _wielder));

            Make(PrimitiveType.Cube, new Vector3(0.0f, 1.5f, 2.0f), new Vector3(2.0f, 4.0f, 0.2f));
            Physics.SyncTransforms();

            Assert.IsTrue(_pistol.IsBarrelBlocked(new Vector3(2.0f, 1.5f, -4.0f), Vector3.forward, _wielder),
                "the reticle would show a clear shot while the wall takes it");
        }

        /// <summary>The hand trace: a barrel poking into scenery cannot shoot through it.</summary>
        [Test]
        public void ABarrelInsideAWallShootsTheWallNotTheTarget()
        {
            SparringDummy target = MakeDummy(new Vector3(0.0f, 1.5f, 10.0f));
            // Right on the line from the body (0, 1.08, 0) to the muzzle (0.3, 1.5, 0.3).
            Make(PrimitiveType.Sphere, new Vector3(0.15f, 1.29f, 0.15f), Vector3.one * 0.3f);

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.AreEqual(0, target.TimesHit);
            Assert.IsTrue(LastShot.BarrelBlocked);
        }

        /// <summary>
        /// The old single Raycast reported the SHOOTER'S own capsule as the
        /// first hit and then threw the shot away: with the camera behind the
        /// body (or the aim camera pulled in close) a click hit nothing at all.
        /// </summary>
        [Test]
        public void TheWieldersOwnBodyNeitherBlocksNorEatsAShot()
        {
            var body = new GameObject("Body");
            body.transform.SetParent(_wielder.transform, false);
            body.transform.localPosition = new Vector3(0.0f, 1.0f, 0.0f);
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.height = 2.0f;
            capsule.radius = 0.5f;
            SparringDummy target = MakeDummy(new Vector3(0.0f, 1.5f, 10.0f));

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.AreEqual(1, target.TimesHit, "the shooter's own collider ate the shot");
            Assert.IsFalse(LastShot.BarrelBlocked, "the shooter's own collider counted as scenery in the way");
        }

        [Test]
        public void ACloseTargetRightAtTheMuzzleStillGetsHit()
        {
            SparringDummy target = MakeDummy(new Vector3(0.3f, 1.5f, 1.0f));

            Fire(new Vector3(0.3f, 1.5f, -3.0f), Vector3.forward);

            Assert.AreEqual(1, target.TimesHit);
        }

        // --- recoil, kills, dry fire --------------------------------------------

        [Test]
        public void EachShotReportsItsRecoilAndAimingSteadiesIt()
        {
            MakeDummy(new Vector3(0.0f, 1.5f, 10.0f));

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward, aiming: false);
            Assert.AreEqual(_pistol.RecoilPitchDegrees, LastShot.RecoilPitchDegrees, 0.0001f);
            Assert.Greater(LastShot.RecoilPitchDegrees, 0.0f, "a pistol with no kick at all");

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward, aiming: true);
            Assert.AreEqual(_pistol.RecoilPitchDegrees * _pistol.AdsRecoilMultiplier, LastShot.RecoilPitchDegrees, 0.0001f);
            Assert.Less(LastShot.RecoilPitchDegrees, _pistol.RecoilPitchDegrees, "aiming did not steady the gun");
        }

        [Test]
        public void ARecoilOfZeroMeansNoKick()
        {
            _pistol.RecoilPitchDegrees = 0.0f;
            MakeDummy(new Vector3(0.0f, 1.5f, 10.0f));

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.AreEqual(0.0f, LastShot.RecoilPitchDegrees, 0.0001f);
        }

        [Test]
        public void AKillIsReportedAsAKillAndAHitIsNot()
        {
            SparringDummy fragile = MakeDummy(new Vector3(0.0f, 1.5f, 10.0f), maxHealth: PistolWeapon.Damage);
            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);
            Assert.IsTrue(LastShot.HitDamageable);
            Assert.IsTrue(LastShot.Killed, "a killing shot was not reported as a kill");
            Assert.IsFalse(fragile.IsAlive);

            Object.DestroyImmediate(fragile.gameObject);
            SparringDummy tough = MakeDummy(new Vector3(0.0f, 1.5f, 10.0f), maxHealth: 500.0f);
            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);
            Assert.IsTrue(LastShot.HitDamageable);
            Assert.IsFalse(LastShot.Killed, "an ordinary hit was reported as a kill");
            Assert.IsTrue(tough.IsAlive);
        }

        [Test]
        public void AShotAtNothingIsNotAHit()
        {
            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.IsTrue(LastShot.Fired);
            Assert.IsFalse(LastShot.HitSomething);
            Assert.IsFalse(LastShot.HitDamageable);
        }

        private void EmptyTheMagazine()
        {
            for (int i = 0; i < PistolWeapon.MagazineSize; i++)
            {
                Assert.IsTrue(Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward), $"shot {i} was refused");
            }
            Assert.AreEqual(0, _pistol.MagazineAmmo);
            _shots.Clear();
        }

        /// <summary>Every modern third-person shooter: pulling the trigger on an empty gun reloads it.</summary>
        [Test]
        public void ClickingOnAnEmptyMagazineReloadsAutomatically()
        {
            EmptyTheMagazine();

            bool fired = Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.IsFalse(fired, "an empty gun fired");
            Assert.IsTrue(_pistol.IsReloading, "the empty click did not start a reload");
            Assert.AreEqual(1, _shots.Count);
            Assert.IsTrue(LastShot.DryClick);
            Assert.IsTrue(LastShot.AutoReloading);
            Assert.IsFalse(LastShot.Fired);
        }

        [Test]
        public void ClickingOnAnEmptyGunWithNoReserveJustClicks()
        {
            EmptyTheMagazine();
            typeof(PistolWeapon)
                .GetField("<ReserveAmmo>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_pistol, 0);

            bool fired = Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.IsFalse(fired);
            Assert.IsFalse(_pistol.IsReloading, "started a reload with nothing to reload from");
            Assert.IsTrue(LastShot.DryClick);
            Assert.IsFalse(LastShot.AutoReloading);
        }

        [Test]
        public void DryClicksAreThrottledSoMashingDoesNotMachineGunThem()
        {
            EmptyTheMagazine();
            typeof(PistolWeapon)
                .GetField("<ReserveAmmo>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_pistol, 0);

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);
            // No ReadyToFire: an immediate second click is inside the cooldown.
            _pistol.Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward, false, _wielder);

            Assert.AreEqual(1, _shots.Count, "two clicks inside the dry-click cooldown both registered");
        }

        [Test]
        public void FiringWhileReloadingDoesNothingAtAll()
        {
            EmptyTheMagazine();
            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);   // starts the auto-reload
            _shots.Clear();

            bool fired = Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);

            Assert.IsFalse(fired);
            Assert.AreEqual(0, _shots.Count, "a click mid-reload was reported as a shot or a dry click");
        }

        // --- the pistol's own snap-back -------------------------------------------

        [UnityTest]
        public IEnumerator TheGunSnapsBackWhenFiredThenSettles()
        {
            var kickField = typeof(PistolWeapon).GetField("_kick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(0.0f, (float)kickField.GetValue(_pistol), 0.0001f);

            Fire(new Vector3(0.0f, 1.5f, -3.0f), Vector3.forward);
            Assert.AreEqual(1.0f, (float)kickField.GetValue(_pistol), 0.0001f, "firing did not kick the gun");

            float waited = 0.0f;
            while (waited < 0.6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Less((float)kickField.GetValue(_pistol), 0.02f, "the gun's kick never settled");
        }
    }
}
