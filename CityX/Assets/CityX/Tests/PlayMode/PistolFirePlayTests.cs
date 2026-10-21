using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The shot sound's own per-shot pitch jitter -- "still choppy if you
    /// spam shots" even after PlayOneShot already fixed shots cutting each
    /// other off (see PistolWeapon.Fire's own comment on why overlapping
    /// identical waveforms is a separate problem from that one).
    /// </summary>
    public sealed class PistolFirePlayTests
    {
        [Test]
        public void RandomShotPitchStaysWithinTheStatedJitterRange()
        {
            float min = 1.0f - PistolWeapon.ShotPitchJitter;
            float max = 1.0f + PistolWeapon.ShotPitchJitter;
            for (int i = 0; i < 100; i++)
            {
                float pitch = PistolWeapon.RandomShotPitch();
                Assert.GreaterOrEqual(pitch, min);
                Assert.LessOrEqual(pitch, max);
            }
        }

        /// <summary>
        /// Not a claim about the distribution, just that it is genuinely
        /// re-rolled -- catches the shape of bug where the jitter is
        /// computed once and never actually varies.
        /// </summary>
        [Test]
        public void RandomShotPitchVariesAcrossCalls()
        {
            float first = PistolWeapon.RandomShotPitch();
            for (int i = 0; i < 100; i++)
            {
                if (!Mathf.Approximately(PistolWeapon.RandomShotPitch(), first))
                {
                    return;
                }
            }
            Assert.Fail($"100 calls all returned the exact same pitch ({first})");
        }

        /// <summary>
        /// The wiring, not just the pure jitter function -- confirms Fire
        /// actually reaches ShotPlayer.pitch rather than computing a jitter
        /// value nobody uses.
        /// </summary>
        [Test]
        public void FiringSetsTheShotPlayersPitchWithinTheJitterRange()
        {
            var pistolObject = new GameObject("Pistol");
            pistolObject.AddComponent<SphereCollider>().isTrigger = true;
            var pistol = pistolObject.AddComponent<PistolWeapon>();

            var shotPlayer = pistolObject.AddComponent<AudioSource>();
            shotPlayer.clip = AudioClip.Create("fake-shot", 100, 1, 44100, false);
            pistol.ShotPlayer = shotPlayer;

            try
            {
                pistol.Fire(Vector3.zero, Vector3.forward, aiming: false, wielder: null);

                float min = 1.0f - PistolWeapon.ShotPitchJitter;
                float max = 1.0f + PistolWeapon.ShotPitchJitter;
                Assert.GreaterOrEqual(shotPlayer.pitch, min);
                Assert.LessOrEqual(shotPlayer.pitch, max);
            }
            finally
            {
                Object.DestroyImmediate(pistolObject);
            }
        }

        /// <summary>
        /// Bug report, TWICE: the first ("coming from all sorts of
        /// different directions") was fixed by drawing the tracer along the
        /// muzzle's own forward instead of muzzle-to-hit-point. The SECOND
        /// report of the exact same symptom, after that fix had already
        /// shipped, was this: ApplyGunAimPose only bends the arm toward the
        /// target while `aiming` is true -- an ordinary hip-fire shot (a
        /// plain left-click, no right-click, how most shots are actually
        /// fired) never corrects the arm at all, so the muzzle's own facing
        /// carries no information about where the shot went. The muzzle here
        /// is deliberately pointed somewhere else entirely (straight up) to
        /// stand in for that real, arbitrary walk/idle-pose facing -- if the
        /// fix regressed back to "muzzle-forward always," this tracer would
        /// end up pointing straight up instead of at the real hit.
        /// </summary>
        [Test]
        public void HipFireTracerEndsAtTheRealHitPointNotTheMuzzlesArbitraryFacing()
        {
            var pistolObject = new GameObject("Pistol");
            pistolObject.AddComponent<SphereCollider>().isTrigger = true;
            var pistol = pistolObject.AddComponent<PistolWeapon>();

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.position = new Vector3(0.3f, 1.5f, 0.0f);
            muzzle.transform.rotation = Quaternion.LookRotation(Vector3.up);
            pistol.MuzzleFlashObject = muzzle;

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0.0f, 0.0f, 10.0f);

            BulletTracer tracer = null;
            try
            {
                // A tracer from an earlier test (this file's own Fire() calls
                // never clean theirs up, since BulletTracer only ever
                // self-destroys via Update(), which a plain synchronous
                // [Test] never lets run) could otherwise make
                // FindFirstObjectByType return a STALE tracer instead of the
                // one this test's own Fire() call just spawned.
                DestroyAllTracers();
                // The target's BoxCollider was just moved into place a few
                // lines up -- without this, PhysX's own internal transform
                // cache still has it at its stale creation-time position for
                // this synchronous [Test] call (no frame boundary to let an
                // automatic sync happen), and Physics.Raycast below misses
                // entirely.
                Physics.SyncTransforms();

                pistol.Fire(Vector3.zero, Vector3.forward, aiming: false, wielder: null);

                tracer = Object.FindFirstObjectByType<BulletTracer>();
                Assert.IsNotNull(tracer, "no tracer was spawned");
                LineRenderer line = tracer.GetComponent<LineRenderer>();
                Vector3 end = line.GetPosition(1);

                Assert.Less(Vector3.Distance(end, new Vector3(0.0f, 0.0f, 9.5f)), 0.1f,
                    $"hip-fire tracer should end at the real hit point, not follow the muzzle's own " +
                    $"(here, straight-up) facing -- ended at {end}");
            }
            finally
            {
                Object.DestroyImmediate(pistolObject);
                Object.DestroyImmediate(muzzle);
                Object.DestroyImmediate(target);
                if (tracer != null) Object.DestroyImmediate(tracer.gameObject);
            }
        }

        /// <summary>
        /// AIMING TOO: the tracer is the bullet's path, muzzle to the real hit
        /// point, whatever the mode. It used to follow the muzzle's own forward
        /// while aiming, which stopped short of a near target (the arm aimed at a
        /// fixed point down the camera ray, not at the hit). The muzzle here is
        /// pointed well off the target, standing in for exactly that.
        /// </summary>
        [Test]
        public void AimingTracerAlsoEndsAtTheRealHitPointNotAlongTheMuzzlesOwnForward()
        {
            var pistolObject = new GameObject("Pistol");
            pistolObject.AddComponent<SphereCollider>().isTrigger = true;
            var pistol = pistolObject.AddComponent<PistolWeapon>();

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.position = new Vector3(0.3f, 1.5f, 0.0f);
            muzzle.transform.rotation = Quaternion.LookRotation(Vector3.up);   // pointing nowhere near the target
            pistol.MuzzleFlashObject = muzzle;

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0.0f, 0.0f, 10.0f);

            BulletTracer tracer = null;
            try
            {
                DestroyAllTracers();
                // See the hip-fire test's own comment on why this is needed.
                Physics.SyncTransforms();

                pistol.Fire(Vector3.zero, Vector3.forward, aiming: true, wielder: null);

                tracer = Object.FindFirstObjectByType<BulletTracer>();
                Assert.IsNotNull(tracer, "no tracer was spawned");
                LineRenderer line = tracer.GetComponent<LineRenderer>();
                Vector3 end = line.GetPosition(1);

                Assert.Less(Vector3.Distance(end, new Vector3(0.0f, 0.0f, 9.5f)), 0.1f,
                    $"aiming tracer should end at the real hit point, not along the muzzle's own (here straight-up) forward -- ended at {end}");
            }
            finally
            {
                Object.DestroyImmediate(pistolObject);
                Object.DestroyImmediate(muzzle);
                Object.DestroyImmediate(target);
                if (tracer != null) Object.DestroyImmediate(tracer.gameObject);
            }
        }

        // --- VISIBILITY (explicit report: "most of the time they arent showing up at all") ---

        [Test]
        public void ATracerHasItsStatedPixelWidthAtAnyRange()
        {
            // 75 degree FOV, 1080 lines: pixel-constant means world width grows with distance.
            float near = BulletTracer.WorldWidthFor(4.0f, 75.0f, 1080.0f);
            float far = BulletTracer.WorldWidthFor(40.0f, 75.0f, 1080.0f);

            float nearPixels = near / (2.0f * 4.0f * Mathf.Tan(37.5f * Mathf.Deg2Rad) / 1080.0f);
            Assert.AreEqual(BulletTracer.PixelWidth, nearPixels, 0.25f, "a tracer at 4m is not the stated pixel width");
            Assert.Greater(far, near * 5.0f, "a tracer 10x further away was not proportionally wider in the world");
            Assert.LessOrEqual(BulletTracer.WorldWidthFor(1000.0f, 75.0f, 1080.0f), BulletTracer.MaxWorldWidth + 0.0001f);
        }

        [Test]
        public void ATracerIsNoLongerAOnePixelHairline()
        {
            // The old fixed 1.5cm width at 6m and 480 lines was about one pixel.
            float oldPixels = BulletTracer.Width / (2.0f * 6.0f * Mathf.Tan(37.5f * Mathf.Deg2Rad) / 480.0f);
            float newPixels = BulletTracer.WorldWidthFor(6.0f, 75.0f, 480.0f) / (2.0f * 6.0f * Mathf.Tan(37.5f * Mathf.Deg2Rad) / 480.0f);

            Assert.Less(oldPixels, 1.5f, "setup: the old width was not a hairline");
            Assert.Greater(newPixels, 2.0f, "the tracer is still nearly invisible at ordinary range");
        }

        [Test]
        public void ATracerLivesLongEnoughForTheEyeToCatchIt()
        {
            Assert.GreaterOrEqual(BulletTracer.LifetimeSeconds, 0.1f, "under about 6 frames at 60fps it is gone before it registers");
            Assert.LessOrEqual(BulletTracer.LifetimeSeconds, 0.25f, "it lingers like a laser beam");
        }

        private static void DestroyAllTracers()
        {
            foreach (BulletTracer stale in Object.FindObjectsByType<BulletTracer>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(stale.gameObject);
            }
        }
    }
}
